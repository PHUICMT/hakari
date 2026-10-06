namespace Hakari.Taskbar.Interop;

internal static class NotifyIconConstants
{
    public const uint Add = 0x00000000;
    public const uint Modify = 0x00000001;
    public const uint Delete = 0x00000002;
    public const uint SetVersion = 0x00000004;

    public const uint MessageFlag = 0x00000001;
    public const uint IconFlag = 0x00000002;
    public const uint TipFlag = 0x00000004;
    public const uint ShowTipFlag = 0x00000080;
    public const uint InfoFlag = 0x00000010;

    /// <summary>Balloon icons, and "stay quiet while the user asked not to be disturbed".</summary>
    public const uint InfoNone = 0x00000000;
    public const uint InfoWarning = 0x00000002;
    public const uint InfoRespectQuietTime = 0x00000080;

    /// <summary>Version 4 sends the mouse event in the low word of the callback's lParam.</summary>
    public const uint Version4 = 4;

    public const uint ContextMenuEvent = 0x007B;
    public const uint SelectEvent = 0x0400;
}
