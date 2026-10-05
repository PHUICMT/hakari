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
}
