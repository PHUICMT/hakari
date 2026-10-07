using Hakari.Core.Localization;
using Hakari.Core.Settings;
using Hakari.Surfaces.Controls;
using Hakari.Surfaces.Flyout;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// Folds a dashboard section away under its header: a click on the header or its chevron
/// folds or opens the body with the same easing as the flyout's cards, and the choice is
/// remembered across visits and restarts.
/// </summary>
internal static class DashboardFold
{
    private const string ChevronGlyph = "";
    private const double ChevronSize = 10;
    private const double FoldedAngle = -90;

    /// <summary>A chevron button for the header; the header and the button both toggle.</summary>
    public static Button Attach(FrameworkElement header, FrameworkElement body, string key)
    {
        var folded = IsFolded(key);
        var turn = new RotateTransform { Angle = folded ? FoldedAngle : 0 };
        var chevron = new FontIcon
        {
            Glyph = ChevronGlyph,
            FontSize = ChevronSize,
            FontFamily = (FontFamily)Application.Current.Resources["HakariIconFont"],
            Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
            RenderTransform = turn,
            RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
        };
        var button = new Button
        {
            Content = chevron,
            Style = (Style)Application.Current.Resources["HakariSubtleButton"],
            VerticalAlignment = VerticalAlignment.Center,
        };
        Name(button, folded);
        body.Visibility = folded ? Visibility.Collapsed : Visibility.Visible;

        void Toggle()
        {
            folded = !folded;
            Remember(key, folded);
            Name(button, folded);
            SurfaceMotion.Settle(turn, "Angle", folded ? FoldedAngle : 0);
            CardFold.Run(body, folded, () => { });
        }

        button.Click += (_, _) => Toggle();
        header.Tapped += (_, args) =>
        {
            // The chevron toggles on its own click; a tap there must not toggle twice.
            if (!IsInside(args.OriginalSource as DependencyObject, button))
            {
                Toggle();
            }
        };
        HandCursor.Apply(header);
        return button;
    }

    private static bool IsInside(DependencyObject? element, DependencyObject container)
    {
        for (var current = element; current is not null;
            current = VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, container))
            {
                return true;
            }
        }

        return false;
    }

    private static void Name(Button button, bool folded) => AutomationProperties.SetName(
        button,
        Texts.Get(folded ? "dashboard.section.open" : "dashboard.section.fold"));

    private static bool IsFolded(string key) =>
        SettingsStore.Default.Load().FoldedDashboardSections.Contains(key);

    private static void Remember(string key, bool folded) =>
        SettingsStore.Default.Update(current => current with
        {
            FoldedDashboardSections = folded
                ? [.. current.FoldedDashboardSections.Append(key).Distinct()]
                : [.. current.FoldedDashboardSections.Where(existing => existing != key)],
        });
}
