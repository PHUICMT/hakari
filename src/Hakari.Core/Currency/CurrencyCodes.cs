namespace Hakari.Core.Currency;

public static class CurrencyCodes
{
    public const string Dollar = "USD";

    private const int CodeLength = 3;

    public static string Normalize(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var isValid = normalized.Length == CodeLength && normalized.All(char.IsAsciiLetterUpper);
        return isValid
            ? normalized
            : throw new ArgumentException($"'{code}' is not a three-letter currency code.");
    }
}
