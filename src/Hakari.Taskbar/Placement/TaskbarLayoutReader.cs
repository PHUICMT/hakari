using System.Drawing;
using System.Windows.Automation;

namespace Hakari.Taskbar.Placement;

/// <summary>
/// Reads the taskbar's XAML layout through UI Automation. Calls cross into Explorer and can
/// take tens of milliseconds, so never call this from the widget thread.
/// </summary>
public static class TaskbarLayoutReader
{
    private const string AppButtonClassPrefix = "Taskbar.TaskListButton";
    private const string NotificationAreaClassPrefix = "SystemTray.";
    private const string ShowDesktopButtonClass = "SystemTray.ShowDesktopButton";

    public static TaskbarLayout? Read(IntPtr taskbarHandle)
    {
        try
        {
            var root = AutomationElement.FromHandle(taskbarHandle);
            var taskbarBounds = ToRectangle(root.Current.BoundingRectangle);
            var elements = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
            var layout = Summarize(elements.Cast<AutomationElement>());
            return layout is not null && IsPlausible(layout, taskbarBounds) ? layout : null;
        }
        catch (Exception exception) when (IsAutomationProblem(exception))
        {
            return null;
        }
    }

    public static bool IsAutomationProblem(Exception exception) =>
        exception is ElementNotAvailableException
            or InvalidOperationException
            or System.Runtime.InteropServices.COMException
            or ArgumentException;

    private static TaskbarLayout? Summarize(IEnumerable<AutomationElement> elements)
    {
        var appButtons = Rectangle.Empty;
        var notificationArea = Rectangle.Empty;

        foreach (var element in elements)
        {
            var className = element.Current.ClassName ?? string.Empty;
            var bounds = ToRectangle(element.Current.BoundingRectangle);
            if (bounds.IsEmpty)
            {
                continue;
            }

            if (className.StartsWith(AppButtonClassPrefix, StringComparison.Ordinal))
            {
                appButtons = Union(appButtons, bounds);
            }
            else if (IsNotificationAreaItem(className))
            {
                notificationArea = Union(notificationArea, bounds);
            }
        }

        return notificationArea.IsEmpty ? null : new TaskbarLayout(appButtons, notificationArea);
    }

    /// <summary>
    /// While Explorer starts, elements can report zero-sized or stacked positions. A layout is
    /// trusted only when the notification area sits in the right half of the taskbar and the
    /// app buttons end before it.
    /// </summary>
    private static bool IsPlausible(TaskbarLayout layout, Rectangle taskbarBounds)
    {
        var notificationArea = layout.NotificationArea;
        var rightHalfStart = taskbarBounds.Left + taskbarBounds.Width / 2;
        var insideTaskbar = taskbarBounds.Contains(notificationArea);
        var inRightHalf = notificationArea.Left >= rightHalfStart;
        var buttonsBeforeTray = layout.AppButtons.IsEmpty
            || layout.AppButtons.Right <= notificationArea.Left;
        return insideTaskbar && inRightHalf && buttonsBeforeTray;
    }

    /// <summary>The 12px show-desktop strip sits right of the tray and is not a boundary.</summary>
    private static bool IsNotificationAreaItem(string className) =>
        className.StartsWith(NotificationAreaClassPrefix, StringComparison.Ordinal)
        && className != ShowDesktopButtonClass;

    private static Rectangle Union(Rectangle current, Rectangle next) =>
        current.IsEmpty ? next : Rectangle.Union(current, next);

    private static Rectangle ToRectangle(System.Windows.Rect bounds) =>
        bounds.IsEmpty
            ? Rectangle.Empty
            : new Rectangle((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height);
}
