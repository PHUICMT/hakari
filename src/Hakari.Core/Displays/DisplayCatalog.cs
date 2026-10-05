using System.Runtime.InteropServices;
using Hakari.Core.Displays.Interop;

namespace Hakari.Core.Displays;

/// <summary>
/// The connected displays, main one first, then left to right. Names and stable ids come
/// from the display configuration; a display it does not describe still gets "Display n".
/// </summary>
public static class DisplayCatalog
{
    private const string NumberedName = "Display {0}";

    public static IReadOnlyList<DisplayInfo> List()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var targets = TargetsByDeviceName();
        var monitors = Monitors()
            .OrderByDescending(monitor => monitor.IsPrimary)
            .ThenBy(monitor => monitor.Left)
            .ThenBy(monitor => monitor.Top)
            .ToList();

        var displays = new List<DisplayInfo>();
        for (var index = 0; index < monitors.Count; index++)
        {
            var monitor = monitors[index];
            var target = targets.GetValueOrDefault(monitor.DeviceName);
            var name = string.IsNullOrWhiteSpace(target.FriendlyName)
                ? string.Format(null, NumberedName, index + 1)
                : target.FriendlyName;
            var id = string.IsNullOrWhiteSpace(target.DevicePath)
                ? monitor.DeviceName
                : target.DevicePath;
            displays.Add(new DisplayInfo(id, monitor.DeviceName, name, monitor.IsPrimary));
        }

        return Disambiguate(displays);
    }

    /// <summary>Two monitors of the same model get "(2)", "(3)" after the first.</summary>
    private static List<DisplayInfo> Disambiguate(List<DisplayInfo> displays)
    {
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        return
        [
            .. displays.Select(display =>
            {
                var count = seen[display.Name] = seen.GetValueOrDefault(display.Name) + 1;
                return count == 1 ? display : display with { Name = $"{display.Name} ({count})" };
            }),
        ];
    }

    private static List<(string DeviceName, bool IsPrimary, int Left, int Top)> Monitors()
    {
        var monitors = new List<(string, bool, int, int)>();
        DisplayNative.MonitorCallback callback = (monitor, _, _, _) =>
        {
            var information = new MonitorInformation
            {
                Size = Marshal.SizeOf<MonitorInformation>(),
            };
            if (DisplayNative.GetMonitorInfo(monitor, ref information))
            {
                var isPrimary = (information.Flags & MonitorInformation.PrimaryFlag) != 0;
                monitors.Add((
                    information.DeviceName,
                    isPrimary,
                    information.MonitorLeft,
                    information.MonitorTop));
            }

            return true;
        };
        DisplayNative.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);
        return monitors;
    }

    private static Dictionary<string, (string FriendlyName, string DevicePath)>
        TargetsByDeviceName()
    {
        var targets = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in ActivePaths())
        {
            var source = SourceName(path);
            if (source is not null && !targets.ContainsKey(source) && TargetName(path) is { } name)
            {
                targets[source] = (name.FriendlyName, name.DevicePath);
            }
        }

        return targets;
    }

    private static DisplayPath[] ActivePaths()
    {
        var flags = DisplayNative.OnlyActivePaths;
        if (DisplayNative.GetDisplayConfigBufferSizes(flags, out var pathCount, out var modeCount)
            != DisplayNative.Success)
        {
            return [];
        }

        var paths = new DisplayPath[pathCount];
        var modes = new DisplayMode[modeCount];
        var result = DisplayNative.QueryDisplayConfig(
            flags,
            ref pathCount,
            paths,
            ref modeCount,
            modes,
            IntPtr.Zero);
        return result == DisplayNative.Success ? paths[..(int)pathCount] : [];
    }

    private static string? SourceName(DisplayPath path)
    {
        var request = new SourceDeviceName
        {
            Header = new DeviceInfoHeader
            {
                Type = DeviceInfoHeader.SourceNameType,
                Size = (uint)Marshal.SizeOf<SourceDeviceName>(),
                AdapterId = path.SourceAdapterId,
                Id = path.SourceId,
            },
        };
        return DisplayNative.DisplayConfigGetDeviceInfo(ref request) == DisplayNative.Success
            ? request.GdiDeviceName
            : null;
    }

    private static (string FriendlyName, string DevicePath)? TargetName(DisplayPath path)
    {
        var request = new TargetDeviceName
        {
            Header = new DeviceInfoHeader
            {
                Type = DeviceInfoHeader.TargetNameType,
                Size = (uint)Marshal.SizeOf<TargetDeviceName>(),
                AdapterId = path.TargetAdapterId,
                Id = path.TargetId,
            },
        };
        return DisplayNative.DisplayConfigGetDeviceInfo(ref request) == DisplayNative.Success
            ? (request.FriendlyName, request.DevicePath)
            : null;
    }
}
