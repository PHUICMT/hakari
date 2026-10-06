using Hakari.Core.Localization;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Settings;

/// <summary>One choice in the bar: everyone's layout, or one account's.</summary>
internal sealed record LayoutTarget(string Id, string Name, bool HasOwnLayout);

/// <summary>
/// Whose layout the editor changes, right above it: a chip for every account and one for
/// all of them, a dot on the accounts that have a layout of their own, a line that says what
/// a change here will do, and a way to put an account back on the shared layout.
/// </summary>
internal sealed partial class LayoutTargetBar : StackPanel
{
    private const double ChipSpacing = 6;
    private const double OwnDotSize = 6;
    private const double StatusSize = 12;
    private const double ChipTextSize = 13;
    private static readonly Thickness ChipPadding = new(12, 5, 12, 5);

    private readonly StackPanel chips = new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = ChipSpacing,
    };
    private readonly TextBlock status = new()
    {
        FontSize = StatusSize,
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly Button reset = new();
    private string shownTarget = string.Empty;

    public LayoutTargetBar()
    {
        Spacing = 8;
        status.Foreground = Brush("HakariInkMutedBrush");
        reset.Style = (Style)Application.Current.Resources["HakariButton"];
        reset.Content = Texts.Get("settings.layoutFor.reset");
        reset.Click += (_, _) => ResetRequested?.Invoke(this, shownTarget);

        var heading = new TextBlock
        {
            Text = Texts.Get("settings.layoutFor.editing"),
            FontSize = StatusSize,
            Foreground = Brush("HakariInkFaintBrush"),
        };
        var statusRow = new Grid { ColumnSpacing = 10 };
        statusRow.ColumnDefinitions.Add(new ColumnDefinition());
        statusRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        statusRow.Children.Add(status);
        Grid.SetColumn(reset, 1);
        statusRow.Children.Add(reset);

        Children.Add(heading);
        Children.Add(LayoutEditor.SideScroller(chips));
        Children.Add(statusRow);
    }

    public event EventHandler<string>? Chosen;

    public event EventHandler<string>? ResetRequested;

    public void Show(IReadOnlyList<LayoutTarget> targets, string chosen)
    {
        var changed = chosen != shownTarget;
        shownTarget = chosen;
        chips.Children.Clear();
        foreach (var target in targets)
        {
            chips.Children.Add(Chip(target, target.Id == chosen));
        }

        var current = targets.FirstOrDefault(target => target.Id == chosen) ?? targets[0];
        status.Text = current.Id.Length == 0
            ? Texts.Get("settings.layoutFor.sharedStatus")
            : current.HasOwnLayout
                ? Texts.Format("settings.layoutFor.ownStatus", current.Name)
                : Texts.Format("settings.layoutFor.followsStatus", current.Name);
        reset.Visibility = current is { Id.Length: > 0, HasOwnLayout: true }
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (changed && SurfaceMotion.Current() != Core.Settings.AnimationSetting.Off)
        {
            status.Opacity = 0;
            SurfaceMotion.Settle(status, "Opacity", 1);
        }
    }

    private Button Chip(LayoutTarget target, bool isChosen)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        content.Children.Add(new TextBlock
        {
            Text = target.Name,
            FontSize = ChipTextSize,
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (target.HasOwnLayout)
        {
            var dot = new Border
            {
                Width = OwnDotSize,
                Height = OwnDotSize,
                CornerRadius = new CornerRadius(OwnDotSize / 2),
                Background = Brush("HakariAccentBrush"),
                VerticalAlignment = VerticalAlignment.Center,
            };
            ToolTipService.SetToolTip(dot, Texts.Get("settings.layoutFor.own"));
            content.Children.Add(dot);
        }

        var chip = new Button
        {
            Content = content,
            Padding = ChipPadding,
            Style = (Style)Application.Current.Resources["HakariButton"],
        };
        if (isChosen)
        {
            chip.Background = Brush("HakariAccentSoftBrush");
            chip.BorderBrush = Brush("HakariAccentBrush");
        }

        chip.Click += (_, _) =>
        {
            if (target.Id != shownTarget)
            {
                Chosen?.Invoke(this, target.Id);
            }
        };
        return chip;
    }

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
