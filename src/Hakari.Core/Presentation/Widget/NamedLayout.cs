namespace Hakari.Core.Presentation.Widget;

/// <summary>A layout the user saved under a name, to come back to later.</summary>
public sealed record NamedLayout(string Name, WidgetLayout Layout);
