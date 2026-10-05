namespace Hakari.Taskbar.Rendering;

/// <param name="Fraction">0 to 1: how much of the ring is filled.</param>
public sealed record WidgetRing(double Fraction, WidgetTone Tone = WidgetTone.Normal);
