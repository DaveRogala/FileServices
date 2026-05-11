namespace MagellanFileServices.Tests;

public class WriteDataToBlobTests
{
    private readonly FileServices _sut = new(Mock.Of<ILogger<FileServices>>());

    private static readonly List<TestRecord> SampleData =
    [
        new() { Id = 1, Name = "Alice", Amount = 10.5m },
        new() { Id = 2, Name = "Bob",   Amount = 20.0m },
    ];

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

    private static (Mock<BlobContainerClient> container, Mock<BlobClient> blobClient) SetupUploadMock(string blobPath)
    {
        var container  = new Mock<BlobContainerClient>();
        var blobClient = new Mock<BlobClient>();
        container.Setup(c => c.GetBlobClient(blobPath)).Returns(blobClient.Object);
        blobClient.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UploadResponse());
        return (container, blobClient);
    }

    // ── Argument guards ──────────────────────────────────────────────────────

    [Fact]
    public async Task WriteDataToBlobAsync_NullContainer_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.WriteDataToBlobAsync(null!, "output/data.csv", SampleData));
    }

    [Fact]
    public async Task WriteDataToBlobAsync_NullBlobPath_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.WriteDataToBlobAsync(new Mock<BlobContainerClient>().Object, null!, SampleData));
    }

    [Fact]
    public async Task WriteDataToBlobAsync_EmptyBlobPath_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.WriteDataToBlobAsync(new Mock<BlobContainerClient>().Object, "", SampleData));
    }

    // ── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteDataToBlobAsync_CallsUploadOnTargetBlob()
    {
        const string blobPath = "output/orders.csv";
        var (container, blobClient) = SetupUploadMock(blobPath);

        await _sut.WriteDataToBlobAsync(container.Object, blobPath, SampleData);

        blobClient.Verify(b => b.UploadAsync(
            It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WriteDataToBlobAsync_UploadedContentContainsRecords()
    {
        const string blobPath = "output/orders.csv";
        var (container, blobClient) = SetupUploadMock(blobPath);

        string? captured = null;
        blobClient
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, bool, CancellationToken>((s, _, _) =>
                captured = new StreamReader(s).ReadToEnd())
            .ReturnsAsync(UploadResponse());

        await _sut.WriteDataToBlobAsync(container.Object, blobPath, SampleData);

        Assert.NotNull(captured);
        Assert.Contains("Alice", captured);
        Assert.Contains("Bob", captured);
    }

    [Fact]
    public async Task WriteDataToBlobAsync_UploadedContentIncludesHeader()
    {
        const string blobPath = "output/orders.csv";
        var (container, blobClient) = SetupUploadMock(blobPath);

        string? captured = null;
        blobClient
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, bool, CancellationToken>((s, _, _) =>
                captured = new StreamReader(s).ReadToEnd())
            .ReturnsAsync(UploadResponse());

        await _sut.WriteDataToBlobAsync(container.Object, blobPath, SampleData);

        Assert.NotNull(captured);
        Assert.Contains("Id", captured);
        Assert.Contains("Name", captured);
        Assert.Contains("Amount", captured);
    }

    [Fact]
    public async Task WriteDataToBlobAsync_EmptyList_UploadsHeaderOnly()
    {
        const string blobPath = "output/orders.csv";
        var (container, blobClient) = SetupUploadMock(blobPath);

        string? captured = null;
        blobClient
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, bool, CancellationToken>((s, _, _) =>
                captured = new StreamReader(s).ReadToEnd())
            .ReturnsAsync(UploadResponse());

        await _sut.WriteDataToBlobAsync(container.Object, blobPath, new List<TestRecord>());

        Assert.NotNull(captured);
        Assert.Contains("Id", captured);
        Assert.DoesNotContain("Alice", captured);
    }

    [Fact]
    public async Task WriteDataToBlobAsync_WithPrintEncoding_FirstLineIsEncodingName()
    {
        const string blobPath = "output/orders.csv";
        var (container, blobClient) = SetupUploadMock(blobPath);

        string? captured = null;
        blobClient
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, bool, CancellationToken>((s, _, _) =>
                captured = new StreamReader(s).ReadToEnd())
            .ReturnsAsync(UploadResponse());

        await _sut.WriteDataToBlobAsync(container.Object, blobPath, SampleData, printEncoding: true);

        Assert.NotNull(captured);
        Assert.StartsWith("utf-8", captured, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WriteDataToBlobAsync_TabDelimiter_UsesTabSeparator()
    {
        const string blobPath = "output/orders.tsv";
        var (container, blobClient) = SetupUploadMock(blobPath);

        string? captured = null;
        blobClient
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, bool, CancellationToken>((s, _, _) =>
                captured = new StreamReader(s).ReadToEnd())
            .ReturnsAsync(UploadResponse());

        await _sut.WriteDataToBlobAsync(container.Object, blobPath, SampleData, delimiter: "\t");

        Assert.NotNull(captured);
        Assert.Contains("\t", captured);
        Assert.DoesNotContain(",", captured);
    }
}
