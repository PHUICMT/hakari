using System.Text.Json;

namespace Hakari.Core.Parsing;

internal static class JsonElementExtensions
{
    public static string? GetStringOrNull(this JsonElement element, string propertyName)
    {
        var found = element.TryGetProperty(propertyName, out var property);
        return found && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    }

    public static long GetInt64OrZero(this JsonElement element, string propertyName)
    {
        var found = element.TryGetProperty(propertyName, out var property);
        return found && property.ValueKind == JsonValueKind.Number ? property.GetInt64() : 0;
    }

    public static bool IsTrue(this JsonElement element, string propertyName)
    {
        var found = element.TryGetProperty(propertyName, out var property);
        return found && property.ValueKind == JsonValueKind.True;
    }

    public static bool TryGetObject(
        this JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        var found = element.TryGetProperty(propertyName, out value);
        return found && value.ValueKind == JsonValueKind.Object;
    }
}
