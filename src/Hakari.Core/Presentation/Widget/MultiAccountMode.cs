namespace Hakari.Core.Presentation.Widget;

/// <summary>How the widget shows two or more signed-in accounts.</summary>
public enum MultiAccountMode
{
    /// <summary>One block: a line per account.</summary>
    Together,

    /// <summary>A block per account, next to each other, each with its own layout.</summary>
    SideBySide,

    /// <summary>One block that shows each account in turn.</summary>
    TakeTurns,
}
