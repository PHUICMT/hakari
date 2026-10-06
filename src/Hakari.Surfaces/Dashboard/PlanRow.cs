namespace Hakari.Surfaces.Dashboard;

/// <summary>One account's month: what its usage would cost per token against its plan.</summary>
/// <param name="Price">The plan's monthly price in the shown currency, or null if unknown.</param>
internal sealed record PlanRow(string Label, decimal Cost, decimal? Price)
{
    /// <summary>How many times over the plan paid for itself, or null with no price.</summary>
    public double? Multiple => Price is > 0 ? (double)(Cost / Price.Value) : null;
}
