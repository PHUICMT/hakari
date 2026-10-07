using Hakari.Core.Interprocess;
using Hakari.Core.Localization;
using Hakari.Core.Presentation.Widget;
using Hakari.Core.Settings;
using Hakari.Core.Startup;
using Hakari.Core.Presentation;
using Hakari.Feed;
using Hakari.Notifications;
using Hakari.Settings;
using Hakari.Surfaces;
using Hakari.Taskbar;
using Hakari.Taskbar.Motion;
using Hakari.Taskbar.Rendering;
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
    private static readonly TimeSpan PauseLength = TimeSpan.FromHours(1);

    private readonly SettingsStore settingsStore;
    private readonly TaskbarWidgetHost widgets;
    private readonly UsageFeed feed;
    private readonly TrayIcon trayIcon;
    private readonly ResidentMenu menu;
    private readonly SettingsWatcher settingsWatcher;
    private readonly WidgetSurfaces widgetSurfaces;
    private readonly CancellationTokenSource listening = new();
    private HakariSettings appliedSettings;
    private TrayBadge? lastBadge;
    private (decimal Amount, string Currency) todayCost = (0, "USD");
    private static readonly TimeSpan StaleCheckInterval = TimeSpan.FromMinutes(1);
    private Timer? staleCheck;
    private Timer? fallbackCheck;
    private static readonly TimeSpan FallbackCheckDelay = TimeSpan.FromSeconds(15);
    private WidgetContent? lastLive;
    private DateTimeOffset lastHeard = DateTimeOffset.Now;
    private bool showingStale;

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
        widgets.Faulted += ErrorLog.Write;
        feed = new UsageFeed(settingsStore);
        menu = new ResidentMenu(settingsStore, Apply, OpenSettings);
        trayIcon = new TrayIcon(TrayTooltip, menu.Build);
        settingsWatcher = new SettingsWatcher(settingsStore);
        settingsWatcher.Changed += (_, _) =>
            widgets.PostAction(() => Apply(settingsStore.Load()));
        RecordLocation();

        menu.QuitRequested += (_, _) => MessageLoop.Quit();
        widgetSurfaces = new WidgetSurfaces(widgets);
        trayIcon.MenuRequested += (_, pointer) => widgetSurfaces.OpenMenu((pointer.X, pointer.Y));
        _ = ResidentChannel.ListenAsync(
            command => widgets.PostAction(() => Handle(command)),
            listening.Token);
        widgets.Clicked += (_, click) =>
            OpenFlyout(click.PointerX, click.TaskbarBounds.Top);
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

    /// <summary>Runs on the widget thread; asked for from the widget's menu.</summary>
    private void Handle(ResidentCommand command)
    {
        switch (command)
        {
            case ResidentCommand.RefreshNow:
                feed.RefreshNow();
                break;
            case ResidentCommand.PauseForAnHour:
                Apply(settingsStore.Update(current => current with
                {
                    PausedUntil = DateTimeOffset.UtcNow + PauseLength,
                }));
                break;
            case ResidentCommand.Quit:
                MessageLoop.Quit();
                break;
            case ResidentCommand.OpenFlyout:
                OpenFlyoutAtWidget();
                break;
            case ResidentCommand.MuteAlertsToday:
                Apply(settingsStore.Update(current => current with
                {
                    AlertsMutedUntil = DateTimeOffset.Now.Date.AddDays(1),
                }));
                break;
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
            feed.Updated += content => widgets.PostAction(() => ShowContent(content));
            widgets.Scrolled += (_, delta) => feed.ShiftTurn(delta > 0 ? -1 : 1);
            feed.CostUpdated += (cost, currency) => widgets.PostAction(() =>
            {
                todayCost = (cost, currency);
                ShowBadge(lastBadge);
            });
            feed.BadgeUpdated += badge => widgets.PostAction(() =>
            {
                Heard();
                ShowBadge(badge);
            });
            staleCheck = new Timer(
                _ => widgets.PostAction(ShowIfStale),
                null,
                StaleCheckInterval,
                StaleCheckInterval);
            ToastNotifier.Register();
            ShowOnboardingOnce();
            fallbackCheck = new Timer(
                _ => widgets.PostAction(TellTrayFallbackOnce),
                null,
                FallbackCheckDelay,
                Timeout.InfiniteTimeSpan);
            feed.AlertRaised += message => widgets.PostAction(() => ShowAlert(message));
            feed.Start();
        }

        MessageLoop.Run();
    }

    /// <summary>
    /// The first-run steps, until they have been finished or closed once. Indexing starts
    /// at the same time, so the numbers are ready by the last step.
    /// </summary>
    private void ShowOnboardingOnce()
    {
        if (appliedSettings.OnboardingDone)
        {
            return;
        }

        ForegroundPermission.GrantForNextWindow();
        var command = new SurfaceCommand(SurfaceKind.Onboarding, 0, 0);
        Task.Run(() => SurfacesLauncher.Show(command));
    }

    /// <summary>Few enough characters for a tray icon: "142", "3.3k", "12k", "1.2M".</summary>
    private static string CompactAmount(decimal amount)
    {
        const decimal Thousand = 1_000;
        const decimal Million = 1_000_000;
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        return amount switch
        {
            < Thousand => decimal.Round(amount).ToString("0", culture),
            < 10 * Thousand => (amount / Thousand).ToString("0.#", culture) + "k",
            < Million => (amount / Thousand).ToString("0", culture) + "k",
            _ => (amount / Million).ToString("0.#", culture) + "M",
        };
    }

    /// <summary>
    /// When no taskbar has room for the widget, or it cannot be placed at all, the meter lives
    /// in the tray icon; that is said once, so the widget is not simply missing.
    /// </summary>
    private void TellTrayFallbackOnce()
    {
        if (widgets.WidgetCount > 0 || appliedSettings.TrayFallbackTold)
        {
            return;
        }

        trayIcon.ShowBalloon(
            Texts.Get("tray.fallback.title"),
            Texts.Get("tray.fallback.body"),
            isWarning: false);
        Apply(settingsStore.Update(current => current with { TrayFallbackTold = true }));
    }

    /// <summary>A Windows notification, or the tray's balloon when that is refused.</summary>
    private void ShowAlert(LimitAlertMessage message)
    {
        if (!ToastNotifier.Show(message))
        {
            var text = string.Join(
                Environment.NewLine,
                new[] { message.Body, message.Detail }.Where(line => line.Length > 0));
            trayIcon.ShowBalloon(message.Title, text, message.IsWarning);
        }
    }

    /// <summary>Runs on the widget thread: every update from the feed shows as it comes.</summary>
    private void ShowContent(WidgetContent content)
    {
        lastHeard = DateTimeOffset.Now;
        showingStale = false;
        if (WidgetText.IsLive(content))
        {
            lastLive = content;
        }

        widgets.PostContent(content);

        // Win+B reaches the tray icon; its tooltip reads out what the widget shows.
        trayIcon.SetTooltip(TrayTooltip + ": " + content.Spoken());
    }

    /// <summary>
    /// The feed only posts what changed, so after a stale spell the last value is put back
    /// here as soon as the feed is heard from again.
    /// </summary>
    private void Heard()
    {
        lastHeard = DateTimeOffset.Now;
        if (showingStale && lastLive is { } live)
        {
            showingStale = false;
            widgets.PostContent(live);
        }
    }

    /// <summary>Runs on the widget thread each minute; a pause is never stale.</summary>
    private void ShowIfStale()
    {
        var now = DateTimeOffset.Now;
        if (lastLive is not { } live
            || appliedSettings.IsPausedAt(DateTimeOffset.UtcNow)
            || !StaleWidget.IsStale(lastHeard, now))
        {
            return;
        }

        showingStale = true;
        widgets.PostContent(WidgetText.Stale(live, lastHeard, now));
    }

    public void Dispose()
    {
        staleCheck?.Dispose();
        fallbackCheck?.Dispose();
        listening.Cancel();
        widgetSurfaces.Dispose();
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
        else if (languageChanged || !settings.PresentsSameAs(appliedSettings))
        {
            feed.SetPresentation(settings);
        }

        var limitsChoiceChanged =
            !settings.LimitsOffAccounts.SequenceEqual(appliedSettings.LimitsOffAccounts)
            || !settings.LimitsAskedAccounts.SequenceEqual(appliedSettings.LimitsAskedAccounts);
        appliedSettings = settings;
        if (limitsChoiceChanged)
        {
            feed.SetPresentation(settings);
            feed.RefreshNow();
        }

        ShowBadge(lastBadge);
    }

    /// <summary>
    /// Runs on the widget thread. Automatic shows the limit only when no widget is on any
    /// taskbar, so the meter is never missing.
    /// </summary>
    private void ShowBadge(TrayBadge? badge)
    {
        lastBadge = badge;
        if (appliedSettings.TrayIcon == TrayIconStyle.Cost)
        {
            trayIcon.SetBadge(new TrayBadge(
                0,
                Taskbar.Rendering.WidgetTone.Normal,
                CompactAmount(todayCost.Amount),
                todayCost.Currency));
            return;
        }

        var showLimit = appliedSettings.TrayIcon switch
        {
            TrayIconStyle.Limit => true,
            TrayIconStyle.Logo => false,
            _ => widgets.WidgetCount == 0,
        };
        trayIcon.SetBadge(showLimit ? badge : null);
    }
}
