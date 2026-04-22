namespace MagellanFileServices.Contracts;
public interface IFileServices
{
    void HandleFileError(string basePath, string fileName, string exceptionMessage, string timeStamp, List<string>? errors = null);
    Task HandleFileErrorAsync(BlobContainerClient containerClient, string filePath, string exceptionMessage, string timeStamp, List<string>? errors = null);
    void HandleFileSuccess(string basePath, string fileName, string timeStamp);
    Task HandleFileSuccessAsync(BlobContainerClient containerClient, string filePath, string timeStamp);
    ObjectResult<T> GetDataFromFile<T>(string filePath, Encoding encoding, bool skipEncodingHeader, string delimiter = ",");
    ObjectResult<T> GetDataFromFile<T>(string filePath, string delimiter = ",");
    ObjectResult<T> GetDataFromFile<T>(Stream stream, Encoding encoding, bool skipEncodingHeader, string delimiter = ",");
    ObjectResult<T> GetDataFromFile<T>(Stream stream, string delimiter = ",");
    void WriteDataToFile<T>(string filePath, List<T> data);
    void WriteDataToFile<T>(string filePath, List<T> data, bool printEncoding);
    void WriteDataToFile<T>(string filePath, List<T> data, string delimiter = ",", bool useHeaders = true, bool printEncoding = false);
    void WriteDataToFile<T>(string filePath, List<T> data, Encoding encoding, string delimiter = ",", bool useHeaders = true, bool printEncoding = false);
}
