namespace Hakari.Core.Presentation.Widget;

/// <param name="Turn">While taking turns: whose turn it is out of how many.</param>
/// <param name="Spark">Points of a small line left of the text, such as the last 12 hours.</param>
/// <param name="TopBar">A thin bar beside the top line.</param>
/// <param name="BottomBar">A thin bar beside the bottom line.</param>
public sealed record ComposedWidget(
    ComposedLine Top,
    ComposedLine Bottom,
    ComposedRing? Ring,
    (int Index, int Count)? Turn = null,
    IReadOnlyList<double>? Spark = null,
    ComposedBar? TopBar = null,
    ComposedBar? BottomBar = null);
