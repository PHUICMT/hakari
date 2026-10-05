using Hakari.Core.Accounts;
using Hakari.Core.Limits;
using Hakari.Core.Presentation.Widget;

namespace Hakari.Core.Tests.Presentation;

public sealed class WidgetPanelsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Side_by_side_gives_each_account_its_own_block_and_money()
    {
        var panels = Compose(MultiAccountMode.SideBySide, Now);

        Assert.Equal(2, panels.Count);
        Assert.Equal("work · $5.00 today", panels[0].Top.Text);
        Assert.Equal("home · $1.00 today", panels[1].Top.Text);
    }

    [Fact]
    public void Each_account_can_have_its_own_layout()
    {
        var weeklyForHome = new Dictionary<string, WidgetLayout>
        {
            ["home"] = new() { Top = WidgetItem.WeeklyLimit },
        };

        var panels = WidgetPanels.Compose(
            MultiAccountMode.SideBySide,
            new WidgetLayout(),
            id => weeklyForHome.GetValueOrDefault(id) ?? new WidgetLayout(),
            Facts(),
            Now);

        Assert.StartsWith("home · Week", panels[1].Top.Text);
    }

    [Fact]
    public void Take_turns_shows_one_account_at_a_time_and_moves_on()
    {
        var first = Compose(MultiAccountMode.TakeTurns, Now);
        var next = Compose(MultiAccountMode.TakeTurns, Now + WidgetPanels.DefaultTurnLength);

        Assert.Single(first);
        Assert.NotEqual(first[0].Top.Text, next[0].Top.Text);
    }

    [Fact]
    public void Together_keeps_one_block()
    {
        Assert.Single(Compose(MultiAccountMode.Together, Now));
    }

    private static IReadOnlyList<ComposedWidget> Compose(MultiAccountMode mode, DateTimeOffset at)
        => WidgetPanels.Compose(mode, new WidgetLayout(), _ => new WidgetLayout(), Facts(), at);

    private static WidgetFacts Facts() =>
        new(6m, 60m, 1m, "USD", [Account("work", 5m), Account("home", 1m)]);

    private static WidgetAccount Account(string name, decimal costToday)
    {
        var info = new AccountInfo(name, $"{name}@example.com", null, null, SubscriptionPlan.Pro);
        var snapshot = new LimitSnapshot(
            [
                new UsageLimit("session", "session", 40, "normal",
                    Now.AddHours(2), null, IsActive: true),
                new UsageLimit("weekly_all", "weekly", 60, "normal",
                    Now.AddDays(2), null, IsActive: true),
            ],
            null,
            Now,
            LimitFreshness.Live);
        return new WidgetAccount(name, info, snapshot, new AccountCosts(costToday, 10m, 0m));
    }
}
