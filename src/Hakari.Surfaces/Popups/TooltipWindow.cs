using Hakari.Surfaces.Motion;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
    private const double LineSpacing = 2;
    private const double AccountSpacing = 10;
    private const string OpacityPath = "Opacity";

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
        foreach (var account in accounts)
        {
            stack.Children.Add(AccountBlock(account));
        }

        Body = stack;
        stack.Opacity = 0;
        ShowAbove(anchorX, anchorY, activate: false, Flyout.AnchorSide.Center);
        SurfaceMotion.Settle(stack, OpacityPath, 1);
    }

    protected override void OnHiding() => generation++;

    private static StackPanel AccountBlock(TooltipAccount account)
    {
        var block = new StackPanel { Spacing = LineSpacing };
        block.Children.Add(new TextBlock
        {
            Text = account.Title,
            FontSize = TitleSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("HakariInkBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 0, LineSpacing * 2),
        });
        foreach (var line in account.Lines)
        {
            block.Children.Add(Line(line, "HakariInkMutedBrush"));
        }

        block.Children.Add(Line(account.Updated, "HakariInkFaintBrush"));
        return block;
    }

    private static TextBlock Line(string text, string brushKey) => new()
    {
        Text = text,
        FontSize = LineSize,
        Foreground = Brush(brushKey),
        TextWrapping = TextWrapping.Wrap,
    };
}
