using System.Text.Json;

namespace Hakari.Core.Parsing;

/// <summary>
/// Reads fields that may be missing or of an unexpected kind without throwing: a line of an
/// odd shape reads as empty values rather than stopping the read of a whole log.
/// </summary>
internal static class JsonElementExtensions
{
    public static string? GetStringOrNull(this JsonElement element, string propertyName) =>
        element.TryGetField(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;

    /// <summary>A whole number; a fraction is rounded down and a huge one is clamped.</summary>
    public static long GetInt64OrZero(this JsonElement element, string propertyName)
    {
        if (!element.TryGetField(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Number)
        {
            return 0;
        }

        if (property.TryGetInt64(out var whole))
        {
            return whole;
        }

        return property.TryGetDouble(out var number) && double.IsFinite(number)
            ? (long)Math.Clamp(Math.Floor(number), long.MinValue, long.MaxValue)
            : 0;
    }

    public static bool IsTrue(this JsonElement element, string propertyName) =>
        element.TryGetField(propertyName, out var property)
        && property.ValueKind == JsonValueKind.True;

    public static bool TryGetObject(
        this JsonElement element,
        string propertyName,
        out JsonElement value) =>
        element.TryGetField(propertyName, out value) && value.ValueKind == JsonValueKind.Object;

    /// <summary>Like TryGetProperty, but false instead of throwing on a non-object.</summary>
    public static bool TryGetField(
        this JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        return element.TryGetProperty(propertyName, out value);
    }
}