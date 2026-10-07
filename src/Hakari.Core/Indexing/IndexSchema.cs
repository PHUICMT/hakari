namespace Hakari.Core.Indexing;

internal static class IndexSchema
{
    /// <summary>
    /// Bump when usage columns change; an index with another version is rebuilt from the logs.
    /// Exchange rates are kept across rebuilds because they come from the network.
    /// </summary>
    public const int CurrentVersion = 3;

    public const string ConnectionPragmas = """
        PRAGMA journal_mode = WAL;
        PRAGMA synchronous = NORMAL;
        """;

    public const string ReadVersion = "PRAGMA user_version;";

    public const string WriteVersionFormat = "PRAGMA user_version = {0};";

    /// <summary>
    /// Tables rebuilt from the logs, and caches, are dropped. Account history can't be rebuilt
    /// from the logs, so it survives a schema change.
    /// </summary>
    public const string DropTables = """
        DROP TABLE IF EXISTS usage_records;
        DROP TABLE IF EXISTS tracked_files;
        DROP TABLE IF EXISTS account_limits;
        DROP TABLE IF EXISTS session_titles;
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

        CREATE TABLE IF NOT EXISTS session_titles (
            session_id TEXT PRIMARY KEY,
            title TEXT NOT NULL,
            is_custom INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS thinking_marks (
            deduplication_key TEXT PRIMARY KEY
        );

        CREATE TABLE IF NOT EXISTS index_options (
            name TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS account_limits (
            account_id TEXT PRIMARY KEY,
            snapshot_json TEXT NOT NULL,
            fetched_at_ms INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS account_limit_failures (
            account_id TEXT PRIMARY KEY,
            failure TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS limit_history (
            account_id TEXT NOT NULL,
            kind TEXT NOT NULL,
            taken_at_ms INTEGER NOT NULL,
            percent REAL NOT NULL,
            PRIMARY KEY (account_id, kind, taken_at_ms)
        ) WITHOUT ROWID;

        CREATE TABLE IF NOT EXISTS accounts (
            account_id TEXT PRIMARY KEY,
            email TEXT,
            display_name TEXT,
            organization_name TEXT,
            plan TEXT NOT NULL,
            last_seen_ms INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS account_periods (
            source_id TEXT NOT NULL,
            account_id TEXT NOT NULL,
            started_at_ms INTEGER NOT NULL,
            PRIMARY KEY (source_id, started_at_ms)
        ) WITHOUT ROWID;

        CREATE TABLE IF NOT EXISTS exchange_rates (
            currency TEXT NOT NULL,
            day TEXT NOT NULL,
            units_per_dollar TEXT NOT NULL,
            source TEXT NOT NULL,
            PRIMARY KEY (currency, day)
        );
        """;

    public const string DeleteAll = """
        DELETE FROM usage_records;
        DELETE FROM tracked_files;
        DELETE FROM session_titles;
        DELETE FROM thinking_marks;
        """;
}
