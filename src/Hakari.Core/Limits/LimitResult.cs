namespace Hakari.Core.Limits;

/// <param name="Snapshot">Live or last known limits; null when nothing is known yet.</param>
/// <param name="Failure">Why a live read did not happen, or None.</param>
public sealed record LimitResult(LimitSnapshot? Snapshot, LimitFailure Failure)
{
    public static LimitResult Succeeded(LimitSnapshot snapshot) => new(snapshot, LimitFailure.None);

    public static LimitResult Failed(LimitFailure failure) => new(null, failure);
}
