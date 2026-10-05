using Microsoft.Data.Sqlite;

namespace Hakari.Core.Indexing;

internal sealed class TrackedFileRepository(SqliteConnection connection)
{
    private const string SelectBySourceSql = """
        SELECT path, source_id, size, modified_ticks, indexed_offset
        FROM tracked_files
        WHERE source_id = $sourceId
        """;

    private const string SelectByPathSql = """
        SELECT path, source_id, size, modified_ticks, indexed_offset
        FROM tracked_files
        WHERE path = $path
        """;

    private const string UpsertSql = """
        INSERT INTO tracked_files (path, source_id, size, modified_ticks, indexed_offset)
        VALUES ($path, $sourceId, $size, $modifiedTicks, $indexedOffset)
        ON CONFLICT (path) DO UPDATE SET
            source_id = excluded.source_id,
            size = excluded.size,
            modified_ticks = excluded.modified_ticks,
            indexed_offset = excluded.indexed_offset
        """;

    public Dictionary<string, TrackedFile> LoadForSource(string sourceId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = SelectBySourceSql;
        command.Parameters.AddWithValue("$sourceId", sourceId);

        var files = new Dictionary<string, TrackedFile>(StringComparer.OrdinalIgnoreCase);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var file = ReadFile(reader);
            files[file.Path] = file;
        }

        return files;
    }

    private static TrackedFile ReadFile(SqliteDataReader reader) => new(
        Path: reader.GetString(0),
        SourceId: reader.GetString(1),
        Size: reader.GetInt64(2),
        ModifiedTicks: reader.GetInt64(3),
        IndexedOffset: reader.GetInt64(4));

    public TrackedFile? Find(string path)
    {
        using var command = connection.CreateCommand();
        command.CommandText = SelectByPathSql;
        command.Parameters.AddWithValue("$path", path);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadFile(reader) : null;
    }

    public void Save(TrackedFile file, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = UpsertSql;
        command.Parameters.AddWithValue("$path", file.Path);
        command.Parameters.AddWithValue("$sourceId", file.SourceId);
        command.Parameters.AddWithValue("$size", file.Size);
        command.Parameters.AddWithValue("$modifiedTicks", file.ModifiedTicks);
        command.Parameters.AddWithValue("$indexedOffset", file.IndexedOffset);
        command.ExecuteNonQuery();
    }
}
