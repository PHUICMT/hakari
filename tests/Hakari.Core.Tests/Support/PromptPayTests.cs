using Hakari.Core.Support;

namespace Hakari.Core.Tests.Support;

public sealed class PromptPayTests
{
    [Fact]
    public void A_phone_number_makes_the_standard_payload()
    {
        // 0812345678 with no amount; the check digits agree with Python's binascii.crc_hqx.
        Assert.Equal(
            "00020101021129370016A000000677010111011300668123456785303764"
                + "5802TH6304823E",
            PromptPay.Payload("081-234-5678"));
    }

    [Fact]
    public void A_national_id_uses_its_own_field()
    {
        var payload = PromptPay.Payload("1234567890123");

        Assert.Contains("02131234567890123", payload);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1812345678")]
    public void Anything_else_makes_no_payload(string id)
    {
        Assert.Null(PromptPay.Payload(id));
    }
}
