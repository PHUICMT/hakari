namespace Hakari.Core.Querying;

public enum GroupBy
{
    None,
    Model,
    Source,
    Day,
    Hour,
    Project,
    Session,
    Branch,
    Account,

    /// <summary>A branch within its project, since every project has its own "main".</summary>
    ProjectBranch,

    /// <summary>A session with its project, so sessions can be told apart by where.</summary>
    ProjectSession,

    /// <summary>Local weekday (0 is Sunday) and hour like "3 14", for a heat map.</summary>
    WeekdayHour,

    /// <summary>Local day, account, project and model, for a CSV export.</summary>
    DayAccountProjectModel,
}
