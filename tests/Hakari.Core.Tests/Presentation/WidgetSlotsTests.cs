using Hakari.Core.Accounts;
using Hakari.Core.Limits;
using Hakari.Core.Presentation.Widget;

namespace Hakari.Core.Tests.Presentation;

public sealed class WidgetSlotsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_single_line_joins_its_text_slots_on_one_line()
    {
        var layout = Layout(
            WidgetTemplate.SingleLine,
            new WidgetSlot(WidgetItem.CostToday),
            new WidgetSlot(WidgetItem.BurnRate));

        var widget = WidgetComposer.Compose(layout, Facts(Account(67)), Now);

        Assert.Equal("$12.50 today · $3.00/h", widget.Top.Text);
        Assert.Equal(string.Empty, widget.Bottom.Text);
    }

    [Fact]
    public void Cycling_shows_the_next_text_slot_after_each_interval()
    {
        var layout = Layout(
                WidgetTemplate.SingleLine,
                new WidgetSlot(WidgetItem.CostToday),
                new WidgetSlot(WidgetItem.BurnRate)) with { CycleSeconds = 10 };

        var first = WidgetComposer.Compose(layout, Facts(Account(67)), Now);
        var second = WidgetComposer.Compose(layout, Facts(Account(67)), Now.AddSeconds(10));

        Assert.NotEqual(first.Top.Text, second.Top.Text);
        Assert.Equal(first.Top.Text, WidgetComposer.Compose(
            layout, Facts(Account(67)), Now.AddSeconds(20)).Top.Text);
    }

    [Fact]
    public void A_ring_slot_draws_the_ring_and_a_sparkline_slot_the_line()
    {
        var layout = Layout(
            WidgetTemplate.RingText,
            new WidgetSlot(WidgetItem.SessionLimit, WidgetSlotStyle.Ring),
            new WidgetSlot(WidgetItem.CostToday),
            new WidgetSlot(WidgetItem.BurnRate, WidgetSlotStyle.Sparkline));
        var facts = Facts(Account(40)) with { HourlySpend = [1m, 2m, 3m] };

        var widget = WidgetComposer.Compose(layout, facts, Now);

        Assert.Equal(0.4, widget.Ring!.Fraction, precision: 2);
        Assert.Equal([1.0, 2.0, 3.0], widget.Spark);
        Assert.Equal("$12.50 today", widget.Top.Text);
    }

    [Fact]
    public void More_slots_than_lines_share_the_last_line()
    {
        var layout = Layout(
            WidgetTemplate.TwoLines,
            new WidgetSlot(WidgetItem.CostToday),
            new WidgetSlot(WidgetItem.BurnRate),
            new WidgetSlot(WidgetItem.CostThisMonth));

        var widget = WidgetComposer.Compose(layout, Facts(Account(40)), Now);

        Assert.Equal("$12.50 today", widget.Top.Text);
        Assert.Equal("$3.00/h · month $400.00", widget.Bottom.Text);
    }

    [Fact]
    public void A_layout_with_only_a_ring_has_no_text()
    {
        var layout = Layout(
            WidgetTemplate.RingText,
            new WidgetSlot(WidgetItem.SessionLimit, WidgetSlotStyle.Ring));

        var widget = WidgetComposer.Compose(layout, Facts(Account(40)), Now);

        Assert.NotNull(widget.Ring);
        Assert.Equal(string.Empty, widget.Top.Text);
        Assert.Equal(string.Empty, widget.Bottom.Text);
    }

    [Fact]
    public void Columns_give_every_slot_a_block_with_its_value_over_its_label()
    {
        var layout = Layout(
            WidgetTemplate.Columns,
            new WidgetSlot(WidgetItem.CostToday),
            new WidgetSlot(WidgetItem.SessionLimit));

        var blocks = WidgetPanels.Compose(
            MultiAccountMode.Together,
            layout,
            _ => layout,
            Facts(Account(67)),
            Now);

        Assert.Equal(2, blocks.Count);
        Assert.Equal("$12.50", blocks[0].Top.Text);
        Assert.Equal("Today", blocks[0].Bottom.Text);
        Assert.Equal("67%", blocks[1].Top.Text);
        Assert.Equal("5h", blocks[1].Bottom.Text);
    }

    [Fact]
    public void Bars_put_a_bar_beside_each_limit_line()
    {
        var layout = Layout(
            WidgetTemplate.Bars,
            new WidgetSlot(WidgetItem.SessionLimit),
            new WidgetSlot(WidgetItem.WeeklyLimit));

        var widget = WidgetComposer.Compose(layout, Facts(Account(67)), Now);

        Assert.Equal(0.67, widget.TopBar!.Fraction, precision: 2);
        Assert.Equal(0.91, widget.BottomBar!.Fraction, precision: 2);
    }

    [Fact]
    public void A_custom_format_replaces_the_text_of_the_lines()
    {
        var layout = new WidgetLayout { CustomFormat = "{cost.today:$0.00} · {limit.5h:0%}" };

        var widget = WidgetComposer.Compose(layout, Facts(Account(67)), Now);

        Assert.Equal("$12.50 · 67%", widget.Top.Text);
        Assert.Equal(string.Empty, widget.Bottom.Text);
    }

    [Theory]
    [InlineData(80, 95, LineTone.Warning)]
    [InlineData(90, 98, LineTone.Normal)]
    [InlineData(50, 70, LineTone.Critical)]
    public void Colors_a_limit_by_the_thresholds_in_the_layout(
        int warnAt,
        int criticalAt,
        LineTone expected)
    {
        var layout = new WidgetLayout { WarnAt = warnAt, CriticalAt = criticalAt };

        var widget = WidgetComposer.Compose(layout, Facts(Account(85)), Now);

        Assert.Equal(expected, widget.Bottom.Tone);
    }

    private static WidgetLayout Layout(WidgetTemplate template, params WidgetSlot[] slots) =>
        new() { Template = template, Slots = slots };

    private static WidgetFacts Facts(params WidgetAccount[] accounts) =>
        new(12.5m, 400m, 3m, "USD", accounts);

    private static WidgetAccount Account(int sessionPercent)
    {
        var info = new AccountInfo("work", "work@example.com", null, null, SubscriptionPlan.Pro);
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
        return new WidgetAccount("work", info, snapshot);
    }
}
