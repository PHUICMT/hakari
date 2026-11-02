using System.Drawing;
using Hakari.Taskbar.Automation;
using Interop.UIAutomationClient;

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
    private const string StartButtonId = "StartButton";

    public static TaskbarLayout? Read(IntPtr taskbarHandle)
    {
        try
        {
            var automation = AutomationClient.Instance;
            var root = automation.ElementFromHandle(taskbarHandle);
            var taskbarBounds = ToRectangle(root.CurrentBoundingRectangle);
            var elements = root.FindAllBuildCache(
                TreeScope.TreeScope_Descendants,
                automation.CreateTrueCondition(),
                CreateCacheRequest(automation));

            var layout = Summarize([.. ReadElements(elements)], taskbarBounds);
            return layout is not null && IsPlausible(layout, taskbarBounds) ? layout : null;
        }
        catch (Exception exception) when (AutomationClient.IsAutomationProblem(exception))
        {
            return null;
        }
    }

    /// <summary>
    /// Caching the properties fetches them in one cross-process call instead of several calls
    /// per element.
    /// </summary>
    private static IUIAutomationCacheRequest CreateCacheRequest(IUIAutomation automation)
    {
        var cacheRequest = automation.CreateCacheRequest();
        cacheRequest.AddProperty(AutomationClient.AutomationIdPropertyId);
        cacheRequest.AddProperty(AutomationClient.ClassNamePropertyId);
        cacheRequest.AddProperty(AutomationClient.BoundingRectanglePropertyId);
        return cacheRequest;
    }

    private static IEnumerable<TaskbarElement> ReadElements(IUIAutomationElementArray elements)
    {
        for (var position = 0; position < elements.Length; position++)
        {
            var element = elements.GetElement(position);
            yield return new TaskbarElement(
                element.CachedClassName ?? string.Empty,
                element.CachedAutomationId ?? string.Empty,
                ToRectangle(element.CachedBoundingRectangle));
        }
    }

    public static TaskbarLayout? Summarize(
        IReadOnlyList<TaskbarElement> elements,
        Rectangle taskbarBounds)
    {
        var appButtons = Rectangle.Empty;
        var notificationArea = Rectangle.Empty;

        foreach (var (className, _, bounds) in elements)
        {
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

        return notificationArea.IsEmpty
            ? null
            : new TaskbarLayout(
                appButtons,
                notificationArea,
                FreeBeforeStart(elements, taskbarBounds));
    }

    /// <summary>
    /// What ends left of Start is in the way, such as the Widgets button or another app's meter.
    /// What reaches across Start, such as the frame and old Win32 windows, is only background.
    /// Hakari's own widget does not count, or it would push itself along each time it is read.
    /// </summary>
    private static Rectangle FreeBeforeStart(
        IReadOnlyList<TaskbarElement> elements,
        Rectangle taskbarBounds)
    {
        var start = elements.FirstOrDefault(element => element.AutomationId == StartButtonId);
        if (start is null || start.Bounds.IsEmpty)
        {
            return Rectangle.Empty;
        }

        var left = elements
            .Where(element => element.ClassName != WidgetWindow.ClassName
                && !element.Bounds.IsEmpty
                && element.Bounds.Right <= start.Bounds.Left)
            .Select(element => element.Bounds.Right)
            .Append(taskbarBounds.Left)
            .Max();
        return Rectangle.FromLTRB(left, taskbarBounds.Top, start.Bounds.Left, taskbarBounds.Bottom);
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

    private static Rectangle ToRectangle(tagRECT bounds) =>
        Rectangle.FromLTRB(bounds.left, bounds.top, bounds.right, bounds.bottom);
}
