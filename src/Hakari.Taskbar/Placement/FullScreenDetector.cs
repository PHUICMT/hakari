using Hakari.Taskbar.Interop;

namespace Hakari.Taskbar.Placement;

public static class FullScreenDetector
{
    private const int Success = 0;

    /// <summary>True while a full-screen game, video, or presentation owns the screen.</summary>
    public static bool IsSomethingFullScreen()
    {
        if (Shell32.SHQueryUserNotificationState(out var state) != Success)
        {
            return false;
        }

        return state is UserNotificationStates.Busy
            or UserNotificationStates.RunningFullScreenDirect3D
            or UserNotificationStates.PresentationMode;
    }
}
