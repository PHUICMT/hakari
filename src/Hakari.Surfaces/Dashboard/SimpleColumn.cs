using Microsoft.UI.Xaml;

namespace Hakari.Surfaces.Dashboard;

/// <param name="Header">The column's title.</param>
/// <param name="Width">Star for the column that takes the rest, a number for the others.</param>
/// <param name="IsNumber">Right-aligned, like figures.</param>
/// <param name="ShownFrom">The table width below which the column is left out.</param>
internal sealed record SimpleColumn(
    string Header,
    GridLength Width,
    bool IsNumber = false,
    double ShownFrom = 0);
