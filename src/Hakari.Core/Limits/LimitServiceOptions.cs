namespace Hakari.Core.Limits;

/// <param name="RefreshSignInAutomatically">
/// Off by default. When on, Hakari renews an expired sign-in itself (only while Claude Code is
/// not running) and writes the new tokens back so Claude Code keeps working.
/// </param>
public sealed record LimitServiceOptions(bool RefreshSignInAutomatically)
{
    public static LimitServiceOptions Default { get; } = new(RefreshSignInAutomatically: false);
}
