using System.Text.Json;
using Hakari.Core.Configuration;

namespace Hakari.Core.Pricing;

/// <summary>
/// Where prices come from: the table shipped with Hakari, a newer copy fetched from the
/// project (daily, or when asked), prices from LiteLLM's public list for models neither
/// table has yet, and the user's own prices on top, which can change a model's price or add
/// one that is not listed. A file that cannot be read is left out rather than breaking the
/// totals.
/// </summary>
public static class PricingSources
{
    public const string LatestAddress =
        "https://raw.githubusercontent.com/PHUICMT/hakari/master/data/pricing.json";

    private const string DownloadedFileName = "pricing-latest.json";
    private const string CustomFileName = "custom-prices.json";
    private const string FallbackFileName = "pricing-fallback.json";

    public static string DownloadedPath =>
        Path.Combine(HakariPaths.DataDirectory, DownloadedFileName);

    public static string FallbackPath => Path.Combine(HakariPaths.DataDirectory, FallbackFileName);

    public static string CustomPath => Path.Combine(HakariPaths.DataDirectory, CustomFileName);

    private static readonly Lock CacheLock = new();
    private static (string Version, PricingTable Table)? cached;

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
        if (TryLoad(FallbackPath) is { Models: not null } fallback)
        {
            // Only for models the project's table does not price: it is checked by hand.
            foreach (var (model, prices) in fallback.Models)
            {
                if (!table.IsPriced(model) && prices is { Count: > 0 })
                {
                    table.Models[model] = prices;
                }
            }
        }

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

    /// <summary>
    /// Changes when a fetched table or the user's prices are written, replaced or deleted:
    /// each file's presence and write time, not only the newest, so removing the newer-dated
    /// file is noticed too.
    /// </summary>
    public static string Version() => string.Join(
        '|',
        new[] { DownloadedPath, FallbackPath, CustomPath }.Select(path => File.Exists(path)
            ? File.GetLastWriteTimeUtc(path).Ticks.ToString(
                System.Globalization.CultureInfo.InvariantCulture)
            : "-"));

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

    /// <summary>
    /// Adds prices found elsewhere for models the tables lack, keeping those found before.
    /// </summary>
    /// <returns>True when a model was added or its price changed.</returns>
    public static bool AcceptFallback(IReadOnlyDictionary<string, List<ModelPrice>> found)
    {
        var kept = TryLoad(FallbackPath) ?? new PricingTable();
        var changed = false;
        foreach (var (model, prices) in found)
        {
            if (!kept.Models.TryGetValue(model, out var before) || !before.SequenceEqual(prices))
            {
                kept.Models[model] = prices;
                changed = true;
            }
        }

        if (changed)
        {
            Directory.CreateDirectory(HakariPaths.DataDirectory);
            File.WriteAllText(FallbackPath, JsonSerializer.Serialize(kept));
        }

        return changed;
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
