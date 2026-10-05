namespace Hakari.Taskbar.Rendering;

/// <param name="PrimaryTone">Normal draws the top line in the strong text color.</param>
/// <param name="Ring">A meter left of the text, or null for text only.</param>
/// <param name="MorePanels">Further blocks drawn to the right, such as one per account.</param>
/// <param name="TurnCount">More than one draws a dot per account, the current one solid.</param>
public sealed record WidgetContent(
    string PrimaryText,
    string SecondaryText,
    WidgetTone SecondaryTone = WidgetTone.Normal,
    WidgetTone PrimaryTone = WidgetTone.Normal,
    WidgetRing? Ring = null,
    IReadOnlyList<WidgetContent>? MorePanels = null,
    int TurnIndex = 0,
    int TurnCount = 0)
{
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
        && (MorePanels ?? []).SequenceEqual(other.MorePanels ?? []);

    public override int GetHashCode() => HashCode.Combine(
        PrimaryText,
        SecondaryText,
        SecondaryTone,
        PrimaryTone,
        Ring,
        MorePanels?.Count ?? 0);
}
