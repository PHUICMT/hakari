namespace Hakari.Core.Presentation.Widget;

/// <summary>One value a format string can show, with the kind that says how to write it.</summary>
public sealed record FormatValue(
    FormatKind Kind,
    decimal Number = 0,
    string Text = "",
    TimeSpan Span = default)
{
    public static FormatValue Money(decimal amount, string currency) =>
        new(FormatKind.Money, amount, currency);

    public static FormatValue Percent(double percent) =>
        new(FormatKind.Percent, (decimal)percent);

    public static FormatValue Duration(TimeSpan span) =>
        new(FormatKind.Duration, Span: span);

    public static FormatValue Tokens(long count) => new(FormatKind.Tokens, count);

    public static FormatValue Count(long count) => new(FormatKind.Count, count);

    public static FormatValue Words(string text) => new(FormatKind.Words, Text: text);
}
