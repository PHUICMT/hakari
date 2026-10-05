using Hakari.Taskbar.Interop;

namespace Hakari.Taskbar;

public static class MessageLoop
{
    private const int NormalExitCode = 0;

    /// <summary>Per-monitor DPI awareness must be set before the first window is created.</summary>
    public static void EnablePerMonitorDpiAwareness() =>
        User32.SetProcessDpiAwarenessContext(DpiAwarenessContexts.PerMonitorAwareVersion2);

    public static void Run()
    {
        while (User32.GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            User32.TranslateMessage(ref message);
            User32.DispatchMessage(ref message);
        }
    }

    public static void Quit() => User32.PostQuitMessage(NormalExitCode);
}
