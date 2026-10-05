using System.Diagnostics;
using Hakari.Core.Configuration;
using Hakari.Feed;
using Hakari.Taskbar;

namespace Hakari.Diagnostics;

/// <summary>
/// With --diagnostics, appends CPU, memory and widget counters to a log every few seconds.
/// Used by tools/Run-TaskbarScenarios.ps1; off in normal use.
/// </summary>
internal sealed class DiagnosticsReporter : IDisposable
{
    public const string LogFileName = "diagnostics.log";
    private const long BytesPerMegabyte = 1024 * 1024;
    private const string Separator = " | ";

    private readonly TaskbarWidgetHost widgets;
    private readonly FeedDiagnostics feed;
    private readonly StreamWriter log;
    private readonly Timer timer;
    private readonly Process process = Process.GetCurrentProcess();
    private readonly DateTime startedAt = DateTime.UtcNow;
    private DateTime previousReportAt;
    private TimeSpan previousProcessorTime;

    public DiagnosticsReporter(TaskbarWidgetHost widgets, FeedDiagnostics feed, TimeSpan interval)
    {
        this.widgets = widgets;
        this.feed = feed;
        previousReportAt = startedAt;
        Directory.CreateDirectory(HakariPaths.DataDirectory);
        var logPath = Path.Combine(HakariPaths.DataDirectory, LogFileName);
        log = new StreamWriter(logPath, append: false) { AutoFlush = true };
        timer = new Timer(_ => Report(), null, interval, interval);
    }

    public void Dispose()
    {
        timer.Dispose();
        Report();
        log.Dispose();
        process.Dispose();
    }

    private void Report()
    {
        process.Refresh();
        var now = DateTime.UtcNow;
        var processorTime = process.TotalProcessorTime;
        var interval = now - previousReportAt;
        var cpuPercent = (processorTime - previousProcessorTime) / interval
            / Environment.ProcessorCount * 100;
        previousProcessorTime = processorTime;
        previousReportAt = now;

        var counters = widgets.Diagnostics;
        string[] parts =
        [
            $"{now - startedAt:mm\\:ss}",
            $"cpu {cpuPercent:N3}%",
            $"ram {process.WorkingSet64 / BytesPerMegabyte} MB",
            $"renders {counters.Renders}",
            $"collisions {counters.Collisions}",
            $"explorer restarts {counters.ExplorerRestarts}",
            $"widgets {widgets.WidgetCount}",
            $"recreated {counters.WidgetsRecreated}",
            $"layout reads {widgets.LayoutRefreshes}",
            $"index passes {feed.IndexPasses}",
            $"limit polls {feed.LimitPolls}",
        ];

        lock (log)
        {
            log.WriteLine(string.Join(Separator, parts));
            WriteWidgetLines();
        }
    }

    /// <summary>Reads window state from the timer thread; fine for diagnostics only.</summary>
    private void WriteWidgetLines()
    {
        try
        {
            foreach (var line in widgets.DescribeWidgets())
            {
                log.WriteLine($"    {line}");
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
