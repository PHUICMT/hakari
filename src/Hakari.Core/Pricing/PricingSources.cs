using System.Text.Json;
using Hakari.Core.Configuration;

namespace Hakari.Core.Pricing;

/// <summary>
/// Where prices come from: the table shipped with Hakari, a newer copy fetched from the
/// project when the user asks ("check now"), and the user's own prices on top, which can
/// change a model's price or add one that is not listed yet. A file that cannot be read is
/// left out rather than breaking the totals.
/// </summary>
public static class PricingSources
{
    public const string LatestAddress =
        "https://raw.githubusercontent.com/PHUICMT/hakari/master/data/pricing.json";

    private const string DownloadedFileName = "pricing-latest.json";
    private const string CustomFileName = "custom-prices.json";

    public static string DownloadedPath =>
        Path.Combine(HakariPaths.DataDirectory, DownloadedFileName);

    public static string CustomPath => Path.Combine(HakariPaths.DataDirectory, CustomFileName);

    private static readonly Lock CacheLock = new();
    private static (DateTime Version, PricingTable Table)? cached;

    /// <summary>
    /// The newer of the shipped and fetched tables, with the user's prices. Read from disk
    /// only when one of those files changed; the table is shared and must not be changed.
    /// </summary>
    public static PricingTable LoadCurrent()
    {
        var version = Version();
        lock (CacheLock)
        {
            if (cached is { } hit && hit.Version == version)
            {
                return hit.Table;
            }
        }

        var table = Read();
        lock (CacheLock)
        {
            cached = (version, table);
        }

        return table;
    }

    private static PricingTable Read()
    {
        var table = Newest(Shipped(), TryLoad(DownloadedPath));
        if (TryLoad(CustomPath) is { Models: not null } custom)
        {
            foreach (var (model, prices) in custom.Models)
            {
                // A hand-written entry with a missing list or a missing price is left out.
                var usable = prices?.Where(price => price is not null).ToList();
                if (!string.IsNullOrWhiteSpace(model) && usable is { Count: > 0 })
                {
                    table.Models[model] = usable;
                }
            }
        }

        return table;
    }

    /// <summary>Changes when a fetched table or the user's prices change, for a reload.</summary>
    public static DateTime Version() => new[] { DownloadedPath, CustomPath }
        .Select(path => File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue)
        .Max();

    /// <summary>
    /// Keeps a fetched table when it reads as a price table and is newer than what Hakari
    /// already has; anything else is turned away.
    /// </summary>
    /// <returns>True when the fetched table was kept.</returns>
    public static bool Accept(string json)
    {
        PricingTable fetched;
        try
        {
            fetched = PricingTable.Load(json);
        }
        catch (JsonException)
        {
            return false;
        }

        var current = Newest(Shipped(), TryLoad(DownloadedPath));
        if (fetched.Models.Count == 0
            || string.CompareOrdinal(fetched.Updated, current.Updated) <= 0)
        {
            return false;
        }

        Directory.CreateDirectory(HakariPaths.DataDirectory);
        File.WriteAllText(DownloadedPath, json);
        return true;
    }

    /// <summary>Starts the user's price file with one example to copy, if there is none.</summary>
    public static string EnsureCustomFile()
    {
        if (!File.Exists(CustomPath))
        {
            Directory.CreateDirectory(HakariPaths.DataDirectory);
            File.WriteAllText(CustomPath, CustomTemplate);
        }

        return CustomPath;
    }

    private const string CustomTemplate = """
        {
          "models": {
            "example-model-name": [
              {
                "input": 3.0,
                "output": 15.0,
                "cacheWriteFiveMinutes": 3.75,
                "cacheWriteOneHour": 6.0,
                "cacheRead": 0.3
              }
            ]
          }
        }
        """;

    /// <summary>The table shipped with Hakari, or an empty one if it cannot be read.</summary>
    private static PricingTable Shipped() =>
        TryLoad(PricingFiles.BundledPath) ?? new PricingTable();

    private static PricingTable Newest(PricingTable bundled, PricingTable? downloaded) =>
        downloaded is { Models.Count: > 0 }
            && string.CompareOrdinal(downloaded.Updated, bundled.Updated) > 0
                ? downloaded
                : bundled;

    private static PricingTable? TryLoad(string path)
    {
        try
        {
            return File.Exists(path) ? PricingTable.LoadFile(path) : null;
        }
        catch (Exception exception) when (exception is JsonException or IOException
            or UnauthorizedAccessException or NotSupportedException
            or InvalidOperationException)
        {
            return null;
        }
    }
}
