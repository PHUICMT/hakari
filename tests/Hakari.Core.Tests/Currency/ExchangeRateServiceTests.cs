using Hakari.Core.Currency;
using Hakari.Core.Indexing;
using Hakari.Core.Tests.Support;
using Microsoft.Data.Sqlite;

namespace Hakari.Core.Tests.Currency;

public sealed class ExchangeRateServiceTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 5);

    private readonly TemporaryDirectory directory = new();
    private readonly IndexStore store;

    public ExchangeRateServiceTests()
    {
        store = new IndexStore(directory.Combine("index.db"));
    }

    [Fact]
    public async Task Fetches_once_and_then_uses_stored_rates()
    {
        var provider = new CountingProvider(throws: false);
        var service = CreateService(provider);

        await service.CreateConverterAsync("THB", RateMode.Latest, Today);
        var converter = await service.CreateConverterAsync("THB", RateMode.Latest, Today);

        Assert.Equal(1, provider.Calls);
        Assert.Equal(33.595m, converter?.LatestRate.UnitsPerDollar);
    }

    [Fact]
    public async Task Falls_back_to_the_next_provider_when_one_fails()
    {
        var failing = new CountingProvider(throws: true);
        var working = new CountingProvider(throws: false);
        var service = CreateService(failing, working);

        var converter = await service.CreateConverterAsync("THB", RateMode.Latest, Today);

        Assert.Equal(1, failing.Calls);
        Assert.NotNull(converter);
    }

    [Fact]
    public async Task Returns_null_when_no_rate_can_be_found()
    {
        var service = CreateService(new CountingProvider(throws: true));

        Assert.Null(await service.CreateConverterAsync("THB", RateMode.Latest, Today));
    }

    [Fact]
    public async Task Needs_no_rates_for_dollars()
    {
        var provider = new CountingProvider(throws: false);

        var converter = await CreateService(provider)
            .CreateConverterAsync("USD", RateMode.Latest, Today);

        Assert.Same(CurrencyConverter.Dollars, converter);
        Assert.Equal(0, provider.Calls);
    }

    public void Dispose()
    {
        store.Dispose();
        SqliteConnection.ClearAllPools();
        directory.Dispose();
    }

    private ExchangeRateService CreateService(params IExchangeRateProvider[] providers) =>
        new(new ExchangeRateRepository(store), providers, new FixedTimeProvider(Today));

    private sealed class CountingProvider(bool throws) : IExchangeRateProvider
    {
        public int Calls { get; private set; }

        public string Name => "counting";

        public Task<IReadOnlyList<ExchangeRate>> FetchAsync(
            string currency,
            DateOnly from,
            DateOnly to,
            CancellationToken cancellationToken)
        {
            Calls++;
            if (throws)
            {
                throw new HttpRequestException("offline");
            }

            IReadOnlyList<ExchangeRate> rates = [new ExchangeRate(Today, currency, 33.595m, Name)];
            return Task.FromResult(rates);
        }
    }

    private sealed class FixedTimeProvider(DateOnly day) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(day.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(12))), TimeSpan.Zero);
    }
}
