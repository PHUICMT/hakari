using System.Globalization;

namespace Hakari.Cli.Output;

public static class DisplayFormat
{
    private const double BytesPerUnit = 1024;

    private static readonly string[] ByteUnits = ["B", "KB", "MB", "GB", "TB"];
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static string Number(long value) => value.ToString("N0", Culture);

    public static string Cost(decimal value) => value.ToString("N2", Culture);

    public static string Percent(double ratio) => ratio.ToString("P0", Culture);

    public static string Bytes(long byteCount)
    {
        double size = byteCount;
        var unitIndex = 0;
        while (size >= BytesPerUnit && unitIndex < ByteUnits.Length - 1)
        {
            size /= BytesPerUnit;
            unitIndex++;
        }

        return $"{size.ToString("0.#", Culture)} {ByteUnits[unitIndex]}";
    }
}
