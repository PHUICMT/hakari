using Hakari.Core.Accounts;

namespace Hakari.Core.Tests.Accounts;

public class PlanDetectorTests
{
    [Theory]
    [InlineData("default_claude_max_20x", SubscriptionPlan.MaxTwentyTimes)]
    [InlineData("default_claude_max_5x", SubscriptionPlan.MaxFiveTimes)]
    [InlineData("claude_team", SubscriptionPlan.Team)]
    [InlineData("claude_enterprise", SubscriptionPlan.Enterprise)]
    [InlineData("claude_pro", SubscriptionPlan.Pro)]
    [InlineData("something_else", SubscriptionPlan.Unknown)]
    public void Detects_plan_from_rate_limit_tier(string tier, SubscriptionPlan expected) =>
        Assert.Equal(expected, PlanDetector.Detect(tier));

    [Fact]
    public void Ignores_missing_hints() =>
        Assert.Equal(SubscriptionPlan.Unknown, PlanDetector.Detect(null, null));
}
