using Hakari.Surfaces.Motion;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Controls;

/// <summary>
/// An on/off switch whose thumb slides between the ends. Colors come from the template's
/// visual states; the slide is done here so it follows the animation setting.
/// </summary>
public sealed partial class HakariToggle : ToggleButton
{
    private const string ThumbPartName = "Thumb";
    private const string OffsetProperty = "X";

    /// <summary>Track width 40, thumb 12, inset 4 on each side, minus the 1 px border.</summary>
    private const double ThumbTravel = 19;

    private readonly TranslateTransform thumbOffset = new();

    public HakariToggle()
    {
        Checked += (_, _) => SlideThumb();
        Unchecked += (_, _) => SlideThumb();
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild(ThumbPartName) is UIElement thumb)
        {
            thumb.RenderTransform = thumbOffset;
            thumbOffset.X = TargetOffset();
        }
    }

    private double TargetOffset() => IsChecked == true ? ThumbTravel : 0;

    /// <summary>Set directly while not yet shown: there is nothing to watch slide.</summary>
    private void SlideThumb()
    {
        if (!IsLoaded)
        {
            thumbOffset.X = TargetOffset();
            return;
        }

        SurfaceMotion.Settle(thumbOffset, OffsetProperty, TargetOffset());
    }
}
