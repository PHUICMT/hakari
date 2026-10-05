using System.Globalization;
using System.Text.Json;

namespace Hakari.Core.Currency;

/// <summary>
/// European Central Bank reference rates through the free, keyless Frankfurter API.
/// Business days only; history is available.
/// </summary>
public sealed class FrankfurterProvider : IExchangeRateProvider
{
    private const string BaseUrl = "https://api.frankfurter.dev/v1";
    private const string DayFormat = "yyyy-MM-dd";
    private const string RatesProperty = "rates";

    public string Name => "Frankfurter (ECB)";

    public async Task<IReadOnlyList<ExchangeRate>> FetchAsync(
        string currency,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var url = BuildRangeUrl(currency, from, to);
        var json = await ExchangeRateHttp.Client.GetStringAsync(url, cancellationToken);
        return ParseRange(json, currency, Name);
    }

    public static IReadOnlyList<ExchangeRate> ParseRange(
        string json,
        string currency,
        string source)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty(RatesProperty, out var ratesByDay))
        {
            return [];
        }

        var rates = new List<ExchangeRate>();
        foreach (var dayEntry in ratesByDay.EnumerateObject())
        {
            if (TryReadRate(dayEntry, currency, source) is { } rate)
            {
                rates.Add(rate);
            }
        }

        return rates;
    }

    private static ExchangeRate? TryReadRate(JsonProperty dayEntry, string currency, string source)
    {
        var parsedDay = DateOnly.TryParseExact(
            dayEntry.Name,
            DayFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var day);

        if (!parsedDay || !dayEntry.Value.TryGetProperty(currency, out var value))
        {
            return null;
        }

        return new ExchangeRate(day, currency, value.GetDecimal(), source);
    }

    private static string BuildRangeUrl(string currency, DateOnly from, DateOnly to)
    {
        var fromText = from.ToString(DayFormat, CultureInfo.InvariantCulture);
        var toText = to.ToString(DayFormat, CultureInfo.InvariantCulture);
        return $"{BaseUrl}/{fromText}..{toText}?base={CurrencyCodes.Dollar}&symbols={currency}";
    }
}
