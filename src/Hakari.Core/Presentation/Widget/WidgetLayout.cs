namespace Hakari.Core.Presentation.Widget;

/// <summary>The user's taskbar layout. The defaults are the automatic layout with a ring.</summary>
public sealed record WidgetLayout
{
    public WidgetRingSource Ring { get; init; } = WidgetRingSource.Session;

    public WidgetItem Top { get; init; } = WidgetItem.Automatic;

    public WidgetItem Bottom { get; init; } = WidgetItem.Automatic;
}
