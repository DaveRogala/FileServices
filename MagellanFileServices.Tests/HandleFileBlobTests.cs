namespace MagellanFileServices.Tests;

public class HandleFileBlobTests
{
    private readonly FileServices _sut = new(Mock.Of<ILogger<FileServices>>());

    private const string FilePath  = "incoming/orders.csv";
    private const string Timestamp = "20240115120000";

    // ── Mock response factories ──────────────────────────────────────────────

    private static Response<BlobDownloadInfo> DownloadResponse() =>
        Response.FromValue(
            BlobsModelFactory.BlobDownloadInfo(content: new MemoryStream(Array.Empty<byte>())),
            new Mock<Response>().Object);

    private static Response<BlobContentInfo> UploadResponse() =>
        Response.FromValue(
            BlobsModelFactory.BlobContentInfo(
                eTag: new ETag("test"),
                lastModified: DateTimeOffset.UtcNow,
                contentHash: null,
                versionId: null,
                encryptionKeySha256: null,
                encryptionScope: null,
                blobSequenceNumber: 0),
            new Mock<Response>().Object);

    private static Response<bool> DeleteResponse() =>
        Response.FromValue(true, new Mock<Response>().Object);

    // Sets up the read → upload → delete chain on a container mock.
    private static (Mock<BlobContainerClient> container, Mock<BlobClient> readClient, Mock<BlobClient> archiveClient)
        SetupMoveMocks(string sourcePath, string archivePath)
    {
        var container    = new Mock<BlobContainerClient>();
        var readClient   = new Mock<BlobClient>();
        var archiveClient = new Mock<BlobClient>();

        container.Setup(c => c.GetBlobClient(sourcePath)).Returns(readClient.Object);
        container.Setup(c => c.GetBlobClient(archivePath)).Returns(archiveClient.Object);

        readClient.Setup(b => b.DownloadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DownloadResponse());
        archiveClient.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UploadResponse());
        readClient.Setup(b => b.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeleteResponse());

        return (container, readClient, archiveClient);
    }

    // ── HandleFileSuccessAsync argument guards ───────────────────────────────

    [Fact]
    public async Task HandleFileSuccessAsync_NullContainer_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.HandleFileSuccessAsync(null!, FilePath, Timestamp));
    }

    [Fact]
    public async Task HandleFileSuccessAsync_NullFilePath_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.HandleFileSuccessAsync(new Mock<BlobContainerClient>().Object, null!, Timestamp));
    }

    [Fact]
    public async Task HandleFileSuccessAsync_EmptyFilePath_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.HandleFileSuccessAsync(new Mock<BlobContainerClient>().Object, "", Timestamp));
    }

    // ── HandleFileSuccessAsync happy path ────────────────────────────────────

    [Fact]
    public async Task HandleFileSuccessAsync_UploadsToProcessedArchivePath()
    {
        string archivePath = "incoming/processed/orders_20240115120000.csv";
        var (container, _, _) = SetupMoveMocks(FilePath, archivePath);

        await _sut.HandleFileSuccessAsync(container.Object, FilePath, Timestamp);

        container.Verify(c => c.GetBlobClient(archivePath), Times.Once);
    }

    [Fact]
    public async Task HandleFileSuccessAsync_DeletesSourceBlob()
    {
        string archivePath = "incoming/processed/orders_20240115120000.csv";
        var (container, readClient, _) = SetupMoveMocks(FilePath, archivePath);

        await _sut.HandleFileSuccessAsync(container.Object, FilePath, Timestamp);

        readClient.Verify(b => b.DeleteIfExistsAsync(
            It.IsAny<DeleteSnapshotsOption>(),
            It.IsAny<BlobRequestConditions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleFileSuccessAsync_BlobAtContainerRoot_BuildsArchivePathCorrectly()
    {
        const string rootBlob    = "orders.csv";
        const string archivePath = "processed/orders_20240115120000.csv";
        var (container, _, _) = SetupMoveMocks(rootBlob, archivePath);

        await _sut.HandleFileSuccessAsync(container.Object, rootBlob, Timestamp);

        container.Verify(c => c.GetBlobClient(archivePath), Times.Once);
    }

    // ── HandleFileErrorAsync argument guards ─────────────────────────────────

    [Fact]
    public async Task HandleFileErrorAsync_NullContainer_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.HandleFileErrorAsync(null!, FilePath, "error", Timestamp));
    }

    [Fact]
    public async Task HandleFileErrorAsync_NullFilePath_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.HandleFileErrorAsync(new Mock<BlobContainerClient>().Object, null!, "error", Timestamp));
    }

    [Fact]
    public async Task HandleFileErrorAsync_EmptyFilePath_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.HandleFileErrorAsync(new Mock<BlobContainerClient>().Object, "", "error", Timestamp));
    }

    // ── HandleFileErrorAsync happy path ──────────────────────────────────────

    [Fact]
    public async Task HandleFileErrorAsync_MovesBlob_ToErrorsArchivePath()
    {
        const string archivePath  = "incoming/errors/orders_20240115120000.csv";
        const string errorLogPath = "incoming/errors/Errors_orders.csv_20240115120000.txt";

        var (container, _, _) = SetupMoveMocks(FilePath, archivePath);
        var mockErrorLog = new Mock<BlobClient>();
        container.Setup(c => c.GetBlobClient(errorLogPath)).Returns(mockErrorLog.Object);
        mockErrorLog.Setup(b => b.UploadAsync(It.IsAny<BinaryData>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UploadResponse());

        await _sut.HandleFileErrorAsync(container.Object, FilePath, "fail", Timestamp);

        container.Verify(c => c.GetBlobClient(archivePath), Times.Once);
    }

    [Fact]
    public async Task HandleFileErrorAsync_UploadsErrorLogBlob()
    {
        const string archivePath  = "incoming/errors/orders_20240115120000.csv";
        const string errorLogPath = "incoming/errors/Errors_orders.csv_20240115120000.txt";

        var (container, _, _) = SetupMoveMocks(FilePath, archivePath);
        var mockErrorLog = new Mock<BlobClient>();
        container.Setup(c => c.GetBlobClient(errorLogPath)).Returns(mockErrorLog.Object);
        mockErrorLog.Setup(b => b.UploadAsync(It.IsAny<BinaryData>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UploadResponse());

        await _sut.HandleFileErrorAsync(container.Object, FilePath, "fail", Timestamp);

        mockErrorLog.Verify(b => b.UploadAsync(
            It.IsAny<BinaryData>(), true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleFileErrorAsync_ErrorLogContainsExceptionMessage()
    {
        const string archivePath  = "incoming/errors/orders_20240115120000.csv";
        const string errorLogPath = "incoming/errors/Errors_orders.csv_20240115120000.txt";

        var (container, _, _) = SetupMoveMocks(FilePath, archivePath);
        var mockErrorLog = new Mock<BlobClient>();
        container.Setup(c => c.GetBlobClient(errorLogPath)).Returns(mockErrorLog.Object);

        BinaryData? captured = null;
        mockErrorLog
            .Setup(b => b.UploadAsync(It.IsAny<BinaryData>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<BinaryData, bool, CancellationToken>((data, _, _) => captured = data)
            .ReturnsAsync(UploadResponse());

        await _sut.HandleFileErrorAsync(container.Object, FilePath, "Disk quota exceeded", Timestamp);

        Assert.NotNull(captured);
        Assert.Contains("Disk quota exceeded", captured!.ToString());
    }

    [Fact]
    public async Task HandleFileErrorAsync_ErrorLogContainsRowLevelErrors()
    {
        const string archivePath  = "incoming/errors/orders_20240115120000.csv";
        const string errorLogPath = "incoming/errors/Errors_orders.csv_20240115120000.txt";

        var (container, _, _) = SetupMoveMocks(FilePath, archivePath);
        var mockErrorLog = new Mock<BlobClient>();
        container.Setup(c => c.GetBlobClient(errorLogPath)).Returns(mockErrorLog.Object);

        BinaryData? captured = null;
        mockErrorLog
            .Setup(b => b.UploadAsync(It.IsAny<BinaryData>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<BinaryData, bool, CancellationToken>((data, _, _) => captured = data)
            .ReturnsAsync(UploadResponse());

        var errors = new List<string> { "row 3: invalid amount", "row 7: missing name" };
        await _sut.HandleFileErrorAsync(container.Object, FilePath, "Parse failed", Timestamp, errors);

        Assert.NotNull(captured);
        string log = captured!.ToString();
        Assert.Contains("row 3: invalid amount", log);
        Assert.Contains("row 7: missing name", log);
    }
}
