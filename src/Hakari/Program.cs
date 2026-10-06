using Hakari;
using Hakari.Core.Settings;
using Hakari.Core.Interprocess;
using Hakari.Diagnostics;
using Hakari.Notifications;
using Hakari.Startup;
using Hakari.Taskbar;

const string DiagnosticsFlag = "--diagnostics";
const string MotionDemoFlag = "--demo-motion";
const string ReportSecondsFlag = "--report-seconds";
const int DefaultReportSeconds = 30;
const string ToastDemoFlag = "--demo-toast";

// Shows one sample limit notification and says whether Windows took it, for checking setup.
if (args.Contains(ToastDemoFlag))
{
    Hakari.Core.Localization.Texts.Use(SettingsStore.Default.Load().Language);
    ToastNotifier.Register();
    var sample = new Hakari.Core.Presentation.LimitAlertMessage(
        "Weekly limit is nearly full (96%)",
        "Resets Tue 22:00",
        "At this pace it fills around 18:40",
        0.96,
        "Weekly limit",
        "96%",
        IsWarning: true);
    return ToastNotifier.Show(sample) ? 0 : 1;
}

// A notification's button starts Hakari.exe with its link; the running copy does the rest.
if (args.FirstOrDefault(argument => argument.StartsWith(
    ToastNotifier.Scheme + ":", StringComparison.OrdinalIgnoreCase)) is { } link)
{
    var mutes = link.StartsWith(ToastNotifier.MuteTodayAction, StringComparison.OrdinalIgnoreCase);
    var command = mutes
        ? ResidentCommand.MuteAlertsToday
        : ResidentCommand.OpenFlyout;
    if (ResidentChannel.TrySend(command) || command == ResidentCommand.MuteAlertsToday)
    {
        return 0;
    }
}

MessageLoop.EnablePerMonitorDpiAwareness();
using var instance = SingleInstance.TryAcquire();
if (instance is null)
{
    return 0;
}

using var app = new ResidentApp(SettingsStore.Default);
using var diagnostics = args.Contains(DiagnosticsFlag)
    ? new DiagnosticsReporter(app.Widgets, app.FeedDiagnostics, ReadReportInterval(args))
    : null;
using var demo = args.Contains(MotionDemoFlag)
    ? MotionDemo.Start(content => app.Widgets.PostContent(content))
    : null;

app.Run(startFeed: demo is null);
return 0;

static TimeSpan ReadReportInterval(string[] arguments)
{
    var position = Array.IndexOf(arguments, ReportSecondsFlag);
    var hasValue = position >= 0 && position + 1 < arguments.Length;
    var seconds = hasValue && int.TryParse(arguments[position + 1], out var value) && value > 0
        ? value
        : DefaultReportSeconds;
    return TimeSpan.FromSeconds(seconds);
}
