using System.Globalization;

namespace Hakari.Core.Pricing;

/// <summary>
/// Sorts requests by prompt length into the bands that long-prompt prices start at, so
/// requests can be summed per band and each band priced at its own rate. A prompt is the
/// input with every cache write and read, as the provider counts it.
/// </summary>
public static class PromptSize
{
    /// <summary>The lengths a price can change at, shortest first.</summary>
    public static readonly long[] Thresholds = [100_000, 200_000];

    /// <summary>The longest threshold the prompt is over, or 0 for none.</summary>
    public static long Over(long promptTokens) =>
        Thresholds.LastOrDefault(threshold => promptTokens > threshold);

    /// <summary>The same band in SQL, for a summary to group by.</summary>
    public static string Sql(string promptTokensSql) =>
        "CASE "
        + string.Concat(Thresholds.Reverse().Select(threshold =>
            $"WHEN {promptTokensSql} > {Number(threshold)} THEN {Number(threshold)} "))
        + "ELSE 0 END";

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
}
