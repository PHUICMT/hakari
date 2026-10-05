using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Hakari.Core.Usage;

namespace Hakari.Core.Pricing;

public sealed partial class PricingTable
{
    private const decimal TokensPerPriceUnit = 1_000_000m;
    private const decimal SearchesPerPriceUnit = 1_000m;
    private const decimal NoMultiplier = 1m;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [JsonPropertyName("models")]
    public Dictionary<string, List<ModelPrice>> Models { get; init; } = [];

    [JsonPropertyName("aliases")]
    public Dictionary<string, string> Aliases { get; init; } = [];

    [JsonPropertyName("inferenceGeographyMultipliers")]
    public Dictionary<string, decimal> InferenceGeographyMultipliers { get; init; } = [];

    [JsonPropertyName("webSearchPerThousand")]
    public decimal WebSearchPerThousand { get; init; }

    public static PricingTable Load(string json) =>
        JsonSerializer.Deserialize<PricingTable>(json, SerializerOptions) ?? new PricingTable();

    public static PricingTable LoadFile(string path) => Load(File.ReadAllText(path));

    public static PricingTable LoadBundled() => LoadFile(PricingFiles.BundledPath);

    public bool IsPriced(string model) => FindPriceHistory(model) is not null;

    public ModelPrice? Find(string model, DateOnly utcDay)
    {
        var history = FindPriceHistory(model);
        if (history is null)
        {
            return null;
        }

        return history
            .Where(price => price.EffectiveFrom is null || price.EffectiveFrom <= utcDay)
            .OrderByDescending(price => price.EffectiveFrom ?? DateOnly.MinValue)
            .FirstOrDefault();
    }

    public decimal? Cost(UsageRecord record) => Cost(PricedUsage.From(record));

    public decimal? Cost(PricedUsage usage)
    {
        if (Find(usage.Model, usage.UtcDay) is not { } price)
        {
            return null;
        }

        var tokenCost = TokenCost(price, usage.Speed, usage.Tokens);
        var geographyMultiplier = GeographyMultiplier(usage.InferenceGeography);
        var searchCost = usage.WebSearchRequests * WebSearchPerThousand / SearchesPerPriceUnit;

        return tokenCost * geographyMultiplier + searchCost;
    }

    private static decimal TokenCost(ModelPrice price, string speed, TokenCounts tokens)
    {
        var fastPrice = speed == SpeedNames.Fast ? price.Fast : null;
        var inputPrice = fastPrice?.Input ?? price.Input;
        var outputPrice = fastPrice?.Output ?? price.Output;
        var cacheMultiplier = fastPrice is null ? NoMultiplier : fastPrice.Input / price.Input;

        var totalPriceUnits =
            tokens.Input * inputPrice
            + tokens.Output * outputPrice
            + tokens.CacheWriteFiveMinutes * price.CacheWriteFiveMinutes * cacheMultiplier
            + tokens.CacheWriteOneHour * price.CacheWriteOneHour * cacheMultiplier
            + tokens.CacheRead * price.CacheRead * cacheMultiplier;

        return totalPriceUnits / TokensPerPriceUnit;
    }

    private decimal GeographyMultiplier(string? inferenceGeography)
    {
        if (inferenceGeography is null)
        {
            return NoMultiplier;
        }

        return InferenceGeographyMultipliers.GetValueOrDefault(inferenceGeography, NoMultiplier);
    }

    private List<ModelPrice>? FindPriceHistory(string model)
    {
        var resolvedModel = Aliases.GetValueOrDefault(model, model);
        if (Models.TryGetValue(resolvedModel, out var history))
        {
            return history;
        }

        var modelWithoutDate = DateSuffixPattern().Replace(resolvedModel, string.Empty);
        return Models.GetValueOrDefault(modelWithoutDate);
    }

    [GeneratedRegex(@"-\d{8}$")]
    private static partial Regex DateSuffixPattern();
}
