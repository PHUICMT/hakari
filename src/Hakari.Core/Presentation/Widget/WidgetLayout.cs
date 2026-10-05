namespace Hakari.Core.Presentation.Widget;

/// <summary>
/// The user's taskbar layout. By default the ring follows the most pressing limit, which
/// is the one that blocks work (a full weekly limit outweighs an empty 5-hour one).
/// </summary>
public sealed record WidgetLayout
{
    public WidgetRingSource Ring { get; init; } = WidgetRingSource.MostPressing;

    public WidgetItem Top { get; init; } = WidgetItem.Automatic;

    public WidgetItem Bottom { get; init; } = WidgetItem.Automatic;
}
