using Hakari.Core.Settings;
using Hakari.Feed;
using Hakari.Taskbar;
using Hakari.Taskbar.Motion;
using Hakari.Taskbar.Tray;
using Hakari.Tray;

namespace Hakari;

/// <summary>
/// The always-running part of Hakari: the taskbar widgets, the tray icon, and the background
/// feed. Windows with real UI (flyout, dashboard, settings) live in a separate process.
/// </summary>
internal sealed class ResidentApp : IDisposable
{
    private const string TrayTooltip = "Hakari";

    private readonly SettingsStore settingsStore;
    private readonly TaskbarWidgetHost widgets;
    private readonly UsageFeed feed;
    private readonly TrayIcon trayIcon;
    private readonly ResidentMenu menu;

    public ResidentApp(SettingsStore settingsStore)
    {
        this.settingsStore = settingsStore;
        var settings = settingsStore.Load();

        var hostOptions = TaskbarWidgetHostOptions.Default with
        {
            Motion = ToMotion(settings.Animation),
            ShowOnSecondaryTaskbars = settings.ShowOnSecondaryTaskbars,
        };
        widgets = new TaskbarWidgetHost(hostOptions, WidgetText.Loading);
        feed = new UsageFeed(settingsStore);
        menu = new ResidentMenu(settingsStore, Apply);
        trayIcon = new TrayIcon(TrayTooltip, menu.Build);

        menu.QuitRequested += (_, _) => MessageLoop.Quit();
        widgets.RightClicked += (_, _) => trayIcon.ShowMenu();
    }

    public TaskbarWidgetHost Widgets => widgets;

    public FeedDiagnostics FeedDiagnostics => feed.Diagnostics;

    /// <param name="startFeed">False for the motion demo, which posts its own content.</param>
    public void Run(bool startFeed = true)
    {
        if (startFeed)
        {
            feed.Updated += content => widgets.PostContent(content);
            feed.Start();
        }

        MessageLoop.Run();
    }

    public void Dispose()
    {
        feed.Dispose();
        trayIcon.Dispose();
        widgets.Dispose();
    }

    private static MotionPreference? ToMotion(AnimationSetting setting) => setting switch
    {
        AnimationSetting.Full => MotionPreference.Full,
        AnimationSetting.Reduced => MotionPreference.Reduced,
        AnimationSetting.Off => MotionPreference.Off,
        _ => null,
    };

    /// <summary>Runs on the UI thread after a menu change.</summary>
    private void Apply(HakariSettings settings)
    {
        widgets.SetMotion(ToMotion(settings.Animation));
        widgets.SetShowOnSecondaryTaskbars(settings.ShowOnSecondaryTaskbars);
        feed.ReloadSettings();
    }
}
