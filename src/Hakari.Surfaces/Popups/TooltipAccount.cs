namespace Hakari.Surfaces.Popups;

/// <summary>One account in the hover card: a title, detail lines, and how fresh they are.</summary>
public sealed record TooltipAccount(string Title, IReadOnlyList<string> Lines, string Updated);
