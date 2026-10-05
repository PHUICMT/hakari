using Hakari.Taskbar.Motion;

namespace Hakari.Taskbar;

/// <param name="Motion">The user's animation choice; null follows the Windows setting.</param>
public sealed record TaskbarWidgetHostOptions(
    AttachMode Mode,
    bool ShowOnSecondaryTaskbars,
    MotionPreference? Motion)
{
    public static TaskbarWidgetHostOptions Default { get; } = new(
        Mode: AttachMode.ChildOfTaskbar,
        ShowOnSecondaryTaskbars: true,
        Motion: null);
}
