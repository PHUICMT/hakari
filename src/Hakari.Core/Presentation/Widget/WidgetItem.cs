namespace Hakari.Core.Presentation.Widget;

/// <summary>What one line of the taskbar widget shows.</summary>
public enum WidgetItem
{
    /// <summary>Money and the pressing limit, or one line per account when there are two.</summary>
    Automatic,
    CostToday,
    CostThisMonth,
    BurnRate,
    SessionLimit,
    WeeklyLimit,
    MostPressingLimit,

    /// <summary>Both at once: "5h 5% · Week 93%".</summary>
    SessionAndWeeklyLimits,

    /// <summary>The most pressing limit of the second signed-in account.</summary>
    SecondAccount,
    Nothing,
}
