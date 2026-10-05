namespace Hakari.Taskbar.Interop;

internal static class SetWindowPositionFlags
{
    public const uint NoSize = 0x0001;
    public const uint NoMove = 0x0002;
    public const uint NoZOrder = 0x0004;
    public const uint NoActivate = 0x0010;
    public const uint FrameChanged = 0x0020;
    public const uint ShowWindow = 0x0040;
}
