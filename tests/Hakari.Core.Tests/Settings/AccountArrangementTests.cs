using Hakari.Core.Settings;

namespace Hakari.Core.Tests.Settings;

public sealed class AccountArrangementTests
{
    private static readonly (string Id, int Rank)[] Accounts = [("a", 10), ("b", 90), ("c", 50)];

    [Fact]
    public void Puts_the_most_pressing_first_by_default()
    {
        Assert.Equal(["b", "c", "a"], Arrange(new HakariSettings()));
    }

    [Fact]
    public void Follows_the_users_order_and_puts_unplaced_accounts_last()
    {
        var settings = new HakariSettings
        {
            AccountOrdering = AccountOrder.Custom,
            CustomAccountOrder = ["a", "c"],
        };

        Assert.Equal(["a", "c", "b"], Arrange(settings));
    }

    [Fact]
    public void Leaves_out_hidden_accounts()
    {
        Assert.Equal(["c", "a"], Arrange(new HakariSettings { HiddenAccounts = ["b"] }));
    }

    [Fact]
    public void Moves_an_account_within_bounds()
    {
        Assert.Equal(["b", "a", "c"], AccountArrangement.Move(["a", "b", "c"], "b", -1));
        Assert.Equal(["a", "b", "c"], AccountArrangement.Move(["a", "b", "c"], "c", 5));
    }

    private static IReadOnlyList<string> Arrange(HakariSettings settings) =>
        [.. AccountArrangement.Arrange(Accounts, item => item.Id, item => item.Rank, settings)
            .Select(item => item.Id)];
}
