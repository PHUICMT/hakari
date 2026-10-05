using Hakari.Core.Presentation;

namespace Hakari.Core.Tests.Presentation;

public class PresentationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("windows:alex:.claude", "Windows")]
    [InlineData("windows:alex:.claude-work", "Windows (.claude-work)")]
    [InlineData("wsl:Ubuntu-24.04:alex:.claude", "WSL Ubuntu-24.04")]
    [InlineData(@"directory:d:\accounts\work", "work")]
    public void Names_sources_for_people(string sourceId, string expected) =>
        Assert.Equal(expected, SourceNames.Display(sourceId));

    [Fact]
    public void Counts_down_a_reset_under_a_day() =>
        Assert.Equal("1:48", ResetText.Short(Now.AddMinutes(108), Now));

    [Theory]
    [InlineData(999.5, "$999.50")]
    [InlineData(1234.56, "$1,235")]
    public void Drops_cents_from_large_amounts(double amount, string expected) =>
        Assert.Equal(expected, MoneyText.Format((decimal)amount, "USD"));

    [Fact]
    public void Uses_the_baht_symbol() =>
        Assert.Equal("฿36.15", MoneyText.Format(36.15m, "THB"));
}
