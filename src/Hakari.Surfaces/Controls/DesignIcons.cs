namespace Hakari.Surfaces.Controls;

/// <summary>
/// The icons of docs/design/ui-preview.html, drawn from the same outlines so they match it.
/// </summary>
internal static class DesignIcons
{
    public static IReadOnlyList<IconShape> Home { get; } =
        [new("M2.5 7 8 2.5 13.5 7v6.5h-3.5V10H6v3.5H2.5Z")];

    public static IReadOnlyList<IconShape> List { get; } =
    [
        new("M5.5 4h8M5.5 8h8M5.5 12h8"),
        IconShape.Circle(2.8, 4, 0.9, filled: true),
        IconShape.Circle(2.8, 8, 0.9, filled: true),
        IconShape.Circle(2.8, 12, 0.9, filled: true),
    ];

    public static IReadOnlyList<IconShape> Folder { get; } =
        [new("M1.8 4a1 1 0 0 1 1-1h3.4l1.5 1.6h5.5a1 1 0 0 1 1 1V12a1 1 0 0 1-1 1"
            + "H2.8a1 1 0 0 1-1-1Z")];

    public static IReadOnlyList<IconShape> Branch { get; } =
    [
        IconShape.Circle(4.5, 3.5, 1.6),
        IconShape.Circle(4.5, 12.5, 1.6),
        IconShape.Circle(11.5, 5.5, 1.6),
        new("M4.5 5.1v5.8M11.5 7.1c0 3-7 2-7 3.8"),
    ];

    public static IReadOnlyList<IconShape> Flow { get; } =
    [
        IconShape.Box(1.8, 2, 5, 4, 1),
        IconShape.Box(9.2, 10, 5, 4, 1),
        new("M4.3 6v3.2a1 1 0 0 0 1 1h3.9"),
    ];

    public static IReadOnlyList<IconShape> User { get; } =
    [
        IconShape.Circle(8, 5.5, 2.7),
        new("M2.8 13.8a5.2 5.2 0 0 1 10.4 0"),
    ];

    public static IReadOnlyList<IconShape> Chart { get; } =
        [new("M2.5 13.5h11M4.5 11V8M8 11V4M11.5 11V6.5")];

    public static IReadOnlyList<IconShape> Coin { get; } =
    [
        IconShape.Circle(8, 8, 6),
        new("M10 5.8C9.6 5.2 8.9 5 8 5c-1.2 0-2 .6-2 1.5 0 2 4 1 4 3 0 .9-.8 1.5-2 1.5"
            + "-.9 0-1.7-.3-2.1-.9M8 3.8V5M8 11v1.2"),
    ];

    public static IReadOnlyList<IconShape> Gear { get; } =
    [
        IconShape.Circle(8, 8, 2.3),
        new("M8 1.5v2M8 12.5v2M1.5 8h2M12.5 8h2M3.4 3.4l1.4 1.4M11.2 11.2l1.4 1.4"
            + "M3.4 12.6l1.4-1.4M11.2 4.8l1.4-1.4"),
    ];
}
