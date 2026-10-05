using Hakari.Core.Accounts;
using Hakari.Core.Limits;

namespace Hakari.Core.Presentation.Widget;

/// <param name="Account">Null when the index has no details for this account yet.</param>
/// <param name="Costs">This account's own spending, for a block of its own.</param>
/// <param name="EstimatedPercents">Finer than whole percent, by limit kind, when wanted.</param>
public sealed record WidgetAccount(
    string AccountId,
    AccountInfo? Account,
    LimitSnapshot Snapshot,
    AccountCosts? Costs = null,
    IReadOnlyDictionary<string, double>? EstimatedPercents = null)
{
    /// <summary>
    /// The estimate when it belongs to this reading (within one percent above it), else the
    /// whole percent read; a window that has since reset ignores its old estimate.
    /// </summary>
    public double PercentOf(UsageLimit limit) =>
        EstimatedPercents is not null
        && EstimatedPercents.TryGetValue(limit.Kind, out var value)
        && value >= limit.Percent
        && value < limit.Percent + 1
            ? value
            : limit.Percent;
}
