using Hakari.Core.Limits.Credentials;
using Hakari.Core.Sources;

namespace Hakari.Core.Accounts;

/// <summary>
/// Notices which account each source is signed in to, and when that changes. Usage from before
/// Hakari first saw a source belongs to no account, since the logs do not say who was signed
/// in; an account counts from the moment it was first seen.
/// </summary>
public sealed class AccountTracker(AccountRepository repository, TimeProvider timeProvider)
{
    private readonly Dictionary<string, DateTime> lastReadFileTimes = [];

    /// <summary>Returns the periods started by this call (first sightings and switches).</summary>
    public IReadOnlyList<AccountPeriod> Observe(IEnumerable<UsageSource> sources)
    {
        var started = new List<AccountPeriod>();
        foreach (var source in sources)
        {
            if (ObserveSource(source) is { } period)
            {
                started.Add(period);
            }
        }

        return started;
    }

    /// <summary>The account a source is signed in to now, as last observed.</summary>
    public string? CurrentAccountId(UsageSource source) =>
        repository.LatestPeriod(source.Id)?.AccountId;

    /// <summary>Sources grouped by the account each is signed in to right now.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<UsageSource>> SourcesByCurrentAccount(
        IEnumerable<UsageSource> sources)
    {
        return sources
            .Select(source => (Source: source, AccountId: CurrentAccountId(source)))
            .Where(entry => entry.AccountId is not null)
            .GroupBy(entry => entry.AccountId!)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<UsageSource>)[.. group.Select(entry => entry.Source)]);
    }

    private AccountPeriod? ObserveSource(UsageSource source)
    {
        var accountFile = AccountFileReader.CandidateFiles(source.ConfigDirectory)
            .FirstOrDefault(File.Exists);
        if (accountFile is null || !HasChangedSinceLastRead(accountFile))
        {
            return null;
        }

        if (AccountFileReader.TryRead(accountFile) is not { } account)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        repository.SaveAccount(WithCredentialPlan(source, account), now);

        var latest = repository.LatestPeriod(source.Id);
        if (latest?.AccountId == account.AccountId)
        {
            return null;
        }

        var period = new AccountPeriod(source.Id, account.AccountId, now);
        repository.AddPeriod(period);
        return period;
    }

    /// <summary>The credentials file names the exact rate-limit tier, so it wins.</summary>
    private static AccountInfo WithCredentialPlan(UsageSource source, AccountInfo account)
    {
        var credentials = CredentialsFile.For(source).Read()?.Credentials;
        if (credentials is null)
        {
            return account;
        }

        var plan = PlanDetector.Detect(credentials.SubscriptionType, credentials.RateLimitTier);
        return plan == SubscriptionPlan.Unknown ? account : account with { Plan = plan };
    }

    /// <summary>Claude Code rewrites its account file often; parse it only when changed.</summary>
    private bool HasChangedSinceLastRead(string accountFile)
    {
        var modified = File.GetLastWriteTimeUtc(accountFile);
        if (lastReadFileTimes.TryGetValue(accountFile, out var previous) && previous == modified)
        {
            return false;
        }

        lastReadFileTimes[accountFile] = modified;
        return true;
    }
}
