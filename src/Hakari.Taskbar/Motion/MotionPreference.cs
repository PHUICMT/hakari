namespace Hakari.Taskbar.Motion;

public enum MotionPreference
{
    /// <summary>Values tick in, colors cross-fade, hover fills fade.</summary>
    Full,

    /// <summary>Short fades only, nothing moves.</summary>
    Reduced,

    /// <summary>Every change is instant; the least work and power.</summary>
    Off,
}
