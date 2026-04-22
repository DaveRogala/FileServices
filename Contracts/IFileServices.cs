namespace MagellanFileServices.Contracts;

/// <summary>
/// Provides file-import utilities for ETL pipelines: reading and writing CSV data,
/// and archiving processed or failed files on local file systems and Azure Blob Storage.
/// </summary>
public interface IFileServices
{
    /// <summary>
    /// Moves a failed local file to an <c>errors</c> subfolder and writes an error log.
    /// </summary>
    /// <remarks>
    /// The <c>errors</c> subfolder is created under <paramref name="basePath"/> if it does not exist.
    /// The archived file is renamed to <c>{name}_{timeStamp}{ext}</c>.
    /// An <c>Errors_{fileName}_{timeStamp}.txt</c> log is written even when the source file is missing.
    /// </remarks>
    /// <param name="basePath">Directory containing the source file.</param>
    /// <param name="fileName">Name of the file to archive (filename only, not a full path).</param>
    /// <param name="exceptionMessage">Primary error description written to the log.</param>
    /// <param name="timeStamp">Timestamp appended to the archived filename and log filename, e.g. <c>yyyyMMddHHmmss</c>.</param>
    /// <param name="errors">Optional row-level error strings appended to the log below the primary message.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="basePath"/> or <paramref name="fileName"/> is <see langword="null"/> or whitespace.</exception>
    void HandleFileError(string basePath, string fileName, string exceptionMessage, string timeStamp, List<string>? errors = null);

    /// <summary>
    /// Moves a failed Azure Blob to an <c>errors</c> subfolder within the same container and uploads an error log blob.
    /// </summary>
    /// <remarks>
    /// The source blob is downloaded, re-uploaded at the archive path, then deleted.
    /// Blob paths use <c>/</c> as the separator regardless of the host operating system.
    /// </remarks>
    /// <param name="containerClient">Client scoped to the container that holds the blob.</param>
    /// <param name="filePath">Path of the blob within the container, e.g. <c>incoming/orders.csv</c>.</param>
    /// <param name="exceptionMessage">Primary error description written to the log blob.</param>
    /// <param name="timeStamp">Timestamp appended to the archived blob name and log blob name.</param>
    /// <param name="errors">Optional row-level error strings appended to the log blob below the primary message.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="containerClient"/> or <paramref name="filePath"/> is <see langword="null"/> or whitespace.</exception>
    Task HandleFileErrorAsync(BlobContainerClient containerClient, string filePath, string exceptionMessage, string timeStamp, List<string>? errors = null);

    /// <summary>
    /// Moves a successfully processed local file to a <c>processed</c> subfolder.
    /// </summary>
    /// <remarks>
    /// The <c>processed</c> subfolder is created under <paramref name="basePath"/> if it does not exist.
    /// The archived file is renamed to <c>{name}_{timeStamp}{ext}</c>.
    /// </remarks>
    /// <param name="basePath">Directory containing the source file.</param>
    /// <param name="fileName">Name of the file to archive (filename only, not a full path).</param>
    /// <param name="timeStamp">Timestamp appended to the archived filename, e.g. <c>yyyyMMddHHmmss</c>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="basePath"/> or <paramref name="fileName"/> is <see langword="null"/> or whitespace.</exception>
    void HandleFileSuccess(string basePath, string fileName, string timeStamp);

    /// <summary>
    /// Moves a successfully processed Azure Blob to a <c>processed</c> subfolder within the same container.
    /// </summary>
    /// <remarks>
    /// The source blob is downloaded, re-uploaded at the archive path, then deleted.
    /// Blob paths use <c>/</c> as the separator regardless of the host operating system.
    /// </remarks>
    /// <param name="containerClient">Client scoped to the container that holds the blob.</param>
    /// <param name="filePath">Path of the blob within the container, e.g. <c>incoming/orders.csv</c>.</param>
    /// <param name="timeStamp">Timestamp appended to the archived blob name.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="containerClient"/> or <paramref name="filePath"/> is <see langword="null"/> or whitespace.</exception>
    Task HandleFileSuccessAsync(BlobContainerClient containerClient, string filePath, string timeStamp);

    /// <summary>
    /// Reads a CSV file into a list of <typeparamref name="T"/> records.
    /// </summary>
    /// <remarks>
    /// Row-level parse errors are collected in <see cref="ObjectResult{T}.Errors"/> rather than thrown,
    /// and parsing continues with the next row. <see cref="ObjectResult{T}.CriticalError"/> is only set
    /// when the file cannot be opened or the CSV structure is fundamentally invalid.
    /// </remarks>
    /// <typeparam name="T">Record type whose properties are matched to CSV headers by name.</typeparam>
    /// <param name="filePath">Full path to the CSV file.</param>
    /// <param name="encoding">Encoding used to read the file. BOM detection is also enabled.</param>
    /// <param name="skipEncodingHeader">
    /// <see langword="true"/> to discard the first line before parsing begins,
    /// for files whose first line is an encoding declaration rather than column headers.
    /// </param>
    /// <param name="delimiter">Column delimiter. Defaults to <c>,</c>.</param>
    /// <returns>An <see cref="ObjectResult{T}"/> containing parsed records and any row-level errors.</returns>
    ObjectResult<T> GetDataFromFile<T>(string filePath, Encoding encoding, bool skipEncodingHeader, string delimiter = ",");

    /// <summary>
    /// Reads a CSV file into a list of <typeparamref name="T"/> records using UTF-8 encoding and a comma delimiter.
    /// </summary>
    /// <typeparam name="T">Record type whose properties are matched to CSV headers by name.</typeparam>
    /// <param name="filePath">Full path to the CSV file.</param>
    /// <param name="delimiter">Column delimiter. Defaults to <c>,</c>.</param>
    /// <returns>An <see cref="ObjectResult{T}"/> containing parsed records and any row-level errors.</returns>
    ObjectResult<T> GetDataFromFile<T>(string filePath, string delimiter = ",");

    /// <summary>
    /// Reads a CSV stream into a list of <typeparamref name="T"/> records.
    /// </summary>
    /// <remarks>
    /// Row-level parse errors are collected in <see cref="ObjectResult{T}.Errors"/> rather than thrown.
    /// The stream is not disposed by this method.
    /// </remarks>
    /// <typeparam name="T">Record type whose properties are matched to CSV headers by name.</typeparam>
    /// <param name="stream">Readable stream positioned at the start of the CSV content.</param>
    /// <param name="encoding">Encoding used to decode the stream. BOM detection is also enabled.</param>
    /// <param name="skipEncodingHeader">
    /// <see langword="true"/> to discard the first line before parsing begins.
    /// </param>
    /// <param name="delimiter">Column delimiter. Defaults to <c>,</c>.</param>
    /// <returns>An <see cref="ObjectResult{T}"/> containing parsed records and any row-level errors.</returns>
    ObjectResult<T> GetDataFromFile<T>(Stream stream, Encoding encoding, bool skipEncodingHeader, string delimiter = ",");

    /// <summary>
    /// Reads a CSV stream into a list of <typeparamref name="T"/> records using UTF-8 encoding and a comma delimiter.
    /// </summary>
    /// <typeparam name="T">Record type whose properties are matched to CSV headers by name.</typeparam>
    /// <param name="stream">Readable stream positioned at the start of the CSV content.</param>
    /// <param name="delimiter">Column delimiter. Defaults to <c>,</c>.</param>
    /// <returns>An <see cref="ObjectResult{T}"/> containing parsed records and any row-level errors.</returns>
    ObjectResult<T> GetDataFromFile<T>(Stream stream, string delimiter = ",");

    /// <summary>
    /// Writes <paramref name="data"/> to a CSV file using UTF-8 encoding, a comma delimiter, and a header row.
    /// </summary>
    /// <typeparam name="T">Record type. Public properties are written as columns.</typeparam>
    /// <param name="filePath">Destination file path. Overwritten if it already exists.</param>
    /// <param name="data">Records to write.</param>
    /// <exception cref="Exception">Re-thrown after logging if the file cannot be written.</exception>
    void WriteDataToFile<T>(string filePath, List<T> data);

    /// <summary>
    /// Writes <paramref name="data"/> to a CSV file, optionally prefixing the output with the encoding name.
    /// </summary>
    /// <typeparam name="T">Record type. Public properties are written as columns.</typeparam>
    /// <param name="filePath">Destination file path. Overwritten if it already exists.</param>
    /// <param name="data">Records to write.</param>
    /// <param name="printEncoding">
    /// When <see langword="true"/>, writes <c>encoding.HeaderName</c> as the first line so that
    /// consumers can detect the encoding using <c>skipEncodingHeader</c> when reading back.
    /// </param>
    void WriteDataToFile<T>(string filePath, List<T> data, bool printEncoding);

    /// <summary>
    /// Writes <paramref name="data"/> to a CSV file with a configurable delimiter, header row, and encoding prefix.
    /// Uses UTF-8 encoding.
    /// </summary>
    /// <typeparam name="T">Record type. Public properties are written as columns.</typeparam>
    /// <param name="filePath">Destination file path. Overwritten if it already exists.</param>
    /// <param name="data">Records to write.</param>
    /// <param name="delimiter">Column delimiter. Defaults to <c>,</c>.</param>
    /// <param name="useHeaders">When <see langword="false"/>, the header row is omitted.</param>
    /// <param name="printEncoding">When <see langword="true"/>, writes the encoding name as the first line.</param>
    void WriteDataToFile<T>(string filePath, List<T> data, string delimiter = ",", bool useHeaders = true, bool printEncoding = false);

    /// <summary>
    /// Writes <paramref name="data"/> to a CSV file with full control over encoding, delimiter, header row, and encoding prefix.
    /// </summary>
    /// <typeparam name="T">Record type. Public properties are written as columns.</typeparam>
    /// <param name="filePath">Destination file path. Overwritten if it already exists.</param>
    /// <param name="data">Records to write.</param>
    /// <param name="encoding">Encoding for the output file.</param>
    /// <param name="delimiter">Column delimiter. Defaults to <c>,</c>.</param>
    /// <param name="useHeaders">When <see langword="false"/>, the header row is omitted.</param>
    /// <param name="printEncoding">When <see langword="true"/>, writes <c>encoding.HeaderName</c> as the first line.</param>
    void WriteDataToFile<T>(string filePath, List<T> data, Encoding encoding, string delimiter = ",", bool useHeaders = true, bool printEncoding = false);
}
