using Hakari.Surfaces.Controls;

namespace Hakari.Surfaces.Dashboard;

/// <param name="Icon">Drawn as the design draws it.</param>
/// <param name="SetApart">A hairline above it, starting a new group of pages.</param>
internal sealed record NavEntry(
    DashboardPage Page,
    IReadOnlyList<IconShape> Icon,
    string TextKey,
    bool SetApart = false);
