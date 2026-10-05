using System.Text.Json;
using Hakari.Core.Indexing;

namespace Hakari.Core.Limits;

/// <summary>The last limits fetched per source, for when the sign-in has expired.</summary>
public sealed class LimitCache(IndexStore store)
{
    private const string SelectSql =
        "SELECT snapshot_json FROM account_limits WHERE source_id = $sourceId";

    private const string UpsertSql = """
        INSERT INTO account_limits (source_id, snapshot_json, fetched_at_ms)
        VALUES ($sourceId, $snapshotJson, $fetchedAtMs)
        ON CONFLICT (source_id) DO UPDATE SET
            snapshot_json = excluded.snapshot_json,
            fetched_at_ms = excluded.fetched_at_ms
        """;

    public LimitSnapshot? Load(string sourceId)
    {
        using var command = store.Connection.CreateCommand();
        command.CommandText = SelectSql;
        command.Parameters.AddWithValue("$sourceId", sourceId);
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

    public void Save(string sourceId, LimitSnapshot snapshot)
    {
        using var command = store.Connection.CreateCommand();
        command.CommandText = UpsertSql;
        command.Parameters.AddWithValue("$sourceId", sourceId);
        command.Parameters.AddWithValue("$snapshotJson", JsonSerializer.Serialize(snapshot));
        var fetchedAtMilliseconds = snapshot.FetchedAt.ToUnixTimeMilliseconds();
        command.Parameters.AddWithValue("$fetchedAtMs", fetchedAtMilliseconds);
        command.ExecuteNonQuery();
    }
}
