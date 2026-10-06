using Hakari.Core.Accounts;
using Hakari.Core.Indexing;
using Hakari.Core.Limits;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Hakari.Core.Querying;
using Hakari.Core.Settings;
using Hakari.Surfaces.Flyout;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// What the Accounts and sources page shows: every account Hakari has seen with its limits,
/// the totals across them, and each place usage is read from.
/// </summary>
internal sealed record AccountsData(
    IReadOnlyList<AccountCardData> Accounts,
    AccountCardData? Earlier,
    string AllTime,
    string Period,
    string Today,
    IReadOnlyList<SourceLine> Sources)
{
    private const string DetailSeparator = " · ";
    private static readonly TimeSpan LimitsAreLive = TimeSpan.FromMinutes(15);

    public static AccountsData Empty { get; } =
        new([], null, string.Empty, string.Empty, string.Empty, []);

    public static AccountsData Load(DashboardFilter filter) =>
        DashboardData.Read((query, store) => Read(query, store, filter), Empty);

    private static AccountsData Read(UsageQuery query, IndexStore store, DashboardFilter filter)
    {
        var now = DateTimeOffset.Now;
        var settings = SettingsStore.Default.Load();
        var repository = new AccountRepository(store);
        var accounts = Ordered(repository.ListAccounts(), new LimitCache(store), settings, now);
        var usage = filter.ToUsageFilter(now) with { AccountId = null };
        var today = new UsageFilter(From: TimePeriods.StartOfToday(now));
        return new AccountsData(
            [.. accounts.Select(entry => Card(entry, query, usage, settings, now))],
            EarlierCard(query, usage),
            Money(query, UsageFilter.Everything),
            Money(query, usage),
            Money(query, today),
            new SourceLines(query, store, repository, settings, now).Load());
    }

    /// <summary>
    /// Usage from before Hakari first saw a source belongs to no account: the logs do not say
    /// who was signed in. It is shown on its own, so nobody is given what they did not spend.
    /// </summary>
    private static AccountCardData? EarlierCard(UsageQuery query, UsageFilter usage)
    {
        var everything = query.Total(UsageFilter.Everything with { AccountId = string.Empty });
        if (everything.Messages == 0)
        {
            return null;
        }

        var range = string.Format(
            Texts.Culture,
            "{0:MMM d, yyyy} – {1:MMM d, yyyy}",
            everything.FirstSeen.ToLocalTime(),
            everything.LastSeen.ToLocalTime());
        return new AccountCardData(
            Texts.Get("dashboard.accounts.earlier"),
            range,
            string.Empty,
            BadgeTone.Neutral,
            [],
            Money(query, usage with { AccountId = string.Empty }),
            MoneyText.Format(everything.Cost, query.Currency));
    }

    private static string Money(UsageQuery query, UsageFilter usage) =>
        MoneyText.Format(query.Total(usage).Cost, query.Currency);

    /// <summary>Shown accounts in the order set in Settings, then the hidden ones.</summary>
    private static List<(AccountInfo Account, LimitSnapshot? Snapshot)> Ordered(
        IReadOnlyList<AccountInfo> accounts,
        LimitCache cache,
        HakariSettings settings,
        DateTimeOffset now)
    {
        var entries = accounts
            .Select(account => (Account: account, Snapshot: cache.Load(account.AccountId)))
            .Select(entry => (entry.Account, Snapshot: entry.Snapshot?.ProjectedTo(now)))
            .ToList();
        var shown = AccountArrangement.Arrange(
            entries,
            entry => entry.Account.AccountId,
            entry => entry.Snapshot is null ? 0 : LimitPriority.Rank(entry.Snapshot),
            settings);
        return [.. shown, .. entries.Where(entry => !shown.Contains(entry))];
    }

    private static AccountCardData Card(
        (AccountInfo Account, LimitSnapshot? Snapshot) entry,
        UsageQuery query,
        UsageFilter usage,
        HakariSettings settings,
        DateTimeOffset now)
    {
        var account = entry.Account;
        var nickname = settings.NicknameOf(account.AccountId);
        var isHidden = settings.HiddenAccounts.Contains(account.AccountId);
        var (badge, tone) = Badge(entry.Snapshot, isHidden, now);
        var cost = query.Total(usage with { AccountId = account.AccountId }).Cost;
        return new AccountCardData(
            AccountLabels.Full(account, nickname),
            Detail(account, nickname),
            badge,
            tone,
            entry.Snapshot is null
                ? []
                : FlyoutDataLoader.LimitRowsOf((account, entry.Snapshot), settings, query, now),
            MoneyText.Format(cost, query.Currency));
    }

    /// <summary>With a nickname as the title, the email moves down here.</summary>
    private static string Detail(AccountInfo account, string? nickname) =>
        string.Join(
            DetailSeparator,
            new[]
            {
                nickname is null ? null : account.Email,
                PlanNames.Short(account.Plan),
                OrganizationOf(account),
            }.Where(part => !string.IsNullOrEmpty(part)));

    /// <summary>A personal organization is named after the email, which is said already.</summary>
    private static string? OrganizationOf(AccountInfo account) =>
        account.Email is { } email
        && account.OrganizationName?.StartsWith(email, StringComparison.OrdinalIgnoreCase) == true
            ? null
            : account.OrganizationName;

    private static (string Text, BadgeTone Tone) Badge(
        LimitSnapshot? snapshot,
        bool isHidden,
        DateTimeOffset now)
    {
        if (isHidden)
        {
            return (Texts.Get("dashboard.accounts.hidden"), BadgeTone.Neutral);
        }

        if (snapshot is null)
        {
            return (Texts.Get("dashboard.accounts.noLimits"), BadgeTone.Neutral);
        }

        var age = now - snapshot.FetchedAt;
        if (age < LimitsAreLive)
        {
            return (Texts.Get("dashboard.accounts.limitsOn"), BadgeTone.Accent);
        }

        var when = FlyoutDataLoader.LastUsedText(age);
        return (Texts.Format("dashboard.accounts.lastKnown", when), BadgeTone.Neutral);
    }
}
