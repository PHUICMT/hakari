namespace Hakari.Core.Currency;

public sealed class CurrencyConverter
{
    private readonly ExchangeRate[] ratesByDay;

    public CurrencyConverter(string currency, IEnumerable<ExchangeRate> rates, RateMode mode)
    {
        Currency = currency;
        Mode = mode;
        ratesByDay = [.. rates.OrderBy(rate => rate.Day)];
        if (ratesByDay.Length == 0)
        {
            throw new ArgumentException("At least one exchange rate is required.", nameof(rates));
        }
    }

    public static CurrencyConverter Dollars { get; } = new(
        CurrencyCodes.Dollar,
        [new ExchangeRate(DateOnly.MinValue, CurrencyCodes.Dollar, 1m, "identity")],
        RateMode.Latest);

    public string Currency { get; }

    public RateMode Mode { get; }

    public ExchangeRate LatestRate => ratesByDay[^1];

    public decimal Convert(decimal dollars, DateOnly usageDay) =>
        dollars * RateFor(usageDay).UnitsPerDollar;

    /// <summary>
    /// Uses the latest rate published on or before the day, so weekends and holidays take the
    /// previous business day. Days before the first known rate use the first known rate.
    /// </summary>
    public ExchangeRate RateFor(DateOnly usageDay)
    {
        if (Mode == RateMode.Latest)
        {
            return LatestRate;
        }

        var low = 0;
        var high = ratesByDay.Length - 1;
        var found = -1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (ratesByDay[middle].Day <= usageDay)
            {
                found = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return found >= 0 ? ratesByDay[found] : ratesByDay[0];
    }
}
