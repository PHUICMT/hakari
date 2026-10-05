using Hakari.Cli.CommandLine;
using Hakari.Core.Currency;
using Hakari.Core.Indexing;
using Hakari.Core.Pricing;
using Hakari.Core.Querying;

namespace Hakari.Cli.Commands;

internal static class CurrencyContext
{
    private const string LatestRateName = "latest";
    private const string UsageDayRateName = "day";

    public static CurrencyConverter? CreateConverter(
        CliArguments arguments,
        IndexStore store,
        PricingTable pricing)
    {
        var currency = arguments.GetValue(OptionNames.Currency);
        if (string.IsNullOrEmpty(currency))
        {
            return null;
        }

        var mode = ParseRateMode(arguments.GetValue(OptionNames.RateMode));
        var firstUsageDay = new UsageQuery(store, pricing).FirstUsageDay()
            ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var service = new ExchangeRateService(
            new ExchangeRateRepository(store),
            ExchangeRateService.DefaultProviders,
            TimeProvider.System);

        var converter = service
            .CreateConverterAsync(currency, mode, firstUsageDay)
            .GetAwaiter()
            .GetResult();

        if (converter is null)
        {
            Console.Error.WriteLine($"No exchange rate for {currency} yet. Showing US dollars.");
        }

        return converter;
    }

    public static void WriteRateNote(CurrencyConverter? converter)
    {
        if (converter is null || converter.Currency == CurrencyCodes.Dollar)
        {
            return;
        }

        var latest = converter.LatestRate;
        var modeText = converter.Mode == RateMode.UsageDay
            ? "each day at that day's rate"
            : "at the latest rate";
        Console.WriteLine(
            $"Converted {modeText}. Latest: 1 USD = {latest.UnitsPerDollar} {latest.Currency} "
            + $"on {latest.Day:yyyy-MM-dd} ({latest.Source}).");
    }

    private static RateMode ParseRateMode(string? value) => value?.ToLowerInvariant() switch
    {
        null or "" or UsageDayRateName => RateMode.UsageDay,
        LatestRateName => RateMode.Latest,
        _ => throw new ArgumentException(
            $"Unknown rate '{value}'. Use {UsageDayRateName} or {LatestRateName}."),
    };
}
