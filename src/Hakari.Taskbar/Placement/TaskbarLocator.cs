using System.Drawing;
using Hakari.Taskbar.Interop;

namespace Hakari.Taskbar.Placement;

public static class TaskbarLocator
{
    private const string PrimaryTaskbarClass = "Shell_TrayWnd";
    private const string SecondaryTaskbarClass = "Shell_SecondaryTrayWnd";
    private const string NotificationAreaClass = "TrayNotifyWnd";
    private const string AppButtonsClass = "ReBarWindow32";
    private const string SecondaryAppButtonsClass = "WorkerW";

    public static TaskbarInfo? FindPrimary()
    {
        var handle = User32.FindWindow(PrimaryTaskbarClass, null);
        return handle == IntPtr.Zero ? null : Describe(handle, isPrimary: true);
    }

    public static IReadOnlyList<TaskbarInfo> FindSecondary()
    {
        var taskbars = new List<TaskbarInfo>();
        var handle = IntPtr.Zero;
        while ((handle = User32.FindWindowEx(IntPtr.Zero, handle, SecondaryTaskbarClass, null))
            != IntPtr.Zero)
        {
            taskbars.Add(Describe(handle, isPrimary: false));
        }

        return taskbars;
    }

    private static TaskbarInfo Describe(IntPtr handle, bool isPrimary)
    {
        var bounds = ReadBounds(handle);
        var notificationArea = ReadChildBounds(handle, NotificationAreaClass);
        var appButtonsClass = isPrimary ? AppButtonsClass : SecondaryAppButtonsClass;
        var appButtons = ReadChildBounds(handle, appButtonsClass);

        return new TaskbarInfo(
            Handle: handle,
            IsPrimary: isPrimary,
            Bounds: bounds,
            NotificationArea: notificationArea ?? new Rectangle(bounds.Right, bounds.Top, 0, 0),
            AppButtons: appButtons ?? new Rectangle(bounds.Left, bounds.Top, 0, 0),
            Dpi: User32.GetDpiForWindow(handle));
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
