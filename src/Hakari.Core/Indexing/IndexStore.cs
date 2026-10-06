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
        CreatedAt = databasePath == InMemoryPath
            ? null
            : new DateTimeOffset(File.GetCreationTimeUtc(databasePath), TimeSpan.Zero);
    }

    private IndexStore(SqliteConnection readOnlyConnection) => Connection = readOnlyConnection;

    /// <summary>
    /// For the window process: reads while Hakari.exe writes (WAL), never changes the schema,
    /// and waits briefly instead of failing when a write is in progress.
    /// </summary>
    public static IndexStore OpenReadOnly(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            DefaultTimeout = ReadOnlyBusyTimeoutSeconds,
        };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return new IndexStore(connection);
    }

    private const int ReadOnlyBusyTimeoutSeconds = 2;

    public SqliteConnection Connection { get; }

    /// <summary>When the index file was first made, which is when Hakari was first run.</summary>
    public DateTimeOffset? CreatedAt { get; }

    public void DeleteAll() => Execute(IndexSchema.DeleteAll);

    /// <summary>A setting the index remembers about how it was built, or null.</summary>
    public string? GetOption(string name)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = "SELECT value FROM index_options WHERE name = $name";
        command.Parameters.AddWithValue("$name", name);
        return command.ExecuteScalar() as string;
    }

    public void SetOption(string name, string value)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = """
            INSERT INTO index_options (name, value) VALUES ($name, $value)
            ON CONFLICT (name) DO UPDATE SET value = excluded.value
            """;
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

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
