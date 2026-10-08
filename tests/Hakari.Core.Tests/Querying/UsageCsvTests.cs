using Hakari.Core.Querying;
using Hakari.Core.Usage;

namespace Hakari.Core.Tests.Querying;

public sealed class UsageCsvTests
{
    private const char Separator = GroupKeys.Separator;

    [Fact]
    public void Writes_a_header_and_a_line_per_group_in_date_order()
    {
        var lines = Write(
            Row($"2026-10-02{Separator}acc{Separator}D:\\work{Separator}opus", 2.5m),
            Row($"2026-10-01{Separator}{Separator}D:\\work{Separator}sonnet", 1m));

        Assert.Equal(3, lines.Length);
        Assert.EndsWith("cost_thb", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("2026-10-01,,D:\\work,sonnet,3,", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("2026-10-02,Main,D:\\work,opus,", lines[2], StringComparison.Ordinal);
        Assert.EndsWith(",2.5", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void Quotes_commas_and_keeps_formulas_as_text()
    {
        var lines = Write(Row($"2026-10-01{Separator}{Separator}=cmd,x{Separator}m", 0m));

        Assert.StartsWith("2026-10-01,,\"'=cmd,x\",m,", lines[1], StringComparison.Ordinal);
    }

    private static string[] Write(params UsageSummary[] rows)
    {
        using var writer = new StringWriter();
        UsageCsv.Write(writer, rows, "THB", id => id.Length == 0 ? string.Empty : "Main");
        return writer.ToString().Split(writer.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }

    private static UsageSummary Row(string key, decimal cost) => UsageSummary.Empty with
    {
        Key = key,
        Messages = 3,
        Tokens = new TokenCounts(10, 20, 0, 5, 100),
        Cost = cost,
    };
}
