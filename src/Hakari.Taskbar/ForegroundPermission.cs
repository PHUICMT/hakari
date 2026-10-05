using Hakari.Taskbar.Interop;

namespace Hakari.Taskbar;

public static class ForegroundPermission
{
    private const int AnyProcess = -1;

    /// <summary>
    /// Lets the window process take focus when it opens the flyout. Call it while handling the
    /// user's click: Windows only allows this from the process that received the input, and
    /// without focus the flyout would never learn that the user clicked elsewhere.
    /// </summary>
    public static void GrantForNextWindow() => User32.AllowSetForegroundWindow(AnyProcess);
}
