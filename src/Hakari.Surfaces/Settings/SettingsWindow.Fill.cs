using Hakari.Core.Accounts;
using Hakari.Core.Configuration;
using Hakari.Core.Currency;
using Hakari.Core.Displays;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Hakari.Core.Settings;
using Hakari.Core.Sources;
using Hakari.Core.Startup;
using Hakari.Surfaces.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Settings;

/// <summary>Setting the controls from the saved settings and the index.</summary>
public sealed partial class SettingsWindow
{
    private const string FolderGlyph = "";
    private const string AccountGlyph = "";
    private const string DisplayGlyph = "";
    /// <summary>Account rows drag by the whole row; the grip shows that they can.</summary>
    private const string GripGlyph = "";
    private const double AccountControlSpacing = 4;
    private const double NicknameBoxWidth = 110;
    private const int NicknameMaximumLength = 12;
    private const string CurrencyGroup = "Currency";
    private const string ProjectsFolderName = "projects";
    private const string DetailSeparator = " · ";
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
        TrayAutomatic.IsChecked = settings.TrayIcon == TrayIconStyle.Automatic;
        TrayLogo.IsChecked = settings.TrayIcon == TrayIconStyle.Logo;
        TrayLimit.IsChecked = settings.TrayIcon == TrayIconStyle.Limit;
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
                Description = display.IsPrimary
                    ? Texts.Get("settings.displays.mainDisplay")
                    : string.Empty,
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
                    ? Texts.Get("settings.folders.hasLogs")
                    : Texts.Get("settings.folders.noLogs"),
                Content = RemoveFolderButton(folder),
            });
        }
    }

    private Button RemoveFolderButton(string folder)
    {
        var button = new Button
        {
            Content = Texts.Get("settings.folders.remove"),
            Style = (Style)Application.Current.Resources["HakariSubtleButton"],
        };
        button.Click += (_, _) => RemoveFolder(folder);
        return button;
    }

    /// <summary>Account ids as listed in the Accounts card, for moving one up or down.</summary>
    private IReadOnlyList<string> shownAccountOrder = [];

    private void FillAccounts(HakariSettings settings)
    {
        AttachAccountReorder();
        AccountList.Children.Clear();
        var accounts = SettingsDataLoader.LoadAccounts();
        if (accounts.Count == 0)
        {
            AccountList.Children.Add(new SettingRow
            {
                Glyph = AccountGlyph,
                Title = Texts.Get("settings.accounts.none"),
                Description = Texts.Get("settings.accounts.none.description"),
                BorderThickness = new Thickness(0),
            });
        }

        // Every account, hidden ones too, in the order the taskbar would use.
        var ordered = AccountArrangement.Arrange(
            accounts,
            account => account.AccountId,
            account => 0,
            settings with { HiddenAccounts = [], AccountOrdering = AccountOrder.Custom });
        shownAccountOrder = [.. ordered.Select(account => account.AccountId)];
        foreach (var account in ordered)
        {
            var isFirst = AccountList.Children.Count == 0;
            AccountList.Children.Add(AccountRow(account, settings, isFirst));
        }

        OrderPressing.IsChecked = settings.AccountOrdering == AccountOrder.MostPressing;
        OrderCustom.IsChecked = settings.AccountOrdering == AccountOrder.Custom;
        RenewSignInToggle.IsChecked = settings.RefreshSignInAutomatically;
    }

    /// <summary>
    /// The email stays the title; drag the row to reorder. On the right: the nickname field
    /// and whether the account shows at all, which keeps a signed-out account available.
    /// </summary>
    private SettingRow AccountRow(AccountInfo account, HakariSettings settings, bool isFirst)
    {
        var details = new[] { PlanNames.Short(account.Plan), account.OrganizationName }
            .Where(detail => !string.IsNullOrWhiteSpace(detail));
        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = AccountControlSpacing,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var nickname = settings.NicknameOf(account.AccountId);
        controls.Children.Add(NicknameBox(account.AccountId, nickname));
        controls.Children.Add(ShowToggle(account.AccountId, settings));

        var row = new SettingRow
        {
            Glyph = GripGlyph,
            Title = AccountLabels.Full(account),
            Description = string.Join(DetailSeparator, details),
            Content = controls,
        };
        if (isFirst)
        {
            row.BorderThickness = new Thickness(0);
        }

        return row;
    }

    private bool accountReorderAttached;

    private void AttachAccountReorder()
    {
        if (accountReorderAttached)
        {
            return;
        }

        accountReorderAttached = true;
        RowReorder.Attach(AccountList, order => ReorderAccounts(
            [.. order.Where(index => index < shownAccountOrder.Count)
                .Select(index => shownAccountOrder[index])]));
    }

    private HakariToggle ShowToggle(string accountId, HakariSettings settings)
    {
        var toggle = new HakariToggle { IsChecked = !settings.HiddenAccounts.Contains(accountId) };
        ToolTipService.SetToolTip(toggle, Texts.Get("settings.accounts.show"));
        toggle.Click += (_, _) => SetAccountShown(accountId, toggle.IsChecked == true);
        return toggle;
    }

    private TextBox NicknameBox(string accountId, string? nickname)
    {
        var box = new TextBox
        {
            Text = nickname ?? string.Empty,
            Width = NicknameBoxWidth,
            MaxLength = NicknameMaximumLength,
            Style = (Style)Application.Current.Resources["HakariTextBox"],
        };
        ToolTipService.SetToolTip(box, Texts.Get("settings.nickname"));
        box.LostFocus += (_, _) => SaveNickname(accountId, box.Text);
        box.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Enter)
            {
                args.Handled = true;
                SaveNickname(accountId, box.Text);
            }
        };
        return box;
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
        LanguageSystem.IsChecked = settings.Language == Texts.FollowSystem;
        LanguageEnglish.IsChecked = settings.Language == Texts.English;
        LanguageThai.IsChecked = settings.Language == Texts.Thai;
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
        StartupRow.Description = resident is null ? Texts.Get("settings.startup.unavailable") : "";
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
        VersionRow.Title = Texts.Format("settings.version", version?.ToString(VersionParts));
        DataFolderRow.Description = HakariPaths.DataDirectory;
    }
}
