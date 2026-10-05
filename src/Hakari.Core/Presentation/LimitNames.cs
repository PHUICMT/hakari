using Hakari.Core.Limits;

namespace Hakari.Core.Presentation;

public static class LimitNames
{
    private const string SessionKind = "session";
    private const string WeeklyAllKind = "weekly_all";

    /// <summary>Compact, for the taskbar: "5h", "Week", or the model name.</summary>
    public static string Short(UsageLimit limit) => limit.Kind switch
    {
        SessionKind => "5h",
        WeeklyAllKind => "Week",
        _ => limit.ScopeName ?? limit.Kind,
    };

    /// <summary>For the flyout: "5-hour limit", "Weekly limit", "Weekly · Fable".</summary>
    public static string Long(UsageLimit limit) => limit.Kind switch
    {
        SessionKind => "5-hour limit",
        WeeklyAllKind => "Weekly limit",
        _ => limit.ScopeName is { } scope ? $"Weekly · {scope}" : limit.Kind,
    };
}
