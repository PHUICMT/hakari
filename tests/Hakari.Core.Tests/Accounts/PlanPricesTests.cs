using Hakari.Core.Accounts;

namespace Hakari.Core.Tests.Accounts;

public sealed class PlanPricesTests
{
    [Theory]
    [InlineData(SubscriptionPlan.Pro, 20)]
    [InlineData(SubscriptionPlan.MaxFiveTimes, 100)]
    [InlineData(SubscriptionPlan.MaxTwentyTimes, 200)]
    public void Knows_the_list_price_of_a_plan(SubscriptionPlan plan, int dollars) =>
        Assert.Equal(dollars, PlanPrices.MonthlyDollars(plan));

    [Theory]
    [InlineData(SubscriptionPlan.Enterprise)]
    [InlineData(SubscriptionPlan.Unknown)]
    public void Has_no_price_for_a_negotiated_or_unknown_plan(SubscriptionPlan plan) =>
        Assert.Null(PlanPrices.MonthlyDollars(plan));
}
