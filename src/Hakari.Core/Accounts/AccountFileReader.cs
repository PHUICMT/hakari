using System.Text.Json;
using Hakari.Core.Parsing;
using Hakari.Core.Sources;

namespace Hakari.Core.Accounts;

public static class AccountFileReader
{
    public static AccountInfo? Read(UsageSource source)
    {
        foreach (var accountFile in CandidateFiles(source.ConfigDirectory))
        {
            if (TryRead(accountFile) is { } account)
            {
                return account;
            }
        }

        return null;
    }

    public static AccountInfo? TryRead(string accountFile)
    {
        try
        {
            if (!File.Exists(accountFile))
            {
                return null;
            }

            using var stream = OpenShared(accountFile);
            using var document = JsonDocument.Parse(stream);
            return ReadAccount(document.RootElement);
        }
        catch (Exception exception) when (IsReadProblem(exception))
        {
            return null;
        }
    }

    public static IEnumerable<string> CandidateFiles(string configDirectory)
    {
        var trimmedDirectory = Path.TrimEndingDirectorySeparator(configDirectory);
        yield return Path.Combine(trimmedDirectory, ClaudeConfigNames.AccountFileName);

        var isDefaultDirectory =
            Path.GetFileName(trimmedDirectory) == ClaudeConfigNames.DefaultConfigDirectoryName;

        if (isDefaultDirectory && Path.GetDirectoryName(trimmedDirectory) is { } homeDirectory)
        {
            yield return Path.Combine(homeDirectory, ClaudeConfigNames.AccountFileName);
        }
    }

    private static AccountInfo? ReadAccount(JsonElement root)
    {
        if (!root.TryGetObject(AccountFieldNames.OAuthAccount, out var account)
            || account.GetStringOrNull(AccountFieldNames.AccountId) is not { } accountId)
        {
            return null;
        }

        var plan = PlanDetector.Detect(
            account.GetStringOrNull(AccountFieldNames.OrganizationType),
            account.GetStringOrNull(AccountFieldNames.OrganizationRateLimitTier),
            account.GetStringOrNull(AccountFieldNames.UserRateLimitTier),
            account.GetStringOrNull(AccountFieldNames.BillingType));

        return new AccountInfo(
            AccountId: accountId,
            Email: account.GetStringOrNull(AccountFieldNames.Email),
            DisplayName: account.GetStringOrNull(AccountFieldNames.DisplayName),
            OrganizationName: account.GetStringOrNull(AccountFieldNames.OrganizationName),
            Plan: plan);
    }

    private static FileStream OpenShared(string path) => new(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.ReadWrite | FileShare.Delete);

    private static bool IsReadProblem(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or JsonException;
}
