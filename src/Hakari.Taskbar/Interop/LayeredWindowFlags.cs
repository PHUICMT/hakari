namespace Hakari.Taskbar.Interop;

internal static class LayeredWindowFlags
{
    public const uint Alpha = 0x00000002;
    public const byte SourceOver = 0x00;
    public const byte SourceAlpha = 0x01;
    public const byte Opaque = 255;
}
