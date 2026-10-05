namespace Hakari.Taskbar;

/// <summary>Counters the spike prints to compare attach modes.</summary>
public sealed class WidgetDiagnostics
{
    public int Renders { get; set; }

    public int Moves { get; set; }

    public int Collisions { get; set; }

    public int ExplorerRestarts { get; set; }
}
