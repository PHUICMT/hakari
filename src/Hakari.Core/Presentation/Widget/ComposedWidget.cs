namespace Hakari.Core.Presentation.Widget;

/// <param name="Turn">While taking turns: whose turn it is out of how many.</param>
public sealed record ComposedWidget(
    ComposedLine Top,
    ComposedLine Bottom,
    ComposedRing? Ring,
    (int Index, int Count)? Turn = null);