using Hakari.Core.Accounts;

namespace Hakari.Core.Presentation;

/// <summary>A few letters that tell accounts apart on the taskbar.</summary>
public static class AccountLabels
{
    private const int MaximumLength = 8;
    private const string Fallback = "Account";

    /// <summary>The part of the email before "@", else the first name, cut to fit.</summary>
    public static string Short(AccountInfo? account)
    {
        var label = account?.Email?.Split('@')[0]
            ?? account?.DisplayName?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()
            ?? Fallback;
        if (label.Length == 0)
        {
            label = Fallback;
        }

        return label.Length <= MaximumLength ? label : label[..MaximumLength];
    }
}
