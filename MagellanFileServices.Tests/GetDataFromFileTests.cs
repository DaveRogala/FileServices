namespace MagellanFileServices.Tests;

public class GetDataFromFileTests
{
    private readonly FileServices _sut = new(Mock.Of<ILogger<FileServices>>());

    private static Stream CsvStream(string content)
        => new MemoryStream(Encoding.UTF8.GetBytes(content));

    [Fact]
    public void GetDataFromFile_ValidCsv_ReturnsAllRecords()
    {
        using var stream = CsvStream("Id,Name,Amount\n1,Alice,10.5\n2,Bob,20.0");

        var result = _sut.GetDataFromFile<TestRecord>(stream);

        Assert.Empty(result.Errors);
        Assert.False(result.CriticalError);
        Assert.Equal(2, result.ObjectResults?.Count);
        Assert.Equal("Alice", result.ObjectResults![0].Name);
        Assert.Equal(10.5m, result.ObjectResults[0].Amount);
    }

    [Fact]
    public void GetDataFromFile_HeaderOnly_ReturnsEmptyList()
    {
        using var stream = CsvStream("Id,Name,Amount\n");

        var result = _sut.GetDataFromFile<TestRecord>(stream);

        Assert.Empty(result.Errors);
        Assert.False(result.CriticalError);
        Assert.Empty(result.ObjectResults!);
    }

    [Fact]
    public void GetDataFromFile_BadRow_AddsRowError()
    {
        using var stream = CsvStream("Id,Name,Amount\nnot-a-number,Alice,10.5\n2,Bob,20.0");

        var result = _sut.GetDataFromFile<TestRecord>(stream);

        Assert.NotEmpty(result.Errors);
        Assert.Contains("not-a-number", result.Errors[0]);
    }

    [Fact]
    public void GetDataFromFile_BadRow_DoesNotSetCriticalError()
    {
        using var stream = CsvStream("Id,Name,Amount\nnot-a-number,Alice,10.5");

        var result = _sut.GetDataFromFile<TestRecord>(stream);

        Assert.False(result.CriticalError);
    }

    [Fact]
    public void GetDataFromFile_BadRow_ContinuesParsingRemainingRows()
    {
        using var stream = CsvStream("Id,Name,Amount\nnot-a-number,Alice,10.5\n2,Bob,20.0");

        var result = _sut.GetDataFromFile<TestRecord>(stream);

        Assert.Single(result.ObjectResults!);
        Assert.Equal("Bob", result.ObjectResults![0].Name);
    }

    [Fact]
    public void GetDataFromFile_SkipEncodingHeader_SkipsFirstLine()
    {
        using var stream = CsvStream("utf-8\nId,Name,Amount\n1,Alice,10.5");

        var result = _sut.GetDataFromFile<TestRecord>(stream, Encoding.UTF8, skipEncodingHeader: true);

        Assert.Empty(result.Errors);
        Assert.Single(result.ObjectResults!);
        Assert.Equal("Alice", result.ObjectResults![0].Name);
    }

    [Fact]
    public void GetDataFromFile_WithoutSkipEncodingHeader_TreatsFirstLineAsHeader()
    {
        using var stream = CsvStream("Id,Name,Amount\n1,Alice,10.5");

        var result = _sut.GetDataFromFile<TestRecord>(stream, Encoding.UTF8, skipEncodingHeader: false);

        Assert.Empty(result.Errors);
        Assert.Single(result.ObjectResults!);
    }

    [Fact]
    public void GetDataFromFile_TabDelimiter_ParsesCorrectly()
    {
        using var stream = CsvStream("Id\tName\tAmount\n1\tAlice\t10.5");

        var result = _sut.GetDataFromFile<TestRecord>(stream, Encoding.UTF8, skipEncodingHeader: false, delimiter: "\t");

        Assert.Empty(result.Errors);
        Assert.Single(result.ObjectResults!);
        Assert.Equal("Alice", result.ObjectResults![0].Name);
    }

    [Fact]
    public void GetDataFromFile_IgnoresBlankLines()
    {
        using var stream = CsvStream("Id,Name,Amount\n1,Alice,10.5\n\n2,Bob,20.0");

        var result = _sut.GetDataFromFile<TestRecord>(stream);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.ObjectResults?.Count);
    }

    [Fact]
    public void GetDataFromFile_PipeDelimiterConvenienceOverload_ParsesCorrectly()
    {
        using var stream = CsvStream("Id|Name|Amount\n1|Alice|10.5");

        var result = _sut.GetDataFromFile<TestRecord>(stream, delimiter: "|");

        Assert.Empty(result.Errors);
        Assert.Single(result.ObjectResults!);
    }

    [Fact]
    public void GetDataFromFile_RowsToSkip_SkipsSpecifiedRows()
    {
        using var stream = CsvStream("meta1\nmeta2\nmeta3\nId,Name,Amount\n1,Alice,10.5");

        var result = _sut.GetDataFromFile<TestRecord>(stream, Encoding.UTF8, rowsToSkip: 3);

        Assert.Empty(result.Errors);
        Assert.Single(result.ObjectResults!);
        Assert.Equal("Alice", result.ObjectResults![0].Name);
    }

    [Fact]
    public void GetDataFromFile_RowsToSkip_Zero_SkipsNothing()
    {
        using var stream = CsvStream("Id,Name,Amount\n1,Alice,10.5");

        var result = _sut.GetDataFromFile<TestRecord>(stream, Encoding.UTF8, rowsToSkip: 0);

        Assert.Empty(result.Errors);
        Assert.Single(result.ObjectResults!);
    }

    [Fact]
    public void GetDataFromFile_RowsToSkip_One_BehavesLikeSkipEncodingHeader()
    {
        using var streamA = CsvStream("utf-8\nId,Name,Amount\n1,Alice,10.5");
        using var streamB = CsvStream("utf-8\nId,Name,Amount\n1,Alice,10.5");

        var resultSkipFlag = _sut.GetDataFromFile<TestRecord>(streamA, Encoding.UTF8, skipEncodingHeader: true);
        var resultRowsToSkip = _sut.GetDataFromFile<TestRecord>(streamB, Encoding.UTF8, rowsToSkip: 1);

        Assert.Equal(resultSkipFlag.ObjectResults?.Count, resultRowsToSkip.ObjectResults?.Count);
        Assert.Equal(resultSkipFlag.ObjectResults![0].Name, resultRowsToSkip.ObjectResults![0].Name);
    }

    [Fact]
    public void GetDataFromFile_RowsToSkip_FileOverload_SkipsRows()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "report_header\nsource_system\nId,Name,Amount\n1,Alice,10.5", Encoding.UTF8);

            var result = _sut.GetDataFromFile<TestRecord>(tempFile, Encoding.UTF8, rowsToSkip: 2);

            Assert.Empty(result.Errors);
            Assert.Single(result.ObjectResults!);
            Assert.Equal("Alice", result.ObjectResults![0].Name);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void GetDataFromFile_FileNotFound_Throws()
    {
        Assert.Throws<FileNotFoundException>(() =>
            _sut.GetDataFromFile<TestRecord>("/nonexistent/path/file.csv"));
    }

    [Fact]
    public void GetDataFromFile_FilePath_ReadsFileCorrectly()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "Id,Name,Amount\n1,Alice,10.5", Encoding.UTF8);

            var result = _sut.GetDataFromFile<TestRecord>(tempFile);

            Assert.Empty(result.Errors);
            Assert.Single(result.ObjectResults!);
            Assert.Equal("Alice", result.ObjectResults![0].Name);
        }
        finally { File.Delete(tempFile); }
    }
}
