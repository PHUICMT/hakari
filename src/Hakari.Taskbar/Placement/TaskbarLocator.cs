using System.Drawing;
using Hakari.Taskbar.Interop;

namespace Hakari.Taskbar.Placement;

public static class TaskbarLocator
{
    private const string NotificationAreaClass = "TrayNotifyWnd";

    /// <summary>
    /// Describes a taskbar with cheap Win32 calls, safe on the widget thread. The XAML layout
    /// comes from <paramref name="layout"/> when the layout monitor has read it; until then the
    /// Win32 notification area is used and app buttons are unknown.
    /// </summary>
    public static TaskbarInfo? Describe(TaskbarTarget target, TaskbarLayoutMonitor? layout)
    {
        var handle = target.Resolve();
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        var bounds = ReadBounds(handle);
        var xamlLayout = layout?.TryGet(handle);
        var notificationArea = xamlLayout?.NotificationArea
            ?? ReadChildBounds(handle, NotificationAreaClass)
            ?? new Rectangle(bounds.Right, bounds.Top, 0, bounds.Height);
        var appButtons = xamlLayout?.AppButtons ?? Rectangle.Empty;

        return new TaskbarInfo(
            Handle: handle,
            IsPrimary: target.IsPrimary,
            Bounds: bounds,
            NotificationArea: notificationArea,
            AppButtons: appButtons,
            Dpi: User32.GetDpiForWindow(handle),
            HasXamlLayout: xamlLayout is not null);
    }

    private static Rectangle? ReadChildBounds(IntPtr parent, string className)
    {
        var child = User32.FindWindowEx(parent, IntPtr.Zero, className, null);
        return child == IntPtr.Zero ? null : ReadBounds(child);
    }

    private static Rectangle ReadBounds(IntPtr handle)
    {
        User32.GetWindowRect(handle, out var rectangle);
        return Rectangle.FromLTRB(
            rectangle.Left,
            rectangle.Top,
            rectangle.Right,
            rectangle.Bottom);
    }
}
