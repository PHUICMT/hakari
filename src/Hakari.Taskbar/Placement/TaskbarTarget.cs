using System.Runtime.InteropServices;
using Hakari.Taskbar.Interop;

namespace Hakari.Taskbar.Placement;

/// <summary>
/// Names a taskbar in a way that survives Explorer restarts, which replace every handle:
/// the primary one, or the n-th secondary one from left to right.
/// </summary>
public sealed record TaskbarTarget(bool IsPrimary, int SecondaryIndex)
{
    public const string PrimaryClass = "Shell_TrayWnd";
    public const string SecondaryClass = "Shell_SecondaryTrayWnd";
    private const uint NearestMonitor = 2;

    public static TaskbarTarget Primary { get; } = new(IsPrimary: true, SecondaryIndex: -1);

    public static IReadOnlyList<TaskbarTarget> All()
    {
        var secondaryCount = FindSecondaryHandles().Count;
        var targets = new List<TaskbarTarget> { Primary };
        for (var position = 0; position < secondaryCount; position++)
        {
            targets.Add(new TaskbarTarget(IsPrimary: false, SecondaryIndex: position));
        }

        return targets;
    }

    public IntPtr Resolve()
    {
        if (IsPrimary)
        {
            return User32.FindWindow(PrimaryClass, null);
        }

        var secondaries = FindSecondaryHandles();
        return SecondaryIndex < secondaries.Count ? secondaries[SecondaryIndex] : IntPtr.Zero;
    }

    /// <summary>The device name of this taskbar's display, or null when it is gone.</summary>
    public string? DisplayDeviceName()
    {
        var taskbar = Resolve();
        if (taskbar == IntPtr.Zero)
        {
            return null;
        }

        var monitor = User32.MonitorFromWindow(taskbar, NearestMonitor);
        var information = new MonitorInformation
        {
            Size = Marshal.SizeOf<MonitorInformation>(),
        };
        return User32.GetMonitorInfo(monitor, ref information) ? information.DeviceName : null;
    }

    private static List<IntPtr> FindSecondaryHandles()
    {
        var handles = new List<(IntPtr Handle, int Left)>();
        var handle = IntPtr.Zero;
        while ((handle = User32.FindWindowEx(IntPtr.Zero, handle, SecondaryClass, null))
            != IntPtr.Zero)
        {
            User32.GetWindowRect(handle, out var bounds);
            handles.Add((handle, bounds.Left));
        }

        return [.. handles.OrderBy(entry => entry.Left).Select(entry => entry.Handle)];
    }
}
