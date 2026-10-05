using System.Globalization;

namespace Hakari.Core.Presentation;

public static class PercentText
{
    private const double DecimalBase = 10;

    /// <summary>
    /// "93%", or "93.4%" with one decimal. Cut, never rounded up, so 93.96 shows as 93.9
    /// and not as a 94 the server has not reported yet.
    /// </summary>
    public static string Format(double percent, int decimals)
    {
        var culture = CultureInfo.InvariantCulture;
        var places = Math.Max(0, decimals);
        var factor = Math.Pow(DecimalBase, places);
        var cut = Math.Floor(percent * factor) / factor;
        return cut.ToString("F" + places.ToString(culture), culture) + "%";
    }
}
