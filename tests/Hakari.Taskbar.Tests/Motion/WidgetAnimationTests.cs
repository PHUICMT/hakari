using System.Diagnostics;
using Hakari.Taskbar.Motion;
using Hakari.Taskbar.Rendering;

namespace Hakari.Taskbar.Tests.Motion;

public class WidgetAnimationTests
{
    private static readonly WidgetContent First = new("$142.18 today", "5h 62%");
    private static readonly WidgetContent Second =
        new("$151.02 today", "5h 84%", WidgetTone.Warning);
    private static readonly MotionTokens Full = MotionTokens.For(MotionPreference.Full);
    private static readonly MotionTokens Off = MotionTokens.For(MotionPreference.Off);

    [Fact]
    public void Shows_both_values_in_the_middle_of_a_change()
    {
        var animation = new WidgetAnimation(First);
        animation.ChangeContent(Second, At(0), Full);

        var frame = animation.FrameAt(At(50), Full);

        Assert.Equal(First, frame.Previous);
        Assert.InRange(frame.ValueProgress, 0.01, 0.99);
        Assert.True(frame.MovesText);
    }

    [Fact]
    public void Settles_after_the_longest_duration()
    {
        var animation = new WidgetAnimation(First);
        animation.ChangeContent(Second, At(0), Full);

        var frame = animation.FrameAt(At(400), Full);

        Assert.False(frame.IsChanging);
        Assert.False(animation.NeedsFrame(At(401), Full));
    }

    [Fact]
    public void Changes_instantly_when_motion_is_off()
    {
        var animation = new WidgetAnimation(First);
        animation.ChangeContent(Second, At(0), Off);

        var frame = animation.FrameAt(At(0), Off);

        Assert.Null(frame.Previous);
        Assert.Equal(Second, frame.Current);
    }

    [Fact]
    public void Fades_the_hover_fill_in()
    {
        var animation = new WidgetAnimation(First);
        animation.ChangeHover(hovered: true, At(0), Full);

        var halfway = animation.FrameAt(At(30), Full).HoverAmount;
        var done = animation.FrameAt(At(200), Full).HoverAmount;

        Assert.InRange(halfway, 0.01, 0.99);
        Assert.Equal(1, done);
    }

    [Fact]
    public void Needs_no_frames_while_idle()
    {
        var animation = new WidgetAnimation(First);

        Assert.False(animation.NeedsFrame(At(1000), Full));
    }

    private static long At(int milliseconds) =>
        BaseTimestamp + milliseconds * Stopwatch.Frequency / 1000;

    private static readonly long BaseTimestamp = Stopwatch.Frequency * 1000;
}
