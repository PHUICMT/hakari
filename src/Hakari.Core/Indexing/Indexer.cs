using System.Diagnostics;
using Hakari.Core.Parsing;
using Hakari.Core.Sources;
using Hakari.Core.Watching;

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
    private readonly bool collectSessionTitles;
    private readonly bool collectThinkingMarks;
    private readonly long progressFromBytes;

    /// <param name="collectSessionTitles">
    /// Also keep each session's title. Off unless the user asked, since titles come from the
    /// conversation and nothing else Hakari reads does.
    /// </param>
    /// <param name="progressFromBytes">Scans of this many new bytes or more show progress.</param>
    /// <param name="collectThinkingMarks">
    /// Also note which responses thought first, by the kind of their parts alone. Off unless
    /// the user asked.
    /// </param>
    public Indexer(
        IndexStore store,
        bool collectSessionTitles = false,
        long progressFromBytes = DefaultProgressFromBytes,
        bool collectThinkingMarks = false)
    {
        this.store = store;
        this.collectSessionTitles = collectSessionTitles;
        this.collectThinkingMarks = collectThinkingMarks;
        this.progressFromBytes = progressFromBytes;
        trackedFiles = new TrackedFileRepository(store.Connection);
    }

    /// <summary>Scans every log of every source, reading only bytes appended since.</summary>
    public IndexStatistics Index(
        IEnumerable<UsageSource> sources,
        CancellationToken cancellationToken = default)
    {
        return Index(PendingWork.FullScansOf(sources), cancellationToken);
    }

    /// <summary>Indexes what a change tracker reported: whole sources or single files.</summary>
    public IndexStatistics Index(PendingWork work, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var totals = new RunningTotals();

        IndexPlanned(Plan(work.FullScans), totals, cancellationToken);

        foreach (var (source, files) in work.ChangedFiles)
        {
            foreach (var logFile in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                totals.FilesScanned++;
                IndexFileIfChanged(source.Id, logFile, trackedFiles.Find(logFile), totals);
            }
        }

        LogFormatWatch.Record(store, totals.Lines);
        return new IndexStatistics(
            totals.FilesScanned,
            totals.FilesChanged,
            totals.BytesRead,
            totals.RecordsChanged,
            stopwatch.Elapsed);
    }

    /// <summary>Raised while a large scan runs, so the widget can say how far it is.</summary>
    public event Action<IndexProgress>? Progressed;

    /// <summary>A scan this large shows progress; smaller ones are over at once.</summary>
    public const long DefaultProgressFromBytes = 64L * 1024 * 1024;

    /// <summary>One log file a scan has to read, and how many bytes of it are new.</summary>
    private sealed record PlannedFile(string Path, TrackedFile? Previous, long NewBytes);

    private List<(UsageSource Source, List<PlannedFile> Files)> Plan(
        IEnumerable<UsageSource> sources) =>
    [
        .. sources
            .Where(source => Directory.Exists(source.ProjectsDirectory))
            .Select(source => (source, PlanSource(source))),
    ];

    private List<PlannedFile> PlanSource(UsageSource source)
    {
        var knownFiles = trackedFiles.LoadForSource(source.Id);
        var planned = new List<PlannedFile>();
        var logFiles = Directory.EnumerateFiles(
            source.ProjectsDirectory,
            ClaudeConfigNames.LogFileSearchPattern,
            LogEnumerationOptions);
        foreach (var logFile in logFiles)
        {
            var info = new FileInfo(logFile);
            var previous = knownFiles.GetValueOrDefault(logFile);
            if (!info.Exists
                || previous is not null
                    && previous.IsUnchanged(info.Length, info.LastWriteTimeUtc.Ticks))
            {
                continue;
            }

            var from = previous?.ResumeOffset(info.Length) ?? 0;
            planned.Add(new PlannedFile(logFile, previous, info.Length - from));
        }

        return planned;
    }

    private void IndexPlanned(
        List<(UsageSource Source, List<PlannedFile> Files)> plan,
        RunningTotals totals,
        CancellationToken cancellationToken)
    {
        var total = plan.Sum(entry => entry.Files.Sum(file => file.NewBytes));
        var report = total >= progressFromBytes;
        var done = 0L;
        foreach (var (source, files) in plan)
        {
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                totals.FilesScanned++;
                IndexFileIfChanged(source.Id, file.Path, file.Previous, totals);
                done += file.NewBytes;
                if (report)
                {
                    Progressed?.Invoke(new IndexProgress(done, total));
                }
            }
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
        using var titleWriter = new SessionTitleWriter(store.Connection, transaction);
        using var thinkingWriter = new ThinkingMarkWriter(store.Connection, transaction);
        var recordsChanged = 0;
        var lines = LineKindCounts.None;
        var endOffset = file.IndexedOffset;

        try
        {
            endOffset = CompleteLineReader.ReadFrom(file.Path, file.IndexedOffset, line =>
            {
                var record = UsageLineParser.TryParse(
                    line, collectThinkingMarks, out var hasThinking, out var kind);
                lines = lines.Add(kind);
                if (record is not null && hasThinking)
                {
                    thinkingWriter.Mark(record.DeduplicationKey);
                }

                if (record is not null && writer.TryUpsert(file.SourceId, record))
                {
                    recordsChanged++;
                }
                else if (collectSessionTitles && SessionTitleParser.TryParse(line) is { } title)
                {
                    titleWriter.Upsert(title);
                }
            });
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
            // A log that cannot be read now is tried again on the next pass.
            return;
        }

        trackedFiles.Save(file with { IndexedOffset = endOffset }, transaction);
        transaction.Commit();

        totals.BytesRead += endOffset - file.IndexedOffset;
        totals.RecordsChanged += recordsChanged;
        totals.Lines += lines;
    }

    private sealed class RunningTotals
    {
        public int FilesScanned { get; set; }

        public int FilesChanged { get; set; }

        public long BytesRead { get; set; }

        public int RecordsChanged { get; set; }

        public LineKindCounts Lines { get; set; } = LineKindCounts.None;
    }
}
