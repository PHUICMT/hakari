namespace Hakari.Taskbar;

public sealed record TaskbarWidgetHostOptions(
    AttachMode Mode,
    bool ShowOnSecondaryTaskbars)
{
    public static TaskbarWidgetHostOptions Default { get; } = new(
        Mode: AttachMode.ChildOfTaskbar,
        ShowOnSecondaryTaskbars: true);
}
