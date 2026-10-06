using System.Globalization;
using Hakari.Core.Accounts;
using Hakari.Core.Configuration;
using Hakari.Core.Currency;
using Hakari.Core.Indexing;
using Hakari.Core.Limits;
using Hakari.Core.Localization;
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
    private const string DetailSeparator = " · ";

    private const int FullPercent = 100;
    private const int SparkHours = 12;
    private const double ExtraUsageWarnAt = 0.8;
    private static readonly TimeSpan RecentSourceWindow = TimeSpan.FromMinutes(10);

    public static FlyoutSnapshot Load()
    {
        var settings = SettingsStore.Default.Load();
        using var store = IndexStore.OpenReadOnly(HakariPaths.DefaultIndexPath);
        var pricing = PricingSources.LoadCurrent();
        var query = new UsageQuery(store, pricing, StoredConverter(store, settings));
        var now = DateTimeOffset.Now;

        var accounts = AccountArrangement.Arrange(
            AccountsWithLimits(store, now),
            entry => entry.Account.AccountId,
            entry => LimitPriority.Rank(entry.Snapshot),
            settings).ToList();
        var newest = accounts.Count == 0
            ? null
            : accounts.MaxBy(account => account.Snapshot.FetchedAt).Snapshot;
        var anyLastKnown = accounts.Any(
            account => account.Snapshot.Freshness == LimitFreshness.LastKnown);

        return new FlyoutSnapshot(
            UpdatedText: accounts.Count > 1 ? string.Empty : UpdatedText(newest, now),
            AccountSummary: AccountSummary(accounts, settings),
            Accounts: [.. accounts.Select(account => Group(account, settings, query, now))],
            Stats: StatTiles(query, now),
            BurnRate: BurnRate(query, now),
            HourlyBurn: HourlyBurn(query, now),
            Sources: SourceRows(store, now),
            Notice: anyLastKnown ? NoticeFor(accounts, new LimitCache(store)) : null);
    }

    /// <summary>Says why the limits are old when it is known, else only that they are.</summary>
    private static string NoticeFor(
        IEnumerable<(AccountInfo Account, LimitSnapshot Snapshot)> accounts,
        LimitCache cache)
    {
        var failures = accounts
            .Select(entry => cache.LoadFailure(entry.Account.AccountId))
            .Where(failure => failure != LimitFailure.None)
            .ToList();
        return failures.FirstOrDefault() switch
        {
            LimitFailure.SignInExpired => Texts.Get("flyout.signInExpired"),
            LimitFailure.Offline => Texts.Get("flyout.offline"),
            LimitFailure.ServiceUnavailable => Texts.Get("flyout.unavailable"),
            _ => Texts.Get("flyout.lastKnown"),
        };
    }

    private static List<(AccountInfo Account, LimitSnapshot Snapshot)> AccountsWithLimits(
        IndexStore store,
        DateTimeOffset now)
    {
        var cache = new LimitCache(store);
        return
        [
            .. new AccountRepository(store).ListAccounts()
                .Select(account => (Account: account, Snapshot: cache.Load(account.AccountId)))
                .Where(entry => entry.Snapshot is not null)
                .Select(entry => (entry.Account, Snapshot: entry.Snapshot!.ProjectedTo(now))),
        ];
    }

    private static string AccountSummary(
        List<(AccountInfo Account, LimitSnapshot Snapshot)> accounts,
        HakariSettings settings) => accounts.Count switch
    {
        0 => Texts.Get("flyout.noAccount"),
        1 => AccountLabels.Full(
            accounts[0].Account,
            settings.NicknameOf(accounts[0].Account.AccountId)),
        _ => Texts.Format("flyout.accounts", accounts.Count),
    };

    /// <summary>
    /// Titled by nickname, else by email, which tells two accounts of one person apart. With
    /// a nickname, the email moves into the detail line.
    /// </summary>
    private static AccountLimitGroup Group(
        (AccountInfo Account, LimitSnapshot Snapshot) entry,
        HakariSettings settings,
        UsageQuery query,
        DateTimeOffset now)
    {
        var nickname = settings.NicknameOf(entry.Account.AccountId);
        var today = query.Total(new UsageFilter(
            From: TimePeriods.StartOfToday(now),
            AccountId: entry.Account.AccountId));
        // Each fact after the first carries its separator, and wraps as a whole.
        string[] facts =
        [
            PlanNames.Short(entry.Account.Plan),
            DetailSeparator.TrimStart() + Texts.Format(
                "widget.today", MoneyText.Format(today.Cost, query.Currency)),
            DetailSeparator.TrimStart() + UpdatedText(entry.Snapshot, now),
        ];
        var percentOf = PercentsOf(entry, settings, query, now);
        var pressing = LimitPriority.MostPressing(entry.Snapshot);
        return new AccountLimitGroup(
            AccountId: entry.Account.AccountId,
            Name: AccountLabels.Full(entry.Account, nickname),
            Email: nickname is null ? string.Empty : entry.Account.Email ?? string.Empty,
            Facts: facts,
            Summary: pressing is null
                ? string.Empty
                : LimitText.Compact(pressing, null, now, Percent(pressing, percentOf, settings)),
            SummaryTone: pressing is null
                ? Tone.Normal
                : ToneOf(pressing, entry.Snapshot.Freshness),
            Limits: LimitRows(
                entry.Snapshot,
                now,
                limit => Percent(limit, percentOf, settings),
                percentOf),
            IsCollapsed: settings.CollapsedAccounts.Contains(entry.Account.AccountId));
    }

    /// <summary>The same estimate as the taskbar, when finer than whole percent.</summary>
    private static Func<UsageLimit, double> PercentsOf(
        (AccountInfo Account, LimitSnapshot Snapshot) entry,
        HakariSettings settings,
        UsageQuery query,
        DateTimeOffset now)
    {
        if (settings.PercentDecimals <= 0)
        {
            return limit => limit.Percent;
        }

        decimal CostBetween(DateTimeOffset from, DateTimeOffset to) => query.Total(
            new UsageFilter(From: from, To: to, AccountId: entry.Account.AccountId)).Cost;

        var fetchedAt = entry.Snapshot.FetchedAt;
        return limit => PercentEstimator.WindowOf(limit) is null
            ? limit.Percent
            : PercentEstimator.Estimate(limit, fetchedAt, now, CostBetween);
    }

    private static string Percent(
        UsageLimit limit,
        Func<UsageLimit, double> percentOf,
        HakariSettings settings) =>
        PercentText.Format(percentOf(limit), settings.PercentDecimals);

    /// <summary>Rates Hakari.exe already stored; opening a window never fetches any.</summary>
    internal static CurrencyConverter? StoredConverter(IndexStore store, HakariSettings settings)
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

    private static string UpdatedText(LimitSnapshot? limits, DateTimeOffset now)
    {
        if (limits is null)
        {
            return Texts.Get("flyout.noLimits");
        }

        var age = now - limits.FetchedAt;
        return age < TimeSpan.FromMinutes(1)
            ? Texts.Get("flyout.updatedNow")
            : Texts.Format("flyout.updatedAgo", AgeText.Format(age));
    }

    /// <summary>The limit lines of one account, as the flyout draws them.</summary>
    internal static List<LimitRow> LimitRowsOf(
        (AccountInfo Account, LimitSnapshot Snapshot) entry,
        HakariSettings settings,
        UsageQuery query,
        DateTimeOffset now)
    {
        var percentOf = PercentsOf(entry, settings, query, now);
        return LimitRows(
            entry.Snapshot,
            now,
            limit => Percent(limit, percentOf, settings),
            percentOf);
    }

    private static List<LimitRow> LimitRows(
        LimitSnapshot limits,
        DateTimeOffset now,
        Func<UsageLimit, string> valueOf,
        Func<UsageLimit, double> percentOf) =>
    [
        .. PlanLimitRows(limits, now, valueOf, percentOf),
        .. ExtraUsageRows(limits.ExtraUsage),
    ];

    /// <summary>Paid credits that cover the account past its limits, when it has a cap.</summary>
    private static IEnumerable<LimitRow> ExtraUsageRows(ExtraUsage? extra)
    {
        if (extra is not { IsEnabled: true, MonthlyLimit: > 0 })
        {
            yield break;
        }

        var fraction = Math.Clamp(extra.Utilization, 0, 1);
        yield return new LimitRow(
            Name: Texts.Get("flyout.extraUsage"),
            Value: $"{MoneyText.Format(extra.Used, extra.Currency)} / "
                + MoneyText.Format(extra.MonthlyLimit, extra.Currency),
            ResetText: Texts.Get("flyout.extraUsage.note"),
            Fraction: fraction,
            Tone: fraction >= ExtraUsageWarnAt ? Tone.Warning : Tone.Normal);
    }

    private static List<LimitRow> PlanLimitRows(
        LimitSnapshot limits,
        DateTimeOffset now,
        Func<UsageLimit, string> valueOf,
        Func<UsageLimit, double> percentOf) =>
    [
        .. limits.Limits.Select(limit => new LimitRow(
            Name: LimitNames.Long(limit),
            Value: limit.Percent >= FullPercent ? Texts.Get("flyout.full") : valueOf(limit),
            ResetText: limit.ResetsAt is { } resetsAt
                ? Texts.Format("flyout.resets", ResetText.Long(resetsAt, now))
                : Texts.Get("flyout.startsNext"),
            Fraction: Math.Clamp(percentOf(limit) / PercentScale, 0, 1),
            Tone: limit.Percent >= FullPercent ? Tone.Critical : ToneOf(limit, limits.Freshness),
            Pace: PaceOf(limit, now),
            PaceText: PaceOf(limit, now) is { } pace
                ? Texts.Format("flyout.evenPace", PercentText.Format(pace * PercentScale, 0))
                : string.Empty)),
    ];

    /// <summary>A full limit has nothing left to pace, so it carries no mark.</summary>
    private static double? PaceOf(UsageLimit limit, DateTimeOffset now) =>
        limit.Percent >= FullPercent ? null : EvenPace.Of(limit, now);

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
        var allTime = query.Total(UsageFilter.Everything);
        return
        [
            new(Texts.Get("flyout.today"), MoneyText.Format(today.Cost, currency), Replies(today)),
            new(Texts.Get("flyout.thisWeek"), MoneyText.Format(week.Cost, currency), SinceMonday),
            new(
                Texts.Get("flyout.thisMonth"),
                MoneyText.Format(month.Cost, currency),
                MonthDetail(month.Cost, currency, now)),
            new(
                Texts.Get("flyout.allTime"),
                MoneyText.Format(allTime.Cost, currency),
                Since(query.FirstUsageDay())),
        ];
    }

    private static string Since(DateOnly? firstDay) => firstDay is { } day
        ? Texts.Format("flyout.since", day.ToString("MMM yyyy", Texts.Culture))
        : string.Empty;

    private static string SinceMonday => Texts.Get("flyout.sinceMonday");

    private static string Replies(UsageSummary today) =>
        Texts.Format("flyout.replies", today.Messages.ToString("N0", CultureInfo.InvariantCulture));

    /// <summary>"≈ ฿9,900 by month end" after the month's first day, else its name.</summary>
    private static string MonthDetail(decimal spent, string currency, DateTimeOffset now) =>
        MonthProjection.Of(spent, now) is { } projected
            ? Texts.Format(
                "flyout.monthProjection",
                MoneyText.Format(decimal.Round(projected), currency))
            : now.ToString("MMMM", Texts.Culture);

    /// <summary>Spending in each of the last twelve hours, oldest first.</summary>
    private static List<decimal> HourlyBurn(UsageQuery query, DateTimeOffset now) =>
    [
        .. Enumerable.Range(0, SparkHours).Select(hour => query.Total(new UsageFilter(
            From: now.AddHours(hour - SparkHours),
            To: now.AddHours(hour - SparkHours + 1))).Cost),
    ];

    private static string BurnRate(UsageQuery query, DateTimeOffset now)
    {
        var lastHour = query.Total(new UsageFilter(From: now.AddHours(-1)));
        return $"{MoneyText.Format(lastHour.Cost, query.Currency)}/h";
    }

    private static List<SourceRow> SourceRows(IndexStore store, DateTimeOffset now) =>
    [
        .. SourceActivity.Load(store).Select(activity => new SourceRow(
            Name: SourceNames.Display(activity.SourceId),
            Detail: Texts.Format("flyout.lastUsed", LastUsedText(now - activity.LastUsage)),
            IsRecent: now - activity.LastUsage < RecentSourceWindow)),
    ];

    internal static string LastUsedText(TimeSpan age) => AgeText.Format(age);
}
