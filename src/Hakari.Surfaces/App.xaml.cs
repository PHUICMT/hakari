using Hakari.Core.Interprocess;
using Hakari.Surfaces.Flyout;
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

    /// <summary>Only the flyout exists so far; other requests open it too.</summary>
    private void Handle(SurfaceCommand command)
    {
        idleExitTimer?.Stop();
        ShowFlyout(command);
    }

    private void ShowFlyout(SurfaceCommand command)
    {
        if (flyout is null)
        {
            flyout = new FlyoutWindow();
            flyout.Hidden += (_, _) => idleExitTimer?.Start();
        }

        flyout.Toggle(command.AnchorX, command.AnchorY);
    }

    private void ExitWhenIdle()
    {
        if (flyout?.IsShowing == true)
        {
            return;
        }

        // Application.Exit leaves the process alive while a hidden window exists. This process
        // only reads, so ending it directly loses nothing.
        listening.Cancel();
        Environment.Exit(NormalExitCode);
    }

    private const int NormalExitCode = 0;
}
