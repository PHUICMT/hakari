namespace Hakari.Core.Limits.Credentials;

/// <summary>
/// The sign-in tokens Claude Code keeps for one config directory. Never logged or stored by
/// Hakari; only held in memory while a request is made.
/// </summary>
public sealed record OAuthCredentials(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RefreshTokenExpiresAt,
    string? SubscriptionType,
    string? RateLimitTier)
{
    /// <summary>Treat a token as expired a little early, so a request never races expiry.</summary>
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromMinutes(2);

    public bool IsAccessTokenValid(DateTimeOffset now) => now < ExpiresAt - ExpiryMargin;

    public bool CanRefresh(DateTimeOffset now) =>
        RefreshToken is not null && (RefreshTokenExpiresAt is not { } expiry || now < expiry);

    /// <summary>Hides the tokens if a record is ever printed.</summary>
    public override string ToString() =>
        $"{nameof(OAuthCredentials)} {{ ExpiresAt = {ExpiresAt:u}, Tier = {RateLimitTier} }}";
}
