using System.Diagnostics;
using System.Globalization;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Hakari.Core.Presentation.Widget;
using Hakari.Core.Pricing;
using Hakari.Core.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Hakari.Surfaces.Settings;

/// <summary>
/// The Alerts card (limit levels, resets, budgets, unusual sessions) and the Prices card (the
/// table in use, a check for a newer one, and the user's own prices).
/// </summary>
public sealed partial class SettingsPage
{
    private static readonly int[] WarnLevels = [50, 60, 70, 75, 80, 85, 90];
    private static readonly int[] CriticalLevels = [80, 85, 90, 95, 98, 100];
    private static readonly HttpClient PriceClient = new() { Timeout = TimeSpan.FromSeconds(15) };
    private const string DateFormat = "d MMM yyyy";
    private const string TextEditor = "notepad.exe";
    private bool alertHandlersAttached;

    private void FillAlerts(HakariSettings settings)
    {
        var wasFilling = filling;
        filling = true;
        NotifyToggle.IsChecked = settings.NotifyOnLimits;
        ResetAlertToggle.IsChecked = settings.NotifyOnResets;
        UnusualToggle.IsChecked = settings.NotifyOnUnusualSessions;
        AlertWarnSelect.SetChoices(Percents(WarnLevels), settings.Widget.WarnAt);
        AlertCriticalSelect.SetChoices(Percents(CriticalLevels), settings.Widget.CriticalAt);
        if (DailyBudgetBox.FocusState == FocusState.Unfocused)
        {
            DailyBudgetBox.Text = Amount(settings.DailyBudget);
        }

        if (MonthlyBudgetBox.FocusState == FocusState.Unfocused)
        {
            MonthlyBudgetBox.Text = Amount(settings.MonthlyBudget);
        }

        filling = wasFilling;
        ShowPriceTable(Texts.Get("settings.prices.inUse"));
        if (alertHandlersAttached)
        {
            return;
        }

        alertHandlersAttached = true;
        AlertWarnSelect.Selected += (_, value) => Save(current => current with
        {
            Widget = current.Widget with
            {
                WarnAt = (int)value,
                CriticalAt = Math.Max(current.Widget.CriticalAt, (int)value + 5),
            },
        });
        AlertCriticalSelect.Selected += (_, value) => Save(current => current with
        {
            Widget = current.Widget with
            {
                CriticalAt = (int)value,
                WarnAt = Math.Min(current.Widget.WarnAt, (int)value - 5),
            },
        });
    }

    private static IEnumerable<(object Value, string Text)> Percents(int[] levels) =>
        levels.Select(percent => ((object)percent, $"{percent}%"));

    private static string Amount(decimal? amount) =>
        amount?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;

    private void OnResetAlertClicked(object sender, RoutedEventArgs args) =>
        Save(current => current with { NotifyOnResets = ResetAlertToggle.IsChecked == true });

    private void OnUnusualClicked(object sender, RoutedEventArgs args) =>
        Save(current => current with
        {
            NotifyOnUnusualSessions = UnusualToggle.IsChecked == true,
        });

    private void OnBudgetKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Enter)
        {
            args.Handled = true;
            SaveBudgets();
        }
    }

    private void OnBudgetLostFocus(object sender, RoutedEventArgs args) => SaveBudgets();

    /// <summary>Blank clears a budget; anything that is not a positive number is ignored.</summary>
    private void SaveBudgets()
    {
        if (filling)
        {
            return;
        }

        var daily = ParseAmount(DailyBudgetBox.Text, out var dayValid);
        var monthly = ParseAmount(MonthlyBudgetBox.Text, out var monthValid);
        Save(current => current with
        {
            DailyBudget = dayValid ? daily : current.DailyBudget,
            MonthlyBudget = monthValid ? monthly : current.MonthlyBudget,
        });
    }

    private static decimal? ParseAmount(string text, out bool valid) =>
        AmountText.Parse(text, out valid);

    /// <summary>"Shipped with Hakari · prices of 5 Oct 2026 · 22 models", plus a status.</summary>
    private void ShowPriceTable(string status)
    {
        var table = PricingSources.LoadCurrent();
        var day = DateOnly.TryParse(table.Updated, CultureInfo.InvariantCulture, out var date)
            ? date.ToString(DateFormat, Texts.Culture)
            : table.Updated;
        PriceTableRow.Description = Texts.Format(
            "settings.prices.table.description", status, day, table.Models.Count);
    }

    /// <summary>
    /// Fetches the table the project keeps up to date and keeps it when it is newer. Hakari.exe
    /// notices the new file within a minute and recounts with it.
    /// </summary>
    private async void OnCheckPricesClicked(object sender, RoutedEventArgs args)
    {
        CheckPricesButton.IsEnabled = false;
        ShowPriceTable(Texts.Get("settings.prices.checking"));
        string status;
        try
        {
            var json = await PriceClient.GetStringAsync(PricingSources.LatestAddress);
            status = Texts.Get(PricingSources.Accept(json)
                ? "settings.prices.updated"
                : "settings.prices.current");
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            status = Texts.Get("settings.prices.failed");
        }

        ShowPriceTable(status);
        CheckPricesButton.IsEnabled = true;
    }

    /// <summary>Opens the user's price file, started with one example to copy.</summary>
    private void OnCustomPricesClicked(object sender, RoutedEventArgs args)
    {
        var path = PricingSources.EnsureCustomFile();
        Process.Start(new ProcessStartInfo(TextEditor, $"\"{path}\"") { UseShellExecute = true });
    }
}
