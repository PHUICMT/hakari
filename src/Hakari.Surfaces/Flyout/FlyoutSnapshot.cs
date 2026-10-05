namespace Hakari.Surfaces.Flyout;

/// <summary>Everything the flyout shows, already formatted, read once when it opens.</summary>
/// <param name="AccountSummary">The only account's name, or how many there are.</param>
/// <param name="Accounts">Every account with known limits, the most pressing first.</param>
public sealed record FlyoutSnapshot(
    string UpdatedText,
    string AccountSummary,
    IReadOnlyList<AccountLimitGroup> Accounts,
    IReadOnlyList<StatTile> Stats,
    string BurnRate,
    IReadOnlyList<decimal> HourlyBurn,
    IReadOnlyList<SourceRow> Sources,
    string? Notice);
