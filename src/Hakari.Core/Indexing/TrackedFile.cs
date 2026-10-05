namespace Hakari.Core.Indexing;

public sealed record TrackedFile(
    string Path,
    string SourceId,
    long Size,
    long ModifiedTicks,
    long IndexedOffset)
{
    public bool IsUnchanged(long size, long modifiedTicks) =>
        Size == size && ModifiedTicks == modifiedTicks;

    public long ResumeOffset(long currentSize) => currentSize < IndexedOffset ? 0 : IndexedOffset;
}
