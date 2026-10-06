using System.Globalization;

namespace Hakari.Core.Presentation;

/// <summary>File sizes the way people read them: 12 KB, 380 MB, 1.4 GB.</summary>
public static class ByteText
{
    private const double Step = 1024;
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public static string Format(long bytes)
    {
        var size = (double)bytes;
        var unit = 0;
        while (size >= Step && unit < Units.Length - 1)
        {
            size /= Step;
            unit++;
        }

        var shown = unit >= 3 ? size.ToString("0.0", CultureInfo.InvariantCulture)
            : size.ToString("0", CultureInfo.InvariantCulture);
        return $"{shown} {Units[unit]}";
    }
}
