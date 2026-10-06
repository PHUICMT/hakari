using Hakari.Core.Indexing;

namespace Hakari.Core.Querying;

/// <summary>The titles of sessions, when the user asked for them to be kept.</summary>
public static class SessionTitles
{
    private const string Sql = "SELECT session_id, title FROM session_titles";

    public static IReadOnlyDictionary<string, string> Load(IndexStore store)
    {
        using var command = store.Connection.CreateCommand();
        command.CommandText = Sql;
        using var reader = command.ExecuteReader();
        var titles = new Dictionary<string, string>();
        while (reader.Read())
        {
            titles[reader.GetString(0)] = reader.GetString(1);
        }

        return titles;
    }
}
