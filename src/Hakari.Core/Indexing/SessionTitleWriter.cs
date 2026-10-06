using Hakari.Core.Parsing;
using Microsoft.Data.Sqlite;

namespace Hakari.Core.Indexing;

/// <summary>
/// Keeps each session's title. A name the user gave outranks the one Claude Code made up, and
/// a later title of the same kind replaces an earlier one.
/// </summary>
internal sealed class SessionTitleWriter : IDisposable
{
    private const string UpsertSql = """
        INSERT INTO session_titles (session_id, title, is_custom)
        VALUES ($sessionId, $title, $isCustom)
        ON CONFLICT (session_id) DO UPDATE SET
            title = excluded.title,
            is_custom = excluded.is_custom
        WHERE excluded.is_custom >= is_custom
        """;

    private readonly SqliteCommand command;

    public SessionTitleWriter(SqliteConnection connection, SqliteTransaction transaction)
    {
        command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = UpsertSql;
        command.Parameters.Add("$sessionId", SqliteType.Text);
        command.Parameters.Add("$title", SqliteType.Text);
        command.Parameters.Add("$isCustom", SqliteType.Integer);
    }

    public void Upsert(SessionTitle title)
    {
        command.Parameters["$sessionId"].Value = title.SessionId;
        command.Parameters["$title"].Value = title.Title;
        command.Parameters["$isCustom"].Value = title.IsCustom ? 1 : 0;
        command.ExecuteNonQuery();
    }

    public void Dispose() => command.Dispose();
}
