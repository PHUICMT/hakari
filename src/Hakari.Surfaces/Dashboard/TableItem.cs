using Hakari.Core.Querying;

namespace Hakari.Surfaces.Dashboard;

/// <summary>One line of a usage table: a project group's header or a row of figures.</summary>
/// <param name="Children">For a group header: the rows it folds away.</param>
/// <param name="IsIndented">A row inside a group.</param>
/// <param name="FadeIn">Just inserted by opening a group, so it fades in once.</param>
internal sealed record TableItem(
    UsageSummary Summary,
    (string Title, string? Detail) Name,
    IReadOnlyList<TableItem>? Children = null,
    bool IsIndented = false,
    bool FadeIn = false)
{
    public bool IsGroup => Children is not null;
}
