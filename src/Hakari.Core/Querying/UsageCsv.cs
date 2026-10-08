using System.Globalization;

namespace Hakari.Core.Querying;

/// <summary>
/// Usage as a CSV file for a spreadsheet: one line per day, account, project and model, with
/// token counts and the cost in the shown currency. Text that a spreadsheet would run as a
/// formula is kept as text.
/// </summary>
public static class UsageCsv
{
    private const char Comma = ',';
    private const char Quote = '"';
    private static readonly char[] NeedsQuotes = [Comma, Quote, '\r', '\n'];
    private static readonly char[] FormulaStarts = ['=', '+', '-', '@', '\t', '\r'];
    private const string FormulaGuard = "'";
    private const int Parts = 4;

    public static readonly string[] Header =
    [
        "date", "account", "project", "model", "responses", "input_tokens", "output_tokens",
        "cache_write_5m_tokens", "cache_write_1h_tokens", "cache_read_tokens",
        "web_searches",
    ];

    /// <param name="rows">Grouped by <see cref="GroupBy.DayAccountProjectModel"/>.</param>
    /// <param name="accountName">The name shown for an account id; empty for none.</param>
    public static void Write(
        TextWriter writer,
        IEnumerable<UsageSummary> rows,
        string currency,
        Func<string, string> accountName)
    {
        writer.WriteLine(Line([.. Header, $"cost_{currency.ToLowerInvariant()}"]));
        foreach (var row in rows.OrderBy(row => row.Key, StringComparer.Ordinal))
        {
            var parts = row.Key.Split(GroupKeys.Separator, Parts);
            string Part(int index) => parts.Length > index ? parts[index] : string.Empty;
            writer.WriteLine(Line(
            [
                Part(0),
                accountName(Part(1)),
                Part(2),
                Part(3),
                Number(row.Messages),
                Number(row.Tokens.Input),
                Number(row.Tokens.Output),
                Number(row.Tokens.CacheWriteFiveMinutes),
                Number(row.Tokens.CacheWriteOneHour),
                Number(row.Tokens.CacheRead),
                Number(row.WebSearchRequests),
                row.Cost.ToString("0.######", CultureInfo.InvariantCulture),
            ]));
        }
    }

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Line(IEnumerable<string> fields) =>
        string.Join(Comma, fields.Select(Field));

    private static string Field(string value)
    {
        var safe = value.Length > 0 && FormulaStarts.Contains(value[0])
            && !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
            ? FormulaGuard + value
            : value;
        return safe.IndexOfAny(NeedsQuotes) < 0
            ? safe
            : Quote + safe.Replace("\"", "\"\"", StringComparison.Ordinal) + Quote;
    }
}
