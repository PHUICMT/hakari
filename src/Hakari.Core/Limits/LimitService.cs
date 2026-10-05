using Hakari.Core.Limits.Credentials;
using Hakari.Core.Sources;

namespace Hakari.Core.Limits;

/// <summary>
/// Gets an account's usage limits: live while a sign-in for it is valid, otherwise the last
/// known values projected forward (a window past its reset time shows zero). With automatic
/// sign-in refresh turned on, an expired sign-in is renewed only while Claude Code is not running.
/// </summary>
public sealed class LimitService(
    UsageLimitClient client,
    LimitCache cache,
    IClaudeCodeActivity activity,
    LimitServiceOptions options,
    TimeProvider timeProvider)
{
    /// <summary>
    /// One request per account, however many sources share it. A source with a valid sign-in
    /// is preferred, so a Windows and a WSL sign-in for the same account need no refresh.
    /// </summary>
    public async Task<LimitResult> GetForAccountAsync(
        string accountId,
        IReadOnlyList<UsageSource> signedInSources,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var candidates = signedInSources
            .Select(source => (Source: source, File: CredentialsFile.For(source)))
            .Select(candidate => (candidate.Source, candidate.File, Read: candidate.File.Read()))
            .Where(candidate => candidate.Read is not null)
            .OrderByDescending(candidate =>
                candidate.Read!.Value.Credentials.IsAccessTokenValid(now))
            .ToList();

        if (candidates.Count == 0)
        {
            return FromCache(accountId, LimitFailure.NoCredentials);
        }

        var (source, file, read) = candidates[0];
        var (credentials, fingerprint) = read!.Value;
        return await GetWithCredentialsAsync(
            accountId,
            new SignIn(source, file, credentials, fingerprint),
            cancellationToken);
    }

    private async Task<LimitResult> GetWithCredentialsAsync(
        string accountId,
        SignIn signIn,
        CancellationToken cancellationToken)
    {
        var credentials = signIn.Credentials;
        if (!credentials.IsAccessTokenValid(timeProvider.GetUtcNow()))
        {
            var renewed = await TryRefreshAsync(signIn, cancellationToken);
            if (renewed is null)
            {
                return FromCache(accountId, LimitFailure.SignInExpired);
            }

            credentials = renewed;
        }

        var result = await client.FetchAsync(credentials, cancellationToken);
        if (result.Snapshot is { } snapshot)
        {
            cache.Save(accountId, snapshot);
            return result;
        }

        return FromCache(accountId, result.Failure);
    }

    private async Task<OAuthCredentials?> TryRefreshAsync(
        SignIn signIn,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var credentials = signIn.Credentials;
        if (!options.RefreshSignInAutomatically
            || !credentials.CanRefresh(now)
            || activity.IsRunning(signIn.Source))
        {
            return null;
        }

        var grant = await client.RefreshAsync(credentials.RefreshToken!, cancellationToken);
        if (grant is null)
        {
            return null;
        }

        if (signIn.File.TryWriteTokens(grant, signIn.Fingerprint))
        {
            return credentials with
            {
                AccessToken = grant.AccessToken,
                RefreshToken = grant.RefreshToken ?? credentials.RefreshToken,
                ExpiresAt = grant.ExpiresAt,
            };
        }

        // Claude Code wrote the file meanwhile; its tokens win, use them if valid.
        var latest = signIn.File.Read()?.Credentials;
        return latest is not null && latest.IsAccessTokenValid(now) ? latest : null;
    }

    private LimitResult FromCache(string accountId, LimitFailure failure)
    {
        var cached = cache.Load(accountId);
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

    private sealed record SignIn(
        UsageSource Source,
        CredentialsFile File,
        OAuthCredentials Credentials,
        string Fingerprint);
}
