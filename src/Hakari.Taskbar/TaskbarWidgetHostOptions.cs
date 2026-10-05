using Hakari.Taskbar.Motion;

namespace Hakari.Taskbar;

/// <param name="ShowOnDisplay">
/// Given a display's current device name (\\.\DISPLAY2), whether it gets a widget; null puts
/// one on every taskbar. When it rejects every connected display, the main one still gets one.
/// </param>
/// <param name="Motion">The user's animation choice; null follows the Windows setting.</param>
public sealed record TaskbarWidgetHostOptions(
    AttachMode Mode,
    Func<string, bool>? ShowOnDisplay,
    MotionPreference? Motion)
{
    public static TaskbarWidgetHostOptions Default { get; } = new(
        Mode: AttachMode.ChildOfTaskbar,
        ShowOnDisplay: null,
        Motion: null);
}
