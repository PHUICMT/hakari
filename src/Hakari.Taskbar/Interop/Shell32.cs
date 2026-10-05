using System.Runtime.InteropServices;

namespace Hakari.Taskbar.Interop;

internal static class Shell32
{
    private const string Library = "shell32.dll";

    [DllImport(Library)]
    public static extern int SHQueryUserNotificationState(out int state);
}
