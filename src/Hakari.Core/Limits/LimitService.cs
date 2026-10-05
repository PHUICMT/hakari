using Hakari.Core.Limits.Credentials;
using Hakari.Core.Sources;

namespace Hakari.Core.Limits;

/// <summary>
/// Gets a source's usage limits: live while its sign-in is valid, otherwise the last known
/// values projected forward (a window past its reset time shows zero). With automatic sign-in
/// refresh turned on, an expired sign-in is renewed only while Claude Code is not running.
/// </summary>
public sealed class LimitService(
    UsageLimitClient client,
    LimitCache cache,
    IClaudeCodeActivity activity,
    LimitServiceOptions options,
    TimeProvider timeProvider)
{
    public async Task<LimitResult> GetAsync(UsageSource source, CancellationToken cancellationToken)
    {
        var file = CredentialsFile.For(source);
        if (file.Read() is not var (credentials, fingerprint))
        {
            return FromCache(source, LimitFailure.NoCredentials);
        }

        var now = timeProvider.GetUtcNow();
        if (!credentials.IsAccessTokenValid(now))
        {
            var renewed = await TryRefreshAsync(
                source,
                file,
                credentials,
                fingerprint,
                cancellationToken);
            if (renewed is null)
            {
                return FromCache(source, LimitFailure.SignInExpired);
            }

            credentials = renewed;
        }

        var result = await client.FetchAsync(credentials, cancellationToken);
        if (result.Snapshot is { } snapshot)
        {
            cache.Save(source.Id, snapshot);
            return result;
        }

        return FromCache(source, result.Failure);
    }

    private async Task<OAuthCredentials?> TryRefreshAsync(
        UsageSource source,
        CredentialsFile file,
        OAuthCredentials credentials,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (!options.RefreshSignInAutomatically
            || !credentials.CanRefresh(now)
            || activity.IsRunning(source))
        {
            return null;
        }

        var grant = await client.RefreshAsync(credentials.RefreshToken!, cancellationToken);
        if (grant is null)
        {
            return null;
        }

        if (file.TryWriteTokens(grant, fingerprint))
        {
            return credentials with
            {
                AccessToken = grant.AccessToken,
                RefreshToken = grant.RefreshToken ?? credentials.RefreshToken,
                ExpiresAt = grant.ExpiresAt,
            };
        }

        // Claude Code wrote the file meanwhile; its tokens win, use them if valid.
        return file.Read() is var (latest, _) && latest.IsAccessTokenValid(now) ? latest : null;
    }

    private LimitResult FromCache(UsageSource source, LimitFailure failure)
    {
        var cached = cache.Load(source.Id);
        if (cached is null)
        {
            return LimitResult.Failed(failure);
        }

        var projected = cached.ProjectedTo(timeProvider.GetUtcNow()) with
        {
            Freshness = LimitFreshness.LastKnown,
        };
        return new LimitResult(projected, failure);
    }
}
