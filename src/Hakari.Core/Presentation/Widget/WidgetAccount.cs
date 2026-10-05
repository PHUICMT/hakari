using Hakari.Core.Accounts;
using Hakari.Core.Limits;

namespace Hakari.Core.Presentation.Widget;

/// <param name="Account">Null when the index has no details for this account yet.</param>
public sealed record WidgetAccount(string AccountId, AccountInfo? Account, LimitSnapshot Snapshot);
