using Microsoft.Data.Sqlite;

namespace Hakari.Core.Indexing;

public sealed class IndexStore : IDisposable
{
    public const string InMemoryPath = ":memory:";

    public IndexStore(string databasePath)
    {
        EnsureDirectoryExists(databasePath);
        Connection = new SqliteConnection($"Data Source={databasePath}");
        Connection.Open();
        Execute(IndexSchema.ConnectionPragmas);
        Execute(IndexSchema.CreateTables);
    }

    public SqliteConnection Connection { get; }

    public void DeleteAll() => Execute(IndexSchema.DeleteAll);

    public void Dispose() => Connection.Dispose();

    private void Execute(string sql)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void EnsureDirectoryExists(string databasePath)
    {
        if (databasePath == InMemoryPath)
        {
            return;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }
    }
}
