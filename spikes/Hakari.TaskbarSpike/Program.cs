using System.Diagnostics;
using Hakari.Taskbar;
using Hakari.Taskbar.Rendering;
using Hakari.TaskbarSpike;

const string OverlayFlag = "--overlay";
const long BytesPerMegabyte = 1024 * 1024;
var reportInterval = TimeSpan.FromSeconds(30);

MessageLoop.EnablePerMonitorDpiAwareness();
var mode = args.Contains(OverlayFlag) ? AttachMode.TopMostOverlay : AttachMode.ChildOfTaskbar;
Console.WriteLine($"Hakari taskbar spike · mode {mode} · right-click the widget to quit");

var loading = new WidgetContent("Hakari", "Reading logs…", WidgetTone.Muted);
using var widget = new TaskbarWidgetWindow(mode, loading);
using var feed = new UsageFeed();
feed.Updated += content => widget.PostContent(content);
widget.Clicked += (_, _) => Console.WriteLine("Clicked: the flyout would open here.");
widget.RightClicked += (_, _) => MessageLoop.Quit();
feed.Start();

var process = Process.GetCurrentProcess();
var startedAt = DateTime.UtcNow;
var previousReportAt = startedAt;
var previousProcessorTime = TimeSpan.Zero;
using var reporter = new Timer(_ => Report(), null, reportInterval, reportInterval);

MessageLoop.Run();
Report();

void Report()
{
    process.Refresh();
    var now = DateTime.UtcNow;
    var elapsed = now - startedAt;
    var processorTime = process.TotalProcessorTime;
    var intervalProcessorTime = processorTime - previousProcessorTime;
    var interval = now - previousReportAt;
    var cpuPercent = intervalProcessorTime / interval / Environment.ProcessorCount * 100;
    previousProcessorTime = processorTime;
    previousReportAt = now;
    var diagnostics = widget.Diagnostics;
    string[] parts =
    [
        $"{elapsed:mm\\:ss}",
        $"cpu {cpuPercent:N3}%",
        $"ram {process.WorkingSet64 / BytesPerMegabyte} MB",
        $"renders {diagnostics.Renders}",
        $"moves {diagnostics.Moves}",
        $"collisions {diagnostics.Collisions}",
        $"explorer restarts {diagnostics.ExplorerRestarts}",
        $"index passes {feed.IndexPasses}",
        $"last index {feed.LastIndexDuration.TotalMilliseconds:N0} ms",
    ];
    Console.WriteLine(string.Join(" | ", parts));
}
