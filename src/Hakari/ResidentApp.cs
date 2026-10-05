using Hakari.Core.Interprocess;
using Hakari.Core.Localization;
using Hakari.Core.Settings;
using Hakari.Core.Startup;
using Hakari.Feed;
using Hakari.Settings;
using Hakari.Surfaces;
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
    private readonly SettingsWatcher settingsWatcher;
    private HakariSettings appliedSettings;

    public ResidentApp(SettingsStore settingsStore)
    {
        this.settingsStore = settingsStore;
        var settings = settingsStore.Load();
        appliedSettings = settings;
        Texts.Use(settings.Language);

        var hostOptions = TaskbarWidgetHostOptions.Default with
        {
            Motion = ToMotion(settings.Animation),
            ShowOnDisplay = DisplayFilter.From(settings),
        };
        widgets = new TaskbarWidgetHost(hostOptions, WidgetText.Loading);
        feed = new UsageFeed(settingsStore);
        menu = new ResidentMenu(settingsStore, Apply, OpenSettings);
        trayIcon = new TrayIcon(TrayTooltip, menu.Build);
        settingsWatcher = new SettingsWatcher(settingsStore);
        settingsWatcher.Changed += (_, _) =>
            widgets.PostAction(() => Apply(settingsStore.Load()));
        RecordLocation();

        menu.QuitRequested += (_, _) => MessageLoop.Quit();
        widgets.RightClicked += (_, _) => trayIcon.ShowMenu();
        widgets.Clicked += (_, click) =>
            OpenFlyout(click.WidgetBounds.Right, click.TaskbarBounds.Top);
        trayIcon.Selected += (_, _) => OpenFlyoutAtWidget();
    }

    /// <summary>
    /// Off the widget thread: connecting to the window process can wait up to 150 ms, and the
    /// widget thread shares its input queue with Explorer.
    /// </summary>
    private static void OpenFlyout(int anchorX, int anchorY)
    {
        ForegroundPermission.GrantForNextWindow();
        var command = new SurfaceCommand(SurfaceKind.Flyout, anchorX, anchorY);
        Task.Run(() => SurfacesLauncher.Show(command));
    }

    private static void OpenSettings()
    {
        ForegroundPermission.GrantForNextWindow();
        var command = new SurfaceCommand(SurfaceKind.Settings, 0, 0);
        Task.Run(() => SurfacesLauncher.Show(command));
    }

    private static void RecordLocation()
    {
        if (Environment.ProcessPath is { } executablePath)
        {
            ResidentLocation.Record(executablePath);
        }
    }

    private void OpenFlyoutAtWidget()
    {
        if (widgets.PrimaryWidgetAnchor() is { } anchor)
        {
            OpenFlyout(anchor.X, anchor.Y);
        }
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
        settingsWatcher.Dispose();
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

    /// <summary>Runs on the UI thread after a menu change or a save in Settings.</summary>
    private void Apply(HakariSettings settings)
    {
        widgets.SetMotion(ToMotion(settings.Animation));
        widgets.SetDisplayFilter(DisplayFilter.From(settings));
        var languageChanged = Texts.Use(settings.Language);
        if (!settings.FeedsSameDataAs(appliedSettings))
        {
            feed.ReloadSettings();
        }
        else if (languageChanged || settings.Widget != appliedSettings.Widget)
        {
            feed.SetLayout(settings.Widget);
        }

        appliedSettings = settings;
    }
}
