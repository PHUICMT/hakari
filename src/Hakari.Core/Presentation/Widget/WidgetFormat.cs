using System.Globalization;
using System.Text;

namespace Hakari.Core.Presentation.Widget;

/// <summary>
/// Writes a custom format string such as <c>{cost.today:$0.00} · {limit.5h:0%}</c>. Each
/// <c>{name}</c> or <c>{name:pattern}</c> is replaced by a value; <c>{{</c> and <c>}}</c> are
/// literal braces. A name that is not known, or a value that is not available, shows as a dash.
/// </summary>
public static class WidgetFormat
{
    public const string Missing = "—";

    private const char Open = '{';
    private const char Close = '}';
    private const char PatternSeparator = ':';
    private const char DecimalMark = '.';
    private const char PercentSign = '%';
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <param name="valueOf">The value for a name, or null if unknown or unavailable.</param>
    public static string Apply(string format, Func<string, FormatValue?> valueOf)
    {
        var result = new StringBuilder();
        var index = 0;
        while (index < format.Length)
        {
            var character = format[index];
            if (character == Open && Next(format, index) == Open)
            {
                result.Append(Open);
                index += 2;
            }
            else if (character == Close && Next(format, index) == Close)
            {
                result.Append(Close);
                index += 2;
            }
            else if (character == Open && format.IndexOf(Close, index) is var end and > 0)
            {
                result.Append(Piece(format[(index + 1)..end], valueOf));
                index = end + 1;
            }
            else
            {
                result.Append(character);
                index++;
            }
        }

        return result.ToString();
    }

    private static char? Next(string text, int index) =>
        index + 1 < text.Length ? text[index + 1] : null;

    private static string Piece(string body, Func<string, FormatValue?> valueOf)
    {
        var split = body.IndexOf(PatternSeparator);
        var name = (split < 0 ? body : body[..split]).Trim();
        var pattern = split < 0 ? string.Empty : body[(split + 1)..];
        return valueOf(name) is { } value ? Write(value, pattern) : Missing;
    }

    private static string Write(FormatValue value, string pattern) => value.Kind switch
    {
        FormatKind.Money => Money(value, pattern),
        FormatKind.Percent => Percent((double)value.Number, pattern),
        FormatKind.Duration => Duration(value.Span, pattern),
        FormatKind.Tokens => pattern.Length == 0
            ? TokenText.Format((long)value.Number)
            : Number(value.Number, pattern),
        FormatKind.Count => Number(value.Number, pattern),
        _ => value.Text,
    };

    /// <summary>
    /// The digits of the pattern say how many decimals; anything around them, such as "$" or
    /// "฿", is written as it is. With no pattern, the usual money text for the currency.
    /// </summary>
    private static string Money(FormatValue value, string pattern)
    {
        if (pattern.Length == 0)
        {
            return MoneyText.Format(value.Number, value.Text);
        }

        var (prefix, digits, suffix) = Split(pattern);
        return prefix + value.Number.ToString("N" + Decimals(digits), Invariant) + suffix;
    }

    /// <summary>"0%" is whole percent, "0.0%" one decimal; with no "%" no sign shows.</summary>
    private static string Percent(double percent, string pattern)
    {
        if (pattern.Length == 0)
        {
            return PercentText.Format(percent, 0);
        }

        var (prefix, digits, suffix) = Split(pattern.Replace(PercentSign.ToString(), string.Empty));
        var text = PercentText.Format(percent, Decimals(digits));
        var number = text.TrimEnd(PercentSign);
        return prefix + number + (pattern.Contains(PercentSign) ? PercentSign + suffix : suffix);
    }

    private static string Duration(TimeSpan span, string pattern)
    {
        var shown = span < TimeSpan.Zero ? TimeSpan.Zero : span;
        if (pattern.Length > 0)
        {
            try
            {
                return shown.ToString(pattern, Invariant);
            }
            catch (FormatException)
            {
                return Missing;
            }
        }

        return shown.TotalDays >= 1
            ? $"{(int)shown.TotalDays}d {shown.Hours}h"
            : $"{(int)shown.TotalHours}h {shown.Minutes:00}m";
    }

    private static string Number(decimal number, string pattern)
    {
        if (pattern.Length == 0)
        {
            return number.ToString("N0", Invariant);
        }

        var (prefix, digits, suffix) = Split(pattern);
        return prefix + number.ToString("N" + Decimals(digits), Invariant) + suffix;
    }

    /// <summary>Splits "$0.00 USD" into "$", "0.00" and " USD".</summary>
    private static (string Prefix, string Digits, string Suffix) Split(string pattern)
    {
        var first = pattern.IndexOfAny(['0', '#']);
        if (first < 0)
        {
            return (pattern, string.Empty, string.Empty);
        }

        var last = first;
        while (last + 1 < pattern.Length && IsDigitSlot(pattern[last + 1]))
        {
            last++;
        }

        return (pattern[..first], pattern[first..(last + 1)], pattern[(last + 1)..]);
    }

    private static bool IsDigitSlot(char character) =>
        character is '0' or '#' or DecimalMark;

    private static int Decimals(string digits)
    {
        var mark = digits.IndexOf(DecimalMark);
        return mark < 0 ? 0 : digits.Length - mark - 1;
    }
}
