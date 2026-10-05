using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Hakari.Core.Usage;

namespace Hakari.Core.Pricing;

public sealed partial class PricingTable
{
    private const decimal TokensPerPriceUnit = 1_000_000m;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [JsonPropertyName("models")]
    public Dictionary<string, ModelPrice> Models { get; init; } = [];

    [JsonPropertyName("aliases")]
    public Dictionary<string, string> Aliases { get; init; } = [];

    public static PricingTable Load(string json) =>
        JsonSerializer.Deserialize<PricingTable>(json, SerializerOptions) ?? new PricingTable();

    public static PricingTable LoadFile(string path) => Load(File.ReadAllText(path));

    public static PricingTable LoadBundled() => LoadFile(PricingFiles.BundledPath);

    public ModelPrice? Find(string model)
    {
        var resolvedModel = Aliases.GetValueOrDefault(model, model);
        if (Models.TryGetValue(resolvedModel, out var price))
        {
            return price;
        }

        var modelWithoutDate = DateSuffixPattern().Replace(resolvedModel, string.Empty);
        return Models.GetValueOrDefault(modelWithoutDate);
    }

    public decimal? Cost(UsageRecord record) => Cost(record.Model, record.Speed, record.Tokens);

    public decimal? Cost(string model, string speed, TokenCounts tokens)
    {
        if (Find(model) is not { } price)
        {
            return null;
        }

        var fastPrice = speed == SpeedNames.Fast ? price.Fast : null;
        var inputPrice = fastPrice?.Input ?? price.Input;
        var outputPrice = fastPrice?.Output ?? price.Output;

        var totalPriceUnits =
            tokens.Input * inputPrice
            + tokens.Output * outputPrice
            + tokens.CacheWriteFiveMinutes * price.CacheWriteFiveMinutes
            + tokens.CacheWriteOneHour * price.CacheWriteOneHour
            + tokens.CacheRead * price.CacheRead;

        return totalPriceUnits / TokensPerPriceUnit;
    }

    [GeneratedRegex(@"-\d{8}$")]
    private static partial Regex DateSuffixPattern();
}
