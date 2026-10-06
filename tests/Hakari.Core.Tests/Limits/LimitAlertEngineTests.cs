using Hakari.Core.Limits;

namespace Hakari.Core.Tests.Limits;

public sealed class LimitAlertEngineTests
{
    private static readonly UsageLimit Session =
        new("session", "session", 0, "normal", null, null, IsActive: true);

    [Fact]
    public void Says_nothing_for_the_first_reading_even_above_a_threshold()
    {
        var engine = new LimitAlertEngine(80, 95);

        Assert.Empty(engine.Observe("a", Session, 97));
    }

    [Fact]
    public void Warns_once_as_the_limit_rises_through_the_warning_threshold()
    {
        var engine = new LimitAlertEngine(80, 95);
        engine.Observe("a", Session, 60);

        var first = engine.Observe("a", Session, 82);
        var second = engine.Observe("a", Session, 84);

        Assert.Equal(LimitAlertKind.Warning, Assert.Single(first).Kind);
        Assert.Empty(second);
    }

    [Fact]
    public void A_jump_past_both_thresholds_only_says_critical()
    {
        var engine = new LimitAlertEngine(80, 95);
        engine.Observe("a", Session, 60);

        var alerts = engine.Observe("a", Session, 96);

        Assert.Equal(LimitAlertKind.Critical, Assert.Single(alerts).Kind);
    }

    [Fact]
    public void A_reading_hovering_around_the_line_stays_quiet()
    {
        var engine = new LimitAlertEngine(80, 95);
        engine.Observe("a", Session, 70);
        engine.Observe("a", Session, 81);

        var alerts = new[] { 79, 81, 78, 82 }
            .SelectMany(percent => engine.Observe("a", Session, percent));

        Assert.Empty(alerts);
    }

    [Fact]
    public void Fires_again_after_falling_well_below_the_threshold()
    {
        var engine = new LimitAlertEngine(80, 95);
        engine.Observe("a", Session, 70);
        engine.Observe("a", Session, 85);
        engine.Observe("a", Session, 60);

        var alerts = engine.Observe("a", Session, 83);

        Assert.Equal(LimitAlertKind.Warning, Assert.Single(alerts).Kind);
    }

    [Fact]
    public void Tells_of_a_reset_only_after_the_limit_had_been_well_used()
    {
        var engine = new LimitAlertEngine(80, 95);
        engine.Observe("a", Session, 40);
        engine.Observe("a", Session, 90);

        var reset = engine.Observe("a", Session, 0);

        Assert.Contains(reset, alert => alert.Kind == LimitAlertKind.Reset);
    }

    [Fact]
    public void A_small_limit_resetting_is_not_news()
    {
        var engine = new LimitAlertEngine(80, 95);
        engine.Observe("a", Session, 30);

        Assert.Empty(engine.Observe("a", Session, 0));
    }

    [Fact]
    public void Keeps_accounts_and_limits_apart()
    {
        var engine = new LimitAlertEngine(80, 95);
        engine.Observe("a", Session, 60);
        engine.Observe("b", Session, 60);

        var alerts = engine.Observe("a", Session, 90);

        Assert.Equal("a", Assert.Single(alerts).AccountId);
        Assert.Empty(engine.Observe("b", Session, 60));
    }
}
