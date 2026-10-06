using System.Text.Json;
using Hakari.Core.Indexing;

namespace Hakari.Core.Limits;

/// <summary>The last limits fetched per account, for when no sign-in is valid.</summary>
public sealed class LimitCache(IndexStore store)
{
    private const string SelectSql =
        "SELECT snapshot_json FROM account_limits WHERE account_id = $accountId";

    private const string UpsertSql = """
        INSERT INTO account_limits (account_id, snapshot_json, fetched_at_ms)
        VALUES ($accountId, $snapshotJson, $fetchedAtMs)
        ON CONFLICT (account_id) DO UPDATE SET
            snapshot_json = excluded.snapshot_json,
            fetched_at_ms = excluded.fetched_at_ms
        """;

    private const string UpsertFailureSql = """
        INSERT INTO account_limit_failures (account_id, failure)
        VALUES ($accountId, $failure)
        ON CONFLICT (account_id) DO UPDATE SET failure = excluded.failure
        """;

    private const string SelectFailureSql =
        "SELECT failure FROM account_limit_failures WHERE account_id = $accountId";

    private const string DeleteFailureSql =
        "DELETE FROM account_limit_failures WHERE account_id = $accountId";

    public LimitSnapshot? Load(string accountId)
    {
        using var command = store.Connection.CreateCommand();
        command.CommandText = SelectSql;
        command.Parameters.AddWithValue("$accountId", accountId);
        if (command.ExecuteScalar() is not string json)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<LimitSnapshot>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Why the last attempt to read an account's limits failed.</summary>
    public LimitFailure LoadFailure(string accountId)
    {
        using var command = store.Connection.CreateCommand();
        command.CommandText = SelectFailureSql;
        command.Parameters.AddWithValue("$accountId", accountId);
        return command.ExecuteScalar() is string name
            && Enum.TryParse<LimitFailure>(name, out var failure)
            ? failure
            : LimitFailure.None;
    }

    /// <summary>Remembers why a read failed; None clears it.</summary>
    public void SaveFailure(string accountId, LimitFailure failure)
    {
        using var command = store.Connection.CreateCommand();
        command.CommandText = failure == LimitFailure.None ? DeleteFailureSql : UpsertFailureSql;
        command.Parameters.AddWithValue("$accountId", accountId);
        if (failure != LimitFailure.None)
        {
            command.Parameters.AddWithValue("$failure", failure.ToString());
        }

        command.ExecuteNonQuery();
    }

    public void Save(string accountId, LimitSnapshot snapshot)
    {
        using var command = store.Connection.CreateCommand();
        command.CommandText = UpsertSql;
        command.Parameters.AddWithValue("$accountId", accountId);
        command.Parameters.AddWithValue("$snapshotJson", JsonSerializer.Serialize(snapshot));
        var fetchedAtMilliseconds = snapshot.FetchedAt.ToUnixTimeMilliseconds();
        command.Parameters.AddWithValue("$fetchedAtMs", fetchedAtMilliseconds);
        command.ExecuteNonQuery();
        SaveFailure(accountId, LimitFailure.None);
    }
}
