using Hakari.Surfaces.Motion;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Controls;

/// <summary>
/// One choice of a segmented control. The raised pill behind the selected choice fades in
/// and out with the animation setting; the text weight comes from the template.
/// </summary>
public sealed partial class HakariSegment : RadioButton
{
    private const string PillPartName = "Pill";
    private const string OpacityPath = "Opacity";

    private UIElement? pill;

    public HakariSegment()
    {
        Checked += (_, _) => SettlePill();
        Unchecked += (_, _) => SettlePill();
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        pill = GetTemplateChild(PillPartName) as UIElement;
        if (pill is not null)
        {
            pill.Opacity = TargetOpacity();
        }
    }

    private double TargetOpacity() => IsChecked == true ? 1 : 0;

    private void SettlePill()
    {
        if (pill is null)
        {
            return;
        }

        if (IsLoaded)
        {
            SurfaceMotion.Settle(pill, OpacityPath, TargetOpacity());
        }
        else
        {
            pill.Opacity = TargetOpacity();
        }
    }
}
