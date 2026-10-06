using Hakari.Core.Currency;

namespace Hakari.Core.Tests.Currency;

public sealed class CurrencyCatalogTests
{
    [Fact]
    public void A_code_comes_first_when_typed_exactly()
    {
        Assert.Equal("SGD", CurrencyCatalog.Search("sgd")[0].Code);
    }

    [Fact]
    public void A_country_finds_its_currency()
    {
        Assert.Contains(CurrencyCatalog.Search("Singapore"), entry => entry.Code == "SGD");
    }

    [Fact]
    public void A_currency_name_finds_its_code()
    {
        Assert.Contains(CurrencyCatalog.Search("Yen"), entry => entry.Code == "JPY");
    }

    [Fact]
    public void Nothing_typed_suggests_nothing()
    {
        Assert.Empty(CurrencyCatalog.Search("  "));
    }
}
