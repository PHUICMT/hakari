using System.Runtime.InteropServices;

namespace Hakari.Taskbar.Interop;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct NotifyIconData
{
    public const int TipLength = 128;
    public const int InfoLength = 256;
    public const int InfoTitleLength = 64;

    public uint Size;
    public IntPtr WindowHandle;
    public uint Id;
    public uint Flags;
    public uint CallbackMessage;
    public IntPtr Icon;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = TipLength)]
    public string Tip;

    public uint State;
    public uint StateMask;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = InfoLength)]
    public string Info;

    public uint VersionOrTimeout;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = InfoTitleLength)]
    public string InfoTitle;

    public uint InfoFlags;
    public Guid ItemGuid;
    public IntPtr BalloonIcon;
}
