using System.Text.Json;

namespace Hakari.Core.Currency;

/// <summary>
/// Fallback: ExchangeRate-API open access endpoint, keyless, latest daily rate only.
/// Its terms ask for attribution, which the app shows next to converted amounts.
/// </summary>
public sealed class OpenExchangeRateProvider : IExchangeRateProvider
{
    private const string LatestUrl = "https://open.er-api.com/v6/latest/" + CurrencyCodes.Dollar;
    private const string ResultProperty = "result";
    private const string SuccessResult = "success";
    private const string UpdatedProperty = "time_last_update_unix";
    private const string RatesProperty = "rates";

    public string Name => "ExchangeRate-API";

    public async Task<IReadOnlyList<ExchangeRate>> FetchAsync(
        string currency,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var json = await ExchangeRateHttp.Client.GetStringAsync(LatestUrl, cancellationToken);
        return ParseLatest(json, currency, Name);
    }

    public static IReadOnlyList<ExchangeRate> ParseLatest(
        string json,
        string currency,
        string source)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var succeeded = root.TryGetProperty(ResultProperty, out var result)
            && result.GetString() == SuccessResult;
        if (!succeeded
            || !root.TryGetProperty(RatesProperty, out var rates)
            || !rates.TryGetProperty(currency, out var value)
            || !root.TryGetProperty(UpdatedProperty, out var updated))
        {
            return [];
        }

        var updatedAt = DateTimeOffset.FromUnixTimeSeconds(updated.GetInt64());
        var day = DateOnly.FromDateTime(updatedAt.UtcDateTime);
        return [new ExchangeRate(day, currency, value.GetDecimal(), source)];
    }
}
