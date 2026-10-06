using System.Diagnostics;
using System.Globalization;
using Hakari.Core.Configuration;
using Hakari.Core.Currency;
using Hakari.Core.Displays;
using Hakari.Core.Localization;
using Hakari.Surfaces.Motion;
using Hakari.Core.Settings;
using Hakari.Core.Sources;
using Hakari.Core.Startup;
using Hakari.Surfaces.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Storage.Pickers;
using Windows.System;

namespace Hakari.Surfaces.Settings;

/// <summary>What each control does when the user changes it.</summary>
public sealed partial class SettingsPage
{
    private const string SupportAddress = "https://ko-fi.com/phuicmt";
    private const string AnyFileType = "*";
    private const string FileExplorer = "explorer.exe";
    private const int CurrencyCodeLength = 3;

    private void OnWslChecked(object sender, RoutedEventArgs args)
    {
        var mode = ReferenceEquals(sender, WslOff) ? WslScanMode.Off
            : ReferenceEquals(sender, WslAll) ? WslScanMode.All
            : WslScanMode.RunningOnly;
        Save(current => current with { WslMode = mode });
    }

    private async void OnAddFolderClicked(object sender, RoutedEventArgs args)
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
        picker.FileTypeFilter.Add(AnyFileType);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, HostWindowHandle);
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            return;
        }

        var updated = store.Update(current => current with
        {
            ExtraConfigDirectories = AddDistinct(current.ExtraConfigDirectories, folder.Path),
        });
        FillFolders(updated.ExtraConfigDirectories);
    }

    private static List<string> AddDistinct(IReadOnlyList<string> folders, string folder) =>
        folders.Contains(folder, StringComparer.OrdinalIgnoreCase)
            ? [.. folders]
            : [.. folders, folder];

    private void RemoveFolder(string folder)
    {
        var updated = store.Update(current => current with
        {
            ExtraConfigDirectories =
            [
                .. current.ExtraConfigDirectories.Where(existing =>
                    !string.Equals(existing, folder, StringComparison.OrdinalIgnoreCase)),
            ],
        });
        FillFolders(updated.ExtraConfigDirectories);
    }

    /// <summary>Choosing starts from the main display, which is what was shown before.</summary>
    private void OnDisplaysChecked(object sender, RoutedEventArgs args)
    {
        if (filling)
        {
            return;
        }

        var displays = ReferenceEquals(sender, DisplaysPrimary) ? TaskbarDisplays.Primary
            : ReferenceEquals(sender, DisplaysChosen) ? TaskbarDisplays.Chosen
            : TaskbarDisplays.All;
        var updated = store.Update(current => current with
        {
            Displays = displays,
            ChosenDisplays = displays == TaskbarDisplays.Chosen && current.ChosenDisplays.Count == 0
                ? [.. DisplayCatalog.List().Where(display => display.IsPrimary)
                    .Select(display => display.Id)]
                : current.ChosenDisplays,
        });
        FillDisplays(updated);
        RevealDisplayList();
    }

    private void OnTrayIconChecked(object sender, RoutedEventArgs args)
    {
        var style = ReferenceEquals(sender, TrayLogo) ? TrayIconStyle.Logo
            : ReferenceEquals(sender, TrayLimit) ? TrayIconStyle.Limit
            : TrayIconStyle.Automatic;
        Save(current => current with { TrayIcon = style });
    }

    private void SetDisplayChosen(string displayId, bool chosen) =>
        Save(current => current with
        {
            ChosenDisplays = chosen
                ? [.. current.ChosenDisplays.Append(displayId).Distinct()]
                : [.. current.ChosenDisplays.Where(id => id != displayId)],
        });

    private void RevealDisplayList()
    {
        if (DisplayList.Visibility == Visibility.Visible)
        {
            DisplayList.Opacity = 0;
            SurfaceMotion.Settle(DisplayList, "Opacity", 1);
        }
    }

    /// <summary>A drag switches to the user's own order.</summary>
    private void ReorderAccounts(IReadOnlyList<string> order)
    {
        var updated = store.Update(current => current with
        {
            AccountOrdering = AccountOrder.Custom,
            CustomAccountOrder = order,
        });
        RefreshAccounts(updated);
    }
    private void SetAccountShown(string accountId, bool shown)
    {
        var updated = store.Update(current => current with
        {
            HiddenAccounts = shown
                ? [.. current.HiddenAccounts.Where(id => id != accountId)]
                : [.. current.HiddenAccounts.Append(accountId).Distinct()],
        });
        RefreshAccounts(updated);
    }

    private void OnAccountOrderChecked(object sender, RoutedEventArgs args)
    {
        if (filling)
        {
            return;
        }

        var ordering = ReferenceEquals(sender, OrderCustom)
            ? AccountOrder.Custom
            : AccountOrder.MostPressing;
        var updated = store.Update(current => current with
        {
            AccountOrdering = ordering,
            CustomAccountOrder = current.CustomAccountOrder.Count == 0
                ? shownAccountOrder
                : current.CustomAccountOrder,
        });
        RefreshAccounts(updated);
    }

    /// <summary>The rows, the "layout for" list and the preview all follow the new order.</summary>
    private void RefreshAccounts(HakariSettings settings)
    {
        filling = true;
        try
        {
            FillAccounts(settings);
        }
        finally
        {
            filling = false;
        }

        previewFacts = null;
        FillTaskbarAfterAccountChange(settings);
    }

    /// <summary>An empty name removes the nickname. The preview shows it at once.</summary>
    private void SaveNickname(string accountId, string text)
    {
        var nickname = text.Trim();
        var updated = store.Update(current =>
        {
            var names = new Dictionary<string, string>(current.AccountNicknames);
            if (nickname.Length == 0)
            {
                names.Remove(accountId);
            }
            else
            {
                names[accountId] = nickname;
            }

            return current with { AccountNicknames = names };
        });
        ShowPreview(updated);
    }

    /// <summary>Empty or not a positive number goes back to the plan's list price.</summary>
    private void SavePlanPrice(string accountId, string text)
    {
        var hasPrice = decimal.TryParse(
            text.Trim(),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var price) && price > 0;
        store.Update(current =>
        {
            var prices = new Dictionary<string, decimal>(current.PlanPriceOverrides);
            if (hasPrice)
            {
                prices[accountId] = price;
            }
            else
            {
                prices.Remove(accountId);
            }

            return current with { PlanPriceOverrides = prices };
        });
    }

    private void OnNotifyClicked(object sender, RoutedEventArgs args) =>
        Save(current => current with { NotifyOnLimits = NotifyToggle.IsChecked == true });

    private void OnRenewSignInClicked(object sender, RoutedEventArgs args) =>
        Save(current => current with
        {
            RefreshSignInAutomatically = RenewSignInToggle.IsChecked == true,
        });

    private void OnStartupClicked(object sender, RoutedEventArgs args)
    {
        if (ResidentLocation.Read() is { } resident)
        {
            StartupRegistration.Set(resident, StartupToggle.IsChecked == true);
        }
    }

    private void OnMotionChecked(object sender, RoutedEventArgs args)
    {
        var animation = ReferenceEquals(sender, MotionFull) ? AnimationSetting.Full
            : ReferenceEquals(sender, MotionReduced) ? AnimationSetting.Reduced
            : ReferenceEquals(sender, MotionOff) ? AnimationSetting.Off
            : AnimationSetting.FollowWindows;
        Save(current => current with { Animation = animation });
    }

    private void OnCurrencyChecked(object sender, RoutedEventArgs args)
    {
        if (filling || sender is not HakariSegment { Tag: string code })
        {
            return;
        }

        OtherCurrencyBox.Text = string.Empty;
        Save(current => current with { Currency = code });
    }

    private void OnOtherCurrencyKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Enter)
        {
            args.Handled = true;
            ApplyOtherCurrency();
        }
    }

    private void OnOtherCurrencyLostFocus(object sender, RoutedEventArgs args) =>
        ApplyOtherCurrency();

    /// <summary>Anything that is not three letters is left unsaved until it is.</summary>
    private void ApplyOtherCurrency()
    {
        var code = OtherCurrencyBox.Text.Trim().ToUpperInvariant();
        if (code.Length != CurrencyCodeLength || !code.All(char.IsAsciiLetter))
        {
            return;
        }

        filling = true;
        try
        {
            SelectCurrency(code);
        }
        finally
        {
            filling = false;
        }

        Save(current => current with { Currency = code });
    }

    private void OnRateChecked(object sender, RoutedEventArgs args)
    {
        var mode = ReferenceEquals(sender, RateLatest) ? RateMode.Latest : RateMode.UsageDay;
        Save(current => current with { RateMode = mode });
    }

    /// <summary>Saved, then the window is rebuilt in the new language by the app.</summary>
    private void OnLanguageChecked(object sender, RoutedEventArgs args)
    {
        if (filling)
        {
            return;
        }

        var language = ReferenceEquals(sender, LanguageEnglish) ? Texts.English
            : ReferenceEquals(sender, LanguageThai) ? Texts.Thai
            : Texts.FollowSystem;
        store.Update(current => current with { Language = language });
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPauseClicked(object sender, RoutedEventArgs args) =>
        Save(current => current with { Paused = PauseToggle.IsChecked == true });

    private async void OnSupportClicked(object sender, RoutedEventArgs args) =>
        await Launcher.LaunchUriAsync(new Uri(SupportAddress));

    private void OnOpenDataFolderClicked(object sender, RoutedEventArgs args)
    {
        Directory.CreateDirectory(HakariPaths.DataDirectory);
        Process.Start(FileExplorer, $"\"{HakariPaths.DataDirectory}\"");
    }
}
