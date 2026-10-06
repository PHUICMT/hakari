namespace Hakari.Taskbar;

/// <summary>Counters the spike prints to compare attach modes.</summary>
public sealed class WidgetDiagnostics
{
    public int Renders { get; set; }

    /// <summary>Frames GDI+ would not draw, skipped instead of ending the process.</summary>
    public int SkippedFrames { get; set; }

    public int Moves { get; set; }

    public int Collisions { get; set; }

    public int ExplorerRestarts { get; set; }

    public int WidgetsRecreated { get; set; }
}
