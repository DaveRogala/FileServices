using System.Diagnostics.CodeAnalysis;

namespace MagellanFileServices.Models;

/// <summary>
/// Wraps the outcome of a <see cref="Contracts.IFileServices.GetDataFromFile{T}(System.IO.Stream,string)"/> call,
/// separating successfully parsed records from row-level errors.
/// </summary>
/// <typeparam name="T">The record type returned by the read operation.</typeparam>
public class ObjectResult<T>
{
    /// <summary>
    /// Initialises an empty result with an empty error list.
    /// </summary>
    [SetsRequiredMembers]
    public ObjectResult()
    {
        Errors = [];
    }

    /// <summary>
    /// Initialises a result with pre-populated records and errors.
    /// </summary>
    /// <param name="objectResults">Parsed records, or <see langword="null"/> on a critical failure.</param>
    /// <param name="errors">Error messages collected during parsing.</param>
    [SetsRequiredMembers]
    public ObjectResult(List<T>? objectResults, List<string> errors)
    {
        ObjectResults = objectResults;
        Errors = errors;
    }

    /// <summary>
    /// Parsed records. <see langword="null"/> only when <see cref="CriticalError"/> is <see langword="true"/>.
    /// </summary>
    public List<T>? ObjectResults { get; set; }

    /// <summary>
    /// Row-level error messages collected during parsing. Empty when all rows parsed successfully.
    /// Each entry follows the format <c>row: {rowNumber} {message}</c>.
    /// </summary>
    public required List<string> Errors { get; set; }

    /// <summary>
    /// <see langword="true"/> when the file could not be opened or its CSV structure is fundamentally
    /// invalid. When set, <see cref="ObjectResults"/> will be <see langword="null"/> and
    /// <see cref="Errors"/> will contain the exception message.
    /// </summary>
    public bool CriticalError { get; set; } = false;
}
