using Hakari.Core.Presentation.Widget;

namespace Hakari.Core.Tests.Presentation;

public sealed class WidgetFormatTests
{
    private static FormatValue? Values(string name) => name switch
    {
        "cost.today" => FormatValue.Money(142.18m, "USD"),
        "cost.month" => FormatValue.Money(2333.9m, "THB"),
        "limit.5h" => FormatValue.Percent(93.96),
        "reset" => FormatValue.Duration(new TimeSpan(3, 7, 0)),
        "tokens" => FormatValue.Tokens(4_200_000),
        "replies" => FormatValue.Count(28905),
        "account" => FormatValue.Words("Work"),
        _ => null,
    };

    [Theory]
    [InlineData("{cost.today:$0.00}", "$142.18")]
    [InlineData("{cost.today:0}", "142")]
    [InlineData("{cost.month:฿0.0}", "฿2,333.9")]
    [InlineData("{cost.today}", "$142.18")]
    [InlineData("{limit.5h:0%}", "93%")]
    [InlineData("{limit.5h:0.0%}", "93.9%")]
    [InlineData("{limit.5h:0.0}", "93.9")]
    [InlineData("{limit.5h}", "93%")]
    [InlineData("{reset:h\\:mm}", "3:07")]
    [InlineData("{reset}", "3h 07m")]
    [InlineData("{tokens}", "4.2M")]
    [InlineData("{replies}", "28,905")]
    [InlineData("{account}", "Work")]
    public void Writes_a_value_the_way_its_pattern_says(string format, string expected) =>
        Assert.Equal(expected, WidgetFormat.Apply(format, Values));

    [Fact]
    public void Keeps_the_text_around_the_values()
    {
        var text = WidgetFormat.Apply("{cost.today:$0.00} · {limit.5h:0%} ↺{reset:h\\:mm}", Values);

        Assert.Equal("$142.18 · 93% ↺3:07", text);
    }

    [Fact]
    public void Shows_a_dash_for_an_unknown_name()
    {
        Assert.Equal("—", WidgetFormat.Apply("{nothing}", Values));
    }

    [Fact]
    public void Writes_doubled_braces_as_single_ones()
    {
        Assert.Equal("{cost}", WidgetFormat.Apply("{{cost}}", Values));
    }

    [Fact]
    public void Leaves_an_unclosed_brace_as_it_is()
    {
        Assert.Equal("a { b", WidgetFormat.Apply("a { b", Values));
    }

    [Fact]
    public void Shows_a_dash_for_a_pattern_that_cannot_be_used()
    {
        Assert.Equal("—", WidgetFormat.Apply("{reset:Q}", Values));
    }
}
