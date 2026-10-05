using System.Diagnostics;
using Hakari.Core.Configuration;
using Hakari.Core.Currency;
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
public sealed partial class SettingsWindow
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
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WindowHandle);
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

    private void OnEveryDisplayClicked(object sender, RoutedEventArgs args) =>
        Save(current => current with
        {
            ShowOnSecondaryTaskbars = EveryDisplayToggle.IsChecked == true,
        });

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
