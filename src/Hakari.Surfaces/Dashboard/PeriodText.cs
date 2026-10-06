using Hakari.Core.Localization;

namespace Hakari.Surfaces.Dashboard;

/// <summary>The chosen period in words: "30 days", "Today".</summary>
internal static class PeriodText
{
    public static string Caption() => Texts.Get(DashboardFilter.Current.Period switch
    {
        DashboardPeriod.Today => "dashboard.period.today",
        DashboardPeriod.SevenDays => "dashboard.period.week",
        DashboardPeriod.ThirtyDays => "dashboard.period.month",
        _ => "dashboard.period.all",
    });
}
