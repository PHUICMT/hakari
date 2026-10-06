using System.Globalization;
using System.Security;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Microsoft.Win32;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Hakari.Notifications;

/// <summary>
/// Limit alerts as Windows notifications, with the limit as a progress bar and two buttons:
/// open Hakari, or mute alerts for the rest of the day. Without a package, Windows needs the
/// app's notification identity and a link scheme for the buttons registered for this user;
/// both are written on start and point at this executable. A click on a button starts
/// Hakari.exe with the link, which hands it to the running copy.
/// </summary>
internal static class ToastNotifier
{
    public const string Scheme = "hakari-meter";
    public const string OpenAction = Scheme + ":open";
    public const string MuteTodayAction = Scheme + ":mute-today";

    private const string AppId = "Hakari.Meter";
    private const string DisplayName = "Hakari";
    private const string IconFile = "Hakari.ico";
    private const string ClassesKey = @"Software\Classes\";
    private const string AppIdKey = ClassesKey + @"AppUserModelId\" + AppId;
    private const string SchemeKey = ClassesKey + Scheme;
    private const string CommandKey = SchemeKey + @"\shell\open\command";

    private static bool registered;

    /// <summary>Writes the identity and the link scheme; cheap and safe to repeat.</summary>
    public static void Register()
    {
        if (Environment.ProcessPath is not { } executable)
        {
            return;
        }

        try
        {
            using (var identity = Registry.CurrentUser.CreateSubKey(AppIdKey))
            {
                identity.SetValue("DisplayName", DisplayName);
                var icon = Path.Combine(AppContext.BaseDirectory, IconFile);
                if (File.Exists(icon))
                {
                    identity.SetValue("IconUri", icon);
                }
            }

            using (var scheme = Registry.CurrentUser.CreateSubKey(SchemeKey))
            {
                scheme.SetValue(string.Empty, "URL:" + DisplayName);
                scheme.SetValue("URL Protocol", string.Empty);
            }

            using (var command = Registry.CurrentUser.CreateSubKey(CommandKey))
            {
                command.SetValue(string.Empty, $"\"{executable}\" \"%1\"");
            }

            registered = true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException
            or SecurityException or IOException)
        {
            registered = false;
        }
    }

    /// <returns>False when Windows would not take it, so the caller can fall back.</returns>
    public static bool Show(LimitAlertMessage message)
    {
        if (!registered)
        {
            return false;
        }

        try
        {
            var document = new XmlDocument();
            document.LoadXml(Xml(message));
            ToastNotificationManager.CreateToastNotifier(AppId).Show(
                new ToastNotification(document));
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
            or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }

    private static string Xml(LimitAlertMessage message)
    {
        var lines = string.Concat(
            new[] { message.Title, message.Detail }
                .Where(line => line.Length > 0)
                .Select(line => $"<text>{Escape(line)}</text>"));
        var value = message.Progress.ToString("0.###", CultureInfo.InvariantCulture);
        var status = message.Body.Length > 0 ? message.Body : " ";
        var progress = $"<progress title=\"{Escape(message.ProgressLabel)}\" value=\"{value}\" "
            + $"valueStringOverride=\"{Escape(message.ProgressValue)}\" "
            + $"status=\"{Escape(status)}\"/>";
        var actions =
            Action(Texts.Get("alert.open"), OpenAction)
            + Action(Texts.Get("alert.muteToday"), MuteTodayAction);
        return $"<toast launch=\"{OpenAction}\" activationType=\"protocol\">"
            + $"<visual><binding template=\"ToastGeneric\">{lines}{progress}</binding></visual>"
            + $"<actions>{actions}</actions></toast>";
    }

    private static string Action(string content, string link) =>
        $"<action content=\"{Escape(content)}\" activationType=\"protocol\" "
        + $"arguments=\"{link}\"/>";

    private static string Escape(string text) => SecurityElement.Escape(text) ?? string.Empty;
}
