# Coding Style

## Readability

- Full, descriptive names everywhere: `configDirectory`, not `dir`/`cfg`/`d`. No single-letter names, including lambda parameters (`source => ...`, not `s => ...`).
- No magic values: strings, numbers, paths, and SQL live in named constants or dedicated name classes (`LogFieldNames`, `ClaudeConfigNames`, `IndexSchema`, ...).
- Lines are at most **100 characters**. `Style/SourceStyleTests` enforces this, so `dotnet test` fails on longer lines.
- When a method or expression gets long, split it into small, well-named private methods.
- One type per file. File name matches the type name.
- Braces on every `if`/`for`/`while`. File-scoped namespaces.

## Structure

```
src/Hakari.Core/
  Usage/          token counts, usage records
  Parsing/        log line parsing
  Pricing/        price table and cost calculation
  Sources/        Windows / WSL / config directory discovery
  Accounts/       account file reading, plan detection
  Indexing/       SQLite index, incremental reader, indexer
  Querying/       aggregation, filters, time periods
  Performance/    background priority helpers
  Configuration/  application paths
src/Hakari.Cli/
  CommandLine/    argument parsing
  Commands/       one class per command
  Output/         table and number formatting
tests/Hakari.Core.Tests/   mirrors the Core folders, plus Support/ and Style/
```

## Performance (never slow the user down)

- Indexing runs inside `BackgroundThreadMode`, which lowers CPU, I/O, and memory priority.
- Read only appended bytes. Never re-read a whole log once it has been indexed.
- Open logs with `FileShare.ReadWrite | FileShare.Delete`. Never block the writer.
- Never start a stopped WSL distribution (`WslScanMode.RunningOnly` is the default).
- UI updates are debounced. Don't poll in tight loops.

## Build

- `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` are on (`Directory.Build.props`).
- Files are UTF-8 without BOM and use LF line endings (`.editorconfig`).
