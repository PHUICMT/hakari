using System.Drawing;

namespace Hakari.Taskbar.Rendering;

/// <summary>Colors for text drawn on the Windows taskbar (see the design handoff tokens).</summary>
public sealed record WidgetPalette(
    Color PrimaryText,
    Color SecondaryText,
    Color Accent,
    Color Warning,
    Color Critical,
    Color HoverFill)
{
    private const int MutedAlpha = 140;
    private const int RingTrackAlpha = 70;

    public static WidgetPalette LightTaskbar { get; } = new(
        PrimaryText: ColorTranslator.FromHtml("#1b1d22"),
        SecondaryText: ColorTranslator.FromHtml("#5d6270"),
        Accent: ColorTranslator.FromHtml("#2f4c8c"),
        Warning: ColorTranslator.FromHtml("#a96500"),
        Critical: ColorTranslator.FromHtml("#c0322b"),
        HoverFill: Color.FromArgb(13, 0, 0, 0));

    public static WidgetPalette DarkTaskbar { get; } = new(
        PrimaryText: ColorTranslator.FromHtml("#f1f2f5"),
        SecondaryText: ColorTranslator.FromHtml("#a4a9b6"),
        Accent: ColorTranslator.FromHtml("#93acea"),
        Warning: ColorTranslator.FromHtml("#f4b14f"),
        Critical: ColorTranslator.FromHtml("#ff7b72"),
        HoverFill: Color.FromArgb(18, 255, 255, 255));

    public Color RingTrack => Color.FromArgb(RingTrackAlpha, SecondaryText);

    public Color ForTone(WidgetTone tone) => tone switch
    {
        WidgetTone.Warning => Warning,
        WidgetTone.Critical => Critical,
        WidgetTone.Muted => Color.FromArgb(MutedAlpha, SecondaryText),
        _ => SecondaryText,
    };

    public Color ForPrimaryTone(WidgetTone tone) =>
        tone == WidgetTone.Normal ? PrimaryText : ForTone(tone);

    public Color ForRingTone(WidgetTone tone) =>
        tone == WidgetTone.Normal ? Accent : ForTone(tone);
}
