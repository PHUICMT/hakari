using System.Globalization;

namespace Hakari.Core.Presentation;

/// <summary>An amount typed for a budget: commas allowed, blank meaning none.</summary>
public static class AmountText
{
    /// <param name="valid">False when the text is neither blank nor a positive number.</param>
    /// <returns>The amount, or null for blank or invalid text.</returns>
    public static decimal? Parse(string text, out bool valid)
    {
        var trimmed = text.Trim().Replace(",", string.Empty, StringComparison.Ordinal);
        if (trimmed.Length == 0)
        {
            valid = true;
            return null;
        }

        valid = decimal.TryParse(
            trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            && amount > 0;
        return valid ? amount : null;
    }
}
