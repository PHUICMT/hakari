using System.Runtime.InteropServices;

namespace Hakari.Core.Displays.Interop;

internal static class DisplayNative
{
    public const uint OnlyActivePaths = 2;
    public const int Success = 0;

    private const string User = "user32.dll";

    public delegate bool MonitorCallback(
        IntPtr monitor,
        IntPtr deviceContext,
        IntPtr bounds,
        IntPtr data);

    [DllImport(User)]
    public static extern int GetDisplayConfigBufferSizes(
        uint flags,
        out uint pathCount,
        out uint modeCount);

    [DllImport(User)]
    public static extern int QueryDisplayConfig(
        uint flags,
        ref uint pathCount,
        [Out] DisplayPath[] paths,
        ref uint modeCount,
        [Out] DisplayMode[] modes,
        IntPtr currentTopology);

    [DllImport(User)]
    public static extern int DisplayConfigGetDeviceInfo(ref SourceDeviceName request);

    [DllImport(User)]
    public static extern int DisplayConfigGetDeviceInfo(ref TargetDeviceName request);

    [DllImport(User)]
    public static extern bool EnumDisplayMonitors(
        IntPtr deviceContext,
        IntPtr clip,
        MonitorCallback callback,
        IntPtr data);

    [DllImport(User, CharSet = CharSet.Unicode)]
    public static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInformation information);
}
