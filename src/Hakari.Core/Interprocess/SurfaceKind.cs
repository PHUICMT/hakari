namespace Hakari.Core.Interprocess;

public enum SurfaceKind
{
    Flyout,
    Dashboard,
    Settings,

    /// <summary>The widget's right-click menu, at the pointer.</summary>
    Menu,

    /// <summary>The widget's hover card, above the widget.</summary>
    Tooltip,

    HideTooltip,

    /// <summary>Start the window process without showing anything, so the next is quick.</summary>
    Warm,

    /// <summary>The first-run steps.</summary>
    Onboarding,
}
