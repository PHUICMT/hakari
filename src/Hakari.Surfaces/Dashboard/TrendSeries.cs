using Hakari.Core.Limits;

namespace Hakari.Surfaces.Dashboard;

/// <summary>One limit's readings over time, for the line chart.</summary>
/// <param name="IsDashed">Told apart by its stroke as well as its color, for high contrast.</param>
internal sealed record TrendSeries(
    string Name,
    string BrushKey,
    IReadOnlyList<LimitReading> Readings,
    bool IsDashed = false);
