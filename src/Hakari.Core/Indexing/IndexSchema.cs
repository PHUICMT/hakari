namespace Hakari.Core.Indexing;

internal static class IndexSchema
{
    /// <summary>Bump when columns change; an index with another version is rebuilt.</summary>
    public const int CurrentVersion = 2;

    public const string ConnectionPragmas = """
        PRAGMA journal_mode = WAL;
        PRAGMA synchronous = NORMAL;
        """;

    public const string ReadVersion = "PRAGMA user_version;";

    public const string WriteVersionFormat = "PRAGMA user_version = {0};";

    public const string DropTables = """
        DROP TABLE IF EXISTS usage_records;
        DROP TABLE IF EXISTS tracked_files;
        """;

    public const string CreateTables = """
        CREATE TABLE IF NOT EXISTS tracked_files (
            path TEXT PRIMARY KEY,
            source_id TEXT NOT NULL,
            size INTEGER NOT NULL,
            modified_ticks INTEGER NOT NULL,
            indexed_offset INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS usage_records (
            deduplication_key TEXT PRIMARY KEY,
            source_id TEXT NOT NULL,
            timestamp_ms INTEGER NOT NULL,
            model TEXT NOT NULL,
            session_id TEXT NOT NULL,
            project TEXT,
            git_branch TEXT,
            is_sidechain INTEGER NOT NULL,
            speed TEXT NOT NULL,
            inference_geography TEXT,
            input_tokens INTEGER NOT NULL,
            output_tokens INTEGER NOT NULL,
            cache_write_five_minutes INTEGER NOT NULL,
            cache_write_one_hour INTEGER NOT NULL,
            cache_read_tokens INTEGER NOT NULL,
            web_search_requests INTEGER NOT NULL
        );

        CREATE INDEX IF NOT EXISTS index_usage_timestamp
            ON usage_records (timestamp_ms);

        CREATE INDEX IF NOT EXISTS index_usage_source_timestamp
            ON usage_records (source_id, timestamp_ms);
        """;

    public const string DeleteAll = """
        DELETE FROM usage_records;
        DELETE FROM tracked_files;
        """;
}
