using Hakari.Core.Pricing;
using Hakari.Core.Usage;

namespace Hakari.Core.Tests.Pricing;

public class PricingTableTests
{
    private static readonly PricingTable Pricing = PricingTable.LoadBundled();

    [Fact]
    public void Prices_one_hour_cache_writes_at_twice_the_input_price()
    {
        var tokens = new TokenCounts(0, 0, 0, CacheWriteOneHour: 1_000_000, 0);

        Assert.Equal(10m, Pricing.Cost("claude-opus-5", SpeedNames.Standard, tokens));
    }

    [Fact]
    public void Prices_five_minute_cache_writes_at_one_and_a_quarter_input_price()
    {
        var tokens = new TokenCounts(0, 0, CacheWriteFiveMinutes: 1_000_000, 0, 0);

        Assert.Equal(6.25m, Pricing.Cost("claude-opus-5", SpeedNames.Standard, tokens));
    }

    [Fact]
    public void Uses_fast_mode_prices_when_speed_is_fast()
    {
        var tokens = new TokenCounts(Input: 1_000_000, Output: 1_000_000, 0, 0, 0);

        Assert.Equal(48m, Pricing.Cost("claude-opus-5-5", SpeedNames.Fast, tokens));
    }

    [Fact]
    public void Resolves_aliases_and_dated_model_names()
    {
        Assert.NotNull(Pricing.Find("claude-haiku-4-5-20251001"));
        Assert.NotNull(Pricing.Find("claude-sonnet-5-20990101"));
    }

    [Fact]
    public void Returns_null_cost_for_unknown_models()
    {
        var tokens = new TokenCounts(1, 1, 0, 0, 0);

        Assert.Null(Pricing.Cost("unknown-model", SpeedNames.Standard, tokens));
    }
}
