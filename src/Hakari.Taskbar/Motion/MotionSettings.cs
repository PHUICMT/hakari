using Hakari.Taskbar.Interop;

namespace Hakari.Taskbar.Motion;

public static class MotionSettings
{
    /// <summary>
    /// An explicit choice wins. Otherwise follow Windows: with "Animation effects" off the
    /// widget starts in <see cref="MotionPreference.Reduced"/>, as the design specifies.
    /// </summary>
    public static MotionPreference Resolve(MotionPreference? userChoice)
    {
        if (userChoice is { } choice)
        {
            return choice;
        }

        return AreWindowsAnimationsEnabled() ? MotionPreference.Full : MotionPreference.Reduced;
    }

    public static bool AreWindowsAnimationsEnabled()
    {
        var enabled = true;
        var succeeded = User32.SystemParametersInfo(
            SystemParameters.GetClientAreaAnimation,
            0,
            ref enabled,
            0);
        return !succeeded || enabled;
    }
}
