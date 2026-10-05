using Hakari.Core.Localization;

namespace Hakari.Core.Presentation.Widget;

/// <summary>
/// Decides how many blocks the widget shows and what each says: one for everything, one per
/// account side by side, or one that takes each account in turn.
/// </summary>
public static class WidgetPanels
{
    /// <summary>More than this would crowd out the taskbar's own buttons.</summary>
    public const int MaximumSideBySide = 3;

    public static readonly TimeSpan TurnLength = TimeSpan.FromSeconds(8);

    /// <param name="layoutOf">An account's own layout, else the shared one.</param>
    public static IReadOnlyList<ComposedWidget> Compose(
        MultiAccountMode mode,
        WidgetLayout shared,
        Func<string, WidgetLayout> layoutOf,
        WidgetFacts facts,
        DateTimeOffset now)
    {
        var accounts = facts.Accounts;
        if (accounts.Count < 2 || mode == MultiAccountMode.Together)
        {
            return [WidgetComposer.Compose(shared, facts, now)];
        }

        if (mode == MultiAccountMode.TakeTurns)
        {
            var account = accounts[TurnIndex(accounts.Count, now)];
            return [ForAccount(layoutOf(account.AccountId), facts, account, now)];
        }

        return
        [
            .. accounts.Take(MaximumSideBySide)
                .Select(account => ForAccount(layoutOf(account.AccountId), facts, account, now)),
        ];
    }

    /// <summary>Whose turn it is: the same answer for everyone at the same moment.</summary>
    public static int TurnIndex(int accountCount, DateTimeOffset now) =>
        (int)(now.ToUnixTimeSeconds() / (long)TurnLength.TotalSeconds % accountCount);

    /// <summary>One account's block: its own money and limits, its name on the top line.</summary>
    public static ComposedWidget ForAccount(
        WidgetLayout layout,
        WidgetFacts facts,
        WidgetAccount account,
        DateTimeOffset now)
    {
        var costs = account.Costs ?? new AccountCosts(0, 0, 0);
        var own = facts with
        {
            CostToday = costs.Today,
            CostThisMonth = costs.ThisMonth,
            CostLastHour = costs.LastHour,
            Accounts = [account],
        };
        var widget = WidgetComposer.Compose(layout, own, now);
        var label = AccountLabels.Short(account.Account, facts.NicknameOf(account.AccountId));
        var top = widget.Top.Text.Length == 0
            ? label
            : Texts.Format("widget.labelled", label, widget.Top.Text);
        return widget with { Top = widget.Top with { Text = top } };
    }
}
