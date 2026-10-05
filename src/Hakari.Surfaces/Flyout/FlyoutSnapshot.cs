namespace Hakari.Surfaces.Flyout;

/// <summary>Everything the flyout shows, already formatted, read once when it opens.</summary>
public sealed record FlyoutSnapshot(
    string UpdatedText,
    string AccountName,
    IReadOnlyList<LimitRow> Limits,
    IReadOnlyList<StatTile> Stats,
    string BurnRate,
    IReadOnlyList<SourceRow> Sources,
    string? Notice);
