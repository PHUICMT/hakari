namespace Hakari.Core.Accounts;

public static class PlanDetector
{
    private static readonly (string Keyword, SubscriptionPlan Plan)[] KeywordsInPriorityOrder =
    [
        ("enterprise", SubscriptionPlan.Enterprise),
        ("team", SubscriptionPlan.Team),
        ("20x", SubscriptionPlan.MaxTwentyTimes),
        ("5x", SubscriptionPlan.MaxFiveTimes),
        ("max", SubscriptionPlan.MaxFiveTimes),
        ("pro", SubscriptionPlan.Pro),
    ];

    public static SubscriptionPlan Detect(params string?[] accountHints)
    {
        var combinedHints = string.Join(' ', accountHints.Where(hint => hint is not null));

        foreach (var (keyword, plan) in KeywordsInPriorityOrder)
        {
            if (combinedHints.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return plan;
            }
        }

        return SubscriptionPlan.Unknown;
    }
}
