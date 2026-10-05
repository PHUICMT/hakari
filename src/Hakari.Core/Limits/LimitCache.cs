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

    public void Save(string accountId, LimitSnapshot snapshot)
    {
        using var command = store.Connection.CreateCommand();
        command.CommandText = UpsertSql;
        command.Parameters.AddWithValue("$accountId", accountId);
        command.Parameters.AddWithValue("$snapshotJson", JsonSerializer.Serialize(snapshot));
        var fetchedAtMilliseconds = snapshot.FetchedAt.ToUnixTimeMilliseconds();
        command.Parameters.AddWithValue("$fetchedAtMs", fetchedAtMilliseconds);
        command.ExecuteNonQuery();
    }
}
