namespace Hakari.Surfaces.Dashboard;

/// <summary>One month of an account's usage at per-token prices.</summary>
/// <param name="Label">The month's short name, such as "Oct".</param>
internal sealed record PlanMonth(string Label, decimal Cost);
