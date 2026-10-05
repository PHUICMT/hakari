using System.Diagnostics;
using Hakari.Core.Displays;
using Hakari.Core.Settings;

namespace Hakari.Settings;

/// <summary>
/// Turns the display choice into the widget host's filter. The host asks every second, so
/// the display list is reused briefly instead of read on each call.
/// </summary>
internal static class DisplayFilter
{
    private static readonly TimeSpan CatalogLifetime = TimeSpan.FromSeconds(10);
    private static IReadOnlyList<DisplayInfo> catalog = [];
    private static long catalogReadAt = long.MinValue;

    public static Func<string, bool>? From(HakariSettings settings)
    {
        var chosen = settings.ChosenDisplays.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return settings.Displays switch
        {
            TaskbarDisplays.All => null,
            TaskbarDisplays.Primary => device => Find(device)?.IsPrimary == true,
            _ => device => Find(device) is { } display && chosen.Contains(display.Id),
        };
    }

    private static DisplayInfo? Find(string deviceName) =>
        Catalog().FirstOrDefault(display => string.Equals(
            display.DeviceName,
            deviceName,
            StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<DisplayInfo> Catalog()
    {
        if (catalogReadAt == long.MinValue
            || Stopwatch.GetElapsedTime(catalogReadAt) > CatalogLifetime)
        {
            catalog = DisplayCatalog.List();
            catalogReadAt = Stopwatch.GetTimestamp();
        }

        return catalog;
    }
}
