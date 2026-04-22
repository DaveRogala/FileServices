# MagellanFileServices

A .NET 8 class library providing common ETL file-import utilities: reading CSV files into typed record lists, writing CSV output, and moving processed or failed files to timestamped archive folders — for both local file systems and Azure Blob Storage.

## Requirements

- .NET 8.0
- `Microsoft.Extensions.Logging` (provided by the host application's DI container)
- `Azure.Storage.Blobs` — only required if using the Azure Blob methods

## Installation

```
dotnet add package MagellanFileServices
```

## Registration

Register `FileServices` with the standard `ILogger<T>` dependency:

```csharp
builder.Services.AddLogging();
builder.Services.AddScoped<IFileServices, FileServices>();
```

---

## Reading CSV files

All overloads return `ObjectResult<T>`. Row-level parse errors are collected in `Errors` rather than throwing; `CriticalError` is set to `true` only when the file cannot be read at all.

```csharp
public class OrderRecord
{
    public int Id { get; set; }
    public string? CustomerName { get; set; }
    public decimal Amount { get; set; }
}
```

**From a file path (simple)**
```csharp
ObjectResult<OrderRecord> result = fileServices.GetDataFromFile<OrderRecord>(@"C:\imports\orders.csv");
```

**From a file path with encoding and tab delimiter**
```csharp
ObjectResult<OrderRecord> result = fileServices.GetDataFromFile<OrderRecord>(
    @"C:\imports\orders.tsv",
    encoding: Encoding.UTF8,
    skipEncodingHeader: false,
    delimiter: "\t");
```

**`skipEncodingHeader`** — pass `true` when the first line of the file contains an encoding declaration rather than data (the line is discarded before CSV parsing begins).

**From a stream**
```csharp
using Stream stream = File.OpenRead(filePath);
ObjectResult<OrderRecord> result = fileServices.GetDataFromFile<OrderRecord>(stream);
```

**Checking the result**
```csharp
if (result.CriticalError || result.Errors.Count > 0)
{
    // result.Errors contains per-row messages
}

List<OrderRecord> records = result.ObjectResults ?? [];
```

---

## Writing CSV files

All overloads throw on failure; no return value.

**Simple write**
```csharp
fileServices.WriteDataToFile(outputPath, records);
```

**With options**
```csharp
fileServices.WriteDataToFile(
    outputPath,
    records,
    encoding: Encoding.UTF8,
    delimiter: "\t",
    useHeaders: true,
    printEncoding: false);   // true writes encoding.HeaderName as the first line
```

---

## Local file archiving

Both methods create the destination subfolder if it does not exist and rename the file with the supplied timestamp.

Archived filename format: `{name}_{timeStamp}{ext}` — e.g. `orders_20240115120000.csv`

**On success** — moves the file to `{basePath}/processed/`
```csharp
string ts = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
fileServices.HandleFileSuccess(@"C:\imports", "orders.csv", ts);
```

**On error** — moves the file to `{basePath}/errors/` and writes an `Errors_{fileName}_{timeStamp}.txt` log file
```csharp
fileServices.HandleFileError(
    @"C:\imports",
    "orders.csv",
    ex.Message,
    ts,
    errors: result.Errors);  // optional list of per-row error strings
```

---

## Azure Blob Storage archiving

Create a `BlobContainerClient` and pass it in directly. The library does not hold a connection internally.

```csharp
BlobContainerClient container = new BlobContainerClient(connectionString, containerName);
string ts = DateTime.UtcNow.ToString("yyyyMMddHHmmssffff");
string blobPath = "incoming/orders.csv";
```

**On success** — copies the blob to `{directory}/processed/{name}_{ts}{ext}` then deletes the source
```csharp
await fileServices.HandleFileSuccessAsync(container, blobPath, ts);
```

**On error** — archives the blob to `{directory}/errors/` and uploads an error log blob
```csharp
await fileServices.HandleFileErrorAsync(
    container,
    blobPath,
    ex.Message,
    ts,
    errors: result.Errors);
```

---

## Extending

All `Handle*` and the primary `GetDataFromFile<T>` overload are `virtual`, allowing behaviour to be customised by subclassing `FileServices`.

---

## ObjectResult&lt;T&gt; reference

| Property | Type | Description |
|---|---|---|
| `ObjectResults` | `List<T>?` | Parsed records; `null` only on a critical failure |
| `Errors` | `List<string>` | Row-level error messages collected during parsing |
| `CriticalError` | `bool` | `true` when the file could not be read at all |
