namespace Hakari.Core.Limits;

public enum LimitAlertKind
{
    /// <summary>A limit rose past the warning threshold.</summary>
    Warning,

    /// <summary>A limit rose past the critical threshold.</summary>
    Critical,

    /// <summary>A limit that was well used started over.</summary>
    Reset,
}
