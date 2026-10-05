namespace Hakari.Taskbar.Rendering;

/// <param name="Previous">Text leaving the line, or null when the line did not change.</param>
internal sealed record LineChange(string? Previous, string Current)
{
    public static LineChange Between(string? previous, string current) =>
        new(previous is null || previous == current ? null : previous, current);
}
