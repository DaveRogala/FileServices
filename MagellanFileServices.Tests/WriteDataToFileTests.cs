namespace MagellanFileServices.Tests;

public class WriteDataToFileTests
{
    private readonly FileServices _sut = new(Mock.Of<ILogger<FileServices>>());

    private static List<TestRecord> SampleRecords() =>
    [
        new() { Id = 1, Name = "Alice", Amount = 10.5m },
        new() { Id = 2, Name = "Bob",   Amount = 20.0m }
    ];

    [Fact]
    public void WriteDataToFile_WritesHeaderRow()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            _sut.WriteDataToFile(tempFile, SampleRecords());

            string content = File.ReadAllText(tempFile);
            Assert.Contains("Id", content);
            Assert.Contains("Name", content);
            Assert.Contains("Amount", content);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void WriteDataToFile_WritesRecordData()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            _sut.WriteDataToFile(tempFile, SampleRecords());

            string content = File.ReadAllText(tempFile);
            Assert.Contains("Alice", content);
            Assert.Contains("Bob", content);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void WriteDataToFile_UseHeadersFalse_OmitsHeaderRow()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            _sut.WriteDataToFile(tempFile, SampleRecords(), useHeaders: false);

            string content = File.ReadAllText(tempFile);
            Assert.DoesNotContain("Name", content);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void WriteDataToFile_CustomDelimiter_UsesDelimiterInOutput()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            _sut.WriteDataToFile(tempFile, SampleRecords(), delimiter: "|");

            string content = File.ReadAllText(tempFile);
            Assert.Contains("|", content);
            Assert.DoesNotContain(",", content);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void WriteDataToFile_PrintEncoding_WritesEncodingNameAsFirstLine()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            _sut.WriteDataToFile(tempFile, SampleRecords(), printEncoding: true);

            string firstLine = File.ReadLines(tempFile).First();
            Assert.Equal(Encoding.UTF8.HeaderName, firstLine);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void WriteDataToFile_PrintEncodingFalse_DoesNotWriteEncodingLine()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            _sut.WriteDataToFile(tempFile, SampleRecords(), printEncoding: false);

            string firstLine = File.ReadLines(tempFile).First();
            Assert.NotEqual(Encoding.UTF8.HeaderName, firstLine);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void WriteDataToFile_EmptyList_WritesOnlyHeader()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            _sut.WriteDataToFile(tempFile, new List<TestRecord>());

            string[] lines = File.ReadAllLines(tempFile);
            Assert.Single(lines); // just the header
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void WriteDataToFile_ThenRead_RoundTrips()
    {
        string tempFile = Path.GetTempFileName();
        var original = SampleRecords();
        try
        {
            _sut.WriteDataToFile(tempFile, original);
            var result = _sut.GetDataFromFile<TestRecord>(tempFile);

            Assert.Empty(result.Errors);
            Assert.Equal(original.Count, result.ObjectResults?.Count);
            Assert.Equal(original[0].Name,   result.ObjectResults![0].Name);
            Assert.Equal(original[0].Amount, result.ObjectResults[0].Amount);
            Assert.Equal(original[1].Id,     result.ObjectResults[1].Id);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void WriteDataToFile_CustomDelimiter_ThenReadWithSameDelimiter_RoundTrips()
    {
        string tempFile = Path.GetTempFileName();
        var original = SampleRecords();
        try
        {
            _sut.WriteDataToFile(tempFile, original, delimiter: "\t");
            var result = _sut.GetDataFromFile<TestRecord>(tempFile, delimiter: "\t");

            Assert.Empty(result.Errors);
            Assert.Equal(original.Count, result.ObjectResults?.Count);
            Assert.Equal("Alice", result.ObjectResults![0].Name);
        }
        finally { File.Delete(tempFile); }
    }
}
