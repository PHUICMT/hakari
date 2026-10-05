namespace Hakari.Taskbar.Rendering;

/// <summary>One moment of the widget, possibly in the middle of a transition.</summary>
/// <param name="Previous">Content being replaced, or null when nothing is changing.</param>
/// <param name="ValueProgress">Eased 0 to 1: how far new text has replaced the old.</param>
/// <param name="ToneProgress">Eased 0 to 1: how far the secondary color has changed.</param>
/// <param name="HoverAmount">0 (no hover fill) to 1 (full hover fill).</param>
/// <param name="MovesText">True slides text in; false only cross-fades it.</param>
public sealed record WidgetFrame(
    WidgetContent Current,
    WidgetContent? Previous,
    double ValueProgress,
    double ToneProgress,
    double HoverAmount,
    bool MovesText)
{
    public bool IsChanging => Previous is not null && (ValueProgress < 1 || ToneProgress < 1);

    public static WidgetFrame Settled(WidgetContent content, double hoverAmount = 0) =>
        new(content, null, 1, 1, hoverAmount, MovesText: false);
}
