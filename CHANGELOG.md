# Changelog

All notable changes to MagellanFileServices are documented here.
This project follows [Semantic Versioning](https://semver.org/).

---

## [3.1.0] - 2026-04-22

### Added

- **`WriteDataToBlobAsync`** — four overloads mirroring `WriteDataToFile` but writing directly to Azure Blob Storage. Data is serialised to CSV in a `MemoryStream` (no temp file) and uploaded with `overwrite: true`. The full overload accepts `BlobContainerClient`, `blobPath`, `List<T>`, `Encoding`, `delimiter`, `useHeaders`, and `printEncoding`; convenience overloads default to UTF-8 / comma / headers.

---

## [3.0.0] - 2026-04-22

### Breaking changes

- **Target framework upgraded from `net8.0` to `net10.0`** — the library now requires .NET 10. Consumers still on .NET 8 or 9 should remain on 2.x.

### Dependencies

- `Microsoft.Extensions.DependencyInjection.Abstractions` updated from 9.0.7 → 10.0.0
- `Microsoft.Extensions.Logging` updated from 9.0.7 → 10.0.0

---

## [2.3.0] - 2026-04-22

### Added

- **`fixUnescapedQuotes` parameter on `GetDataFromFile`** — opt-in pre-processing pass that corrects bare double-quote characters inside quoted CSV fields before handing content to CsvHelper. When enabled, the raw content is read into memory, unescaped `"` characters are doubled to `""`, and parsing proceeds normally. Only enable this for files known to have the problem; the flag defaults to `false` so existing call sites are unaffected. Available on the `string filePath` and `Stream` `rowsToSkip` overloads.

---

## [2.2.0] - 2026-04-22

### Added

- **`GetDataFromFile` `rowsToSkip` overloads** — new overloads on both the `string filePath` and `Stream` variants accept an `int rowsToSkip` parameter that discards a fixed number of leading rows before CSV parsing begins. Designed for files that carry metadata rows (report headers, source-system identifiers, etc.) before the column header row. The existing `skipEncodingHeader` overloads remain and are implemented as `rowsToSkip: skipEncodingHeader ? 1 : 0` delegates, so all existing call sites continue to compile and behave identically.

---

## [2.1.0] - 2026-04-22

### Added

- **`ServiceCollectionExtensions.AddMagellanFileServices()`** — extension method on `IServiceCollection` that registers `FileServices` as `IFileServices` with a scoped lifetime. Callers no longer need to wire up the registration manually. The method returns `IServiceCollection` for fluent chaining.
- **`MagellanFileServices.sln`** — solution file wrapping both the library and test projects so the whole repository builds and tests from a single entry point.
- **`MagellanFileServices.Tests`** — xUnit 2.9 / Moq 4.20 test project covering CSV reading, CSV writing, local file archiving, Azure Blob archiving, and DI registration (47 tests).

### Dependencies

- Added `Microsoft.Extensions.DependencyInjection.Abstractions 9.0.7` to the library (provides `IServiceCollection` and `ServiceDescriptor` for the registration extension without pulling in the full DI container).

---

## [2.0.0] - 2026-04-22

### Breaking changes

- **`HandlFileErrorAsync` renamed to `HandleFileErrorAsync`** — corrects a long-standing typo in the public interface.
- **Async Handle methods now accept `BlobContainerClient`** instead of raw `blobConnectionString` + `containerName` parameters. Callers are responsible for constructing and managing the client, which enables DI, connection reuse, and unit testing without live Azure credentials.
- **Removed unused `Stream stream` parameter** from `HandleFileErrorAsync` and `HandleFileSuccessAsync` — the parameter was never read by the implementation.
- **`WriteDataToFile` overloads changed from `bool` to `void`** — the previous return type always returned `true` or threw; callers should use try/catch to detect failure.
- **`GetDataFromFile` interface parameter renamed** from `connectionString` to `filePath` to match the implementation and prevent misuse.
- **`firstLineContainsEncoding` renamed to `skipEncodingHeader`** on both `GetDataFromFile` overloads — the new name more clearly describes the effect.
- **`FileWriteResult` class removed** — it was defined but never used.

### Bug fixes

- Azure blob archive paths were constructed with `Path.Combine`, which uses `\` on Windows. Paths are now built with `/` to match Azure Blob Storage requirements.
- `HandleFileError` and `HandleFileSuccess` previously created the destination directory before validating `basePath` and `fileName`, allowing partially formed paths to be created. Argument guards now run first.
- `HandleFileErrorAsync` and `HandleFileSuccessAsync` validated `containerName` twice and never validated `blobConnectionString`. The async Handle methods now validate the `BlobContainerClient` argument instead.
- `TargetFileName` used `string.Replace` to insert the timestamp, which produced incorrect results for filenames containing multiple dots (e.g. `report.2024.csv` → `report_ts.2024.csv` instead of `report.2024_ts.csv`). Rewritten using `Path.GetFileNameWithoutExtension` and `Path.GetExtension`.
- The file-path overload of `GetDataFromFile` opened a `StreamReader` and then passed its `BaseStream` to the stream overload, which wrapped it in a second `StreamReader`. The inner dispose closed the stream unexpectedly. The overload now opens the file with `File.OpenRead` and passes the stream directly.
- Error log strings in `HandleFileError` and `HandleFileErrorAsync` used `\n\r` (non-standard) instead of `Environment.NewLine`.

### Improvements

- `Encoding.Default` (OS-dependent) replaced with `Encoding.UTF8` as the default for all read and write operations.
- Per-row errors in `HandleFileError` and `HandleFileErrorAsync` now appended with `string.Join` instead of a loop with string concatenation.
- `"errors"` and `"processed"` folder name literals extracted to `private const` fields.
- `GetDataFromFile` stream overload now passes the `Encoding` parameter to the `StreamReader` constructor and enables BOM detection.
- Removed unused `Microsoft.Extensions.Configuration` NuGet package reference.
- `WriteDataToFile` overloads now chain through a single implementation instead of duplicating the `Encoding.Default` default.
- `MoveProcessedFileAsync` reduced from three `BlobClient` instances to two by reusing the read client for the delete call.

---

## [1.7.3] - prior release

Initial packaged release. Included CSV read/write via CsvHelper, local file archiving, and Azure Blob Storage archiving.
