using System.Diagnostics;
using Hakari.Taskbar;
using Hakari.Taskbar.Motion;
using Hakari.Taskbar.Rendering;
using Hakari.TaskbarSpike;

const string OverlayFlag = "--overlay";
const long BytesPerMegabyte = 1024 * 1024;
const string ReportSecondsFlag = "--report-seconds";
const string MotionDemoFlag = "--demo-motion";
const string MotionFlag = "--motion";
const int DefaultReportSeconds = 30;
var reportInterval = TimeSpan.FromSeconds(ReadReportSeconds(args));

MessageLoop.EnablePerMonitorDpiAwareness();
var mode = args.Contains(OverlayFlag) ? AttachMode.TopMostOverlay : AttachMode.ChildOfTaskbar;
Console.WriteLine($"Hakari taskbar spike · mode {mode} · right-click the widget to quit");

var loading = new WidgetContent("Hakari", "Reading logs…", WidgetTone.Muted);
var hostOptions = TaskbarWidgetHostOptions.Default with
{
    Mode = mode,
    Motion = ReadMotion(args),
};
using var widget = new TaskbarWidgetHost(hostOptions, loading);
using var feed = new UsageFeed();
widget.Clicked += (_, _) => Console.WriteLine("Clicked: the flyout would open here.");
widget.RightClicked += (_, _) => MessageLoop.Quit();

using var demo = args.Contains(MotionDemoFlag)
    ? MotionDemo.Start(content => widget.PostContent(content))
    : null;
if (demo is null)
{
    feed.Updated += content => widget.PostContent(content);
    feed.Start();
}

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
        $"widgets {widget.WidgetCount}",
        $"recreated {diagnostics.WidgetsRecreated}",
        $"layout reads {widget.LayoutRefreshes}",
        $"index passes {feed.IndexPasses}",
        $"last index {feed.LastIndexDuration.TotalMilliseconds:N0} ms",
    ];
    Console.WriteLine(string.Join(" | ", parts));

    // Reads window state from the timer thread; good enough for a spike's diagnostics.
    try
    {
        foreach (var line in widget.DescribeWidgets())
        {
            Console.WriteLine($"    {line}");
        }
    }
    catch (InvalidOperationException)
    {
    }
}

static MotionPreference? ReadMotion(string[] arguments)
{
    var position = Array.IndexOf(arguments, MotionFlag);
    var hasValue = position >= 0 && position + 1 < arguments.Length;
    return hasValue && Enum.TryParse<MotionPreference>(arguments[position + 1], true, out var value)
        ? value
        : null;
}

static int ReadReportSeconds(string[] arguments)
{
    var position = Array.IndexOf(arguments, ReportSecondsFlag);
    var hasValue = position >= 0 && position + 1 < arguments.Length;
    return hasValue && int.TryParse(arguments[position + 1], out var seconds) && seconds > 0
        ? seconds
        : DefaultReportSeconds;
}
