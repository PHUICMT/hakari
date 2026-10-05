namespace Hakari.Taskbar.Rendering;

public sealed record WidgetContent(
    string PrimaryText,
    string SecondaryText,
    WidgetTone SecondaryTone = WidgetTone.Normal);
