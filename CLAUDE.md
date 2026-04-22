# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build MagellanFileServices.csproj

# Run all tests
dotnet test MagellanFileServices.Tests/MagellanFileServices.Tests.csproj

# Run a single test
dotnet test MagellanFileServices.Tests/MagellanFileServices.Tests.csproj --filter "FullyQualifiedName~TestMethodName"
```

Note: `FileServices.sln` intentionally excludes the test project — always target the test `.csproj` directly when running tests.

## Architecture

This is a single-file-class library (`Services/FileServices.cs`) published as the NuGet package `MagellanFileServices`. It provides CSV read/write and post-processing file archival for ETL pipelines.

**Public surface:**
- `IFileServices` (`Contracts/IFileServices.cs`) — the interface consumers depend on
- `ObjectResult<T>` (`Models/ObjectResult.cs`) — return type for all read operations; collects row-level errors in `Errors` without throwing; sets `CriticalError = true` only on unrecoverable failures
- `ServiceCollectionExtensions.AddMagellanFileServices()` — registers `FileServices` as `IFileServices` with scoped lifetime

**`FileServices` implementation** handles two distinct concerns:

1. **CSV I/O** — wraps CsvHelper. `GetDataFromFile<T>` has overloads for file path or stream, with options for encoding, row-skipping, delimiter, and the `fixUnescapedQuotes` pre-processor (reads whole file into memory; off by default). All thin overloads delegate to the two `virtual` core overloads (`filePath`+`int rowsToSkip` and `stream`+`int rowsToSkip`). `WriteDataToFile<T>` thin overloads delegate to the full-parameter version.

2. **File archival** — moves source files to `processed/` or `errors/` subfolders with a `{name}_{timestamp}{ext}` rename. Two flavours:
   - Local filesystem: synchronous `HandleFileSuccess` / `HandleFileError`
   - Azure Blob Storage: async `HandleFileSuccessAsync` / `HandleFileErrorAsync` — downloads the blob, re-uploads to the archive path, then deletes the source via `MoveProcessedFileAsync` (private). The three Azure SDK calls must use explicit overloads (`DownloadAsync(CancellationToken)`, `UploadAsync(Stream, bool, CancellationToken)`, `DeleteIfExistsAsync(DeleteSnapshotsOption, BlobRequestConditions, CancellationToken)`) so Moq mocks resolve correctly.

All `Handle*` methods and the primary `GetDataFromFile` overloads are `virtual` to allow subclass customisation.

**Test project** (`MagellanFileServices.Tests/`) uses xunit + Moq. Blob tests mock `BlobContainerClient` and `BlobClient` directly — Azure SDK blob move is download → upload → delete, so all three calls must be mocked. The main `.csproj` excludes the test folder via `<Compile Remove="MagellanFileServices.Tests/**" />` to prevent the default glob from pulling test files into the library build.
