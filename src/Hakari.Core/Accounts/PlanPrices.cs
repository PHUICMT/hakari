namespace Hakari.Core.Accounts;

/// <summary>
/// What each plan costs per month at its list price in US dollars, for comparing with the
/// same usage priced per token. A plan's price is not in the logs, so these are the public
/// prices; Enterprise is negotiated and has none.
/// </summary>
public static class PlanPrices
{
    private const decimal Pro = 20m;
    private const decimal MaxFiveTimes = 100m;
    private const decimal MaxTwentyTimes = 200m;
    private const decimal TeamSeat = 30m;

    public static decimal? MonthlyDollars(SubscriptionPlan plan) => plan switch
    {
        SubscriptionPlan.Pro => Pro,
        SubscriptionPlan.MaxFiveTimes => MaxFiveTimes,
        SubscriptionPlan.MaxTwentyTimes => MaxTwentyTimes,
        SubscriptionPlan.Team => TeamSeat,
        _ => null,
    };
}
