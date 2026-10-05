namespace Hakari.Taskbar.Rendering;

/// <summary>Sizes at 100% scale, from the design handoff.</summary>
internal static class WidgetMetrics
{
    public const float Height = 40f;
    public const float HorizontalPadding = 10f;
    public const float PrimaryFontPixels = 13f;
    public const float SecondaryFontPixels = 11f;
    public const float LineGap = 1f;
    public const float CornerRadius = 4f;

    /// <summary>Content never comes closer than this to any edge, even mid-animation.</summary>
    public const float MinimumInset = 2f;

    /// <summary>How far a changing value travels, as a share of its line height.</summary>
    public const float ValueTravel = 0.45f;

    /// <summary>
    /// Fully transparent pixels of a layered window let clicks fall through, so the background
    /// keeps an alpha of one: invisible, but still clickable.
    /// </summary>
    public const int ClickableBackgroundAlpha = 1;

    public static readonly string[] FontFamilies = ["Segoe UI Variable Text", "Segoe UI"];
}
