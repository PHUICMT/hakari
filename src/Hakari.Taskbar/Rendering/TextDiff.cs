namespace Hakari.Taskbar.Rendering;

/// <summary>
/// What changed between two versions of a line: the text both share at the start and end
/// stays still, and only the middle rolls, like the wheels of an odometer.
/// </summary>
internal sealed record TextDiff(string Prefix, string OldMiddle, string NewMiddle, string Suffix)
{
    public bool SharesAnything => Prefix.Length > 0 || Suffix.Length > 0;

    public static TextDiff Between(string previous, string current)
    {
        var prefixLength = 0;
        var shortest = Math.Min(previous.Length, current.Length);
        while (prefixLength < shortest && previous[prefixLength] == current[prefixLength])
        {
            prefixLength++;
        }

        var suffixLength = 0;
        while (suffixLength < shortest - prefixLength
            && previous[previous.Length - 1 - suffixLength]
                == current[current.Length - 1 - suffixLength])
        {
            suffixLength++;
        }

        return new TextDiff(
            current[..prefixLength],
            previous[prefixLength..(previous.Length - suffixLength)],
            current[prefixLength..(current.Length - suffixLength)],
            current[(current.Length - suffixLength)..]);
    }
}
