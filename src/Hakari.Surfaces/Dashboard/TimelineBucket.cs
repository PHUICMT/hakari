namespace Hakari.Surfaces.Dashboard;

/// <param name="Key">What the database groups by: a day or an hour.</param>
/// <param name="Label">What the chart shows under it.</param>
internal sealed record TimelineBucket(string Key, string Label);
