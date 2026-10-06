namespace Hakari.Surfaces.Dashboard;

internal enum PlanCardKind
{
    /// <summary>One account against its own plan.</summary>
    Account,

    /// <summary>Every account's usage, earlier usage too, against all the plans together.</summary>
    Combined,

    /// <summary>Usage from before Hakari was installed, which belongs to no account.</summary>
    Unassigned,
}
