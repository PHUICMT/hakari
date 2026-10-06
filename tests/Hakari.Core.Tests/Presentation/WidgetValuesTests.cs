using Hakari.Core.Presentation.Widget;

namespace Hakari.Core.Tests.Presentation;

public sealed class WidgetValuesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private static readonly WidgetFacts Facts = new(
        CostToday: 12.5m,
        CostThisMonth: 300m,
        CostLastHour: 4m,
        Currency: "USD",
        Accounts: [],
        RepliesToday: 42,
        Extras: new Dictionary<string, FormatValue>
        {
            [WidgetExtraValues.CostWeek] = FormatValue.Money(80m, "USD"),
        });

    [Fact]
    public void The_new_names_and_the_old_ones_read_the_same()
    {
        var valueOf = WidgetValues.Lookup(Facts, Now);

        Assert.Equal(valueOf("cost.hour"), valueOf("burn.cost"));
        Assert.Equal(valueOf("replies.today"), valueOf("responses.today"));
    }

    [Fact]
    public void Extra_values_come_from_what_was_read_for_the_format()
    {
        var text = WidgetFormat.Apply("{cost.week:$0}", WidgetValues.Lookup(Facts, Now));

        Assert.Equal("$80", text);
    }

    [Fact]
    public void An_account_that_is_not_there_shows_a_dash()
    {
        var text = WidgetFormat.Apply("{cost.today@nobody}", WidgetValues.Lookup(Facts, Now));

        Assert.Equal(WidgetFormat.Missing, text);
    }

    [Fact]
    public void A_format_names_what_it_uses_without_patterns_or_scopes()
    {
        var names = WidgetFormat.NamesIn("{cost.week:$0} · {limit.5h@work:0%} {{x}}");

        Assert.Equal(["cost.week", "limit.5h"], names.Order());
    }
}
