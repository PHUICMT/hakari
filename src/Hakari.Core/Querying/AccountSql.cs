namespace Hakari.Core.Querying;

internal static class AccountSql
{
    /// <summary>
    /// The account a usage record belongs to: the latest period of its source that started
    /// at or before the record. Sources never seen signed in give an empty id.
    /// </summary>
    public const string AccountOfRecord = """
        coalesce((
            SELECT period.account_id
            FROM account_periods AS period
            WHERE period.source_id = usage_records.source_id
                AND period.started_at_ms <= usage_records.timestamp_ms
            ORDER BY period.started_at_ms DESC
            LIMIT 1), '')
        """;
}
