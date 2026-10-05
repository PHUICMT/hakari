namespace Hakari.Core.Currency;

public interface IExchangeRateProvider
{
    string Name { get; }

    /// <summary>
    /// Returns the rates available between the two days, inclusive. Providers without history
    /// return only their latest rate.
    /// </summary>
    Task<IReadOnlyList<ExchangeRate>> FetchAsync(
        string currency,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);
}
