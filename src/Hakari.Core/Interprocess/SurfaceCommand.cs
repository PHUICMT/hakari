using System.Globalization;

namespace Hakari.Core.Interprocess;

/// <summary>
/// "Open this window", sent from Hakari.exe to the window process. The anchor is the screen
/// point (physical pixels) the flyout grows from: the widget's right edge on the taskbar's top.
/// </summary>
public sealed record SurfaceCommand(SurfaceKind Kind, int AnchorX, int AnchorY)
{
    private const char Separator = ' ';
    private const int PartCount = 3;
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public string ToLine() => string.Join(
        Separator,
        Kind.ToString(),
        AnchorX.ToString(Culture),
        AnchorY.ToString(Culture));

    public string[] ToArguments() => ToLine().Split(Separator);

    public static SurfaceCommand? Parse(IReadOnlyList<string> parts)
    {
        if (parts.Count != PartCount
            || !Enum.TryParse<SurfaceKind>(parts[0], ignoreCase: true, out var kind)
            || !int.TryParse(parts[1], NumberStyles.Integer, Culture, out var anchorX)
            || !int.TryParse(parts[2], NumberStyles.Integer, Culture, out var anchorY))
        {
            return null;
        }

        return new SurfaceCommand(kind, anchorX, anchorY);
    }

    public static SurfaceCommand? ParseLine(string? line) =>
        line is null ? null : Parse(line.Split(Separator, StringSplitOptions.RemoveEmptyEntries));
}
