using Hakari.Core.Pricing;
using Hakari.Core.Querying;
using Hakari.Core.Usage;

namespace Hakari.Core.Tests.Pricing;

public sealed class CostSharesTests
{
    [Fact]
    public void Splits_cost_by_token_kind_at_each_models_price()
    {
        var pricing = PricingTable.Load("""
            { "models": { "test-model": [ { "input": 1, "output": 10, "cacheWriteFiveMinutes": 0,
              "cacheWriteOneHour": 0, "cacheRead": 0 } ] } }
            """);
        var summary = UsageSummary.Empty with
        {
            Key = "test-model",
            Tokens = new TokenCounts(Input: 1_000_000, Output: 100_000, 0, 0, 0),
        };

        var shares = CostShares.Of([summary], pricing, new DateOnly(2026, 10, 5));

        Assert.Equal(0.5, shares.Single(share => share.Kind == TokenKind.Input).Share, 6);
        Assert.Equal(0.5, shares.Single(share => share.Kind == TokenKind.Output).Share, 6);
    }
}
