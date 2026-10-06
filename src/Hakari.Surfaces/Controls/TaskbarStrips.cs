using Hakari.Core.Presentation.Widget;
using Hakari.Core.Settings;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Controls;

/// <summary>
/// The widget as it will sit at the right end of a light taskbar and of a dark one, next to
/// a faint stand-in for the tray. A widget wider than the strips scrolls sideways.
/// </summary>
public sealed partial class TaskbarStrips : UserControl
{
    private const double StripHeight = 48;
    private const double StripRadius = 6;
    private const double StripGap = 6;
    private const double BlockGap = 2;
    private const double TrayWidth = 120;
    private const double TrayHeight = 20;
    private const double TrayOpacity = 0.12;
    private const double UnfocusedOpacity = 0.4;
    private const double ScrollBarRoom = 14;
    private static readonly Thickness StripPadding = new(8, 0, 8, 0);

    private readonly StackPanel lightBlocks = Blocks();
    private readonly StackPanel darkBlocks = Blocks();

    public TaskbarStrips()
    {
        var stack = new StackPanel { Spacing = StripGap };
        stack.Children.Add(Strip(TaskbarPalette.Light, lightBlocks));
        stack.Children.Add(Strip(TaskbarPalette.Dark, darkBlocks));
        var scroller = new ScrollViewer
        {
            Content = stack,
            HorizontalScrollMode = ScrollMode.Enabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        scroller.SizeChanged += (_, args) => stack.MinWidth = args.NewSize.Width;
        stack.SizeChanged += (_, _) => stack.Margin = new Thickness(
            0, 0, 0, stack.ActualWidth > scroller.ActualWidth + 1 ? ScrollBarRoom : 0);
        Content = scroller;
    }

    /// <param name="focused">The block left lit while the others dim; -1 lights them all.</param>
    public void Show(IReadOnlyList<ComposedWidget> panels, int focused = -1)
    {
        Fill(lightBlocks, TaskbarPalette.Light, panels, focused);
        Fill(darkBlocks, TaskbarPalette.Dark, panels, focused);
    }

    private static void Fill(
        StackPanel blocks,
        TaskbarPalette palette,
        IReadOnlyList<ComposedWidget> panels,
        int focused)
    {
        while (blocks.Children.Count > panels.Count)
        {
            blocks.Children.RemoveAt(blocks.Children.Count - 1);
        }

        var motion = SurfaceMotion.Current();
        for (var index = 0; index < panels.Count; index++)
        {
            var target = focused < 0 || focused == index ? 1 : UnfocusedOpacity;
            if (index >= blocks.Children.Count)
            {
                blocks.Children.Add(NewBlock(palette, target, motion));
            }

            var block = (WidgetPreview)blocks.Children[index];
            block.Show(panels[index]);
            if (motion == AnimationSetting.Off || !block.IsLoaded)
            {
                block.Tag = target;
                block.Opacity = motion == AnimationSetting.Off ? target : block.Opacity;
            }
            else if (block.Opacity != target)
            {
                SurfaceMotion.Settle(block, "Opacity", target);
            }
        }
    }

    /// <summary>A block that appears fades in to its opacity once it is on screen.</summary>
    private static WidgetPreview NewBlock(
        TaskbarPalette palette,
        double opacity,
        AnimationSetting motion)
    {
        var block = new WidgetPreview { Palette = palette, Tag = opacity };
        if (motion == AnimationSetting.Off)
        {
            block.Opacity = opacity;
            return block;
        }

        block.Opacity = 0;
        block.Loaded += (_, _) =>
            SurfaceMotion.Settle(block, "Opacity", block.Tag is double target ? target : 1);
        return block;
    }

    private static StackPanel Blocks() => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = BlockGap,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static Border Strip(TaskbarPalette palette, StackPanel blocks)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = BlockGap,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(blocks);
        row.Children.Add(new Border
        {
            Width = TrayWidth,
            Height = TrayHeight,
            Margin = new Thickness(6, 0, 0, 0),
            CornerRadius = new CornerRadius(3),
            Background = palette.Ink,
            Opacity = TrayOpacity,
        });
        return new Border
        {
            Height = StripHeight,
            Padding = StripPadding,
            CornerRadius = new CornerRadius(StripRadius),
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["HakariLineBrush"],
            Background = palette.Background,
            Child = row,
        };
    }
}
