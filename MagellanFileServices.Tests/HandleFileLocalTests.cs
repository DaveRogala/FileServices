namespace MagellanFileServices.Tests;

public class HandleFileLocalTests : IDisposable
{
    private readonly FileServices _sut = new(Mock.Of<ILogger<FileServices>>());
    private readonly string _tempDir;

    private const string Timestamp = "20240115120000";

    public HandleFileLocalTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"magellan_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private void CreateFile(string fileName, string content = "col1,col2\nval1,val2")
        => File.WriteAllText(Path.Combine(_tempDir, fileName), content);

    // ── HandleFileError ──────────────────────────────────────────────────────

    [Fact]
    public void HandleFileError_MovesSourceFile_ToErrorsSubfolder()
    {
        CreateFile("orders.csv");

        _sut.HandleFileError(_tempDir, "orders.csv", "Something failed", Timestamp);

        Assert.False(File.Exists(Path.Combine(_tempDir, "orders.csv")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "errors", $"orders_{Timestamp}.csv")));
    }

    [Fact]
    public void HandleFileError_CreatesErrorsDirectory_WhenAbsent()
    {
        CreateFile("orders.csv");

        _sut.HandleFileError(_tempDir, "orders.csv", "fail", Timestamp);

        Assert.True(Directory.Exists(Path.Combine(_tempDir, "errors")));
    }

    [Fact]
    public void HandleFileError_WritesErrorLogFile()
    {
        CreateFile("orders.csv");

        _sut.HandleFileError(_tempDir, "orders.csv", "Main error message", Timestamp);

        string logPath = Path.Combine(_tempDir, "errors", $"Errors_orders.csv_{Timestamp}.txt");
        Assert.True(File.Exists(logPath));
        Assert.Contains("Main error message", File.ReadAllText(logPath));
    }

    [Fact]
    public void HandleFileError_AppendsErrorList_ToLogFile()
    {
        CreateFile("orders.csv");
        var errors = new List<string> { "row 2: bad value", "row 5: missing field" };

        _sut.HandleFileError(_tempDir, "orders.csv", "Parse failed", Timestamp, errors);

        string log = File.ReadAllText(Path.Combine(_tempDir, "errors", $"Errors_orders.csv_{Timestamp}.txt"));
        Assert.Contains("row 2: bad value", log);
        Assert.Contains("row 5: missing field", log);
    }

    [Fact]
    public void HandleFileError_MissingSourceFile_StillWritesErrorLog()
    {
        var ex = Record.Exception(() =>
            _sut.HandleFileError(_tempDir, "missing.csv", "Something failed", Timestamp));

        Assert.Null(ex);
        Assert.True(File.Exists(Path.Combine(_tempDir, "errors", $"Errors_missing.csv_{Timestamp}.txt")));
    }

    [Fact]
    public void HandleFileError_NullBasePath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _sut.HandleFileError(null!, "orders.csv", "error", Timestamp));
    }

    [Fact]
    public void HandleFileError_NullFileName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _sut.HandleFileError(_tempDir, null!, "error", Timestamp));
    }

    // ── HandleFileSuccess ────────────────────────────────────────────────────

    [Fact]
    public void HandleFileSuccess_MovesSourceFile_ToProcessedSubfolder()
    {
        CreateFile("orders.csv");

        _sut.HandleFileSuccess(_tempDir, "orders.csv", Timestamp);

        Assert.False(File.Exists(Path.Combine(_tempDir, "orders.csv")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "processed", $"orders_{Timestamp}.csv")));
    }

    [Fact]
    public void HandleFileSuccess_CreatesProcessedDirectory_WhenAbsent()
    {
        CreateFile("orders.csv");

        _sut.HandleFileSuccess(_tempDir, "orders.csv", Timestamp);

        Assert.True(Directory.Exists(Path.Combine(_tempDir, "processed")));
    }

    [Fact]
    public void HandleFileSuccess_MissingSourceFile_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
            _sut.HandleFileSuccess(_tempDir, "missing.csv", Timestamp));

        Assert.Null(ex);
    }

    [Fact]
    public void HandleFileSuccess_NullBasePath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _sut.HandleFileSuccess(null!, "orders.csv", Timestamp));
    }

    [Fact]
    public void HandleFileSuccess_NullFileName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _sut.HandleFileSuccess(_tempDir, null!, Timestamp));
    }

    // ── TargetFileName behaviour (via HandleFileSuccess) ─────────────────────

    [Fact]
    public void HandleFileSuccess_DottedFileName_TimestampsBeforeExtension()
    {
        CreateFile("report.2024.csv");

        _sut.HandleFileSuccess(_tempDir, "report.2024.csv", Timestamp);

        Assert.True(File.Exists(Path.Combine(_tempDir, "processed", $"report.2024_{Timestamp}.csv")));
    }

    [Fact]
    public void HandleFileSuccess_FileWithNoExtension_AppendsTimestamp()
    {
        CreateFile("datafile");

        _sut.HandleFileSuccess(_tempDir, "datafile", Timestamp);

        Assert.True(File.Exists(Path.Combine(_tempDir, "processed", $"datafile_{Timestamp}")));
    }
}
