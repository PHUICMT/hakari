namespace Hakari.Taskbar.Rendering;

/// <param name="Fraction">0 to 1. At 1 it is drawn as a solid "stop" disc.</param>
/// <param name="InnerFraction">A second, smaller ring inside, or null for one ring.</param>
public sealed record WidgetRing(
    double Fraction,
    WidgetTone Tone = WidgetTone.Normal,
    double? InnerFraction = null,
    WidgetTone InnerTone = WidgetTone.Normal);
