using Hakari.Core.Settings;

namespace Hakari.Core.Tests.Settings;

/// <summary>A dragged order, saved as the custom order, is the order shown.</summary>
public sealed class ReorderOrderTests
{
    [Fact]
    public void A_saved_custom_order_puts_the_dragged_account_first()
    {
        var settings = new HakariSettings
        {
            AccountOrdering = AccountOrder.Custom,
            CustomAccountOrder = ["home", "work"],
        };
        (string Id, int Rank)[] accounts = [("work", 95), ("home", 100)];

        var arranged = AccountArrangement.Arrange(accounts, item => item.Id, item => 0, settings);

        Assert.Equal(["home", "work"], arranged.Select(item => item.Id));
    }
}
