using Hakari.Cli.CommandLine;
using Hakari.Cli.Output;
using Hakari.Core.Pricing;
using Hakari.Core.Querying;

namespace Hakari.Cli.Commands;

internal sealed class ReportCommand : ICliCommand
{
    private const int DefaultRowLimit = 30;
    private const string UnpricedMarker = "*";

    public string Name => "report";

    public string Description =>
        "Index, then summarize cost (--by model|source|day|hour|project|session|branch, "
        + "--period all|today|week|month|<n>d|<n>h)";

    public int Execute(CliArguments arguments)
    {
        var groupBy = ParseGroupBy(arguments.GetValue(OptionNames.GroupBy));
        var period = arguments.GetValue(OptionNames.Period);
        var filter = TimePeriods.ToFilter(period, DateTimeOffset.Now);
        var rowLimit = ParseRowLimit(arguments.GetValue(OptionNames.Limit));

        using var store = CommandContext.OpenStore(arguments);
        CommandContext.RunIndexer(store, CommandContext.DiscoverSources(arguments), rebuild: false);

        var pricing = PricingSources.LoadCurrent();
        var converter = CurrencyContext.CreateConverter(arguments, store, pricing);
        var query = new UsageQuery(store, pricing, converter);

        var summaries = query.Summarize(filter, groupBy).Take(rowLimit);
        if (groupBy == GroupBy.Account)
        {
            summaries = summaries.Select(summary =>
                summary with { Key = CommandContext.AccountLabel(store, summary.Key) });
        }

        WriteSummaries(summaries, groupBy, query.Currency);
        WriteTotal(query.Total(filter), query.Currency);
        CurrencyContext.WriteRateNote(converter);
        WriteUnpricedWarning(query.FindUnpricedModels());
        return ExitCodes.Success;
    }

    private static void WriteSummaries(
        IEnumerable<UsageSummary> summaries,
        GroupBy groupBy,
        string currency)
    {
        var table = new TableWriter(
            groupBy.ToString(), "Messages", "Input", "Output",
            "Write 5m", "Write 1h", "Cache read", "Hit %", "Searches", $"Cost {currency}");

        foreach (var summary in summaries)
        {
            var tokens = summary.Tokens;
            table.AddRow(
                summary.Key,
                DisplayFormat.Number(summary.Messages),
                DisplayFormat.Number(tokens.Input),
                DisplayFormat.Number(tokens.Output),
                DisplayFormat.Number(tokens.CacheWriteFiveMinutes),
                DisplayFormat.Number(tokens.CacheWriteOneHour),
                DisplayFormat.Number(tokens.CacheRead),
                DisplayFormat.Percent(tokens.CacheHitRate),
                DisplayFormat.Number(summary.WebSearchRequests),
                FormatCost(summary));
        }

        table.WriteTo(Console.Out);
    }

    private static string FormatCost(UsageSummary summary)
    {
        var marker = summary.HasUnpricedModels ? UnpricedMarker : string.Empty;
        return DisplayFormat.Cost(summary.Cost) + marker;
    }

    private static void WriteTotal(UsageSummary total, string currency) =>
        Console.WriteLine(
            $"{Environment.NewLine}Total {DisplayFormat.Cost(total.Cost)} {currency} · "
            + $"{DisplayFormat.Number(total.Messages)} messages · "
            + $"cache hit {DisplayFormat.Percent(total.Tokens.CacheHitRate)}");

    private static void WriteUnpricedWarning(IReadOnlyList<string> unpricedModels)
    {
        if (unpricedModels.Count == 0)
        {
            return;
        }

        var modelList = string.Join(", ", unpricedModels);
        Console.WriteLine($"{UnpricedMarker} unpriced models: {modelList}");
    }

    private static GroupBy ParseGroupBy(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return GroupBy.Model;
        }

        return Enum.TryParse<GroupBy>(value, ignoreCase: true, out var groupBy)
            ? groupBy
            : throw new ArgumentException($"Unknown grouping '{value}'.");
    }

    private static int ParseRowLimit(string? value) =>
        int.TryParse(value, out var limit) && limit > 0 ? limit : DefaultRowLimit;
}
