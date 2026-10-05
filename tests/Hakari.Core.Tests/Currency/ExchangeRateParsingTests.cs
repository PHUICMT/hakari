using Hakari.Core.Currency;

namespace Hakari.Core.Tests.Currency;

public class ExchangeRateParsingTests
{
    [Fact]
    public void Reads_every_day_of_a_frankfurter_range()
    {
        const string json = """
            {"amount":1.0,"base":"USD","start_date":"2026-10-01","end_date":"2026-10-02",
             "rates":{"2026-10-01":{"THB":33.71},"2026-10-02":{"THB":33.595}}}
            """;

        var rates = FrankfurterProvider.ParseRange(json, "THB", "test");

        Assert.Equal(2, rates.Count);
        Assert.Equal(new DateOnly(2026, 10, 2), rates[1].Day);
        Assert.Equal(33.595m, rates[1].UnitsPerDollar);
    }

    [Fact]
    public void Reads_the_latest_open_exchange_rate()
    {
        const string json = """
            {"result":"success","time_last_update_unix":1791158400,"rates":{"THB":33.6}}
            """;

        var rates = OpenExchangeRateProvider.ParseLatest(json, "THB", "test");

        Assert.Single(rates);
        Assert.Equal(33.6m, rates[0].UnitsPerDollar);
    }

    [Fact]
    public void Returns_nothing_when_the_currency_is_missing()
    {
        const string json = """{"rates":{"2026-10-01":{"EUR":0.86}}}""";

        Assert.Empty(FrankfurterProvider.ParseRange(json, "THB", "test"));
    }

    [Theory]
    [InlineData("thb", "THB")]
    [InlineData(" usd ", "USD")]
    public void Normalizes_currency_codes(string input, string expected) =>
        Assert.Equal(expected, CurrencyCodes.Normalize(input));

    [Fact]
    public void Rejects_invalid_currency_codes() =>
        Assert.Throws<ArgumentException>(() => CurrencyCodes.Normalize("baht"));
}
