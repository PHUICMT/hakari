using System.Text.Json;
using System.Text.Json.Serialization;
using Hakari.Core.Configuration;

namespace Hakari.Core.Accounts;

/// <summary>One plan as the catalog lists it.</summary>
/// <param name="Match">Words that, found in the account's tier or subscription type, mean this
/// plan; plans are tried in the catalog's order, so a more specific one comes first.</param>
/// <param name="MonthlyDollars">The public monthly price; null when it is negotiated.</param>
public sealed record PlanEntry(
    SubscriptionPlan Plan,
    string Name,
    IReadOnlyList<string> Match,
    decimal? MonthlyDollars = null);

/// <summary>
/// Plan names, prices and how to recognize them, from data rather than code: Anthropic
/// renames and reprices plans (Max 20x became Ultra in October 2026), and a new list on the
/// project's GitHub reaches every copy without a new version. The newer of the shipped list
/// and a fetched one is used.
/// </summary>
public sealed class PlanCatalog
{
    public const string BundledFileName = "plans.json";
    public const string LatestAddress =
        "https://raw.githubusercontent.com/PHUICMT/hakari/master/data/plans.json";

    private const string DownloadedFileName = "plans-latest.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly Lock CacheLock = new();
    private static (long Stamp, PlanCatalog Catalog)? cached;

    [JsonPropertyName("updated")]
    public string Updated { get; init; } = string.Empty;

    [JsonPropertyName("plans")]
    public IReadOnlyList<PlanEntry> Plans { get; init; } = [];

    public static string BundledPath => Path.Combine(AppContext.BaseDirectory, BundledFileName);

    public static string DownloadedPath =>
        Path.Combine(HakariPaths.DataDirectory, DownloadedFileName);

    /// <summary>The list in use, read again only when the fetched copy changes.</summary>
    public static PlanCatalog Current
    {
        get
        {
            var stamp = File.Exists(DownloadedPath)
                ? File.GetLastWriteTimeUtc(DownloadedPath).Ticks
                : 0;
            lock (CacheLock)
            {
                if (cached is { } hit && hit.Stamp == stamp)
                {
                    return hit.Catalog;
                }
            }

            var catalog = Newest(TryLoad(BundledPath), TryLoad(DownloadedPath));
            lock (CacheLock)
            {
                cached = (stamp, catalog);
            }

            return catalog;
        }
    }

    public static PlanCatalog Load(string json) =>
        JsonSerializer.Deserialize<PlanCatalog>(json, Options) ?? new PlanCatalog();

    public PlanEntry? Find(SubscriptionPlan plan) =>
        Plans.FirstOrDefault(entry => entry.Plan == plan);

    /// <summary>The first plan, in the catalog's order, whose words appear in the hints.</summary>
    public SubscriptionPlan Detect(string hints) =>
        Plans.FirstOrDefault(entry => entry.Match.Any(word =>
            hints.Contains(word, StringComparison.OrdinalIgnoreCase)))?.Plan
        ?? SubscriptionPlan.Unknown;

    /// <summary>Keeps a fetched list that reads as one and is newer than the one in use.</summary>
    /// <returns>True when it was kept.</returns>
    public static bool Accept(string json)
    {
        PlanCatalog fetched;
        try
        {
            fetched = Load(json);
        }
        catch (JsonException)
        {
            return false;
        }

        if (fetched.Plans.Count == 0
            || string.CompareOrdinal(fetched.Updated, Current.Updated) <= 0)
        {
            return false;
        }

        Directory.CreateDirectory(HakariPaths.DataDirectory);
        File.WriteAllText(DownloadedPath, json);
        return true;
    }

    private static PlanCatalog Newest(PlanCatalog? bundled, PlanCatalog? downloaded) =>
        downloaded is { Plans.Count: > 0 }
            && string.CompareOrdinal(downloaded.Updated, bundled?.Updated) > 0
                ? downloaded
                : bundled ?? new PlanCatalog();

    private static PlanCatalog? TryLoad(string path)
    {
        try
        {
            return File.Exists(path) ? Load(File.ReadAllText(path)) : null;
        }
        catch (Exception exception) when (exception is JsonException or IOException
            or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }
}
