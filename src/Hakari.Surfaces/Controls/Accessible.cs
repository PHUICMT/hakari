using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Controls;

/// <summary>
/// Names a control for a screen reader as well as for the pointer: an icon-only button's
/// tooltip is all it says, so that becomes its accessible name too.
/// </summary>
internal static class Accessible
{
    public static void Name(DependencyObject element, string text)
    {
        ToolTipService.SetToolTip(element, text);
        AutomationProperties.SetName(element, text);
    }
}