using Hakari.Core.Limits;
using Hakari.Core.Localization;

namespace Hakari.Core.Presentation;

/// <summary>What a notification about a limit says: a title, and a line below it.</summary>
public static class LimitAlertText
{
    private const string Separator = " · ";

    /// <param name="accountName">Shown in the body, or empty with only one account.</param>
    public static (string Title, string Body) Compose(
        LimitAlert alert,
        string accountName,
        DateTimeOffset now)
    {
        var name = LimitNames.Long(alert.Limit);
        var percent = PercentText.Format(alert.Percent, 0);
        var title = alert.Kind switch
        {
            LimitAlertKind.Reset => Texts.Format("alert.reset", name),
            LimitAlertKind.Critical when alert.Percent >= LimitForecaster.FullPercent =>
                Texts.Format("alert.full", name),
            LimitAlertKind.Critical => Texts.Format("alert.critical", name, percent),
            _ => Texts.Format("alert.warning", name, percent),
        };

        var resets = alert.Kind != LimitAlertKind.Reset && alert.Limit.ResetsAt is { } at
            ? Texts.Format("flyout.resets", ResetText.Long(at, now))
            : string.Empty;
        var body = string.Join(
            Separator,
            new[] { accountName, resets }.Where(part => part.Length > 0));
        return (title, body);
    }
}
