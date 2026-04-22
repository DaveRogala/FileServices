namespace MagellanFileServices.Services;

public class FileServices : IFileServices
{
    private readonly ILogger<FileServices> _logger;

    private const string ErrorFolder = "errors";
    private const string ProcessedFolder = "processed";

    public FileServices(ILogger<FileServices> logger)
    {
        _logger = logger;
    }

    public virtual ObjectResult<T> GetDataFromFile<T>(string filePath, Encoding encoding, bool skipEncodingHeader, string delimiter = ",")
    {
        try
        {
            using Stream stream = File.OpenRead(filePath);
            return GetDataFromFile<T>(stream, encoding, skipEncodingHeader, delimiter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw;
        }
    }

    public ObjectResult<T> GetDataFromFile<T>(string filePath, string delimiter = ",")
    {
        return GetDataFromFile<T>(filePath, Encoding.UTF8, skipEncodingHeader: false, delimiter);
    }

    public ObjectResult<T> GetDataFromFile<T>(Stream stream, Encoding encoding, bool skipEncodingHeader, string delimiter = ",")
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
            if (skipEncodingHeader)
            {
                reader.ReadLine();
            }
            using CsvReader csv = new(reader, config);
            result.ObjectResults = csv.GetRecords<T>().ToList();
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

    public ObjectResult<T> GetDataFromFile<T>(Stream stream, string delimiter = ",")
    {
        return GetDataFromFile<T>(stream, Encoding.UTF8, skipEncodingHeader: false, delimiter);
    }

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

    public void WriteDataToFile<T>(string filePath, List<T> data)
    {
        WriteDataToFile<T>(filePath, data, ",", true, false);
    }

    public void WriteDataToFile<T>(string filePath, List<T> data, bool printEncoding)
    {
        WriteDataToFile<T>(filePath, data, ",", true, printEncoding);
    }

    public void WriteDataToFile<T>(string filePath, List<T> data, string delimiter = ",", bool useHeaders = true, bool printEncoding = false)
    {
        WriteDataToFile<T>(filePath, data, Encoding.UTF8, delimiter, useHeaders, printEncoding);
    }

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
