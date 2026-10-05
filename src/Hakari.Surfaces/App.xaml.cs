using Hakari.Core.Interprocess;
using Hakari.Surfaces.Flyout;
using Hakari.Surfaces.Settings;
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
    private SettingsWindow? settings;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        dispatcher = DispatcherQueue.GetForCurrentThread();
        idleExitTimer = dispatcher.CreateTimer();
        idleExitTimer.Interval = IdleExitDelay;
        idleExitTimer.IsRepeating = false;
        idleExitTimer.Tick += (_, _) => ExitWhenIdle();

        _ = SurfaceChannel.ListenAsync(
            command => dispatcher.TryEnqueue(() => Handle(command)),
            listening.Token);

        var commandLine = Environment.GetCommandLineArgs().Skip(1).ToArray();
        Handle(SurfaceCommand.Parse(commandLine) ?? DefaultCommand());
    }

    private static SurfaceCommand DefaultCommand() => new(SurfaceKind.Flyout, 0, 0);

    /// <summary>The dashboard does not exist yet; asking for it opens the flyout.</summary>
    private void Handle(SurfaceCommand command)
    {
        idleExitTimer?.Stop();
        if (command.Kind == SurfaceKind.Settings)
        {
            ShowSettings();
        }
        else
        {
            ShowFlyout(command);
        }
    }

    private void ShowFlyout(SurfaceCommand command)
    {
        if (flyout is null)
        {
            flyout = new FlyoutWindow();
            flyout.Hidden += (_, _) => idleExitTimer?.Start();
            flyout.SettingsRequested += (_, _) => ShowSettings();
        }

        flyout.Toggle(command.AnchorX, command.AnchorY);
    }

    /// <summary>A closed window cannot be shown again, so each opening builds a new one.</summary>
    private void ShowSettings()
    {
        idleExitTimer?.Stop();
        if (settings is null)
        {
            settings = new SettingsWindow();
            settings.Closed += (_, _) =>
            {
                settings = null;
                idleExitTimer?.Start();
            };
        }

        settings.Present();
    }

    private void ExitWhenIdle()
    {
        if (flyout?.IsShowing == true || settings is not null)
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
