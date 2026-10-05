namespace Hakari.Core.Settings;

/// <summary>
/// Which accounts show and in what order, the same everywhere: the taskbar, the flyout and
/// the settings preview.
/// </summary>
public static class AccountArrangement
{
    /// <param name="idOf">The account id of an item.</param>
    /// <param name="pressingRank">Higher means closer to a limit.</param>
    public static IReadOnlyList<T> Arrange<T>(
        IEnumerable<T> items,
        Func<T, string> idOf,
        Func<T, int> pressingRank,
        HakariSettings settings)
    {
        var shown = items.Where(item => !settings.HiddenAccounts.Contains(idOf(item)));
        if (settings.AccountOrdering == AccountOrder.MostPressing)
        {
            return [.. shown.OrderByDescending(pressingRank)];
        }

        // Accounts not placed yet, such as a new sign-in, go last in pressing order.
        return
        [
            .. shown
                .OrderBy(item => PositionOf(settings.CustomAccountOrder, idOf(item)))
                .ThenByDescending(pressingRank),
        ];
    }

    /// <summary>Moves one account up or down, starting from the order shown now.</summary>
    public static IReadOnlyList<string> Move(
        IReadOnlyList<string> shownOrder,
        string accountId,
        int steps)
    {
        var order = shownOrder.ToList();
        var index = order.IndexOf(accountId);
        if (index < 0)
        {
            return order;
        }

        var target = Math.Clamp(index + steps, 0, order.Count - 1);
        order.RemoveAt(index);
        order.Insert(target, accountId);
        return order;
    }

    private static int PositionOf(IReadOnlyList<string> order, string accountId)
    {
        var index = order.ToList().IndexOf(accountId);
        return index < 0 ? int.MaxValue : index;
    }
}
