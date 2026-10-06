using System.Globalization;
using Hakari.Core.Localization;

namespace Hakari.Core.Currency;

/// <summary>A currency to pick: its code, name and symbol, and where it is used.</summary>
public sealed record CurrencyEntry(
    string Code,
    string Name,
    string Symbol,
    IReadOnlyList<string> Places,
    IReadOnlyList<string> SearchTerms);

/// <summary>
/// Every currency Windows knows a country for, so typing "sgd", "singapore", "สิงคโปร์" or
/// "dollar" finds the right code. Names come from the app's language where it has one,
/// otherwise from Windows in English.
/// </summary>
public static class CurrencyCatalog
{
    private const int MostShown = 8;
    private const string NameKeyPrefix = "currency.";

    private static readonly Lazy<IReadOnlyList<(string Code, RegionInfo Region)>> Regions =
        new(LoadRegions);

    private static (string Language, IReadOnlyList<CurrencyEntry> Entries)? cached;

    /// <summary>Built once per app language, since the names follow it.</summary>
    public static IReadOnlyList<CurrencyEntry> All()
    {
        if (cached is { } built && built.Language == Texts.Language)
        {
            return built.Entries;
        }

        IReadOnlyList<CurrencyEntry> entries =
        [
            .. Regions.Value
                .GroupBy(region => region.Code)
                .Select(group => Entry(group.Key, [.. group.Select(entry => entry.Region)]))
                .OrderBy(entry => entry.Code, StringComparer.Ordinal),
        ];
        cached = (Texts.Language, entries);
        return entries;
    }

    public static CurrencyEntry? Find(string code) =>
        All().FirstOrDefault(entry => entry.Code == code.Trim().ToUpperInvariant());

    /// <summary>
    /// The best few for what was typed: a code that starts with it first, then a name or a
    /// place that starts with it, then one that contains it.
    /// </summary>
    public static IReadOnlyList<CurrencyEntry> Search(string typed)
    {
        var query = typed.Trim();
        if (query.Length == 0)
        {
            return [];
        }

        return
        [
            .. All()
                .Select(entry => (Entry: entry, Rank: Rank(entry, query)))
                .Where(match => match.Rank >= 0)
                .OrderBy(match => match.Rank)
                .ThenBy(match => match.Entry.Code, StringComparer.Ordinal)
                .Take(MostShown)
                .Select(match => match.Entry),
        ];
    }

    private static int Rank(CurrencyEntry entry, string query)
    {
        const StringComparison Loose = StringComparison.CurrentCultureIgnoreCase;
        if (entry.Code.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (entry.Code.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (entry.SearchTerms.Any(term => term.StartsWith(query, Loose)
            || term.Split(' ').Any(word => word.StartsWith(query, Loose))))
        {
            return 2;
        }

        return entry.SearchTerms.Any(term => term.Contains(query, Loose)) ? 3 : -1;
    }

    private static CurrencyEntry Entry(string code, IReadOnlyList<RegionInfo> regions)
    {
        var first = regions[0];
        var localName = Texts.Find(NameKeyPrefix + code);
        var places = regions.Select(region => region.EnglishName).Distinct().ToList();
        var terms = new List<string>
        {
            first.CurrencyEnglishName,
            first.CurrencyNativeName,
        };
        terms.AddRange(regions.SelectMany(region =>
            new[] { region.EnglishName, region.NativeName }));
        if (localName is not null)
        {
            terms.Add(localName);
        }

        var knownSymbol = CurrencySymbols.PrefixFor(code).Trim();
        return new CurrencyEntry(
            code,
            localName ?? first.CurrencyEnglishName,
            knownSymbol != code ? knownSymbol : first.CurrencySymbol,
            places,
            [.. terms.Distinct()]);
    }

    private static IReadOnlyList<(string Code, RegionInfo Region)> LoadRegions()
    {
        var regions = new List<(string, RegionInfo)>();
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new RegionInfo(culture.Name);
                if (region.ISOCurrencySymbol.Length == 3)
                {
                    regions.Add((region.ISOCurrencySymbol, region));
                }
            }
            catch (ArgumentException)
            {
                // A culture without a country, such as a language on its own.
            }
        }

        return
        [
            .. regions.DistinctBy(entry => (entry.Item1, entry.Item2.Name)),
        ];
    }
}
