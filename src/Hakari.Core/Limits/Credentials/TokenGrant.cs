namespace Hakari.Core.Limits.Credentials;

/// <summary>New tokens from a refresh; the rotated refresh token replaces the old.</summary>
public sealed record TokenGrant(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset ExpiresAt)
{
    public override string ToString() => $"{nameof(TokenGrant)} {{ ExpiresAt = {ExpiresAt:u} }}";
}
