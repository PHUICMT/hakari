namespace Hakari.Surfaces.Flyout;

/// <summary>One signed-in account and its limits, as a section of the flyout.</summary>
/// <param name="Detail">Plan and how fresh the limits are.</param>
public sealed record AccountLimitGroup(
    string Name,
    string Detail,
    IReadOnlyList<LimitRow> Limits);
