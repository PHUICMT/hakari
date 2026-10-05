using Hakari.Core.Pricing;
using Hakari.Core.Usage;

namespace Hakari.Core.Tests.Pricing;

public class PricingTableTests
{
    private static readonly PricingTable Pricing = PricingTable.LoadBundled();
    private static readonly DateOnly SampleDay = new(2026, 10, 5);

    [Fact]
    public void Prices_one_hour_cache_writes_at_twice_the_input_price()
    {
        var tokens = new TokenCounts(0, 0, 0, CacheWriteOneHour: 1_000_000, 0);

        Assert.Equal(10m, Pricing.Cost(Usage("claude-opus-5", tokens)));
    }

    [Fact]
    public void Prices_five_minute_cache_writes_at_one_and_a_quarter_input_price()
    {
        var tokens = new TokenCounts(0, 0, CacheWriteFiveMinutes: 1_000_000, 0, 0);

        Assert.Equal(6.25m, Pricing.Cost(Usage("claude-opus-5", tokens)));
    }

    [Fact]
    public void Uses_fast_mode_prices_when_speed_is_fast()
    {
        var tokens = new TokenCounts(Input: 1_000_000, Output: 1_000_000, 0, 0, 0);

        var cost = Pricing.Cost(Usage("claude-opus-5-5", tokens, speed: SpeedNames.Fast));

        Assert.Equal(48m, cost);
    }

    [Fact]
    public void Stacks_cache_multipliers_on_top_of_fast_mode()
    {
        var tokens = new TokenCounts(0, 0, 0, 0, CacheRead: 1_000_000);

        var cost = Pricing.Cost(Usage("claude-opus-5-5", tokens, speed: SpeedNames.Fast));

        Assert.Equal(0.40m, cost);
    }

    [Fact]
    public void Applies_the_us_inference_multiplier()
    {
        var tokens = new TokenCounts(Input: 1_000_000, 0, 0, 0, 0);

        var cost = Pricing.Cost(Usage("claude-opus-5", tokens, geography: "us"));

        Assert.Equal(5.5m, cost);
    }

    [Fact]
    public void Ignores_geographies_without_a_multiplier()
    {
        var tokens = new TokenCounts(Input: 1_000_000, 0, 0, 0, 0);

        var cost = Pricing.Cost(Usage("claude-opus-5", tokens, geography: "not_available"));

        Assert.Equal(5m, cost);
    }

    [Fact]
    public void Adds_web_search_charges()
    {
        var cost = Pricing.Cost(Usage("claude-opus-5", default, webSearches: 3));

        Assert.Equal(0.03m, cost);
    }

    [Fact]
    public void Uses_the_price_that_was_in_effect_on_the_day()
    {
        var pricing = PricingTable.Load("""
            {"models": {"model-a": [
              {"input": 3, "output": 15, "cacheWriteFiveMinutes": 0, "cacheWriteOneHour": 0,
               "cacheRead": 0},
              {"input": 2, "output": 10, "cacheWriteFiveMinutes": 0, "cacheWriteOneHour": 0,
               "cacheRead": 0, "effectiveFrom": "2026-09-01"}
            ]}}
            """);
        var tokens = new TokenCounts(Input: 1_000_000, 0, 0, 0, 0);

        var before = pricing.Cost(Usage("model-a", tokens, day: new DateOnly(2026, 8, 31)));
        var after = pricing.Cost(Usage("model-a", tokens, day: new DateOnly(2026, 9, 1)));

        Assert.Equal(3m, before);
        Assert.Equal(2m, after);
    }

    [Fact]
    public void Resolves_aliases_and_dated_model_names()
    {
        Assert.True(Pricing.IsPriced("claude-haiku-4-5-20251001"));
        Assert.True(Pricing.IsPriced("claude-sonnet-4-20250514"));
    }

    [Fact]
    public void Returns_null_cost_for_unknown_models()
    {
        var tokens = new TokenCounts(1, 1, 0, 0, 0);

        Assert.Null(Pricing.Cost(Usage("unknown-model", tokens)));
    }

    [Fact]
    public void Matches_the_official_price_for_every_bundled_model()
    {
        var input = new TokenCounts(Input: 1_000_000, 0, 0, 0, 0);

        Assert.Equal(4m, Pricing.Cost(Usage("claude-opus-5-5", input)));
        Assert.Equal(10m, Pricing.Cost(Usage("claude-fable-5-1", input)));
        Assert.Equal(2m, Pricing.Cost(Usage("claude-sonnet-5-5", input)));
        Assert.Equal(1m, Pricing.Cost(Usage("claude-haiku-4-5", input)));
    }

    private static PricedUsage Usage(
        string model,
        TokenCounts tokens,
        string speed = SpeedNames.Standard,
        string? geography = null,
        long webSearches = 0,
        DateOnly? day = null) =>
        new(model, speed, geography, day ?? SampleDay, tokens, webSearches);
}
