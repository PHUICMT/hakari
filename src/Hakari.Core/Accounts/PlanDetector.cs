namespace Hakari.Core.Accounts;

/// <summary>Which plan an account is on, from its tier and subscription type.</summary>
public static class PlanDetector
{
    public static SubscriptionPlan Detect(params string?[] accountHints) =>
        DetectIn(PlanCatalog.Current, accountHints);

    public static SubscriptionPlan DetectIn(PlanCatalog catalog, params string?[] accountHints) =>
        catalog.Detect(string.Join(' ', accountHints.Where(hint => hint is not null)));
}
