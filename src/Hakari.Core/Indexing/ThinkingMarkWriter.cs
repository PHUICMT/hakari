using Microsoft.Data.Sqlite;

namespace Hakari.Core.Indexing;

/// <summary>Notes that a response had a thinking part, by its key alone.</summary>
internal sealed class ThinkingMarkWriter : IDisposable
{
    private const string InsertSql = """
        INSERT OR IGNORE INTO thinking_marks (deduplication_key) VALUES ($key)
        """;

    private readonly SqliteCommand command;

    public ThinkingMarkWriter(SqliteConnection connection, SqliteTransaction transaction)
    {
        command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = InsertSql;
        command.Parameters.Add("$key", SqliteType.Text);
    }

    public void Mark(string deduplicationKey)
    {
        command.Parameters["$key"].Value = deduplicationKey;
        command.ExecuteNonQuery();
    }

    public void Dispose() => command.Dispose();
}