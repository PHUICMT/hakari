namespace Hakari.Core.Settings;

/// <summary>The color of the widget's text on the taskbar.</summary>
public enum WidgetTextColor
{
    /// <summary>Whichever stands out against what is behind the widget, checked again.</summary>
    Automatic,

    /// <summary>Light text, for a dark taskbar or wallpaper.</summary>
    Light,

    /// <summary>Dark text, for a light taskbar or wallpaper.</summary>
    Dark,
}
