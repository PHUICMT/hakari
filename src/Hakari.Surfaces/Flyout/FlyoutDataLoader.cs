using Hakari.Core.Accounts;
using Hakari.Core.Configuration;
using Hakari.Core.Currency;
using Hakari.Core.Indexing;
using Hakari.Core.Limits;
using Hakari.Core.Presentation;
using Hakari.Core.Pricing;
using Hakari.Core.Querying;
using Hakari.Core.Settings;

namespace Hakari.Surfaces.Flyout;

/// <summary>
/// Reads the index Hakari.exe keeps up to date. Read-only and offline: limits and exchange
/// rates come from what Hakari.exe already stored, so opening the flyout makes no requests.
/// </summary>
internal static class FlyoutDataLoader
{
    private const double PercentScale = 100.0;
    private static readonly TimeSpan RecentSourceWindow = TimeSpan.FromMinutes(10);

    public static FlyoutSnapshot Load()
    {
        var settings = SettingsStore.Default.Load();
        using var store = IndexStore.OpenReadOnly(HakariPaths.DefaultIndexPath);
        var pricing = PricingTable.LoadBundled();
        var query = new UsageQuery(store, pricing, StoredConverter(store, settings));
        var now = DateTimeOffset.Now;

        var account = new AccountRepository(store).ListAccounts().FirstOrDefault();
        var limits = account is null ? null : new LimitCache(store).Load(account.AccountId);
        var projected = limits?.ProjectedTo(now);

        return new FlyoutSnapshot(
            UpdatedText: UpdatedText(projected, now),
            AccountName: account is null ? "No account yet" : AccountName(account),
            Limits: projected is null ? [] : LimitRows(projected, now),
            Stats: StatTiles(query, now),
            BurnRate: BurnRate(query, now),
            Sources: SourceRows(store, now),
            Notice: projected?.Freshness == LimitFreshness.LastKnown
                ? "Showing the last known limits. They refresh while Claude Code is in use."
                : null);
    }

    private static CurrencyConverter? StoredConverter(IndexStore store, HakariSettings settings)
    {
        if (settings.Currency == CurrencyCodes.Dollar)
        {
            return null;
        }

        var rates = new ExchangeRateRepository(store).Load(settings.Currency);
        return rates.Count == 0
            ? null
            : new CurrencyConverter(settings.Currency, rates, settings.RateMode);
    }

    private static string AccountName(AccountInfo account) =>
        account.DisplayName is { Length: > 0 } name
            ? $"{name} · {PlanNames.Short(account.Plan)}"
            : PlanNames.Short(account.Plan);

    private static string UpdatedText(LimitSnapshot? limits, DateTimeOffset now)
    {
        if (limits is null)
        {
            return "No limits yet";
        }

        var age = now - limits.FetchedAt;
        return age < TimeSpan.FromMinutes(1)
            ? "Updated just now"
            : $"Updated {(int)age.TotalMinutes} min ago";
    }

    private static List<LimitRow> LimitRows(LimitSnapshot limits, DateTimeOffset now) =>
    [
        .. limits.Limits.Select(limit => new LimitRow(
            Name: LimitNames.Long(limit),
            Value: $"{limit.Percent}%",
            ResetText: limit.ResetsAt is { } resetsAt
                ? $"Resets {ResetText.Long(resetsAt, now)}"
                : "Starts with your next request",
            Fraction: Math.Clamp(limit.Percent / PercentScale, 0, 1),
            Tone: ToneOf(limit, limits.Freshness))),
    ];

    private static Tone ToneOf(UsageLimit limit, LimitFreshness freshness) =>
        LimitPriority.SeverityRank(limit.Severity) switch
        {
            0 when freshness == LimitFreshness.LastKnown => Tone.Muted,
            0 => Tone.Normal,
            1 => Tone.Warning,
            _ => Tone.Critical,
        };

    private static List<StatTile> StatTiles(UsageQuery query, DateTimeOffset now)
    {
        var currency = query.Currency;
        var today = query.Total(new UsageFilter(From: TimePeriods.StartOfToday(now)));
        var week = query.Total(new UsageFilter(From: TimePeriods.StartOfWeek(now)));
        var month = query.Total(new UsageFilter(From: TimePeriods.StartOfMonth(now)));
        return
        [
            new("Today", MoneyText.Format(today.Cost, currency), $"{today.Messages:N0} replies"),
            new("This week", MoneyText.Format(week.Cost, currency), "since Monday"),
            new("This month", MoneyText.Format(month.Cost, currency), $"{now:MMMM}"),
        ];
    }

    private static string BurnRate(UsageQuery query, DateTimeOffset now)
    {
        var lastHour = query.Total(new UsageFilter(From: now.AddHours(-1)));
        return $"{MoneyText.Format(lastHour.Cost, query.Currency)}/h";
    }

    private static List<SourceRow> SourceRows(IndexStore store, DateTimeOffset now) =>
    [
        .. SourceActivity.Load(store).Select(activity => new SourceRow(
            Name: SourceNames.Display(activity.SourceId),
            Detail: $"last used {LastUsedText(now - activity.LastUsage)}",
            IsRecent: now - activity.LastUsage < RecentSourceWindow)),
    ];

    private static string LastUsedText(TimeSpan age) => age switch
    {
        _ when age < TimeSpan.FromMinutes(1) => "just now",
        _ when age < TimeSpan.FromHours(1) => $"{(int)age.TotalMinutes} min ago",
        _ when age < TimeSpan.FromDays(1) => $"{(int)age.TotalHours} h ago",
        _ => $"{(int)age.TotalDays} d ago",
    };
}
