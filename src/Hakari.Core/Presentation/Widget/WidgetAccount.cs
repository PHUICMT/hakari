using Hakari.Core.Accounts;
using Hakari.Core.Limits;

namespace Hakari.Core.Presentation.Widget;

/// <param name="Account">Null when the index has no details for this account yet.</param>
/// <param name="Costs">This account's own spending, for a block of its own.</param>
public sealed record WidgetAccount(
    string AccountId,
    AccountInfo? Account,
    LimitSnapshot Snapshot,
    AccountCosts? Costs = null);
