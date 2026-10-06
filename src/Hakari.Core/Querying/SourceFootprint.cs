using Hakari.Core.Indexing;

namespace Hakari.Core.Querying;

/// <summary>How many log files a source has and how large they are, as last indexed.</summary>
public sealed record SourceFootprint(string SourceId, long Files, long Bytes)
{
    private const string Sql = """
        SELECT source_id, count(*), sum(size)
        FROM tracked_files
        GROUP BY source_id
        """;

    public static IReadOnlyList<SourceFootprint> Load(IndexStore store)
    {
        using var command = store.Connection.CreateCommand();
        command.CommandText = Sql;
        using var reader = command.ExecuteReader();
        var footprints = new List<SourceFootprint>();
        while (reader.Read())
        {
            footprints.Add(new SourceFootprint(
                SourceId: reader.GetString(0),
                Files: reader.GetInt64(1),
                Bytes: reader.GetInt64(2)));
        }

        return footprints;
    }
}
