using Hakari.Taskbar.Rendering;

namespace Hakari.Diagnostics;

/// <summary>Sample values that walk through every tone, to judge motion at real size.</summary>
internal sealed class MotionDemo : IDisposable
{
    private static readonly TimeSpan StepInterval = TimeSpan.FromSeconds(2.5);

    private static readonly WidgetContent[] Steps =
    [
        new("$142.18 today", "5h 62% · ↺ 1:48"),
        new("$151.02 today", "5h 71% · ↺ 1:41"),
        new("$163.47 today", "5h 84% · ↺ 1:30", WidgetTone.Warning),
        new("$172.90 today", "5h 91% · ↺ 1:22", WidgetTone.Warning),
        new("$181.02 today", "Full in ~6 min", WidgetTone.Critical),
    ];

    private readonly Timer timer;
    private int stepIndex;

    private MotionDemo(Action<WidgetContent> publish)
    {
        timer = new Timer(_ => publish(NextStep()), null, TimeSpan.Zero, StepInterval);
    }

    public static MotionDemo Start(Action<WidgetContent> publish) => new(publish);

    public void Dispose() => timer.Dispose();

    private WidgetContent NextStep()
    {
        var step = Steps[stepIndex];
        stepIndex = (stepIndex + 1) % Steps.Length;
        return step;
    }
}
