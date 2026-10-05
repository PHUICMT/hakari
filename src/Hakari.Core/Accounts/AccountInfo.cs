namespace Hakari.Core.Accounts;

public sealed record AccountInfo(
    string AccountId,
    string? Email,
    string? DisplayName,
    string? OrganizationName,
    SubscriptionPlan Plan);
