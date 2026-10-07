using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Hakari.Core.Settings;
using Hakari.Surfaces.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Dashboard;

/// <summary>Period, account, source and model, plus refresh; the same on every page.</summary>
internal sealed partial class DashboardFilterBar : StackPanel
{
    private const string AllChoice = "";
    private const string EarlierChoice = "earlier";
    private const string RefreshGlyph = "";
    private const double RefreshGlyphSize = 12;
    private const double BarSpacing = 8;
    private static readonly Thickness RefreshPadding = new(9, 7, 9, 7);

    private static readonly (DashboardPeriod Period, string TextKey)[] Periods =
    [
        (DashboardPeriod.Today, "dashboard.period.today"),
        (DashboardPeriod.SevenDays, "dashboard.period.week"),
        (DashboardPeriod.ThirtyDays, "dashboard.period.month"),
        (DashboardPeriod.AllTime, "dashboard.period.all"),
    ];

    private readonly StackPanel firstLine = Line();
    private readonly StackPanel secondLine = Line();
    private readonly HakariSelect accountSelect = new();
    private readonly HakariSelect sourceSelect = new();
    private readonly HakariSelect modelSelect = new();

    public DashboardFilterBar()
    {
        Orientation = Orientation.Horizontal;
        Spacing = BarSpacing;
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Center;
        firstLine.Children.Add(PeriodChoices());
        firstLine.Children.Add(RefreshButton());
        secondLine.Children.Add(accountSelect);
        secondLine.Children.Add(sourceSelect);
        secondLine.Children.Add(modelSelect);
        Children.Add(firstLine);
        Children.Add(secondLine);
        FillSelects();
        accountSelect.Selected += (_, value) => Change(DashboardFilter.Current with
        {
            AccountId = AccountOf((string)value),
        });
        sourceSelect.Selected += (_, value) => Change(DashboardFilter.Current with
        {
            SourceId = (string)value == AllChoice ? null : (string)value,
        });
        modelSelect.Selected += (_, value) => Change(DashboardFilter.Current with
        {
            Model = (string)value == AllChoice ? null : (string)value,
        });
    }

    private static StackPanel Line() =>
        new() { Orientation = Orientation.Horizontal, Spacing = BarSpacing };

    /// <summary>One line when there is room; account and source go under it when not.</summary>
    public void SetTwoLines(bool twoLines) =>
        Orientation = twoLines ? Orientation.Vertical : Orientation.Horizontal;

    /// <summary>The filter changed (or refresh was asked for); the page reads again.</summary>
    public event EventHandler? Changed;

    private HakariSegmented PeriodChoices()
    {
        var choices = new HakariSegmented();
        foreach (var (period, textKey) in Periods)
        {
            var choice = new HakariSegment
            {
                GroupName = "DashboardPeriod" + GetHashCode(),
                Content = Texts.Get(textKey),
                IsChecked = DashboardFilter.Current.Period == period,
            };
            choice.Checked += (_, _) => Change(DashboardFilter.Current with { Period = period });
            choices.Children.Add(choice);
        }

        return choices;
    }

    /// <summary>"All" at once; the accounts and sources are read off the UI thread.</summary>
    private async void FillSelects()
    {
        var allAccounts = (AllChoice, Texts.Get("dashboard.allAccounts"));
        var allSources = (AllChoice, Texts.Get("dashboard.allSources"));
        var allModels = (AllChoice, Texts.Get("dashboard.allModels"));
        accountSelect.SetChoices([allAccounts], AllChoice);
        sourceSelect.SetChoices([allSources], AllChoice);
        modelSelect.SetChoices([allModels], AllChoice);

        var (accounts, sourceIds, settings, hasEarlier, models) = await Task.Run(() => (
            DashboardData.Accounts(),
            DashboardData.SourceIds(),
            SettingsStore.Default.Load(),
            DashboardData.HasUsageBeforeAccounts(),
            DashboardData.Models()));
        modelSelect.SetChoices(
            models.Select(model => ((object)model, model)).Prepend(allModels),
            DashboardFilter.Current.Model ?? AllChoice);
        var accountChoices = accounts
            .Select(account => ((object)account.AccountId, AccountLabels.Full(
                account,
                settings.NicknameOf(account.AccountId))))
            .Prepend(allAccounts)
            .Concat(hasEarlier
                ? [((object)EarlierChoice, Texts.Get("dashboard.accounts.earlier"))]
                : []);
        accountSelect.SetChoices(accountChoices, ChoiceOf(DashboardFilter.Current.AccountId));

        var sourceChoices = sourceIds
            .Select(sourceId => ((object)sourceId, SourceNames.Display(sourceId)))
            .Prepend(allSources);
        sourceSelect.SetChoices(sourceChoices, DashboardFilter.Current.SourceId ?? AllChoice);
    }

    /// <summary>The filter's account: none for all, empty for usage before any account.</summary>
    private static string? AccountOf(string choice) => choice switch
    {
        AllChoice => null,
        EarlierChoice => string.Empty,
        _ => choice,
    };

    private static string ChoiceOf(string? accountId) => accountId switch
    {
        null => AllChoice,
        "" => EarlierChoice,
        _ => accountId,
    };

    private Button RefreshButton()
    {
        var button = new Button
        {
            Content = new FontIcon
            {
                Glyph = RefreshGlyph,
                FontSize = RefreshGlyphSize,
                FontFamily = (FontFamily)Application.Current.Resources["HakariIconFont"],
            },
            Style = (Style)Application.Current.Resources["HakariButton"],
            Padding = RefreshPadding,
        };
        Accessible.Name(button, Texts.Get("dashboard.refresh"));
        button.Click += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        return button;
    }

    private void Change(DashboardFilter filter)
    {
        DashboardFilter.Current = filter;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
