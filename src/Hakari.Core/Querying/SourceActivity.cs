using Hakari.Core.Indexing;

namespace Hakari.Core.Querying;

/// <summary>When each source last produced usage, for the "where numbers come from" list.</summary>
public sealed record SourceActivity(string SourceId, DateTimeOffset LastUsage, long Responses)
{
    private const string Sql = """
        SELECT source_id, max(timestamp_ms), count(*)
        FROM usage_records
        GROUP BY source_id
        ORDER BY max(timestamp_ms) DESC
        """;

    public static IReadOnlyList<SourceActivity> Load(IndexStore store)
    {
        using var command = store.Connection.CreateCommand();
        command.CommandText = Sql;
        using var reader = command.ExecuteReader();
        var activities = new List<SourceActivity>();
        while (reader.Read())
        {
            activities.Add(new SourceActivity(
                SourceId: reader.GetString(0),
                LastUsage: DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1)),
                Responses: reader.GetInt64(2)));
        }

        return activities;
    }
}
