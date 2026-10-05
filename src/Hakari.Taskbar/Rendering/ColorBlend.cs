using System.Drawing;

namespace Hakari.Taskbar.Rendering;

internal static class ColorBlend
{
    public static Color Mix(Color from, Color to, double amount)
    {
        var clamped = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(
            Channel(from.A, to.A, clamped),
            Channel(from.R, to.R, clamped),
            Channel(from.G, to.G, clamped),
            Channel(from.B, to.B, clamped));
    }

    public static Color WithOpacity(Color color, double opacity) =>
        Color.FromArgb((int)Math.Round(color.A * Math.Clamp(opacity, 0, 1)), color);

    private static int Channel(byte from, byte to, double amount) =>
        (int)Math.Round(from + (to - from) * amount);
}
