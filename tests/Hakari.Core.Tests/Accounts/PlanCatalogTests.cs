using Hakari.Core.Accounts;
using Hakari.Core.Presentation;

namespace Hakari.Core.Tests.Accounts;

public sealed class PlanCatalogTests
{
    [Theory]
    [InlineData(SubscriptionPlan.MaxTwentyTimes, "Ultra")]
    [InlineData(SubscriptionPlan.MaxFiveTimes, "Max")]
    [InlineData(SubscriptionPlan.Pro, "Pro")]
    public void Names_plans_as_they_are_called_now(SubscriptionPlan plan, string name) =>
        Assert.Equal(name, PlanNames.Short(plan));

    [Fact]
    public void A_list_from_the_project_renames_and_reprices_without_new_code()
    {
        var catalog = PlanCatalog.Load("""
            {
              "updated": "2030-01-01",
              "plans": [
                { "plan": "MaxTwentyTimes", "name": "Mega", "match": ["mega"],
                  "monthlyDollars": 250 }
              ]
            }
            """);

        Assert.Equal(SubscriptionPlan.MaxTwentyTimes, PlanDetector.DetectIn(catalog, "tier_mega"));
        Assert.Equal("Mega", catalog.Find(SubscriptionPlan.MaxTwentyTimes)?.Name);
        Assert.Equal(250m, catalog.Find(SubscriptionPlan.MaxTwentyTimes)?.MonthlyDollars);
    }

    [Fact]
    public void Tries_plans_in_order_so_the_specific_word_wins() =>
        Assert.Equal(
            SubscriptionPlan.MaxTwentyTimes,
            PlanDetector.Detect("default_claude_max_20x"));
}
