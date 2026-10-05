using Hakari;
using Hakari.Core.Settings;
using Hakari.Diagnostics;
using Hakari.Startup;
using Hakari.Taskbar;

const string DiagnosticsFlag = "--diagnostics";
const string MotionDemoFlag = "--demo-motion";
const string ReportSecondsFlag = "--report-seconds";
const int DefaultReportSeconds = 30;

MessageLoop.EnablePerMonitorDpiAwareness();
using var instance = SingleInstance.TryAcquire();
if (instance is null)
{
    return;
}

using var app = new ResidentApp(SettingsStore.Default);
using var diagnostics = args.Contains(DiagnosticsFlag)
    ? new DiagnosticsReporter(app.Widgets, app.FeedDiagnostics, ReadReportInterval(args))
    : null;
using var demo = args.Contains(MotionDemoFlag)
    ? MotionDemo.Start(content => app.Widgets.PostContent(content))
    : null;

app.Run(startFeed: demo is null);

static TimeSpan ReadReportInterval(string[] arguments)
{
    var position = Array.IndexOf(arguments, ReportSecondsFlag);
    var hasValue = position >= 0 && position + 1 < arguments.Length;
    var seconds = hasValue && int.TryParse(arguments[position + 1], out var value) && value > 0
        ? value
        : DefaultReportSeconds;
    return TimeSpan.FromSeconds(seconds);
}
