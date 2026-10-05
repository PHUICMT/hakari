using System.Globalization;
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
        MigrateSchema();
    }

    public SqliteConnection Connection { get; }

    public void DeleteAll() => Execute(IndexSchema.DeleteAll);

    public void Dispose() => Connection.Dispose();

    /// <summary>
    /// The index only caches what the logs already hold, so an outdated schema is dropped and
    /// rebuilt from the logs instead of being migrated column by column.
    /// </summary>
    private void MigrateSchema()
    {
        if (ReadSchemaVersion() != IndexSchema.CurrentVersion)
        {
            Execute(IndexSchema.DropTables);
        }

        Execute(IndexSchema.CreateTables);
        var writeVersion = string.Format(
            CultureInfo.InvariantCulture,
            IndexSchema.WriteVersionFormat,
            IndexSchema.CurrentVersion);
        Execute(writeVersion);
    }

    private long ReadSchemaVersion()
    {
        using var command = Connection.CreateCommand();
        command.CommandText = IndexSchema.ReadVersion;
        return (long)(command.ExecuteScalar() ?? 0L);
    }

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
