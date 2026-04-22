namespace MagellanFileServices.Contracts;
public interface IFileServices
{
    void HandleFileError(string basePath, string fileName, string exceptionMessage, string timeStamp, List<string>? errors = null);
    Task HandlFileErrorAsync(Stream stream, string blobConnectionString, string containerName, string filePath, string exceptionMessage, string timeStamp, List<string>? errors = null);
    void HandleFileSuccess(string basePath, string fileName, string timeStamp);
    Task HandleFileSuccessAsync(Stream stream, string blobConnectionString, string containerName, string filePath, string timeStamp);
    ObjectResult<T> GetDataFromFile<T>(string filePath,Encoding encoding, bool firstLineContainsEncoding, string delimiter = ",");
    ObjectResult<T> GetDataFromFile<T>(string connectionString,string delimiter = ",");
    ObjectResult<T> GetDataFromFile<T>(Stream stream, Encoding encoding, bool firstLineContainsEncoding, string delimiter = ",");
    ObjectResult<T> GetDataFromFile<T>(Stream stream, string delimiter = ",");
    bool WriteDataToFile<T>(string filePath, List<T> data);
    bool WriteDataToFile<T>(string filePath, List<T> data, bool printEncodings);
    bool WriteDataToFile<T>(string filePath,List<T> data, string delimiter = ",", bool useHeaders = true,bool printEncoding = false);
    bool WriteDataToFile<T>(string filePath,List<T> data,Encoding encoding, string delimiter = ",", bool useHeaders = true, bool printEncoding = false);
}
