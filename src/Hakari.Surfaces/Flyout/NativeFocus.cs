using System.Runtime.InteropServices;

namespace Hakari.Surfaces.Flyout;

internal static partial class NativeFocus
{
    /// <summary>
    /// Takes focus so a click elsewhere deactivates the flyout. Hakari.exe grants the right
    /// while handling the click, but a cold start can outlive that grant; joining the
    /// foreground window's input queue for a moment is the documented-safe fallback.
    /// </summary>
    public static void BringToFront(IntPtr windowHandle)
    {
        if (SetForegroundWindow(windowHandle))
        {
            return;
        }

        var foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        var ownThread = GetCurrentThreadId();
        if (foregroundThread == 0 || foregroundThread == ownThread)
        {
            return;
        }

        AttachThreadInput(ownThread, foregroundThread, attach: true);
        try
        {
            SetForegroundWindow(windowHandle);
        }
        finally
        {
            AttachThreadInput(ownThread, foregroundThread, attach: false);
        }
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr windowHandle);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachThreadInput(
        uint attachingThread,
        uint targetThread,
        [MarshalAs(UnmanagedType.Bool)] bool attach);
}
