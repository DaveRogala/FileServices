namespace MagellanFileServices.Services;

/// <summary>
/// Default implementation of <see cref="IFileServices"/>.
/// </summary>
/// <remarks>
/// All <c>Handle*</c> methods and the primary <see cref="GetDataFromFile{T}(System.IO.Stream,System.Text.Encoding,int,string)"/>
/// overload are <see langword="virtual"/> and can be overridden in a subclass to customise behaviour.
/// </remarks>
public class FileServices : IFileServices
{
    private readonly ILogger<FileServices> _logger;

    private const string ErrorFolder = "errors";
    private const string ProcessedFolder = "processed";

    /// <summary>
    /// Initialises a new instance with the supplied logger.
    /// </summary>
    /// <param name="logger">Logger used for error and diagnostic output.</param>
    public FileServices(ILogger<FileServices> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public virtual ObjectResult<T> GetDataFromFile<T>(string filePath, Encoding encoding, bool skipEncodingHeader, string delimiter = ",")
        => GetDataFromFile<T>(filePath, encoding, skipEncodingHeader ? 1 : 0, delimiter);

    /// <inheritdoc/>
    public ObjectResult<T> GetDataFromFile<T>(string filePath, string delimiter = ",")
        => GetDataFromFile<T>(filePath, Encoding.UTF8, rowsToSkip: 0, delimiter);

    /// <inheritdoc/>
    public virtual ObjectResult<T> GetDataFromFile<T>(string filePath, Encoding encoding, int rowsToSkip, string delimiter = ",", bool fixUnescapedQuotes = false)
    {
        try
        {
            using Stream stream = File.OpenRead(filePath);
            return GetDataFromFile<T>(stream, encoding, rowsToSkip, delimiter, fixUnescapedQuotes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw;
        }
    }

    /// <inheritdoc/>
    public ObjectResult<T> GetDataFromFile<T>(Stream stream, Encoding encoding, bool skipEncodingHeader, string delimiter = ",")
        => GetDataFromFile<T>(stream, encoding, skipEncodingHeader ? 1 : 0, delimiter);

    /// <inheritdoc/>
    public virtual ObjectResult<T> GetDataFromFile<T>(Stream stream, Encoding encoding, int rowsToSkip, string delimiter = ",", bool fixUnescapedQuotes = false)
    {
        ObjectResult<T> result = new();
        try
        {
            CsvConfiguration config = new(CultureInfo.InvariantCulture)
            {
                ReadingExceptionOccurred = re =>
                {
                    _logger.LogError(re.Exception.Message);
                    result.Errors.Add($"row: {re.Exception?.Context?.Parser?.RawRow ?? 0} {re.Exception?.Message ?? "No error message"}");
                    return false;
                },
                IgnoreBlankLines = true,
                Delimiter = delimiter
            };
            using StreamReader reader = new(stream, encoding, detectEncodingFromByteOrderMarks: true);
            for (int i = 0; i < rowsToSkip; i++)
                reader.ReadLine();
            if (fixUnescapedQuotes)
            {
                using StringReader fixedReader = new(FixUnescapedQuotes(reader.ReadToEnd(), delimiter));
                using CsvReader csv = new(fixedReader, config);
                result.ObjectResults = csv.GetRecords<T>().ToList();
            }
            else
            {
                using CsvReader csv = new(reader, config);
                result.ObjectResults = csv.GetRecords<T>().ToList();
            }
            return result;
        }
        catch (CsvHelperException ex)
        {
            _logger.LogError(ex, ex.Message);
            result.Errors.Add(ex.Message);
            result.CriticalError = true;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw;
        }
    }

    /// <inheritdoc/>
    public ObjectResult<T> GetDataFromFile<T>(Stream stream, string delimiter = ",")
        => GetDataFromFile<T>(stream, Encoding.UTF8, rowsToSkip: 0, delimiter);

    /// <inheritdoc/>
    public virtual void HandleFileError(string basePath, string fileName, string exceptionMessage, string timeStamp, List<string>? errors = null)
    {
        try
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(basePath);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(fileName);

            string sourceFilePath = Path.Combine(basePath, fileName);
            string targetFilePath = Path.Combine(basePath, ErrorFolder, TargetFileName(fileName, timeStamp));

            Directory.CreateDirectory(Path.Combine(basePath, ErrorFolder));

            if (File.Exists(sourceFilePath))
            {
                File.Move(sourceFilePath, targetFilePath);
            }

            if (errors is not null)
            {
                exceptionMessage += Environment.NewLine + string.Join(Environment.NewLine, errors);
            }

            File.WriteAllText(Path.Combine(basePath, ErrorFolder, $"Errors_{fileName}_{timeStamp}.txt"), exceptionMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw;
        }
    }

    /// <inheritdoc/>
    public virtual void HandleFileSuccess(string basePath, string fileName, string timeStamp)
    {
        try
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(basePath);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(fileName);

            string sourceFilePath = Path.Combine(basePath, fileName);
            string targetFilePath = Path.Combine(basePath, ProcessedFolder, TargetFileName(fileName, timeStamp));

            Directory.CreateDirectory(Path.Combine(basePath, ProcessedFolder));

            if (File.Exists(sourceFilePath))
            {
                File.Move(sourceFilePath, targetFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw;
        }
    }

    /// <inheritdoc/>
    public virtual async Task HandleFileErrorAsync(BlobContainerClient containerClient, string filePath, string exceptionMessage, string timeStamp, List<string>? errors = null)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(containerClient);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(filePath);

            await MoveProcessedFileAsync(containerClient, filePath, timeStamp, ErrorFolder);

            if (errors is not null)
            {
                exceptionMessage += Environment.NewLine + string.Join(Environment.NewLine, errors);
            }

            string dir = (Path.GetDirectoryName(filePath) ?? "").Replace('\\', '/');
            string errorFileString = string.IsNullOrEmpty(dir)
                ? $"{ErrorFolder}/Errors_{Path.GetFileName(filePath)}_{timeStamp}.txt"
                : $"{dir}/{ErrorFolder}/Errors_{Path.GetFileName(filePath)}_{timeStamp}.txt";

            BlobClient errorBlobClient = containerClient.GetBlobClient(errorFileString);
            await errorBlobClient.UploadAsync(BinaryData.FromString(exceptionMessage), overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw;
        }
    }

    /// <inheritdoc/>
    public virtual async Task HandleFileSuccessAsync(BlobContainerClient containerClient, string filePath, string timeStamp)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(containerClient);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(filePath);

            await MoveProcessedFileAsync(containerClient, filePath, timeStamp, ProcessedFolder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw;
        }
    }

    /// <inheritdoc/>
    public void WriteDataToFile<T>(string filePath, List<T> data)
    {
        WriteDataToFile<T>(filePath, data, ",", true, false);
    }

    /// <inheritdoc/>
    public void WriteDataToFile<T>(string filePath, List<T> data, bool printEncoding)
    {
        WriteDataToFile<T>(filePath, data, ",", true, printEncoding);
    }

    /// <inheritdoc/>
    public void WriteDataToFile<T>(string filePath, List<T> data, string delimiter = ",", bool useHeaders = true, bool printEncoding = false)
    {
        WriteDataToFile<T>(filePath, data, Encoding.UTF8, delimiter, useHeaders, printEncoding);
    }

    /// <inheritdoc/>
    public void WriteDataToFile<T>(string filePath, List<T> data, Encoding encoding, string delimiter = ",", bool useHeaders = true, bool printEncoding = false)
    {
        try
        {
            CsvConfiguration config = new(CultureInfo.InvariantCulture)
            {
                Delimiter = delimiter,
                HasHeaderRecord = useHeaders,
                Encoding = encoding
            };
            using StreamWriter writer = new(filePath, false, encoding);
            using CsvWriter csv = new(writer, config);
            if (printEncoding)
            {
                writer.WriteLine(encoding.HeaderName);
            }
            csv.WriteRecords<T>(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw;
        }
    }

    /// <inheritdoc/>
    public Task WriteDataToBlobAsync<T>(BlobContainerClient containerClient, string blobPath, List<T> data)
        => WriteDataToBlobAsync<T>(containerClient, blobPath, data, Encoding.UTF8, ",", true, false);

    /// <inheritdoc/>
    public Task WriteDataToBlobAsync<T>(BlobContainerClient containerClient, string blobPath, List<T> data, bool printEncoding)
        => WriteDataToBlobAsync<T>(containerClient, blobPath, data, Encoding.UTF8, ",", true, printEncoding);

    /// <inheritdoc/>
    public Task WriteDataToBlobAsync<T>(BlobContainerClient containerClient, string blobPath, List<T> data, string delimiter = ",", bool useHeaders = true, bool printEncoding = false)
        => WriteDataToBlobAsync<T>(containerClient, blobPath, data, Encoding.UTF8, delimiter, useHeaders, printEncoding);

    /// <inheritdoc/>
    public virtual async Task WriteDataToBlobAsync<T>(BlobContainerClient containerClient, string blobPath, List<T> data, Encoding encoding, string delimiter = ",", bool useHeaders = true, bool printEncoding = false)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(containerClient);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(blobPath);

            using MemoryStream ms = new();
            CsvConfiguration config = new(CultureInfo.InvariantCulture)
            {
                Delimiter = delimiter,
                HasHeaderRecord = useHeaders,
                Encoding = encoding
            };
            using (StreamWriter writer = new(ms, encoding, bufferSize: -1, leaveOpen: true))
            {
                if (printEncoding)
                    writer.WriteLine(encoding.HeaderName);
                using CsvWriter csv = new(writer, config);
                csv.WriteRecords<T>(data);
            }
            ms.Position = 0;
            BlobClient blobClient = containerClient.GetBlobClient(blobPath);
            await blobClient.UploadAsync(ms, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw;
        }
    }

    // Scans content character-by-character and doubles any " found inside a quoted field that is
    // not already escaped (i.e. not followed by another ", a delimiter, a newline, or end-of-input).
    // Handles the ambiguous "" case by peeking one position further: if the char after the second "
    // is a field boundary (delimiter / newline / end), the first " is an unescaped interior quote and
    // the second " is the closing quote; otherwise both " form a genuine escape sequence.
    private static string FixUnescapedQuotes(string content, string delimiter)
    {
        var sb = new StringBuilder(content.Length + 32);
        bool inQuotedField = false;
        bool atFieldStart = true;
        char delimLast = delimiter[delimiter.Length - 1];
        int i = 0;

        while (i < content.Length)
        {
            char c = content[i];

            if (inQuotedField)
            {
                if (c != '"')
                {
                    sb.Append(c);
                    i++;
                }
                else
                {
                    int next = i + 1;

                    if (next >= content.Length)
                    {
                        // End of content: closing quote
                        sb.Append('"');
                        inQuotedField = false;
                        i++;
                    }
                    else if (content[next] == '"')
                    {
                        // Two consecutive quotes: peek past the second one to decide
                        int afterNext = next + 1;
                        bool secondIsFieldEnd = afterNext >= content.Length
                            || content[afterNext] == '\r'
                            || content[afterNext] == '\n'
                            || IsDelimiterAt(content, afterNext, delimiter);

                        if (secondIsFieldEnd)
                        {
                            // First " is an unescaped interior quote, second " is the closing quote.
                            // Escape the interior (output "") then output the closing ".
                            sb.Append('"');
                            sb.Append('"');
                            sb.Append('"');
                            inQuotedField = false;
                            i += 2;
                        }
                        else
                        {
                            // Genuine escape sequence: output both and stay in field
                            sb.Append('"');
                            sb.Append('"');
                            i += 2;
                        }
                    }
                    else if (content[next] == '\r' || content[next] == '\n' || IsDelimiterAt(content, next, delimiter))
                    {
                        // Closing quote before delimiter or newline
                        sb.Append('"');
                        inQuotedField = false;
                        atFieldStart = false;
                        i++;
                    }
                    else
                    {
                        // Unescaped interior quote: escape it
                        sb.Append('"');
                        sb.Append('"');
                        i++;
                    }
                }
            }
            else
            {
                if (atFieldStart && c == '"')
                {
                    sb.Append('"');
                    inQuotedField = true;
                    atFieldStart = false;
                }
                else
                {
                    sb.Append(c);
                    atFieldStart = c == '\n' || c == '\r' || c == delimLast;
                }
                i++;
            }
        }

        return sb.ToString();
    }

    private static bool IsDelimiterAt(string content, int index, string delimiter)
    {
        if (index + delimiter.Length > content.Length) return false;
        return content.AsSpan(index, delimiter.Length).SequenceEqual(delimiter.AsSpan());
    }

    private async Task MoveProcessedFileAsync(BlobContainerClient containerClient, string filePath, string timeStamp, string targetFolder)
    {
        try
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(targetFolder);

            timeStamp = string.IsNullOrWhiteSpace(timeStamp) ? DateTime.UtcNow.ToString("yyyyMMddHHmmssffff") : timeStamp;

            string dir = (Path.GetDirectoryName(filePath) ?? "").Replace('\\', '/');
            string archiveFilePath = string.IsNullOrEmpty(dir)
                ? $"{targetFolder}/{TargetFileName(Path.GetFileName(filePath), timeStamp)}"
                : $"{dir}/{targetFolder}/{TargetFileName(Path.GetFileName(filePath), timeStamp)}";

            BlobClient readClient = containerClient.GetBlobClient(filePath);
            BlobDownloadInfo blobDownloadInfo = await readClient.DownloadAsync();

            BlobClient blobClient = containerClient.GetBlobClient(archiveFilePath);
            await blobClient.UploadAsync(blobDownloadInfo.Content, overwrite: true);

            await readClient.DeleteIfExistsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw;
        }
    }

    private static string TargetFileName(string fileName, string timeStamp)
    {
        return $"{Path.GetFileNameWithoutExtension(fileName)}_{timeStamp}{Path.GetExtension(fileName)}";
    }
}
