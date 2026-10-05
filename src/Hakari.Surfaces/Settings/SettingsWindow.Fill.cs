using Hakari.Core.Accounts;
using Hakari.Core.Configuration;
using Hakari.Core.Currency;
using Hakari.Core.Displays;
using Hakari.Core.Presentation;
using Hakari.Core.Settings;
using Hakari.Core.Sources;
using Hakari.Core.Startup;
using Hakari.Surfaces.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Settings;

/// <summary>Setting the controls from the saved settings and the index.</summary>
public sealed partial class SettingsWindow
{
    private const string FolderGlyph = "";
    private const string AccountGlyph = "";
    private const string DisplayGlyph = "";
    private const string CurrencyGroup = "Currency";
    private const string ProjectsFolderName = "projects";
    private const string DetailSeparator = " · ";
    private const string VersionFormat = "Hakari {0}";
    private const int VersionParts = 3;

    /// <summary>Indented under the row that opened them.</summary>
    private static readonly Thickness NestedRowPadding = new(40, 10, 16, 10);

    private static readonly string[] CommonCurrencies =
    [
        CurrencyCodes.Dollar, "THB", "EUR", "JPY",
    ];

    private void Fill()
    {
        filling = true;
        try
        {
            var settings = store.Load();
            FillTaskbar(settings);
            FillSources(settings);
            FillAccounts(settings);
            FillGeneral(settings);
            FillAbout();
        }
        finally
        {
            filling = false;
        }
    }

    private void FillSources(HakariSettings settings)
    {
        WslOff.IsChecked = settings.WslMode == WslScanMode.Off;
        WslRunningOnly.IsChecked = settings.WslMode == WslScanMode.RunningOnly;
        WslAll.IsChecked = settings.WslMode == WslScanMode.All;
        FillFolders(settings.ExtraConfigDirectories);
        DisplaysPrimary.IsChecked = settings.Displays == TaskbarDisplays.Primary;
        DisplaysAll.IsChecked = settings.Displays == TaskbarDisplays.All;
        DisplaysChosen.IsChecked = settings.Displays == TaskbarDisplays.Chosen;
        FillDisplays(settings);
    }

    /// <summary>One switch per connected display, shown only while choosing.</summary>
    private void FillDisplays(HakariSettings settings)
    {
        DisplayList.Children.Clear();
        DisplayList.Visibility = settings.Displays == TaskbarDisplays.Chosen
            ? Visibility.Visible
            : Visibility.Collapsed;
        var chosen = settings.ChosenDisplays.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var display in DisplayCatalog.List())
        {
            var toggle = new HakariToggle { IsChecked = chosen.Contains(display.Id) };
            toggle.Click += (_, _) => SetDisplayChosen(display.Id, toggle.IsChecked == true);
            DisplayList.Children.Add(new SettingRow
            {
                Glyph = DisplayGlyph,
                Title = display.Name,
                Description = display.IsPrimary ? "Main display" : string.Empty,
                Content = toggle,
                Padding = NestedRowPadding,
            });
        }
    }

    private void FillFolders(IReadOnlyList<string> folders)
    {
        FolderList.Children.Clear();
        foreach (var folder in folders)
        {
            FolderList.Children.Add(new SettingRow
            {
                Glyph = FolderGlyph,
                Title = folder,
                Description = Directory.Exists(Path.Combine(folder, ProjectsFolderName))
                    ? "Has Claude Code logs"
                    : "No Claude Code logs here yet",
                Content = RemoveFolderButton(folder),
            });
        }
    }

    private Button RemoveFolderButton(string folder)
    {
        var button = new Button
        {
            Content = "Remove",
            Style = (Style)Application.Current.Resources["HakariSubtleButton"],
        };
        button.Click += (_, _) => RemoveFolder(folder);
        return button;
    }

    private void FillAccounts(HakariSettings settings)
    {
        AccountList.Children.Clear();
        var accounts = SettingsDataLoader.LoadAccounts();
        if (accounts.Count == 0)
        {
            AccountList.Children.Add(new SettingRow
            {
                Glyph = AccountGlyph,
                Title = "No accounts yet",
                Description = "They appear once Claude Code has signed in on a source.",
                BorderThickness = new Thickness(0),
            });
        }

        foreach (var account in accounts)
        {
            AccountList.Children.Add(AccountRow(account, isFirst: AccountList.Children.Count == 0));
        }

        RenewSignInToggle.IsChecked = settings.RefreshSignInAutomatically;
    }

    private static SettingRow AccountRow(AccountInfo account, bool isFirst)
    {
        var details = new[] { PlanNames.Short(account.Plan), account.OrganizationName }
            .Where(detail => !string.IsNullOrWhiteSpace(detail));
        var row = new SettingRow
        {
            Glyph = AccountGlyph,
            Title = account.Email ?? account.DisplayName ?? "Signed-in account",
            Description = string.Join(DetailSeparator, details),
        };
        if (isFirst)
        {
            row.BorderThickness = new Thickness(0);
        }

        return row;
    }

    private void FillGeneral(HakariSettings settings)
    {
        FillStartup();
        MotionFollow.IsChecked = settings.Animation == AnimationSetting.FollowWindows;
        MotionFull.IsChecked = settings.Animation == AnimationSetting.Full;
        MotionReduced.IsChecked = settings.Animation == AnimationSetting.Reduced;
        MotionOff.IsChecked = settings.Animation == AnimationSetting.Off;
        SelectCurrency(settings.Currency);
        RateUsageDay.IsChecked = settings.RateMode == RateMode.UsageDay;
        RateLatest.IsChecked = settings.RateMode == RateMode.Latest;
        PauseToggle.IsChecked = settings.Paused;
    }

    /// <summary>
    /// Registering needs Hakari.exe's path, which it records when it starts; this window is a
    /// different program.
    /// </summary>
    private void FillStartup()
    {
        var resident = ResidentLocation.Read();
        StartupToggle.IsEnabled = resident is not null;
        StartupToggle.IsChecked = resident is not null
            && StartupRegistration.IsRegistered(resident);
        StartupRow.Description = resident is null ? "Available once Hakari is running." : "";
    }

    private void BuildCurrencyChoices()
    {
        foreach (var code in CommonCurrencies)
        {
            var choice = new HakariSegment
            {
                GroupName = CurrencyGroup,
                Content = code,
                Tag = code,
            };
            choice.Checked += OnCurrencyChecked;
            CurrencyChoices.Children.Add(choice);
        }
    }

    /// <summary>A code not offered as a choice goes into the text field instead.</summary>
    private void SelectCurrency(string currency)
    {
        var isCommon = false;
        foreach (var choice in CurrencyChoices.Choices)
        {
            var matches = string.Equals(
                choice.Tag as string,
                currency,
                StringComparison.OrdinalIgnoreCase);
            choice.IsChecked = matches;
            isCommon |= matches;
        }

        OtherCurrencyBox.Text = isCommon ? string.Empty : currency;
    }

    private void FillAbout()
    {
        var version = typeof(SettingsWindow).Assembly.GetName().Version;
        VersionRow.Title = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            VersionFormat,
            version?.ToString(VersionParts));
        DataFolderRow.Description = HakariPaths.DataDirectory;
    }
}
