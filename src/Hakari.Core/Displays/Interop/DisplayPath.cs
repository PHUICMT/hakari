using System.Runtime.InteropServices;

namespace Hakari.Core.Displays.Interop;

/// <summary>DISPLAYCONFIG_PATH_INFO: one source (desktop) shown on one target (monitor).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DisplayPath
{
    public Luid SourceAdapterId;
    public uint SourceId;
    public uint SourceModeIndex;
    public uint SourceStatusFlags;

    public Luid TargetAdapterId;
    public uint TargetId;
    public uint TargetModeIndex;
    public uint OutputTechnology;
    public uint Rotation;
    public uint Scaling;
    public uint RefreshRateNumerator;
    public uint RefreshRateDenominator;
    public uint ScanLineOrdering;
    public int TargetAvailable;
    public uint TargetStatusFlags;

    public uint Flags;
}
