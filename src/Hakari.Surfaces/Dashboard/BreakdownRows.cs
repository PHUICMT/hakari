using Hakari.Core.Querying;

namespace Hakari.Surfaces.Dashboard;

/// <summary>What a breakdown page shows: its rows, their total and the currency.</summary>
internal sealed record BreakdownRows(
    IReadOnlyList<UsageSummary> Rows,
    decimal Total,
    string Currency);
