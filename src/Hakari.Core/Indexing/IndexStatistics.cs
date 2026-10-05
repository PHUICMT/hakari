namespace Hakari.Core.Indexing;

public sealed record IndexStatistics(
    int FilesScanned,
    int FilesChanged,
    long BytesRead,
    int RecordsChanged,
    TimeSpan Elapsed);
