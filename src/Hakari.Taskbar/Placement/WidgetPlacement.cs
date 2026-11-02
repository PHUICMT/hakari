using System.Drawing;

namespace Hakari.Taskbar.Placement;

public static class WidgetPlacement
{
    private const int GapBeforeNotificationArea = 4;
    private const int MinimumGapAfterAppButtons = 8;
    private const int GapInFreeSpace = 8;

    /// <summary>
    /// Beside the tray when it fits. On a centered taskbar the app buttons can take that side
    /// while the left end stays empty, so the widget goes there instead of shrinking or hiding.
    /// </summary>
    public static Rectangle? Find(TaskbarInfo taskbar, Size widgetSize) =>
        LeftOfNotificationArea(taskbar, widgetSize) ?? BeforeStart(taskbar, widgetSize);

    /// <summary>
    /// Places the widget at the left end of the free stretch before Start, so it stays put while
    /// the centered buttons grow and shrink. Null when it does not fit.
    /// </summary>
    public static Rectangle? BeforeStart(TaskbarInfo taskbar, Size widgetSize)
    {
        var gap = (int)Math.Round(GapInFreeSpace * taskbar.Scale);
        var left = taskbar.FreeBeforeStart.Left + gap;
        if (left + widgetSize.Width + gap > taskbar.FreeBeforeStart.Right)
        {
            return null;
        }

        var top = taskbar.Bounds.Top + (taskbar.Bounds.Height - widgetSize.Height) / 2;
        return new Rectangle(left, top, widgetSize.Width, widgetSize.Height);
    }

    /// <summary>
    /// Places the widget immediately left of the notification area, vertically centered.
    /// Returns null when it would overlap the app buttons, so the caller can go compact.
    /// </summary>
    public static Rectangle? LeftOfNotificationArea(TaskbarInfo taskbar, Size widgetSize)
    {
        // Secondary taskbars draw their clock in XAML with no Win32 window to fall back on,
        // so until UI Automation has read the layout, any guess could cover the clock.
        if (!taskbar.IsPrimary && !taskbar.HasXamlLayout)
        {
            return null;
        }

        var gap = (int)Math.Round(GapBeforeNotificationArea * taskbar.Scale);
        var right = taskbar.NotificationArea.Left - gap;
        var left = right - widgetSize.Width;
        var top = taskbar.Bounds.Top + (taskbar.Bounds.Height - widgetSize.Height) / 2;

        var appButtonsGap = (int)Math.Round(MinimumGapAfterAppButtons * taskbar.Scale);
        var minimumLeft = taskbar.AppButtons.IsEmpty
            ? taskbar.Bounds.Left
            : taskbar.AppButtons.Right + appButtonsGap;
        if (left < minimumLeft)
        {
            return null;
        }

        return new Rectangle(left, top, widgetSize.Width, widgetSize.Height);
    }
}
