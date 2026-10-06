namespace Hakari.Surfaces.Dashboard;

/// <summary>One model's cost in each bar of a chart over time.</summary>
internal sealed record MixSeries(string Name, string BrushKey, IReadOnlyList<decimal> Costs);
