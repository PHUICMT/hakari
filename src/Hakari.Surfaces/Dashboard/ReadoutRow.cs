namespace Hakari.Surfaces.Dashboard;

/// <summary>A line of a chart's hover readout: a label and its value.</summary>
/// <param name="BrushKey">The series' color, shown as a square, or null for none.</param>
internal sealed record ReadoutRow(string? BrushKey, string Label, string Value);
