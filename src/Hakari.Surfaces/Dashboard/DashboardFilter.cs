using Hakari.Core.Querying;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// The question every page answers: which period, account and source. It is shared, so a
/// choice made on one page carries to the next.
/// </summary>
/// <param name="AccountId">Null for every account.</param>
/// <param name="SourceId">Null for every source.</param>
/// <param name="Model">Null for every model.</param>
public sealed record DashboardFilter(
    DashboardPeriod Period = DashboardPeriod.ThirtyDays,
    string? AccountId = null,
    string? SourceId = null,
    string? Model = null)
{
    private const int Week = 7;
    private const int Month = 30;

    private static DashboardFilter current = new();

    /// <summary>Setting it tells <see cref="Changed"/>, so the choice can be remembered.</summary>
    public static DashboardFilter Current
    {
        get => current;
        set
        {
            var changed = value != current;
            current = value;
            if (changed)
            {
                Changed?.Invoke(null, value);
            }
        }
    }

    public static event EventHandler<DashboardFilter>? Changed;

    public int? Days => Period switch
    {
        DashboardPeriod.Today => 1,
        DashboardPeriod.SevenDays => Week,
        DashboardPeriod.ThirtyDays => Month,
        _ => null,
    };

    /// <summary>The period's first moment: midnight, so "7 days" means seven whole days.</summary>
    public DateTimeOffset? From(DateTimeOffset now) =>
        Days is { } days ? TimePeriods.StartOfToday(now).AddDays(1 - days) : null;

    public UsageFilter ToUsageFilter(DateTimeOffset now) => new(
        From: From(now),
        AccountId: AccountId,
        SourceIds: SourceId is null ? null : [SourceId],
        Model: Model);

    /// <summary>The same length of time just before, for "vs previous 30 days".</summary>
    public UsageFilter? PreviousUsageFilter(DateTimeOffset now) =>
        From(now) is { } from && Days is { } days
            ? new UsageFilter(
                From: from.AddDays(-days),
                To: from,
                AccountId: AccountId,
                SourceIds: SourceId is null ? null : [SourceId],
                Model: Model)
            : null;
}
