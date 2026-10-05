namespace Hakari.Taskbar.Rendering;

/// <param name="PrimaryTone">Normal draws the top line in the strong text color.</param>
/// <param name="Ring">A meter left of the text, or null for text only.</param>
public sealed record WidgetContent(
    string PrimaryText,
    string SecondaryText,
    WidgetTone SecondaryTone = WidgetTone.Normal,
    WidgetTone PrimaryTone = WidgetTone.Normal,
    WidgetRing? Ring = null);
