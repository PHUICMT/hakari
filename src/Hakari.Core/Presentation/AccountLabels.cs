using Hakari.Core.Accounts;
using Hakari.Core.Localization;

namespace Hakari.Core.Presentation;

/// <summary>What an account is called: the user's nickname first, else its own details.</summary>
public static class AccountLabels
{
    private const int MaximumLength = 8;
    private const int MaximumNicknameLength = 12;
    private const string FallbackKey = "account.fallback";

    /// <summary>
    /// For the taskbar: the nickname, else the part of the email before "@", else the first
    /// name, cut to fit.
    /// </summary>
    public static string Short(AccountInfo? account, string? nickname = null)
    {
        if (!string.IsNullOrWhiteSpace(nickname))
        {
            var trimmed = nickname.Trim();
            return trimmed.Length <= MaximumNicknameLength
                ? trimmed
                : trimmed[..MaximumNicknameLength];
        }

        var label = account?.Email?.Split('@')[0]
            ?? account?.DisplayName?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()
            ?? Texts.Get(FallbackKey);
        if (label.Length == 0)
        {
            label = Texts.Get(FallbackKey);
        }

        return label.Length <= MaximumLength ? label : label[..MaximumLength];
    }

    /// <summary>For titles: the nickname, else the email, else the name on the account.</summary>
    public static string Full(AccountInfo? account, string? nickname = null) =>
        !string.IsNullOrWhiteSpace(nickname) ? nickname.Trim()
            : account?.Email ?? account?.DisplayName ?? Texts.Get("account.signedIn");
}
