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
using Hakari.Core.Sources;
using Hakari.Core.Updates;
using Hakari.Core.Startup;

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

    /// <summary>
    /// Never fails: before the first index exists, or when it cannot be read, the flyout
    /// opens with a note instead of numbers.
    /// </summary>
    public static FlyoutSnapshot Load()
    {
        try
        {
            if (File.Exists(HakariPaths.DefaultIndexPath))
            {
                return Read();
            }
        }
        catch (Exception exception) when (exception is Microsoft.Data.Sqlite.SqliteException
            or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Shown as not ready below.
        }

        return new FlyoutSnapshot(
            string.Empty,
            Texts.Get("flyout.noAccount"),
            [],
            [],
            string.Empty,
            [],
            [],
            [new FlyoutNotice(
                Tone.Normal,
                Texts.Get("flyout.notice.reading"),
                Texts.Get("flyout.notice.readingDetail"))]);
    }

    private static FlyoutSnapshot Read()
    {
        var settings = SettingsStore.Default.Load();
        using var store = IndexStore.OpenReadOnly(HakariPaths.DefaultIndexPath);
        var pricing = PricingSources.LoadCurrent();
        var query = new UsageQuery(store, pricing, StoredConverter(store, settings));
        var history = new LimitHistory(store);
        var now = DateTimeOffset.Now;

        var accounts = AccountArrangement.Arrange(
            AccountsWithLimits(store, now),
            entry => entry.Account.AccountId,
            entry => LimitPriority.Rank(entry.Snapshot),
            settings).ToList();
        var newest = accounts.Count == 0
            ? null
            : accounts.MaxBy(account => account.Snapshot.FetchedAt).Snapshot;
        // One pass over everything serves both the all-time tile and the per-source costs.
        var bySource = query.Summarize(UsageFilter.Everything, GroupBy.Source);
        var anyLastKnown = accounts.Any(
            account => account.Snapshot.Freshness == LimitFreshness.LastKnown);

        return new FlyoutSnapshot(
            UpdatedText: accounts.Count > 1 ? string.Empty : UpdatedText(newest, now),
            AccountSummary: AccountSummary(accounts, settings),
            Accounts:
            [
                .. accounts.Select(account => Group(account, settings, query, history, now)),
            ],
            Stats: StatTiles(query, bySource.Sum(source => source.Cost), now),
            BurnRate: BurnRate(query, now),
            HourlyBurn: HourlyBurn(query, now),
            Sources: SourceRows(store, query, bySource, now),
            Notices:
            [
                .. FullLimits(accounts, settings, now),
                .. anyLastKnown
                    ? new[] { NoticeFor(accounts, new LimitCache(store)) }
                    : [],
                .. Unpriced(query),
                .. FirstRead(query, store),
                .. LogFormat(store),
                .. LimitsQuestions(store, settings),
                .. StoppedDistributions(store, settings),
                .. NewerVersion(settings),
                .. WhatsNewSinceLast(settings),
                .. AskForReview(store, settings, now),
            ]);
    }

    /// <summary>
    /// A limit used up on one account, when it blocks work: when it resets, and which other
    /// account still has room on the same limit.
    /// </summary>
    private static IEnumerable<FlyoutNotice> FullLimits(
        List<(AccountInfo Account, LimitSnapshot Snapshot)> accounts,
        HakariSettings settings,
        DateTimeOffset now)
    {
        foreach (var (account, snapshot) in accounts)
        {
            foreach (var limit in snapshot.Limits.Where(limit => limit.Percent >= FullPercent))
            {
                var name = AccountLabels.Full(account, settings.NicknameOf(account.AccountId));
                var room = accounts
                    .Where(other => other.Account.AccountId != account.AccountId)
                    .Select(other => (other.Account, Limit: other.Snapshot.Limits.FirstOrDefault(
                        candidate => candidate.Kind == limit.Kind
                            && candidate.ScopeName == limit.ScopeName)))
                    .Where(other => other.Limit is { Percent: < FullPercent })
                    .OrderBy(other => other.Limit!.Percent)
                    .FirstOrDefault();
                var resets = limit.ResetsAt is { } at
                    ? Texts.Format("flyout.resets", ResetText.Long(at, now))
                    : string.Empty;
                var elsewhere = room.Account is null
                    ? string.Empty
                    : Texts.Format(
                        "alert.roomElsewhere",
                        AccountLabels.Full(room.Account, settings.NicknameOf(
                            room.Account.AccountId)),
                        $"{FullPercent - room.Limit!.Percent}%");
                yield return new FlyoutNotice(
                    Tone.Critical,
                    Texts.Format("flyout.notice.full", LimitNames.Long(limit), name),
                    string.Join(DetailSeparator, new[] { resets, elsewhere }
                        .Where(part => part.Length > 0)));
            }
        }
    }

    /// <summary>
    /// Before the first read of the logs has finished there is nothing to count yet; the
    /// flyout says so instead of showing zeros.
    /// </summary>
    /// <summary>
    /// Claude Code seems to log in a new shape that this version cannot read, so the numbers
    /// may be short; a newer Hakari usually reads it.
    /// </summary>
    private static IEnumerable<FlyoutNotice> LogFormat(IndexStore store)
    {
        if (LogFormatWatch.SeemsChanged(store))
        {
            yield return new FlyoutNotice(
                Tone.Warning,
                Texts.Get("flyout.notice.logFormat"),
                Texts.Get("flyout.notice.logFormatDetail"),
                NoticeAction.OpenUpdate,
                Texts.Get("flyout.notice.updateAction"));
        }
    }

    private static IEnumerable<FlyoutNotice> FirstRead(UsageQuery query, IndexStore store)
    {
        if (query.HasAny())
        {
            yield break;
        }

        // With no Claude Code folder anywhere there is nothing to read; waiting would never end.
        yield return CurrentSources.FoundNone(store)
            ? new FlyoutNotice(
                Tone.Normal,
                Texts.Get("flyout.notice.noLogs"),
                Texts.Get("flyout.notice.noLogsDetail"))
            : new FlyoutNotice(
                Tone.Normal,
                Texts.Get("flyout.notice.reading"),
                Texts.Get("flyout.notice.readingDetail"));
    }

    /// <summary>A newer release than this copy, when the daily look found one.</summary>
    private static IEnumerable<FlyoutNotice> NewerVersion(HakariSettings settings)
    {
        var current = typeof(FlyoutDataLoader).Assembly.GetName().Version;
        if (!settings.CheckForUpdates
            || Hakari.Core.Startup.PackageIdentity.IsPackaged
            || current is null
            || Hakari.Core.Updates.UpdateCheck.NewerThan(current) is not { } update)
        {
            yield break;
        }

        var have = $"{current.Major}.{current.Minor}.{current.Build}";
        yield return Updates.SelfUpdate.CanUpdateInPlace
            ? new FlyoutNotice(
                Tone.Normal,
                Texts.Format("flyout.notice.update", update.Latest),
                Texts.Format(
                    InstallSource.Current == InstallKind.Winget
                        ? "flyout.notice.updateWinget"
                        : "flyout.notice.updateSelf",
                    have),
                NoticeAction.UpdateNow,
                Texts.Get("flyout.notice.updateNow"),
                AccountId: update.Latest,
                SecondActionText: Texts.Get("flyout.notice.whatsNewAll"))
            : new FlyoutNotice(
                Tone.Normal,
                Texts.Format("flyout.notice.update", update.Latest),
                Texts.Format("flyout.notice.updateDetail", have),
                NoticeAction.OpenUpdate,
                Texts.Get("flyout.notice.updateAction"));
    }

    /// <summary>Once, two weeks in, in the Store copy only: ratings help others find it.</summary>
    private static IEnumerable<FlyoutNotice> AskForReview(
        IndexStore store,
        HakariSettings settings,
        DateTimeOffset now)
    {
        if (ReviewAsk.IsDue(InstallSource.Current, settings.ReviewAsked, store.CreatedAt, now))
        {
            yield return new FlyoutNotice(
                Tone.Normal,
                Texts.Get("flyout.notice.review"),
                Texts.Get("flyout.notice.reviewDetail"),
                NoticeAction.RateApp,
                Texts.Get("flyout.notice.reviewAction"),
                SecondActionText: Texts.Get("flyout.notice.reviewLater"));
        }
    }

    private const int WhatsNewShown = 3;
    private const string WhatsNewSeparator = " · ";

    /// <summary>
    /// The running version's changes, once, on its first start after an update. A first
    /// install only remembers the version and shows nothing.
    /// </summary>
    private static IEnumerable<FlyoutNotice> WhatsNewSinceLast(HakariSettings settings)
    {
        if (typeof(FlyoutDataLoader).Assembly.GetName().Version is not { } current)
        {
            yield break;
        }

        var version = WhatsNew.Text(current);
        if (settings.LastSeenVersion.Length == 0)
        {
            SettingsStore.Default.Update(saved => saved with { LastSeenVersion = version });
            yield break;
        }

        var changes = WhatsNew.Changes(AppContext.BaseDirectory);
        if (!WhatsNew.IsNewSince(settings.LastSeenVersion, current) || changes.Count == 0)
        {
            yield break;
        }

        yield return new FlyoutNotice(
            Tone.Normal,
            Texts.Format("flyout.notice.whatsNew", version),
            string.Join(WhatsNewSeparator, changes.Take(WhatsNewShown)),
            NoticeAction.DismissWhatsNew,
            Texts.Get("flyout.notice.whatsNewOk"),
            AccountId: version,
            SecondActionText: Texts.Get("flyout.notice.whatsNewAll"));
    }

    /// <summary>
    /// A newly seen account whose limits were never read: Hakari asks before reading its
    /// sign-in, and offers estimates from its usage instead.
    /// </summary>
    private static IEnumerable<FlyoutNotice> LimitsQuestions(
        IndexStore store,
        HakariSettings settings)
    {
        var cache = new LimitCache(store);
        foreach (var account in new AccountRepository(store).ListAccounts())
        {
            if (settings.LimitsAskedAccounts.Contains(account.AccountId)
                || cache.Load(account.AccountId) is not null)
            {
                continue;
            }

            var name = AccountLabels.Full(account, settings.NicknameOf(account.AccountId));
            yield return new FlyoutNotice(
                Tone.Normal,
                Texts.Format("flyout.notice.limitsAsk", name),
                Texts.Get("flyout.notice.limitsAskDetail"),
                NoticeAction.TurnOnLimits,
                Texts.Get("flyout.notice.turnOn"),
                account.AccountId,
                Texts.Get("flyout.notice.useEstimate"));
        }
    }

    private const string WslPrefix = "wsl:";

    /// <summary>
    /// A WSL distribution with usage before that is not running now: its new usage shows up
    /// the next time it runs, since Hakari never starts WSL itself.
    /// </summary>
    private static IEnumerable<FlyoutNotice> StoppedDistributions(
        IndexStore store,
        HakariSettings settings)
    {
        if (settings.WslMode != WslScanMode.RunningOnly
            || CurrentSources.Load(store) is not { } current)
        {
            yield break;
        }

        foreach (var activity in SourceActivity.Load(store))
        {
            if (activity.SourceId.StartsWith(WslPrefix, StringComparison.OrdinalIgnoreCase)
                && !current.Contains(activity.SourceId))
            {
                yield return new FlyoutNotice(
                    Tone.Warning,
                    Texts.Format(
                        "flyout.notice.wslStopped", SourceNames.Display(activity.SourceId)),
                    Texts.Get("flyout.notice.wslStoppedDetail"));
            }
        }
    }

    /// <summary>Models seen in the logs that the price table does not know yet.</summary>
    private static IEnumerable<FlyoutNotice> Unpriced(UsageQuery query)
    {
        var models = query.FindUnpricedModels();
        if (models.Count == 0)
        {
            yield break;
        }

        yield return new FlyoutNotice(
            Tone.Warning,
            Texts.Format("flyout.notice.unpriced", models.Count),
            Texts.Format("flyout.notice.unpricedBody", string.Join(", ", models)),
            NoticeAction.OpenPrices,
            Texts.Get("flyout.notice.updatePrices"));
    }

    /// <summary>Says why the limits are old when it is known, else only that they are.</summary>
    private static FlyoutNotice NoticeFor(
        IEnumerable<(AccountInfo Account, LimitSnapshot Snapshot)> accounts,
        LimitCache cache)
    {
        var failures = accounts
            .Select(entry => cache.LoadFailure(entry.Account.AccountId))
            .Where(failure => failure != LimitFailure.None)
            .ToList();
        return failures.FirstOrDefault() switch
        {
            LimitFailure.SignInExpired =>
                new FlyoutNotice(Tone.Warning, Texts.Get("flyout.signInExpired")),
            LimitFailure.Offline => new FlyoutNotice(Tone.Warning, Texts.Get("flyout.offline")),
            LimitFailure.ServiceUnavailable =>
                new FlyoutNotice(Tone.Warning, Texts.Get("flyout.unavailable")),
            _ => new FlyoutNotice(Tone.Normal, Texts.Get("flyout.lastKnown")),
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
        LimitHistory history,
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
                percentOf,
                FullAtOf(entry, history, now)),
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
        LimitHistory history,
        DateTimeOffset now)
    {
        var percentOf = PercentsOf(entry, settings, query, now);
        return LimitRows(
            entry.Snapshot,
            now,
            limit => Percent(limit, percentOf, settings),
            percentOf,
            FullAtOf(entry, history, now));
    }

    /// <summary>
    /// When the recent pace fills each limit before it resets. Only for limits read live: a
    /// last known reading says nothing about the pace now.
    /// </summary>
    internal static Func<UsageLimit, DateTimeOffset?> FullAtOf(
        (AccountInfo Account, LimitSnapshot Snapshot) entry,
        LimitHistory history,
        DateTimeOffset now) =>
        limit => entry.Snapshot.Freshness == LimitFreshness.Live
            ? LimitForecaster.FullAt(history, entry.Account.AccountId, limit, now)
            : null;

    private static List<LimitRow> LimitRows(
        LimitSnapshot limits,
        DateTimeOffset now,
        Func<UsageLimit, string> valueOf,
        Func<UsageLimit, double> percentOf,
        Func<UsageLimit, DateTimeOffset?> fullAtOf) =>
    [
        .. PlanLimitRows(limits, now, valueOf, percentOf, fullAtOf),
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
        Func<UsageLimit, double> percentOf,
        Func<UsageLimit, DateTimeOffset?> fullAtOf) =>
    [
        .. limits.Limits.Select(limit =>
        {
            var fullAt = fullAtOf(limit);
            var tone = limit.Percent >= FullPercent
                ? Tone.Critical
                : ToneOf(limit, limits.Freshness);
            return new LimitRow(
                Name: LimitNames.Long(limit),
                Value: limit.Percent >= FullPercent ? Texts.Get("flyout.full") : valueOf(limit),
                ResetText: limit.ResetsAt is { } resetsAt
                    ? Texts.Format("flyout.resets", ResetText.Long(resetsAt, now))
                    : Texts.Get("flyout.startsNext"),
                Fraction: Math.Clamp(percentOf(limit) / PercentScale, 0, 1),
                Tone: fullAt is not null && tone == Tone.Normal ? Tone.Warning : tone,
                Pace: PaceOf(limit, now),
                PaceText: PaceOf(limit, now) is { } pace
                    ? Texts.Format("flyout.evenPace", PercentText.Format(pace * PercentScale, 0))
                    : string.Empty,
                FullAtText: fullAt is { } full
                    ? Texts.Format("flyout.fullAt", ResetText.Long(full, now))
                    : string.Empty);
        }),
    ];


    /// <summary>A full limit has nothing left to pace, so it carries no mark.</summary>
    private static double? PaceOf(UsageLimit limit, DateTimeOffset now) =>
        limit.Percent >= FullPercent ? null : EvenPace.Of(limit, now);

    internal static Tone ToneOf(UsageLimit limit, LimitFreshness freshness) =>
        LimitPriority.SeverityRank(limit.Severity) switch
        {
            0 when freshness == LimitFreshness.LastKnown => Tone.Muted,
            0 => Tone.Normal,
            1 => Tone.Warning,
            _ => Tone.Critical,
        };

    private static List<StatTile> StatTiles(
        UsageQuery query,
        decimal allTimeCost,
        DateTimeOffset now)
    {
        var currency = query.Currency;
        var today = query.Total(new UsageFilter(From: TimePeriods.StartOfToday(now)));
        var week = query.Total(new UsageFilter(From: TimePeriods.StartOfWeek(now)));
        var month = query.Total(new UsageFilter(From: TimePeriods.StartOfMonth(now)));
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
                MoneyText.Format(allTimeCost, currency),
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

    /// <summary>"live · ฿6,035": in use now or when it last was, and all it cost.</summary>
    private static List<SourceRow> SourceRows(
        IndexStore store,
        UsageQuery query,
        IReadOnlyList<UsageSummary> bySource,
        DateTimeOffset now)
    {
        var costs = bySource.ToDictionary(source => source.Key, source => source.Cost);
        return
        [
            .. SourceActivity.Load(store).Select(activity =>
            {
                var isRecent = now - activity.LastUsage < RecentSourceWindow;
                var when = isRecent
                    ? Texts.Get("flyout.live")
                    : Texts.Format("flyout.lastUsed", LastUsedText(now - activity.LastUsage));
                var cost = MoneyText.Format(
                    costs.GetValueOrDefault(activity.SourceId), query.Currency);
                return new SourceRow(
                    Name: SourceNames.Display(activity.SourceId),
                    Detail: when + DetailSeparator + cost,
                    IsRecent: isRecent);
            }),
        ];
    }

    internal static string LastUsedText(TimeSpan age) => AgeText.Format(age);
}
