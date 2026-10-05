namespace Hakari.Core.Limits;

public enum LimitFreshness
{
    /// <summary>Fetched just now from the account.</summary>
    Live,

    /// <summary>Last fetched values, shown because a live read was not possible.</summary>
    LastKnown,
}
