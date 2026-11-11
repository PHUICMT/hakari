using Hakari.Core.Pricing;

namespace Hakari.Core.Tests.Pricing;

public class PriceRefreshTests
{
    private const string KnownModel = "claude-fable-5-2";
    private const string NewModel = "claude-fable-5-3";
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan HalfAnHour = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan TwoHours = TimeSpan.FromHours(2);

    [Fact]
    public void A_model_seen_unpriced_for_the_first_time_is_looked_for_at_once()
    {
        var state = LookedAt(Now - HalfAnHour, [KnownModel]);

        Assert.True(PriceRefresh.IsDue(state, Now, [KnownModel, NewModel]));
    }

    [Fact]
    public void A_model_already_looked_for_waits_for_the_hourly_look()
    {
        var state = LookedAt(Now - HalfAnHour, [KnownModel]);

        Assert.False(PriceRefresh.IsDue(state, Now, [KnownModel]));
    }

    [Fact]
    public void A_model_still_unpriced_is_looked_for_again_after_an_hour()
    {
        var state = LookedAt(Now - TwoHours, [KnownModel]);

        Assert.True(PriceRefresh.IsDue(state, Now, [KnownModel]));
    }

    [Fact]
    public void With_everything_priced_the_table_is_read_once_a_day()
    {
        Assert.False(PriceRefresh.IsDue(LookedAt(Now - TwoHours, []), Now, []));
        Assert.True(PriceRefresh.IsDue(LookedAt(Now - TimeSpan.FromDays(2), []), Now, []));
    }

    [Fact]
    public void A_state_saved_before_models_were_kept_looks_for_them_at_once()
    {
        var state = new PriceRefreshState(Now - HalfAnHour, Now - HalfAnHour);

        Assert.True(PriceRefresh.IsDue(state, Now, [KnownModel]));
    }

    private static PriceRefreshState LookedAt(DateTimeOffset when, string[] models) =>
        new(when, when, models);
}
