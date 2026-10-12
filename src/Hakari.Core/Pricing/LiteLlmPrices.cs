using System.Globalization;
using System.Text.Json;

namespace Hakari.Core.Pricing;

/// <summary>
/// Reads prices for models Hakari's own table does not list yet from LiteLLM's public price
/// list, which picks up new models within days. Only entries billed by Anthropic directly
/// are used, not a cloud's regional copies, and only the models asked for are kept.
/// </summary>
public static class LiteLlmPrices
{
    public const string Address = "https://raw.githubusercontent.com/BerriAI/litellm/main/"
        + "model_prices_and_context_window.json";

    private const string DirectProvider = "anthropic";
    private const decimal TokensPerPriceUnit = 1_000_000m;
    private const long TokensPerThousand = 1_000;

    /// <summary>A missing hour-long cache write costs twice the input, as it is billed.</summary>
    private const decimal OneHourWriteToInput = 2m;

    /// <summary>A missing five-minute cache write costs 1.25 times the input.</summary>
    private const decimal FiveMinuteWriteToInput = 1.25m;

    private const string Input = "input_cost_per_token";
    private const string Output = "output_cost_per_token";
    private const string FiveMinuteWrite = "cache_creation_input_token_cost";
    private const string OneHourWrite = "cache_creation_input_token_cost_above_1hr";
    private const string CacheRead = "cache_read_input_token_cost";
    private const string ProviderField = "litellm_provider";
    private const string ProviderEntries = "provider_specific_entry";
    private const string FastEntry = "fast";

    /// <returns>The prices found, by model; a model the list lacks is left out.</returns>
    public static Dictionary<string, List<ModelPrice>> Extract(
        string json,
        IEnumerable<string> models)
    {
        using var document = JsonDocument.Parse(json);
        var found = new Dictionary<string, List<ModelPrice>>(StringComparer.Ordinal);
        foreach (var model in models.Distinct(StringComparer.Ordinal))
        {
            if (document.RootElement.TryGetProperty(model, out var entry)
                && entry.ValueKind == JsonValueKind.Object
                && entry.TryGetProperty(ProviderField, out var provider)
                && provider.GetString() == DirectProvider
                && Read(entry) is { } price)
            {
                found[model] = [price];
            }
        }

        return found;
    }

    private static ModelPrice? Read(JsonElement entry)
    {
        if (PerMillion(entry, Input) is not { } input
            || PerMillion(entry, Output) is not { } output)
        {
            return null;
        }

        return new ModelPrice(
            input,
            output,
            PerMillion(entry, FiveMinuteWrite) ?? input * FiveMinuteWriteToInput,
            PerMillion(entry, OneHourWrite) ?? input * OneHourWriteToInput,
            PerMillion(entry, CacheRead) ?? 0,
            Fast(entry, input, output),
            LongPrompt: LongPrompt(entry));
    }

    /// <summary>Fast mode, which the list gives as one multiplier on input and output.</summary>
    private static FastModePrice? Fast(JsonElement entry, decimal input, decimal output) =>
        entry.TryGetProperty(ProviderEntries, out var extras)
        && extras.ValueKind == JsonValueKind.Object
        && extras.TryGetProperty(FastEntry, out var fast)
        && fast.TryGetDecimal(out var multiplier)
            ? new FastModePrice(input * multiplier, output * multiplier)
            : null;

    /// <summary>The first long-prompt band the list prices, as "_above_100k_tokens".</summary>
    private static LongPromptPrice? LongPrompt(JsonElement entry)
    {
        foreach (var threshold in PromptSize.Thresholds)
        {
            var thousands = (threshold / TokensPerThousand).ToString(CultureInfo.InvariantCulture);
            var band = $"_above_{thousands}k_tokens";
            if (PerMillion(entry, Input + band) is not { } input
                || PerMillion(entry, Output + band) is not { } output)
            {
                continue;
            }

            return new LongPromptPrice(
                threshold,
                input,
                output,
                PerMillion(entry, FiveMinuteWrite + band) ?? input * FiveMinuteWriteToInput,
                PerMillion(entry, OneHourWrite + band) ?? input * OneHourWriteToInput,
                PerMillion(entry, CacheRead + band) ?? 0);
        }

        return null;
    }

    private static decimal? PerMillion(JsonElement entry, string field) =>
        entry.TryGetProperty(field, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var perToken)
            ? decimal.Round((decimal)perToken * TokensPerPriceUnit, 6)
            : null;
}
