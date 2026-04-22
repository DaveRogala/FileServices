namespace MagellanFileServices.Services;

public class FileServices : IFileServices
{
    private readonly ILogger<FileServices> _logger;
    public FileServices(ILogger<FileServices> logger)
    {
        _logger = logger;
    }    

    public virtual ObjectResult<T> GetDataFromFile<T>(string filePath, Encoding encoding, bool firstLineContainsEncoding, string delimiter = ",")
    {
        ObjectResult<T> result = new();
        try
        {
            using (StreamReader reader = new (filePath,encoding,detectEncodingFromByteOrderMarks:true))
            {
                result = GetDataFromFile<T>(reader.BaseStream, encoding, firstLineContainsEncoding, delimiter); 
            }            
            return result;
        }        
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw;
        }
    }
    public ObjectResult<T> GetDataFromFile<T>(string filePath, string delimiter = ",")
    {
        return GetDataFromFile<T>(filePath, encoding: Encoding.Default, firstLineContainsEncoding: false, delimiter);
    }

    public ObjectResult<T> GetDataFromFile<T>(Stream stream, Encoding encoding, bool firstLineContainsEncoding, string delimiter = ",")
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
            using (StreamReader reader = new(stream))
            {
                if (firstLineContainsEncoding)
                {
                    reader.ReadLine();
                }
                using (CsvReader csv = new(reader, config))
                {
                    result.ObjectResults = csv.GetRecords<T>().ToList();
                }
            }
            return result;
        }
        catch (CsvHelperException ex)
        {
            _logger.LogError(ex, ex.Message);
            result.Errors.Add($"{ex.Message}");
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
        return GetDataFromFile<T>(stream, encoding: Encoding.Default, firstLineContainsEncoding: false, delimiter);
    }
    public virtual void HandleFileError(string basePath, string fileName, string exceptionMessage, string timeStamp, List<string>? errors = null)
    {
        try
        {
            string targetFilePath = Path.Combine(basePath, "errors", TargetFileName(fileName, timeStamp));

            string sourceFilePath = Path.Combine(basePath, fileName);
            Directory.CreateDirectory(Path.Combine(basePath, "errors"));
            if (!string.IsNullOrWhiteSpace(basePath) && !string.IsNullOrWhiteSpace(fileName) && File.Exists(sourceFilePath))
            {
                File.Move(sourceFilePath, targetFilePath);
            }
            if (errors is not null)
            {
                foreach (string error in errors)
                {
                    exceptionMessage += $"\n\r{error}";
                }
            }
            File.WriteAllText(Path.Combine(basePath, "errors", $"Errors_{fileName}_{timeStamp}.txt"), exceptionMessage);
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

            timeStamp = String.IsNullOrWhiteSpace(timeStamp) ? DateTime.UtcNow.ToString("yyyyMMddHHmmssffff") : timeStamp;

            string archiveFilePath = Path.Combine(Path.GetDirectoryName(filePath) ?? "",
                                                  targetFolder,
                                                  $"{Path.GetFileNameWithoutExtension(filePath)}_{timeStamp}{Path.GetExtension(filePath)}");

            BlobClient blobClient = containerClient.GetBlobClient(archiveFilePath);

            BlobClient readClient = containerClient.GetBlobClient(filePath);
            BlobDownloadInfo blobDownLoadInfo = await readClient.DownloadAsync();

            await blobClient.UploadAsync(blobDownLoadInfo.Content, true);

            BlobClient deleteBlobClient = containerClient.GetBlobClient(filePath);

            await deleteBlobClient.DeleteIfExistsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw;
        }
    }
    public virtual async Task HandlFileErrorAsync(Stream stream, string blobConnectionString, string containerName, string filePath, string exceptionMessage, string timeStamp, List<string>? errors = null)
    {
        try
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(containerName);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(containerName);

            BlobServiceClient blobServiceClient = new(blobConnectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            await MoveProcessedFileAsync(containerClient, filePath, timeStamp,"errors");

            if (errors is not null)
            {
                foreach (string error in errors)
                {
                    exceptionMessage += $"\n\r{error}";
                }
            }
            string errorFileString = Path.Combine(Path.GetDirectoryName(filePath) ?? "",
                                                  "errors",
                                                  $"Errors_{Path.GetFileName(filePath)}_{timeStamp}.txt");

            BlobClient errorBlobClient = containerClient.GetBlobClient(errorFileString);

            await errorBlobClient.UploadAsync(BinaryData.FromString(exceptionMessage), overwrite: true);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw;
        }
    }
    public virtual async Task HandleFileSuccessAsync(Stream stream, string blobConnectionString, string containerName, string filePath, string timeStamp)
    {
        try
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(containerName);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(containerName);

            BlobServiceClient blobServiceClient = new (blobConnectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            await MoveProcessedFileAsync(containerClient, filePath, timeStamp, "processed");

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
            string sourceFilePath = Path.Combine(basePath, fileName);
            string targetFilePath = Path.Combine(basePath, "processed", TargetFileName(fileName, timeStamp));
            Directory.CreateDirectory(Path.Combine(basePath, "processed"));
            if (!string.IsNullOrWhiteSpace(basePath) && !string.IsNullOrWhiteSpace(fileName) && File.Exists(sourceFilePath))
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
    public bool WriteDataToFile<T>(string filePath, List<T> data)
    {
        return WriteDataToFile<T>(filePath, data, ",", true, false);
    }
    public bool WriteDataToFile<T>(string filePath, List<T> data, bool printEncodings)
    {
        return WriteDataToFile<T>(filePath, data, ",", true, printEncodings);
    }
    public bool WriteDataToFile<T>(string filePath, List<T> data, string delimiter = ",", bool useHeaders = true, bool printEncoding = false)
    {
        return WriteDataToFile<T>(filePath, data, Encoding.Default, delimiter, useHeaders, printEncoding);
    }
    public bool WriteDataToFile<T>(string filePath, List<T> data, Encoding encoding, string delimiter = ",", bool useHeaders = true, bool printEncoding = false)
    {
        try
        {
            CsvConfiguration config = new(CultureInfo.InvariantCulture)
            {                    
                Delimiter = delimiter,
                HasHeaderRecord = useHeaders,
                Encoding = encoding
            };
            using (StreamWriter writer = new(filePath,false, encoding))
            {
                using (CsvWriter csv = new(writer, config))
                {
                    if(printEncoding)
                    {
                        writer.WriteLine(encoding.HeaderName);
                    }
                    csv.WriteRecords<T>(data);
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw;
        }
    }
    private string TargetFileName(string fileName, string timeStamp)
    {
        if (fileName.Contains('.'))
        {
            string ext = fileName.Substring(fileName.LastIndexOf("."));
            return fileName.Replace(ext, $"_{timeStamp}{ext}");
        }
        else
        {
            return $"{fileName}_{timeStamp}";
        }
    }
}
