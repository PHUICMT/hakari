using Hakari.Core.Currency;

namespace Hakari.Core.Tests.Currency;

public class CurrencyConverterTests
{
    private static readonly ExchangeRate[] SampleRates =
    [
        new(new DateOnly(2026, 10, 1), "THB", 34m, "test"),
        new(new DateOnly(2026, 10, 2), "THB", 33m, "test"),
    ];

    [Fact]
    public void Uses_the_rate_of_the_usage_day()
    {
        var converter = new CurrencyConverter("THB", SampleRates, RateMode.UsageDay);

        Assert.Equal(340m, converter.Convert(10m, new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public void Uses_the_previous_business_day_on_weekends()
    {
        var converter = new CurrencyConverter("THB", SampleRates, RateMode.UsageDay);
        var saturday = new DateOnly(2026, 10, 3);

        Assert.Equal(33m, converter.RateFor(saturday).UnitsPerDollar);
    }

    [Fact]
    public void Uses_the_first_known_rate_for_earlier_days()
    {
        var converter = new CurrencyConverter("THB", SampleRates, RateMode.UsageDay);

        Assert.Equal(34m, converter.RateFor(new DateOnly(2026, 6, 1)).UnitsPerDollar);
    }

    [Fact]
    public void Uses_the_latest_rate_for_every_day_in_latest_mode()
    {
        var converter = new CurrencyConverter("THB", SampleRates, RateMode.Latest);

        Assert.Equal(330m, converter.Convert(10m, new DateOnly(2026, 10, 1)));
    }
}
