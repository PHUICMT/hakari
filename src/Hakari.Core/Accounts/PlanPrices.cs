namespace Hakari.Core.Accounts;

/// <summary>
/// What each plan costs per month at its list price in US dollars, for comparing with the
/// same usage priced per token. A plan's price is not in the logs, so these are the public
/// prices from <see cref="PlanCatalog"/>; Enterprise is negotiated and has none.
/// </summary>
public static class PlanPrices
{
    public static decimal? MonthlyDollars(SubscriptionPlan plan) =>
        PlanCatalog.Current.Find(plan)?.MonthlyDollars;
}
