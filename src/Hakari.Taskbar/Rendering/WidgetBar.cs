namespace Hakari.Taskbar.Rendering;

/// <summary>A thin bar beside a line of text, filled to a share from 0 to 1.</summary>
public sealed record WidgetBar(double Fraction, WidgetTone Tone = WidgetTone.Normal);
