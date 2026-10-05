using System.Drawing;

namespace Hakari.Taskbar.Placement;

public static class WidgetPlacement
{
    private const int GapBeforeNotificationArea = 4;
    private const int MinimumGapAfterAppButtons = 8;

    /// <summary>
    /// Places the widget immediately left of the notification area, vertically centered.
    /// Returns null when it would overlap the app buttons, so the caller can go compact.
    /// </summary>
    public static Rectangle? LeftOfNotificationArea(TaskbarInfo taskbar, Size widgetSize)
    {
        var gap = (int)Math.Round(GapBeforeNotificationArea * taskbar.Scale);
        var right = taskbar.NotificationArea.Left - gap;
        var left = right - widgetSize.Width;
        var top = taskbar.Bounds.Top + (taskbar.Bounds.Height - widgetSize.Height) / 2;

        var appButtonsGap = (int)Math.Round(MinimumGapAfterAppButtons * taskbar.Scale);
        var minimumLeft = taskbar.AppButtons.Right + appButtonsGap;
        if (left < minimumLeft)
        {
            return null;
        }

        return new Rectangle(left, top, widgetSize.Width, widgetSize.Height);
    }
}
