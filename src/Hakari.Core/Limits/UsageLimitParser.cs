using System.Text.Json;
using Hakari.Core.Parsing;

namespace Hakari.Core.Limits;

/// <summary>Reads the usage response. Unknown fields are ignored.</summary>
public static class UsageLimitParser
{
    public const string NormalSeverity = "normal";

    private const string LimitsProperty = "limits";
    private const string KindProperty = "kind";
    private const string GroupProperty = "group";
    private const string PercentProperty = "percent";
    private const string SeverityProperty = "severity";
    private const string ResetsAtProperty = "resets_at";
    private const string ScopeProperty = "scope";
    private const string ModelProperty = "model";
    private const string DisplayNameProperty = "display_name";
    private const string IsActiveProperty = "is_active";
    private const string ExtraUsageProperty = "extra_usage";
    private const string IsEnabledProperty = "is_enabled";
    private const string MonthlyLimitProperty = "monthly_limit";
    private const string UsedCreditsProperty = "used_credits";
    private const string CurrencyProperty = "currency";
    private const string DecimalPlacesProperty = "decimal_places";

    public static LimitSnapshot Parse(string json, DateTimeOffset fetchedAt)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new LimitSnapshot(
            Limits: ReadLimits(root),
            ExtraUsage: ReadExtraUsage(root),
            FetchedAt: fetchedAt,
            Freshness: LimitFreshness.Live);
    }

    private static List<UsageLimit> ReadLimits(JsonElement root)
    {
        if (!root.TryGetField(LimitsProperty, out var limits)
            || limits.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return [.. limits.EnumerateArray().Select(ReadLimit)];
    }

    private const long MaximumPercent = 1_000;
    private const long MaximumDecimalPlaces = 8;

    private static UsageLimit ReadLimit(JsonElement limit) => new(
        Kind: limit.GetStringOrNull(KindProperty) ?? string.Empty,
        Group: limit.GetStringOrNull(GroupProperty) ?? string.Empty,
        Percent: (int)Math.Clamp(limit.GetInt64OrZero(PercentProperty), 0, MaximumPercent),
        Severity: limit.GetStringOrNull(SeverityProperty) ?? NormalSeverity,
        ResetsAt: ReadTimestamp(limit, ResetsAtProperty),
        ScopeName: ReadScopeName(limit),
        IsActive: limit.IsTrue(IsActiveProperty));

    private static string? ReadScopeName(JsonElement limit)
    {
        if (!limit.TryGetObject(ScopeProperty, out var scope)
            || !scope.TryGetObject(ModelProperty, out var model))
        {
            return null;
        }

        return model.GetStringOrNull(DisplayNameProperty);
    }

    /// <summary>Credits are counted in minor units, such as cents.</summary>
    private static ExtraUsage? ReadExtraUsage(JsonElement root)
    {
        if (!root.TryGetObject(ExtraUsageProperty, out var extra))
        {
            return null;
        }

        var decimalPlaces = (int)Math.Clamp(
            extra.GetInt64OrZero(DecimalPlacesProperty), 0, MaximumDecimalPlaces);
        var divisor = (decimal)Math.Pow(10, decimalPlaces);
        return new ExtraUsage(
            IsEnabled: extra.IsTrue(IsEnabledProperty),
            Used: ReadDecimal(extra, UsedCreditsProperty) / divisor,
            MonthlyLimit: ReadDecimal(extra, MonthlyLimitProperty) / divisor,
            Currency: extra.GetStringOrNull(CurrencyProperty) ?? string.Empty);
    }

    private static decimal ReadDecimal(JsonElement element, string propertyName)
    {
        return element.TryGetField(propertyName, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetDecimal(out var number)
                ? number
                : 0;
    }

    private static DateTimeOffset? ReadTimestamp(JsonElement element, string propertyName) =>
        element.GetStringOrNull(propertyName) is { } text
            && DateTimeOffset.TryParse(text, out var timestamp)
            ? timestamp
            : null;
}
