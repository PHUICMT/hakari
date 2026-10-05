using Hakari.Core.Accounts;
using Hakari.Core.Localization;

namespace Hakari.Core.Presentation;

public static class PlanNames
{
    public static string Short(SubscriptionPlan plan) => plan switch
    {
        SubscriptionPlan.Pro => "Pro",
        SubscriptionPlan.MaxFiveTimes => "Max 5x",
        SubscriptionPlan.MaxTwentyTimes => "Max 20x",
        SubscriptionPlan.Team => "Team",
        SubscriptionPlan.Enterprise => "Enterprise",
        _ => Texts.Get("plan.unknown"),
    };
}
