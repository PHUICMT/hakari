namespace Hakari.Core.Querying;

/// <summary>
/// Everything ever spent, without adding up the whole history every half minute: the days
/// before today are added up once and kept while their record count, the day and the query
/// (its prices and currency) stay the same; only today is added on each read.
/// </summary>
public static class AllTimeCost
{
    private static readonly Lock KeptLock = new();
    private static Kept? kept;

    public static decimal Of(UsageQuery query, DateTimeOffset now)
    {
        var startOfToday = TimePeriods.StartOfToday(now);
        var before = new UsageFilter(To: startOfToday);
        var rowsBefore = query.Count(before);
        decimal costBefore;
        lock (KeptLock)
        {
            costBefore = kept is { } hit
                && ReferenceEquals(hit.Query, query)
                && hit.StartOfToday == startOfToday
                && hit.RowsBefore == rowsBefore
                    ? hit.CostBefore
                    : -1;
        }

        if (costBefore < 0)
        {
            costBefore = query.Total(before).Cost;
            lock (KeptLock)
            {
                kept = new Kept(query, startOfToday, rowsBefore, costBefore);
            }
        }

        return costBefore + query.Total(new UsageFilter(From: startOfToday)).Cost;
    }

    private sealed record Kept(
        UsageQuery Query,
        DateTimeOffset StartOfToday,
        long RowsBefore,
        decimal CostBefore);
}
