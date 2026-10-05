namespace Hakari.Core.Currency;

/// <summary>
/// How many units of <see cref="Currency"/> one US dollar bought on <see cref="Day"/>.
/// </summary>
public sealed record ExchangeRate(
    DateOnly Day,
    string Currency,
    decimal UnitsPerDollar,
    string Source);
