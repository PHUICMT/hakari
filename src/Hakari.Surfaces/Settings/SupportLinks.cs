using System.Globalization;
using Hakari.Core.Localization;
using Hakari.Core.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using QRCoder;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Streams;

namespace Hakari.Surfaces.Settings;

/// <summary>
/// The ways to support Hakari from the About card: Ko-fi, GitHub Sponsors, and for Thai
/// users a PromptPay QR code, drawn here from the PromptPay number so nothing is fetched.
/// None is ever pushed; they sit on the About card only.
/// </summary>
internal static class SupportLinks
{
    public const string KoFi = "https://ko-fi.com/phuicmt";
    public const string GitHubSponsors = "https://github.com/sponsors/PHUICMT";

    /// <summary>The PromptPay phone number or ID; empty keeps the button hidden.</summary>
    public const string PromptPayId = "";

    private const string ThaiRegion = "TH";
    private const double IconSize = 14;

    /// <summary>Each service in its own color and mark, so each button reads at a glance.</summary>
    public static void Brand(Button button, SupportService service)
    {
        (Windows.UI.Color Color, string? Glyph, string? Emoji, string Text) look = service switch
        {
            SupportService.GitHubSponsors =>
                (Windows.UI.Color.FromArgb(0xFF, 0xBF, 0x39, 0x89), "", null, "Sponsor"),
            SupportService.KoFi =>
                (Windows.UI.Color.FromArgb(0xFF, 0xFF, 0x5E, 0x5B), null, "☕", "Ko-fi"),
            _ => (Windows.UI.Color.FromArgb(0xFF, 0x11, 0x35, 0x66), "", null, "PromptPay"),
        };
        var (color, glyph, emoji, text) = look;
        var brush = new SolidColorBrush(color);
        button.Background = brush;
        button.BorderBrush = brush;
        button.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        content.Children.Add(glyph is not null
            ? new FontIcon
            {
                Glyph = glyph,
                FontSize = IconSize,
                FontFamily = (FontFamily)Application.Current.Resources["HakariIconFont"],
                Foreground = button.Foreground,
            }
            : new TextBlock
            {
                Text = emoji,
                FontSize = IconSize,
                FontFamily = new FontFamily("Segoe UI Emoji"),
                VerticalAlignment = VerticalAlignment.Center,
            });
        content.Children.Add(new TextBlock
        {
            Text = text,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        button.Content = content;
    }
    private const int ModulePixels = 8;
    private const double CodeSize = 200;
    private const double CardWidth = 240;

    /// <summary>Thai by the app's language or by the region Windows is set to.</summary>
    public static bool ShowsPromptPay =>
        PromptPay.Payload(PromptPayId) is not null
        && (Texts.Language == Texts.Thai
            || string.Equals(
                RegionInfo.CurrentRegion.TwoLetterISORegionName,
                ThaiRegion,
                StringComparison.OrdinalIgnoreCase));

    /// <summary>The code on a white square, so any banking app can read it in any theme.</summary>
    public static async void ShowPromptPay(FrameworkElement anchor)
    {
        if (PromptPay.Payload(PromptPayId) is not { } payload)
        {
            return;
        }

        var image = new Image { Width = CodeSize, Height = CodeSize };
        image.Source = await CodeImage(payload);
        var body = new StackPanel { Spacing = 10, Width = CardWidth, Padding = new Thickness(12) };
        body.Children.Add(new Border
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.White),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = image,
        });
        body.Children.Add(new TextBlock
        {
            Text = Texts.Get("settings.support.promptPay.note"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["HakariInkMutedBrush"],
        });
        var flyout = new Microsoft.UI.Xaml.Controls.Flyout
        {
            Content = body,
            Placement = FlyoutPlacementMode.Bottom,
            FlyoutPresenterStyle = (Style)Application.Current.Resources["HakariListPresenter"],
        };
        flyout.ShowAt(anchor);
    }

    private static async Task<BitmapImage> CodeImage(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(ModulePixels);
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(png.AsBuffer());
        stream.Seek(0);
        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(stream);
        return bitmap;
    }
}

/// <summary>The ways to support Hakari, for their buttons.</summary>
internal enum SupportService
{
    GitHubSponsors,
    KoFi,
    PromptPay,
}
