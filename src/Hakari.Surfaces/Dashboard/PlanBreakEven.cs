namespace Hakari.Surfaces.Dashboard;

/// <summary>When this month's usage at per-token prices reaches the plan's price.</summary>
/// <param name="ReachedOn">The day it did, or null if it has not yet.</param>
/// <param name="ProjectedOn">
/// The day it will at this month's pace, or null if it will not before the month ends.
/// </param>
internal sealed record PlanBreakEven(DateOnly? ReachedOn, DateOnly? ProjectedOn);
