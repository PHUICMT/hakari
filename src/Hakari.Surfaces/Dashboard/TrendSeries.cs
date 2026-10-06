using Hakari.Core.Limits;

namespace Hakari.Surfaces.Dashboard;

/// <summary>One limit's readings over time, for the line chart.</summary>
internal sealed record TrendSeries(
    string Name,
    string BrushKey,
    IReadOnlyList<LimitReading> Readings);
