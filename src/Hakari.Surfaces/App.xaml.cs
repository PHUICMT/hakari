using Hakari.Core.Interprocess;
using Hakari.Core.Localization;
using Hakari.Core.Settings;
using Hakari.Surfaces.Flyout;
using Hakari.Surfaces.Popups;
using Hakari.Surfaces.Dashboard;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Hakari.Surfaces;

/// <summary>
/// The window process. It shows the window asked for on the command line, keeps listening for
/// more requests from Hakari.exe, and exits once nothing has been shown for a while.
/// </summary>
public partial class App : Application
{
    private static readonly TimeSpan IdleExitDelay = TimeSpan.FromSeconds(60);

    private readonly CancellationTokenSource listening = new();
    private DispatcherQueue? dispatcher;
    private DispatcherQueueTimer? idleExitTimer;
    private FlyoutWindow? flyout;
    private DashboardWindow? dashboard;
    private TooltipWindow? tooltip;
    private MenuWindow? menu;

    public App()
    {
        InitializeComponent();

        // A failure on the UI thread is logged and survived: one broken page or popup must
        // not close every window. Background failures are logged too.
        UnhandledException += (_, args) =>
        {
            CrashLog.Write(args.Exception, args.Message);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            CrashLog.Write(args.ExceptionObject as Exception, "background thread");
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            CrashLog.Write(args.Exception, "unobserved task");
            args.SetObserved();
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // One window process at a time: a second one hands its request to the first and goes.
        var commandLine = Environment.GetCommandLineArgs().Skip(1).ToArray();
        var command = SurfaceCommand.Parse(commandLine) ?? DefaultCommand();
        if (SurfaceChannel.TrySend(command))
        {
            Environment.Exit(NormalExitCode);
            return;
        }

        dispatcher = DispatcherQueue.GetForCurrentThread();
        idleExitTimer = dispatcher.CreateTimer();
        idleExitTimer.Interval = IdleExitDelay;
        idleExitTimer.IsRepeating = false;
        idleExitTimer.Tick += (_, _) => ExitWhenIdle();

        _ = SurfaceChannel.ListenAsync(
            command => dispatcher.TryEnqueue(() => Handle(command)),
            listening.Token);

        Handle(command);
    }

    private static SurfaceCommand DefaultCommand() => new(SurfaceKind.Flyout, 0, 0);

    private void Handle(SurfaceCommand command)
    {
        idleExitTimer?.Stop();
        ApplyLanguage();
        switch (command.Kind)
        {
            case SurfaceKind.Settings:
                ShowSettings();
                break;
            case SurfaceKind.Dashboard:
                tooltip?.HidePopup();
                ShowDashboard(PageOf(command));
                break;
            case SurfaceKind.Menu:
                tooltip?.HidePopup();
                Menu.ShowAt(command.AnchorX, command.AnchorY);
                break;
            case SurfaceKind.Tooltip:
                if (menu?.IsShowing != true && flyout?.IsShowing != true)
                {
                    Tooltip.ShowAt(command.AnchorX, command.AnchorY);
                }

                break;
            case SurfaceKind.HideTooltip:
                tooltip?.HidePopup();
                idleExitTimer?.Start();
                break;
            case SurfaceKind.Warm:
                idleExitTimer?.Start();
                break;
            case SurfaceKind.Onboarding:
                ShowOnboarding();
                break;
            default:
                tooltip?.HidePopup();
                ShowFlyout(command);
                break;
        }
    }

    /// <summary>Built once and reused: the hover card fills itself each time it shows.</summary>
    private TooltipWindow Tooltip => tooltip ??= new TooltipWindow();

    private MenuWindow Menu => menu ??= new MenuWindow(
        ShowSettings,
        () => ShowDashboard(null));

    private void ShowFlyout(SurfaceCommand command)
    {
        if (flyout is null)
        {
            flyout = new FlyoutWindow();
            flyout.Hidden += (_, _) => idleExitTimer?.Start();
            flyout.SettingsRequested += (_, _) => ShowSettings();
            flyout.DashboardRequested += (_, _) => ShowDashboard(null);
        }

        flyout.Toggle(command.AnchorX, command.AnchorY);
    }

    private void ShowSettings() => ShowDashboard(DashboardPage.Settings);

    private Onboarding.OnboardingWindow? onboarding;

    private void ShowOnboarding()
    {
        if (onboarding is null)
        {
            onboarding = new Onboarding.OnboardingWindow();
            onboarding.Closed += (_, _) =>
            {
                onboarding = null;
                idleExitTimer?.Start();
            };
        }

        onboarding.Activate();
    }

    /// <summary>A dashboard command may name its page in the first number; 0 is Overview.</summary>
    private static DashboardPage PageOf(SurfaceCommand command) =>
        Enum.IsDefined((DashboardPage)command.AnchorX)
            ? (DashboardPage)command.AnchorX
            : DashboardPage.Overview;

    /// <summary>
    /// A closed window cannot be shown again, so each opening builds a new one. Without a
    /// page it opens where it was left.
    /// </summary>
    private void ShowDashboard(DashboardPage? page)
    {
        idleExitTimer?.Stop();
        if (dashboard is null)
        {
            dashboard = new DashboardWindow();
            var window = dashboard;
            dashboard.Closed += (_, _) =>
            {
                if (dashboard == window)
                {
                    dashboard = null;
                    idleExitTimer?.Start();
                }
            };
            dashboard.LanguageChanged += (_, _) => RebuildForLanguage(window);
        }

        dashboard.Present(page ?? DashboardWindow.RememberedPage());
    }

    /// <summary>
    /// Window text is read when a window is built, so a new language means a new window. It
    /// reopens on the page the user was on.
    /// </summary>
    private void RebuildForLanguage(DashboardWindow oldWindow)
    {
        ApplyLanguage();
        var page = oldWindow.CurrentPage;
        dashboard = null;
        ShowDashboard(page);
        oldWindow.Close();
    }
    private void ApplyLanguage()
    {
        if (Texts.Use(SettingsStore.Default.Load().Language) && flyout is not null)
        {
            flyout.Close();
            flyout = null;
        }
    }

    private void ExitWhenIdle()
    {
        if (flyout?.IsShowing == true || dashboard is not null
            || menu?.IsShowing == true || tooltip?.IsShowing == true)
        {
            return;
        }

        // Application.Exit leaves the process alive while a hidden window exists. Settings are
        // saved the moment they change, so ending it directly loses nothing.
        listening.Cancel();
        Environment.Exit(NormalExitCode);
    }

    private const int NormalExitCode = 0;
}
