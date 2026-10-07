using System.Reflection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;

namespace Hakari.Surfaces.Controls;

/// <summary>
/// A pointing hand over something that opens on a click, for elements built in code whose
/// cursor setter is not reachable from outside their class.
/// </summary>
internal static class HandCursor
{
    private static readonly PropertyInfo? CursorProperty = typeof(UIElement).GetProperty(
        "ProtectedCursor",
        BindingFlags.Instance | BindingFlags.NonPublic);

    public static void Apply(UIElement element)
    {
        try
        {
            CursorProperty?.SetValue(
                element,
                InputSystemCursor.Create(InputSystemCursorShape.Hand));
        }
        catch (TargetInvocationException)
        {
            // Without the hand the row still opens; the hover color still shows it.
        }
    }
}
