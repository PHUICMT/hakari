using Hakari.Core.Limits;
using Hakari.Core.Localization;

namespace Hakari.Core.Presentation;

public static class LimitNames
{
    private const string SessionKind = "session";
    private const string WeeklyAllKind = "weekly_all";

    /// <summary>Compact, for the taskbar: "5h", "Week", or the model name.</summary>
    public static string Short(UsageLimit limit) => limit.Kind switch
    {
        SessionKind => Texts.Get("limit.short.session"),
        WeeklyAllKind => Texts.Get("limit.short.weekly"),
        _ => limit.ScopeName ?? limit.Kind,
    };

    /// <summary>For the flyout: "5-hour limit", "Weekly limit", "Weekly · Fable".</summary>
    public static string Long(UsageLimit limit) => limit.Kind switch
    {
        SessionKind => Texts.Get("limit.long.session"),
        WeeklyAllKind => Texts.Get("limit.long.weekly"),
        _ => limit.ScopeName is { } scope
            ? Texts.Format("limit.long.scoped", scope)
            : limit.Kind,
    };
}
