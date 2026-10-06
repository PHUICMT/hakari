namespace Hakari.Core.Indexing;

/// <summary>How far a large scan of the logs has got, in bytes.</summary>
public sealed record IndexProgress(long BytesDone, long BytesTotal)
{
    public double Fraction =>
        BytesTotal <= 0 ? 1 : Math.Clamp((double)BytesDone / BytesTotal, 0, 1);
}
