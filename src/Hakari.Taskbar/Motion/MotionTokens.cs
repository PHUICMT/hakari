namespace Hakari.Taskbar.Motion;

/// <summary>Durations from the design handoff, per motion preference.</summary>
public sealed record MotionTokens(
    TimeSpan ValueChange,
    TimeSpan ToneChange,
    TimeSpan Hover,
    bool MovesText)
{
    public static MotionTokens For(MotionPreference preference) => preference switch
    {
        MotionPreference.Full => new(
            ValueChange: TimeSpan.FromMilliseconds(200),
            ToneChange: TimeSpan.FromMilliseconds(320),
            Hover: TimeSpan.FromMilliseconds(120),
            MovesText: true),
        MotionPreference.Reduced => new(
            ValueChange: TimeSpan.FromMilliseconds(150),
            ToneChange: TimeSpan.FromMilliseconds(150),
            Hover: TimeSpan.FromMilliseconds(120),
            MovesText: false),
        _ => new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, MovesText: false),
    };
}
