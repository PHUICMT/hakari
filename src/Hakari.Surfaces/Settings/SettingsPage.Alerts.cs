using System.Diagnostics;
using System.Globalization;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Hakari.Core.Presentation.Widget;
using Hakari.Core.Pricing;
using Hakari.Core.SessionNotes;
using Hakari.Core.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Hakari.Surfaces.Settings;

/// <summary>
/// The Alerts card (limit levels, resets, budgets, unusual sessions) and the Prices card (the
/// table in use, a check for a newer one, and the user's own prices).
/// </summary>
public sealed partial class SettingsPage
{
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
        SessionNotesToggle.IsChecked = settings.TellSessionsAboutLimits;
        FillSound(settings);
        SessionNotesFromSlider.Value = NoteLevel.Usable(settings.SessionNotesFrom);
        AlertWarnSlider.Value = settings.Widget.WarnAt;
        AlertCriticalSlider.Value = settings.Widget.CriticalAt;
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
        AlertWarnSlider.Accent = (Brush)Application.Current.Resources["HakariWarnBrush"];
        AlertCriticalSlider.Accent = (Brush)Application.Current.Resources["HakariCriticalBrush"];

        // The two levels keep a gap: dragging one past the other pushes the other along.
        AlertWarnSlider.Changing += (_, value) =>
            AlertCriticalSlider.Value = Math.Max(AlertCriticalSlider.Value, value + LevelGap);
        AlertCriticalSlider.Changing += (_, value) =>
            AlertWarnSlider.Value = Math.Min(AlertWarnSlider.Value, value - LevelGap);
        AlertWarnSlider.Changed += (_, _) => SaveLevels();
        AlertCriticalSlider.Changed += (_, _) => SaveLevels();
        SessionNotesFromSlider.Changed += (_, value) =>
            Save(current => current with { SessionNotesFrom = value });
    }

    private const int LevelGap = 5;

    private void SaveLevels() => Save(current => current with
    {
        Widget = current.Widget with
        {
            WarnAt = AlertWarnSlider.Value,
            CriticalAt = AlertCriticalSlider.Value,
        },
    });

    private static string Amount(decimal? amount) =>
        amount?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>
    /// Adds the hook to, or takes it out of, Claude Code's settings first, and keeps the
    /// choice only if that worked, so the switch never says on while no session is told.
    /// </summary>
    private async void OnSessionNotesClicked(object sender, RoutedEventArgs args)
    {
        var turnOn = SessionNotesToggle.IsChecked == true;
        var directories = SessionHookInstaller.ConfigDirectories(store.Load());
        var result = await Task.Run(() => turnOn
            ? SessionHookInstaller.Install(directories)
            : SessionHookInstaller.Remove(directories));

        var worked = result.Failed == 0 || result.Changed > 0;
        SessionNotesRow.Description = Texts.Get(worked
            ? "settings.alerts.sessions.description"
            : "settings.alerts.sessions.failed");
        if (!worked)
        {
            SessionNotesToggle.IsChecked = !turnOn;
            return;
        }

        Save(current => current with { TellSessionsAboutLimits = turnOn });
    }

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
