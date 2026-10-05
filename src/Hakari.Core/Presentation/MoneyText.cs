using System.Globalization;
using Hakari.Core.Currency;

namespace Hakari.Core.Presentation;

/// <summary>Money as people read it: cents only while they still matter.</summary>
public static class MoneyText
{
    private const decimal WholeUnitsFrom = 1000m;
    private const string WithCents = "N2";
    private const string WholeUnits = "N0";
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static string Format(decimal amount, string currency)
    {
        var format = amount >= WholeUnitsFrom ? WholeUnits : WithCents;
        return CurrencySymbols.PrefixFor(currency) + amount.ToString(format, Culture);
    }
}
