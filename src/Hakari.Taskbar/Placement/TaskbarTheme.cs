using System.Drawing;
using Hakari.Taskbar.Interop;
using Microsoft.Win32;

namespace Hakari.Taskbar.Placement;

public static class TaskbarTheme
{
    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>The taskbar follows the Windows mode, not the app mode.</summary>
    private const string SystemUsesLightThemeValue = "SystemUsesLightTheme";

    /// <summary>Pixels from the taskbar's left and right edges: empty background on both.</summary>
    private const int SampleInset = 3;

    private const double LightLuminanceThreshold = 0.5;
    private const double MaximumChannel = 255.0;
    private const uint ChannelMask = 0xFF;
    private const int GreenShift = 8;
    private const int BlueShift = 16;
    private const double RedWeight = 0.2126;
    private const double GreenWeight = 0.7152;
    private const double BlueWeight = 0.0722;

    /// <summary>How far left of the widget its background is read, clear of the widget.</summary>
    private const int BesideWidget = 6;

    /// <summary>Rows read beside the widget, as shares of the taskbar's height.</summary>
    private static readonly double[] BesideRows = [0.3, 0.5, 0.7];

    /// <summary>
    /// Decides by what is actually behind the widget: just left of it when it has been
    /// placed, which on a see-through taskbar is the wallpaper the text sits on, else the
    /// taskbar's empty ends. A secondary taskbar does not always follow a theme change, so
    /// the Windows setting is only the last resort.
    /// </summary>
    public static bool IsLight(TaskbarInfo taskbar, Rectangle widgetBounds) =>
        (SampleBesideWidget(taskbar, widgetBounds) ?? SampleLuminance(taskbar))
            is { } luminance
            ? luminance >= LightLuminanceThreshold
            : IsWindowsModeLight();

    public static bool IsLight(TaskbarInfo taskbar) => IsLight(taskbar, Rectangle.Empty);

    /// <summary>
    /// The middle of three readings beside the widget, so one app icon edge or badge in the
    /// way does not decide it.
    /// </summary>
    private static double? SampleBesideWidget(TaskbarInfo taskbar, Rectangle widgetBounds)
    {
        var column = widgetBounds.Left - BesideWidget;
        if (widgetBounds.IsEmpty || column <= taskbar.Bounds.Left
            || !taskbar.Bounds.IntersectsWith(widgetBounds))
        {
            return null;
        }

        var points = BesideRows
            .Select(share => (column, taskbar.Bounds.Top + (int)(taskbar.Bounds.Height * share)))
            .ToList();
        var samples = Read(points);
        return samples.Count == 0 ? null : samples.Order().ElementAt(samples.Count / 2);
    }

    public static bool IsWindowsModeLight()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue(SystemUsesLightThemeValue) is int value && value != 0;
    }

    private static double? SampleLuminance(TaskbarInfo taskbar)
    {
        var middle = taskbar.Bounds.Top + taskbar.Bounds.Height / 2;
        var samples = Read(
        [
            (taskbar.Bounds.Left + SampleInset, middle),
            (taskbar.Bounds.Right - SampleInset, middle),
        ]);
        return samples.Count == 0 ? null : samples.Average();
    }

    /// <summary>The luminance of each screen point that could be read.</summary>
    private static List<double> Read(IReadOnlyList<(int X, int Y)> points)
    {
        var screenContext = User32.GetDC(IntPtr.Zero);
        try
        {
            return
            [
                .. points
                    .Select(point => Gdi32.GetPixel(screenContext, point.X, point.Y))
                    .Where(color => color != Gdi32.InvalidColor)
                    .Select(RelativeLuminance),
            ];
        }
        finally
        {
            User32.ReleaseDC(IntPtr.Zero, screenContext);
        }
    }

    /// <summary>Rec. 709 luma of a 0x00BBGGRR color, from 0 (black) to 1 (white).</summary>
    private static double RelativeLuminance(uint color)
    {
        var red = (color & ChannelMask) / MaximumChannel;
        var green = ((color >> GreenShift) & ChannelMask) / MaximumChannel;
        var blue = ((color >> BlueShift) & ChannelMask) / MaximumChannel;
        return RedWeight * red + GreenWeight * green + BlueWeight * blue;
    }
}
