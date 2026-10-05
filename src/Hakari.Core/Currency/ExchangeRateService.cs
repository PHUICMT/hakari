namespace Hakari.Core.Currency;

public sealed class ExchangeRateService(
    ExchangeRateRepository repository,
    IReadOnlyList<IExchangeRateProvider> providers,
    TimeProvider timeProvider)
{
    /// <summary>Reference rates skip weekends and holidays, so a few days' gap is normal.</summary>
    private const int AcceptableGapDays = 4;

    private const int LatestModeLookbackDays = 7;

    public static IReadOnlyList<IExchangeRateProvider> DefaultProviders { get; } =
    [
        new FrankfurterProvider(),
        new OpenExchangeRateProvider(),
    ];

    /// <summary>
    /// Returns a converter backed by stored rates, fetching only what is missing. Network
    /// problems are tolerated: stored rates are used, and null means no rate is known yet.
    /// </summary>
    public async Task<CurrencyConverter?> CreateConverterAsync(
        string currency,
        RateMode mode,
        DateOnly firstUsageDay,
        CancellationToken cancellationToken = default)
    {
        var normalizedCurrency = CurrencyCodes.Normalize(currency);
        if (normalizedCurrency == CurrencyCodes.Dollar)
        {
            return CurrencyConverter.Dollars;
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var neededFrom = mode == RateMode.UsageDay
            ? firstUsageDay
            : today.AddDays(-LatestModeLookbackDays);

        var request = new RateRequest(normalizedCurrency, neededFrom, today, mode);
        await FetchMissingAsync(request, cancellationToken);

        var rates = repository.Load(normalizedCurrency);
        return rates.Count == 0 ? null : new CurrencyConverter(normalizedCurrency, rates, mode);
    }

    private async Task FetchMissingAsync(RateRequest request, CancellationToken cancellationToken)
    {
        var stored = repository.Load(request.Currency);
        var coversStart = CoversStart(stored, request);
        var isFresh = stored.Count > 0
            && stored[^1].Day >= request.Today.AddDays(-AcceptableGapDays);
        if (coversStart && isFresh)
        {
            return;
        }

        var fetchFrom = coversStart ? stored[^1].Day : request.NeededFrom;
        foreach (var provider in providers)
        {
            var fetched = await TryFetchAsync(
                provider,
                request with { NeededFrom = fetchFrom },
                cancellationToken);

            if (fetched.Count > 0)
            {
                repository.Save(fetched);
                return;
            }
        }
    }

    private static bool CoversStart(IReadOnlyList<ExchangeRate> stored, RateRequest request)
    {
        if (stored.Count == 0)
        {
            return false;
        }

        var needsHistory = request.Mode == RateMode.UsageDay;
        return !needsHistory || stored[0].Day <= request.NeededFrom.AddDays(AcceptableGapDays);
    }

    private static async Task<IReadOnlyList<ExchangeRate>> TryFetchAsync(
        IExchangeRateProvider provider,
        RateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await provider.FetchAsync(
                request.Currency,
                request.NeededFrom,
                request.Today,
                cancellationToken);
        }
        catch (Exception exception) when (ExchangeRateHttp.IsNetworkProblem(exception))
        {
            return [];
        }
    }

    private sealed record RateRequest(
        string Currency,
        DateOnly NeededFrom,
        DateOnly Today,
        RateMode Mode);
}
