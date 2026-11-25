using Hakari.Core.Localization;
using Hakari.Core.SessionNotes;
using Hakari.Core.Settings;
using Hakari.Surfaces.Controls;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Hakari.Surfaces.Settings;

/// <summary>
/// The text sessions are told when a limit is nearly full: the user writes it, with values in
/// braces that are filled in each time, and sees at once how a session would read it.
/// </summary>
public sealed partial class SettingsPage
{
    /// <summary>Typing pauses this long before the text is saved.</summary>
    private static readonly TimeSpan NoteSaveDelay = TimeSpan.FromMilliseconds(600);

    private DispatcherQueueTimer? noteSaveTimer;
    private bool noteHandlersAttached;

    private void FillSessionNote(HakariSettings settings)
    {
        if (SessionNoteBox.FocusState == FocusState.Unfocused)
        {
            SessionNoteBox.Text = string.IsNullOrWhiteSpace(settings.SessionNoteTemplate)
                ? LimitNoteText.DefaultTemplate
                : settings.SessionNoteTemplate;
        }

        ShowNoteSample();
        if (noteHandlersAttached)
        {
            return;
        }

        noteHandlersAttached = true;
        noteSaveTimer = DispatcherQueue.CreateTimer();
        noteSaveTimer.Interval = NoteSaveDelay;
        noteSaveTimer.IsRepeating = false;
        noteSaveTimer.Tick += (_, _) => SaveNote();
        SessionNoteBox.TextChanged += (_, _) =>
        {
            ShowNoteSample();
            noteSaveTimer.Stop();
            noteSaveTimer.Start();
        };
        SessionNoteBox.LostFocus += (_, _) => SaveNote();
        ShowPlaceholderHint(null);
        foreach (var name in LimitNoteText.Placeholders)
        {
            SessionNotePlaceholders.Children.Add(PlaceholderButton(name));
        }
    }

    /// <summary>A value to put in at the cursor, so nobody has to type the braces right.</summary>
    private Button PlaceholderButton(string name)
    {
        var token = "{" + name + "}";
        var button = new Button
        {
            Content = token,
            Style = (Style)Application.Current.Resources["HakariButton"],
            Padding = new Thickness(8, 2, 8, 2),
            FontSize = 12,
        };
        Accessible.Name(button, $"{token}: {Texts.Get(PlaceholderHelpKey(name))}");

        // Said at once in the line below, from the pointer or the keyboard alike.
        button.AddHandler(PointerEnteredEvent, new PointerEventHandler(
            (_, _) => ShowPlaceholderHint(name)), handledEventsToo: true);
        button.AddHandler(PointerExitedEvent, new PointerEventHandler(
            (_, _) => ShowPlaceholderHint(null)), handledEventsToo: true);
        button.GotFocus += (_, _) => ShowPlaceholderHint(name);
        button.LostFocus += (_, _) => ShowPlaceholderHint(null);
        button.Click += (_, _) =>
        {
            var at = SessionNoteBox.SelectionStart;
            var text = SessionNoteBox.Text;
            var after = text[(at + SessionNoteBox.SelectionLength)..];
            SessionNoteBox.Text = text[..at] + token + after;
            SessionNoteBox.Focus(FocusState.Programmatic);
            SessionNoteBox.SelectionStart = at + token.Length;
        };
        return button;
    }

    /// <summary>What a value stands for, or how to find out when none is pointed at.</summary>
    private void ShowPlaceholderHint(string? name) =>
        SessionNoteHint.Text = name is null
            ? Texts.Get("settings.alerts.sessions.text.hint")
            : "{" + name + "}  " + Texts.Get(PlaceholderHelpKey(name));

    /// <summary>Written out in full, so the check for missing texts can see every key.</summary>
    private static string PlaceholderHelpKey(string name) => name switch
    {
        "limits" => "settings.alerts.sessions.text.limits",
        "limit" => "settings.alerts.sessions.text.limit",
        "percent" => "settings.alerts.sessions.text.percent",
        "state" => "settings.alerts.sessions.text.state",
        "resets" => "settings.alerts.sessions.text.resets",
        "in" => "settings.alerts.sessions.text.in",
        "advice" => "settings.alerts.sessions.text.advice",
        _ => "settings.alerts.sessions.text.readAt",
    };

    private void OnSessionNoteDefaultClicked(object sender, RoutedEventArgs args)
    {
        SessionNoteBox.Text = LimitNoteText.DefaultTemplate;
        SaveNote();
    }

    /// <summary>Hakari's own text, or nothing at all, is saved as "use the default".</summary>
    private void SaveNote()
    {
        noteSaveTimer?.Stop();
        var text = SessionNoteBox.Text.Trim();
        var template = text.Length == 0 || text == LimitNoteText.DefaultTemplate
            ? string.Empty
            : text;
        if (store.Load().SessionNoteTemplate != template)
        {
            Save(current => current with { SessionNoteTemplate = template });
        }
    }

    private void ShowNoteSample() =>
        SessionNotePreview.Text = LimitNoteText.Sample(
            SessionNoteBox.Text,
            DateTimeOffset.Now,
            TimeZoneInfo.Local);
}
