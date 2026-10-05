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

    /// <summary>
    /// Decides by the taskbar's actual background, because a secondary taskbar does not
    /// always follow a theme change. Falls back to the Windows setting.
    /// </summary>
    public static bool IsLight(TaskbarInfo taskbar) =>
        SampleLuminance(taskbar) is { } luminance
            ? luminance >= LightLuminanceThreshold
            : IsWindowsModeLight();

    public static bool IsWindowsModeLight()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue(SystemUsesLightThemeValue) is int value && value != 0;
    }

    private static double? SampleLuminance(TaskbarInfo taskbar)
    {
        var middle = taskbar.Bounds.Top + taskbar.Bounds.Height / 2;
        int[] sampleColumns =
        [
            taskbar.Bounds.Left + SampleInset,
            taskbar.Bounds.Right - SampleInset,
        ];

        var screenContext = User32.GetDC(IntPtr.Zero);
        try
        {
            var samples = sampleColumns
                .Select(column => Gdi32.GetPixel(screenContext, column, middle))
                .Where(color => color != Gdi32.InvalidColor)
                .Select(RelativeLuminance)
                .ToList();
            return samples.Count == 0 ? null : samples.Average();
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
