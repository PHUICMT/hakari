using System.Diagnostics;
using Hakari.Taskbar.Rendering;

namespace Hakari.Taskbar.Motion;

/// <summary>
/// Tracks the transitions of one widget and produces the frame for any moment. Times are
/// <see cref="Stopwatch"/> timestamps so the caller decides when frames happen.
/// </summary>
internal sealed class WidgetAnimation(WidgetContent initialContent)
{
    private WidgetContent? previous;
    private long valueStart;
    private double hoverFrom;
    private double hoverTo;
    private long hoverStart;
    private bool finalFramePending;

    public WidgetContent Current { get; private set; } = initialContent;

    public void ChangeContent(WidgetContent next, long now, MotionTokens tokens)
    {
        if (next == Current)
        {
            return;
        }

        previous = tokens.ValueChange > TimeSpan.Zero ? Current : null;
        Current = next;
        valueStart = now;
        finalFramePending = true;
    }

    public void ChangeHover(bool hovered, long now, MotionTokens tokens)
    {
        hoverFrom = HoverAmountAt(now, tokens);
        hoverTo = hovered ? 1 : 0;
        hoverStart = now;
        finalFramePending = true;
    }

    /// <summary>True while a frame is still needed, including the one that settles it.</summary>
    public bool NeedsFrame(long now, MotionTokens tokens) =>
        finalFramePending || IsMoving(now, tokens);

    public WidgetFrame FrameAt(long now, MotionTokens tokens)
    {
        var valueProgress = Progress(valueStart, now, tokens.ValueChange);
        var toneProgress = Progress(valueStart, now, tokens.ToneChange);
        var frame = new WidgetFrame(
            Current: Current,
            Previous: previous,
            ValueProgress: CubicBezierEasing.Decelerate.Evaluate(valueProgress),
            ToneProgress: CubicBezierEasing.Standard.Evaluate(toneProgress),
            HoverAmount: HoverAmountAt(now, tokens),
            MovesText: tokens.MovesText);

        if (!IsMoving(now, tokens))
        {
            previous = null;
            finalFramePending = false;
        }

        return frame;
    }

    private bool IsMoving(long now, MotionTokens tokens)
    {
        var valueMoving = previous is not null
            && Progress(valueStart, now, Longest(tokens.ValueChange, tokens.ToneChange)) < 1;
        var hoverMoving = Progress(hoverStart, now, tokens.Hover) < 1;
        return valueMoving || hoverMoving;
    }

    private double HoverAmountAt(long now, MotionTokens tokens)
    {
        var eased = CubicBezierEasing.Standard.Evaluate(Progress(hoverStart, now, tokens.Hover));
        return hoverFrom + (hoverTo - hoverFrom) * eased;
    }

    private static double Progress(long start, long now, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return 1;
        }

        var elapsed = Stopwatch.GetElapsedTime(start, now);
        return Math.Clamp(elapsed / duration, 0, 1);
    }

    private static TimeSpan Longest(TimeSpan first, TimeSpan second) =>
        first > second ? first : second;
}
