using Hakari.Surfaces.Flyout;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Hakari.Surfaces.Popups;

/// <summary>
/// The widget's hover card: each shown account's limits with their reset times, its money
/// and burn rate. It never takes focus, so hovering never disturbs the window in use.
/// </summary>
public sealed partial class TooltipWindow : PopupWindow
{
    private const double MaximumWidth = 320;
    private const double TitleSize = 13;
    private const double LineSize = 12;
    private const double SmallSize = 11;
    private const double LineSpacing = 2;
    private const double LimitSpacing = 8;
    private const double SectionSpacing = 10;
    private const double AccountSpacing = 12;
    private const double TooltipRise = 6;
    private const double NameColumn = 124;
    private const double ValueColumn = 44;
    private const double MeterHeight = 4;
    private const double DividerHeight = 1;

    private int generation;

    public TooltipWindow()
        : base(MaximumWidth)
    {
    }

    /// <summary>
    /// Reads off the UI thread, so a hover never stalls the app; a hide or a newer hover
    /// while reading drops this one.
    /// </summary>
    public async void ShowAt(int anchorX, int anchorY)
    {
        // Async void: anything thrown here would end the window process, so all of it is
        // caught, the drawing included.
        try
        {
            await ShowAtAsync(anchorX, anchorY);
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception, "hover card");
        }
    }

    private async Task ShowAtAsync(int anchorX, int anchorY)
    {
        var mine = ++generation;
        var accounts = await Task.Run(TooltipDataLoader.Load);
        if (mine != generation)
        {
            return;
        }

        if (accounts.Count == 0)
        {
            HidePopup();
            return;
        }

        var stack = new StackPanel { Spacing = AccountSpacing };
        var meters = new List<ScaleTransform>();
        for (var index = 0; index < accounts.Count; index++)
        {
            if (index > 0)
            {
                stack.Children.Add(Divider());
            }

            stack.Children.Add(AccountBlock(accounts[index], meters));
        }

        Body = stack;
        stack.Opacity = 0;
        ShowAbove(anchorX, anchorY, activate: false, Flyout.AnchorSide.Center);
        SurfaceMotion.Enter(stack, fromY: TooltipRise);
        GrowMeters(meters);
    }

    /// <summary>Under full motion the meters fill from empty as the card comes in.</summary>
    private static void GrowMeters(List<ScaleTransform> meters)
    {
        if (SurfaceMotion.Current() != Hakari.Core.Settings.AnimationSetting.Full)
        {
            return;
        }

        var storyboard = new Storyboard();
        foreach (var meter in meters)
        {
            storyboard.Children.Add(SurfaceMotion.Animate(
                meter, "ScaleX", 0, meter.ScaleX, SurfaceMotion.Entrance));
        }

        storyboard.Begin();
    }

    protected override void OnHiding() => generation++;

    /// <summary>
    /// Name and plan, then each limit as a row with a meter, then money and freshness in
    /// quieter type.
    /// </summary>
    private static StackPanel AccountBlock(TooltipAccount account, List<ScaleTransform> meters)
    {
        var block = new StackPanel { Spacing = SectionSpacing };
        block.Children.Add(Header(account));
        if (account.Limits is { Count: > 0 } limits)
        {
            var rows = new StackPanel { Spacing = LimitSpacing };
            foreach (var limit in limits)
            {
                rows.Children.Add(LimitRow(limit, meters));
            }

            block.Children.Add(rows);
        }

        var footer = new StackPanel { Spacing = LineSpacing };
        foreach (var line in account.Lines)
        {
            footer.Children.Add(Line(line, LineSize, "HakariInkMutedBrush"));
        }

        if (account.Updated.Length > 0)
        {
            footer.Children.Add(Line(account.Updated, SmallSize, "HakariInkFaintBrush"));
        }

        if (footer.Children.Count > 0)
        {
            block.Children.Add(footer);
        }

        return block;
    }

    private static Grid Header(TooltipAccount account)
    {
        var header = new Grid { ColumnSpacing = LimitSpacing };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock
        {
            Text = account.Title,
            FontSize = TitleSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("HakariInkBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = account.Plan.Length == 0 ? TextWrapping.Wrap : TextWrapping.NoWrap,
        });
        if (account.Plan.Length > 0)
        {
            var plan = Line(account.Plan, SmallSize, "HakariInkFaintBrush");
            plan.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(plan, 1);
            header.Children.Add(plan);
        }

        return header;
    }

    /// <summary>Name, meter and value on one line; when it resets, and fills, below.</summary>
    private static StackPanel LimitRow(TooltipLimit limit, List<ScaleTransform> meters)
    {
        var line = new Grid { ColumnSpacing = LimitSpacing };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(NameColumn) });
        line.ColumnDefinitions.Add(new ColumnDefinition());
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ValueColumn) });
        var name = Line(limit.Name, LineSize, "HakariInkMutedBrush");
        name.TextWrapping = TextWrapping.NoWrap;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        line.Children.Add(name);

        var fill = new ScaleTransform { ScaleX = limit.Fraction, ScaleY = 1 };
        meters.Add(fill);
        var meter = new Border
        {
            Height = MeterHeight,
            CornerRadius = new CornerRadius(MeterHeight / 2),
            Background = Brush("HakariLineBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new Border
            {
                CornerRadius = new CornerRadius(MeterHeight / 2),
                Background = FlyoutBrushes.ForTone(limit.Tone),
                RenderTransformOrigin = new Windows.Foundation.Point(0, 0.5),
                RenderTransform = fill,
            },
        };
        Grid.SetColumn(meter, 1);
        line.Children.Add(meter);

        var value = new TextBlock
        {
            Text = limit.Value,
            FontSize = LineSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = limit.Tone is Tone.Warning or Tone.Critical
                ? FlyoutBrushes.ForTone(limit.Tone)
                : Brush("HakariInkBrush"),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(value, 2);
        line.Children.Add(value);

        var row = new StackPanel { Spacing = LineSpacing };
        row.Children.Add(line);
        if (limit.Reset.Length > 0 || limit.FullAt.Length > 0)
        {
            row.Children.Add(When(limit));
        }

        return row;
    }

    /// <summary>The reset in faint type, and the forecast, if any, in the warning colour.</summary>
    private static TextBlock When(TooltipLimit limit)
    {
        var when = Line(string.Empty, SmallSize, "HakariInkFaintBrush");
        if (limit.Reset.Length > 0)
        {
            when.Inlines.Add(new Run { Text = limit.Reset });
        }

        if (limit.FullAt.Length > 0)
        {
            if (limit.Reset.Length > 0)
            {
                when.Inlines.Add(new Run { Text = Separator });
            }

            when.Inlines.Add(new Run
            {
                Text = limit.FullAt,
                Foreground = Brush("HakariWarnBrush"),
            });
        }

        return when;
    }

    private const string Separator = " · ";

    private static Border Divider() => new()
    {
        Height = DividerHeight,
        Background = Brush("HakariLineBrush"),
    };

    private static TextBlock Line(string text, double size, string brushKey) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = Brush(brushKey),
        TextWrapping = TextWrapping.Wrap,
    };
}
