using System.Diagnostics;
using Hakari.Core.Parsing;
using Hakari.Core.Sources;

namespace Hakari.Core.Indexing;

public sealed class Indexer
{
    private static readonly EnumerationOptions LogEnumerationOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    private readonly IndexStore store;
    private readonly TrackedFileRepository trackedFiles;

    public Indexer(IndexStore store)
    {
        this.store = store;
        trackedFiles = new TrackedFileRepository(store.Connection);
    }

    public IndexStatistics Index(
        IEnumerable<UsageSource> sources,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var totals = new RunningTotals();

        foreach (var source in sources)
        {
            IndexSource(source, totals, cancellationToken);
        }

        return new IndexStatistics(
            totals.FilesScanned,
            totals.FilesChanged,
            totals.BytesRead,
            totals.RecordsChanged,
            stopwatch.Elapsed);
    }

    private void IndexSource(
        UsageSource source,
        RunningTotals totals,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(source.ProjectsDirectory))
        {
            return;
        }

        var knownFiles = trackedFiles.LoadForSource(source.Id);
        var logFiles = Directory.EnumerateFiles(
            source.ProjectsDirectory,
            ClaudeConfigNames.LogFileSearchPattern,
            LogEnumerationOptions);

        foreach (var logFile in logFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            totals.FilesScanned++;
            IndexFileIfChanged(source.Id, logFile, knownFiles.GetValueOrDefault(logFile), totals);
        }
    }

    private void IndexFileIfChanged(
        string sourceId,
        string logFile,
        TrackedFile? previous,
        RunningTotals totals)
    {
        var fileInfo = new FileInfo(logFile);
        if (!fileInfo.Exists)
        {
            return;
        }

        var size = fileInfo.Length;
        var modifiedTicks = fileInfo.LastWriteTimeUtc.Ticks;
        if (previous is not null && previous.IsUnchanged(size, modifiedTicks))
        {
            return;
        }

        var startOffset = previous?.ResumeOffset(size) ?? 0;
        totals.FilesChanged++;
        IndexFile(new TrackedFile(logFile, sourceId, size, modifiedTicks, startOffset), totals);
    }

    private void IndexFile(TrackedFile file, RunningTotals totals)
    {
        using var transaction = store.Connection.BeginTransaction();
        using var writer = new UsageRecordWriter(store.Connection, transaction);
        var recordsChanged = 0;
        var endOffset = file.IndexedOffset;

        try
        {
            endOffset = CompleteLineReader.ReadFrom(file.Path, file.IndexedOffset, line =>
            {
                var record = UsageLineParser.TryParse(line);
                if (record is not null && writer.TryUpsert(file.SourceId, record))
                {
                    recordsChanged++;
                }
            });
        }
        catch (IOException)
        {
            return;
        }

        trackedFiles.Save(file with { IndexedOffset = endOffset }, transaction);
        transaction.Commit();

        totals.BytesRead += endOffset - file.IndexedOffset;
        totals.RecordsChanged += recordsChanged;
    }

    private sealed class RunningTotals
    {
        public int FilesScanned { get; set; }

        public int FilesChanged { get; set; }

        public long BytesRead { get; set; }

        public int RecordsChanged { get; set; }
    }
}
