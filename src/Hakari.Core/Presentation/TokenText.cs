using System.Globalization;

namespace Hakari.Core.Presentation;

/// <summary>Token counts the way people read big numbers: 812, 12.4K, 4.2M, 1.3B.</summary>
public static class TokenText
{
    private static readonly (double Size, string Suffix)[] Steps =
    [
        (1_000_000_000, "B"),
        (1_000_000, "M"),
        (1_000, "K"),
    ];

    private const string ShortFormat = "0.#";
    private const string WholeFormat = "N0";

    public static string Format(long count)
    {
        foreach (var (size, suffix) in Steps)
        {
            if (count >= size)
            {
                return (count / size).ToString(ShortFormat, CultureInfo.InvariantCulture) + suffix;
            }
        }

        return count.ToString(WholeFormat, CultureInfo.InvariantCulture);
    }
}
