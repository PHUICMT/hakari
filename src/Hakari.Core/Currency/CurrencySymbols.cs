namespace Hakari.Core.Currency;

public static class CurrencySymbols
{
    private static readonly Dictionary<string, string> Symbols = new(StringComparer.Ordinal)
    {
        ["USD"] = "$",
        ["THB"] = "฿",
        ["EUR"] = "€",
        ["GBP"] = "£",
        ["JPY"] = "¥",
        ["SGD"] = "S$",
        ["AUD"] = "A$",
        ["CAD"] = "C$",
    };

    /// <summary>A short symbol, or the code and a space when there is none.</summary>
    public static string PrefixFor(string currency) =>
        Symbols.TryGetValue(currency, out var symbol) ? symbol : currency + " ";
}
