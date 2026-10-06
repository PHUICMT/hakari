using Hakari.Core.Indexing;
using Microsoft.Data.Sqlite;

namespace Hakari.Core.Accounts;

public sealed class AccountRepository(IndexStore store)
{
    private const string UpsertAccountSql = """
        INSERT INTO accounts (
            account_id, email, display_name, organization_name, plan, last_seen_ms)
        VALUES ($accountId, $email, $displayName, $organizationName, $plan, $lastSeenMs)
        ON CONFLICT (account_id) DO UPDATE SET
            email = excluded.email,
            display_name = excluded.display_name,
            organization_name = excluded.organization_name,
            plan = excluded.plan,
            last_seen_ms = excluded.last_seen_ms
        """;

    private const string SelectAccountsSql = """
        SELECT account_id, email, display_name, organization_name, plan
        FROM accounts
        ORDER BY last_seen_ms DESC
        """;

    private const string SelectLatestPeriodSql = """
        SELECT account_id, started_at_ms
        FROM account_periods
        WHERE source_id = $sourceId
        ORDER BY started_at_ms DESC
        LIMIT 1
        """;

    private const string RebaseOpeningPeriodsSql = """
        UPDATE OR IGNORE account_periods
        SET started_at_ms = min($installedAtMs, coalesce((
            SELECT min(later.started_at_ms)
            FROM account_periods AS later
            WHERE later.source_id = account_periods.source_id
                AND later.started_at_ms > 0), $installedAtMs))
        WHERE started_at_ms = 0
        """;

    private const string InsertPeriodSql = """
        INSERT OR IGNORE INTO account_periods (source_id, account_id, started_at_ms)
        VALUES ($sourceId, $accountId, $startedAtMs)
        """;

    /// <summary>
    /// Earlier versions gave the first account of a source all of its earlier history, with a
    /// period starting at the epoch. That history belongs to no account, so those periods are
    /// moved to when Hakari was first installed, but never past the next period's start.
    /// </summary>
    public void StartOpeningPeriodsAt(DateTimeOffset installedAt)
    {
        using var command = store.Connection.CreateCommand();
        command.CommandText = RebaseOpeningPeriodsSql;
        command.Parameters.AddWithValue("$installedAtMs", installedAt.ToUnixTimeMilliseconds());
        command.ExecuteNonQuery();
    }

    public void SaveAccount(AccountInfo account, DateTimeOffset seenAt)
    {
        using var command = store.Connection.CreateCommand();
        command.CommandText = UpsertAccountSql;
        command.Parameters.AddWithValue("$accountId", account.AccountId);
        command.Parameters.AddWithValue("$email", OrNull(account.Email));
        command.Parameters.AddWithValue("$displayName", OrNull(account.DisplayName));
        command.Parameters.AddWithValue("$organizationName", OrNull(account.OrganizationName));
        command.Parameters.AddWithValue("$plan", account.Plan.ToString());
        command.Parameters.AddWithValue("$lastSeenMs", seenAt.ToUnixTimeMilliseconds());
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<AccountInfo> ListAccounts()
    {
        using var command = store.Connection.CreateCommand();
        command.CommandText = SelectAccountsSql;
        using var reader = command.ExecuteReader();
        var accounts = new List<AccountInfo>();
        while (reader.Read())
        {
            accounts.Add(ReadAccount(reader));
        }

        return accounts;
    }

    public AccountPeriod? LatestPeriod(string sourceId)
    {
        using var command = store.Connection.CreateCommand();
        command.CommandText = SelectLatestPeriodSql;
        command.Parameters.AddWithValue("$sourceId", sourceId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var startedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1));
        return new AccountPeriod(sourceId, reader.GetString(0), startedAt);
    }

    public void AddPeriod(AccountPeriod period)
    {
        using var command = store.Connection.CreateCommand();
        command.CommandText = InsertPeriodSql;
        command.Parameters.AddWithValue("$sourceId", period.SourceId);
        command.Parameters.AddWithValue("$accountId", period.AccountId);
        command.Parameters.AddWithValue("$startedAtMs", period.StartedAt.ToUnixTimeMilliseconds());
        command.ExecuteNonQuery();
    }

    private static object OrNull(string? value) => (object?)value ?? DBNull.Value;

    private static AccountInfo ReadAccount(SqliteDataReader reader) => new(
        AccountId: reader.GetString(0),
        Email: reader.IsDBNull(1) ? null : reader.GetString(1),
        DisplayName: reader.IsDBNull(2) ? null : reader.GetString(2),
        OrganizationName: reader.IsDBNull(3) ? null : reader.GetString(3),
        Plan: Enum.TryParse<SubscriptionPlan>(reader.GetString(4), out var plan)
            ? plan
            : SubscriptionPlan.Unknown);
}
