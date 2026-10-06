namespace Hakari.Taskbar.Rendering;

/// <param name="PrimaryTone">Normal draws the top line in the strong text color.</param>
/// <param name="Ring">A meter left of the text, or null for text only.</param>
/// <param name="MorePanels">Further blocks drawn to the right, such as one per account.</param>
/// <param name="TurnCount">More than one draws a dot per account, the current one solid.</param>
/// <param name="Spark">Points of a small line left of the text, such as recent spending.</param>
/// <param name="PrimaryBar">A thin bar beside the top line.</param>
/// <param name="SecondaryBar">A thin bar beside the second line.</param>
/// <param name="Pill">A small label before the text, such as PAUSED; replaces the ring.</param>
public sealed record WidgetContent(
    string PrimaryText,
    string SecondaryText,
    WidgetTone SecondaryTone = WidgetTone.Normal,
    WidgetTone PrimaryTone = WidgetTone.Normal,
    WidgetRing? Ring = null,
    IReadOnlyList<WidgetContent>? MorePanels = null,
    int TurnIndex = 0,
    int TurnCount = 0,
    IReadOnlyList<double>? Spark = null,
    WidgetBar? PrimaryBar = null,
    WidgetBar? SecondaryBar = null,
    string? Pill = null)
{
    /// <summary>
    /// What still fits when the taskbar has little room: the first block's ring and its top
    /// line only, like the minimal template.
    /// </summary>
    public WidgetContent Compact() => this with
    {
        SecondaryText = string.Empty,
        MorePanels = null,
        Spark = null,
        PrimaryBar = null,
        SecondaryBar = null,
        TurnCount = 0,
    };

    public IReadOnlyList<WidgetContent> Panels =>
        MorePanels is { Count: > 0 } more ? [this with { MorePanels = null }, .. more] : [this];

    /// <summary>By value, panels included, so an unchanged update starts no transition.</summary>
    public bool Equals(WidgetContent? other) =>
        other is not null
        && PrimaryText == other.PrimaryText
        && SecondaryText == other.SecondaryText
        && SecondaryTone == other.SecondaryTone
        && PrimaryTone == other.PrimaryTone
        && Ring == other.Ring
        && TurnIndex == other.TurnIndex
        && TurnCount == other.TurnCount
        && PrimaryBar == other.PrimaryBar
        && SecondaryBar == other.SecondaryBar
        && Pill == other.Pill
        && (Spark ?? []).SequenceEqual(other.Spark ?? [])
        && (MorePanels ?? []).SequenceEqual(other.MorePanels ?? []);

    public override int GetHashCode() => HashCode.Combine(
        PrimaryText,
        SecondaryText,
        SecondaryTone,
        PrimaryTone,
        Ring,
        MorePanels?.Count ?? 0,
        Spark?.Count ?? 0);
}
