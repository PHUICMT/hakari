using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Hakari.Surfaces.Controls;

/// <summary>
/// The colors the widget takes on one kind of taskbar, so a preview can show how it reads on
/// a light taskbar and on a dark one, whatever the app's own theme is.
/// </summary>
public sealed record TaskbarPalette(
    Brush Background,
    Brush Ink,
    Brush Faint,
    Brush Normal,
    Brush Warn,
    Brush Critical,
    Brush Track)
{
    public static TaskbarPalette Light { get; } = new(
        Solid(0xEE, 0xF0, 0xF4),
        Solid(0x1B, 0x1D, 0x22),
        Solid(0x5D, 0x62, 0x70),
        Solid(0x2F, 0x4C, 0x8C),
        Solid(0xA9, 0x65, 0x00),
        Solid(0xC0, 0x32, 0x2B),
        Solid(0x1F, 0x00, 0x00, 0x00));

    public static TaskbarPalette Dark { get; } = new(
        Solid(0x1D, 0x20, 0x27),
        Solid(0xF1, 0xF2, 0xF5),
        Solid(0xA4, 0xA9, 0xB6),
        Solid(0x9F, 0xB6, 0xEE),
        Solid(0xF4, 0xB1, 0x4F),
        Solid(0xFF, 0x7B, 0x72),
        Solid(0x29, 0xFF, 0xFF, 0xFF));

    private static SolidColorBrush Solid(byte red, byte green, byte blue) =>
        new(Color.FromArgb(0xFF, red, green, blue));

    private static SolidColorBrush Solid(byte alpha, byte red, byte green, byte blue) =>
        new(Color.FromArgb(alpha, red, green, blue));
}
