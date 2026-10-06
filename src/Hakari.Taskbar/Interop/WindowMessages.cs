namespace Hakari.Taskbar.Interop;

internal static class WindowMessages
{
    public const uint Destroy = 0x0002;
    public const uint SettingChange = 0x001A;
    public const uint DisplayChange = 0x007E;
    public const uint Timer = 0x0113;
    public const uint MouseMove = 0x0200;
    public const uint LeftButtonUp = 0x0202;
    public const uint RightButtonUp = 0x0205;
    public const uint MouseWheel = 0x020A;
    public const uint MouseLeave = 0x02A3;
    public const uint DpiChanged = 0x02E0;
    public const uint Application = 0x8000;
}
