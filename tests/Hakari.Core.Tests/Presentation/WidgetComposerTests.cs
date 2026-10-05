using Hakari.Core.Accounts;
using Hakari.Core.Limits;
using Hakari.Core.Presentation.Widget;

namespace Hakari.Core.Tests.Presentation;

public sealed class WidgetComposerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Automatic_shows_money_and_the_pressing_limit_for_one_account()
    {
        var widget = WidgetComposer.Compose(new WidgetLayout(), Facts(Account("work", 67)), Now);

        Assert.Equal("$12.50 today", widget.Top.Text);
        Assert.StartsWith("5h 67%", widget.Bottom.Text);
        Assert.Equal(0.67, widget.Ring!.Fraction, precision: 2);
    }

    [Fact]
    public void Automatic_gives_each_of_two_accounts_a_labelled_line()
    {
        var facts = Facts(Account("work", 100), Account("home", 20));

        var widget = WidgetComposer.Compose(new WidgetLayout(), facts, Now);

        Assert.StartsWith("work 5h full", widget.Top.Text);
        Assert.Equal(LineTone.Critical, widget.Top.Tone);
        Assert.StartsWith("home 5h 20%", widget.Bottom.Text);
    }

    [Fact]
    public void Follows_the_chosen_items_and_ring()
    {
        var layout = new WidgetLayout
        {
            Top = WidgetItem.WeeklyLimit,
            Bottom = WidgetItem.BurnRate,
            Ring = WidgetRingSource.Off,
        };

        var widget = WidgetComposer.Compose(layout, Facts(Account("work", 67)), Now);

        Assert.StartsWith("Week 91%", widget.Top.Text);
        Assert.Equal("$3.00/h", widget.Bottom.Text);
        Assert.Null(widget.Ring);
    }

    [Fact]
    public void Shows_both_windows_in_two_rings_and_one_line()
    {
        var layout = new WidgetLayout
        {
            Ring = WidgetRingSource.SessionAndWeekly,
            Bottom = WidgetItem.SessionAndWeeklyLimits,
        };

        var widget = WidgetComposer.Compose(layout, Facts(Account("work", 100)), Now);

        Assert.Equal(0.91, widget.Ring!.Fraction, precision: 2);
        Assert.Equal(1.0, widget.Ring.InnerFraction);
        Assert.Equal("5h full · Week 91%", widget.Bottom.Text);
        Assert.Equal(LineTone.Critical, widget.Bottom.Tone);
    }

    [Fact]
    public void Never_leaves_the_widget_empty()
    {
        var layout = new WidgetLayout { Top = WidgetItem.Nothing, Bottom = WidgetItem.Nothing };

        var widget = WidgetComposer.Compose(layout, Facts(), Now);

        Assert.Equal("$12.50 today", widget.Top.Text);
    }

    private static WidgetFacts Facts(params WidgetAccount[] accounts) =>
        new(12.5m, 400m, 3m, "USD", accounts);

    private static WidgetAccount Account(string name, int sessionPercent)
    {
        var info = new AccountInfo(name, $"{name}@example.com", null, null, SubscriptionPlan.Pro);
        var snapshot = new LimitSnapshot(
            [
                new UsageLimit("session", "session", sessionPercent, "normal",
                    Now.AddHours(2), null, IsActive: true),
                new UsageLimit("weekly_all", "weekly", 91, "normal",
                    Now.AddDays(2), null, IsActive: true),
            ],
            null,
            Now,
            LimitFreshness.Live);
        return new WidgetAccount(name, info, snapshot);
    }
}
