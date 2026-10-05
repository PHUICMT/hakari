using Hakari.Core.Accounts;
using Hakari.Core.Limits;

namespace Hakari.Feed;

/// <param name="Account">Null when the index has no details for this account yet.</param>
internal sealed record AccountLimits(
    string AccountId,
    AccountInfo? Account,
    LimitSnapshot Snapshot);
