using Hakari.Core.Localization;
using Hakari.Core.Settings;
using Hakari.Core.Startup;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Services.Store;

namespace Hakari.Surfaces.Settings;

/// <summary>A tip on offer: its Store title and local price, and the product to buy.</summary>
/// <param name="Product">Null for a preview tip, which buys nothing.</param>
internal sealed record TipOffer(string Title, string Price, StoreProduct? Product);

/// <summary>
/// Tips through the Microsoft Store, in the Store build only: the app's consumable add-ons,
/// read from the Store with their local prices, so a new tip size needs no app update. A tip
/// unlocks nothing; it is fulfilled at once so it can be given again. With no add-ons, or
/// outside the Store build, the button stays hidden.
/// </summary>
internal static class StoreTips
{
    private const string DeveloperManaged = "Consumable";
    private const string StoreManaged = "UnmanagedConsumable";
    private const double ListWidth = 260;
    private const double RowGap = 2;
    private static readonly Thickness ListPadding = new(0, 4, 0, 4);

    private const double PictureSize = 18;
    private const double PictureColumn = 26;

    /// <summary>A picture per tip, smallest first, in color from the emoji font.</summary>
    private static readonly string[] Pictures = ["\u2615", "\U0001F370", "\U0001F371"];

    /// <summary>
    /// Set to 1 on a build run from source to see the list without the Store. Its tips have
    /// sample prices and buy nothing.
    /// </summary>
    private const string PreviewVariable = "HAKARI_TIP_PREVIEW";

    private static readonly TipOffer[] PreviewTips =
    [
        new("Coffee", "\u0E3F34.00", null),
        new("Coffee and cake", "\u0E3F104.00", null),
        new("Lunch", "\u0E3F174.00", null),
    ];

    private static StoreContext? context;
    private static IntPtr contextWindow;

    /// <summary>
    /// The tips on offer in the order of their product ids (name them "tip-1-small",
    /// "tip-2-coffee" and so on); empty outside the Store build unless previewing.
    /// </summary>
    public static async Task<IReadOnlyList<TipOffer>> ListAsync(IntPtr window)
    {
        if (!PackageIdentity.IsPackaged)
        {
            return Environment.GetEnvironmentVariable(PreviewVariable) == "1" ? PreviewTips : [];
        }

        if (Context(window) is not { } store)
        {
            return [];
        }

        try
        {
            var result = await store.GetAssociatedStoreProductsAsync(
                [DeveloperManaged, StoreManaged]);
            return result.ExtendedError is null
                ? [.. result.Products.Values
                    .OrderBy(product => product.InAppOfferToken, StringComparer.Ordinal)
                    .Select(product => new TipOffer(
                        product.Title, product.Price.FormattedPrice, product))]
                : [];
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception, "store tips");
            return [];
        }
    }

    /// <summary>A list of tips under the button, each with a picture and its Store price.</summary>
    public static void ShowList(
        FrameworkElement anchor,
        IReadOnlyList<TipOffer> tips,
        IntPtr window,
        Action<string> thanked)
    {
        var list = new StackPanel { Width = ListWidth, Padding = ListPadding, Spacing = RowGap };

        // Under the button, its right edge on the button's, and free to pass the window's
        // edge so a button near the bottom does not squeeze the list against it.
        var flyout = new Microsoft.UI.Xaml.Controls.Flyout
        {
            Content = list,
            Placement = FlyoutPlacementMode.BottomEdgeAlignedRight,
            ShouldConstrainToRootBounds = false,
            FlyoutPresenterStyle = (Style)Application.Current.Resources["HakariListPresenter"],
        };
        SurfaceMotion.EnterOnOpen(flyout);
        for (var index = 0; index < tips.Count; index++)
        {
            var tip = tips[index];
            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(PictureColumn) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock
            {
                Text = Pictures[Math.Min(index, Pictures.Length - 1)],
                FontSize = PictureSize,
                FontFamily = new FontFamily("Segoe UI Emoji"),
                IsColorFontEnabled = true,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            var title = new TextBlock
            {
                Text = tip.Title,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(title, 1);
            row.Children.Add(title);
            var price = new TextBlock
            {
                Text = tip.Price,
                Foreground = (Brush)Application.Current.Resources["HakariInkMutedBrush"],
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(price, 2);
            row.Children.Add(price);
            var button = new Button
            {
                Content = row,
                Style = (Style)Application.Current.Resources["HakariListItemButton"],
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };
            button.Click += async (_, _) =>
            {
                flyout.Hide();
                try
                {
                    if (tip.Product is { } product && await BuyAsync(product, window))
                    {
                        thanked(Texts.Get("settings.support.thanks"));
                    }
                }
                catch (Exception exception)
                {
                    // Async void: an escaping error would end the window process.
                    CrashLog.Write(exception, "store tip");
                }
            };
            list.Children.Add(button);
        }

        flyout.ShowAt(anchor);
    }

    /// <summary>The Store's own rating and review dialog, over the given window.</summary>
    public static async Task RequestReviewAsync(IntPtr window)
    {
        if (!PackageIdentity.IsPackaged || Context(window) is not { } store)
        {
            return;
        }

        try
        {
            await store.RequestRateAndReviewAppAsync();
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception, "store review");
        }
    }

    /// <returns>True when the tip went through.</returns>
    private static async Task<bool> BuyAsync(StoreProduct tip, IntPtr window)
    {
        if (Context(window) is not { } store)
        {
            return false;
        }

        try
        {
            var result = await store.RequestPurchaseAsync(tip.StoreId);
            var bought = result.Status is StorePurchaseStatus.Succeeded
                or StorePurchaseStatus.AlreadyPurchased;
            if (bought && tip.ProductKind == DeveloperManaged)
            {
                // Given at once, so the same tip can be given again later.
                await store.ReportConsumableFulfillmentAsync(tip.StoreId, 1, Guid.NewGuid());
            }

            return result.Status == StorePurchaseStatus.Succeeded;
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception, "store tip");
            return false;
        }
    }

    /// <summary>A desktop app's Store dialogs need the window that owns them.</summary>
    /// <remarks>
    /// Kept per window: the dashboard is rebuilt after a close or a language change, and a
    /// context tied to the old window's handle could no longer show the Store's dialog.
    /// </remarks>
    private static StoreContext? Context(IntPtr window)
    {
        if (context is not null && contextWindow == window)
        {
            return context;
        }

        if (window == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            context = StoreContext.GetDefault();
            WinRT.Interop.InitializeWithWindow.Initialize(context, window);
            contextWindow = window;
            return context;
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception, "store context");
            context = null;
            return null;
        }
    }
}
