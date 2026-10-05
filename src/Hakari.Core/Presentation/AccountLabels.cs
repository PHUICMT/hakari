using Hakari.Core.Accounts;
using Hakari.Core.Localization;

namespace Hakari.Core.Presentation;

/// <summary>A few letters that tell accounts apart on the taskbar.</summary>
public static class AccountLabels
{
    private const int MaximumLength = 8;
    private const string FallbackKey = "account.fallback";

    /// <summary>The part of the email before "@", else the first name, cut to fit.</summary>
    public static string Short(AccountInfo? account)
    {
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
}
