namespace Hakari.Core.Limits;

/// <summary>Something worth telling the user about one of an account's limits.</summary>
public sealed record LimitAlert(
    string AccountId,
    UsageLimit Limit,
    LimitAlertKind Kind,
    double Percent);
