using System.Globalization;
using System.Text;

namespace Hakari.Core.Support;

/// <summary>
/// The text a Thai PromptPay QR code carries, for a phone number, a national ID or an
/// e-wallet ID, with no amount so the giver chooses one. Built to the Thai QR payment
/// standard (EMVCo fields and a CRC-16 check at the end).
/// </summary>
public static class PromptPay
{
    private const int PhoneDigits = 10;
    private const int NationalIdDigits = 13;
    private const int WalletDigits = 15;
    private const string PayloadFormat = "000201";
    private const string StaticCode = "010211";
    private const string Application = "0016A000000677010111";
    private const string MerchantTag = "29";
    private const string PhoneTag = "01";
    private const string NationalIdTag = "02";
    private const string WalletTag = "03";
    private const string ThaiCountryCode = "0066";
    private const string BahtField = "5303764";
    private const string CountryField = "5802TH";
    private const string ChecksumTag = "6304";
    private const ushort CrcPolynomial = 0x1021;
    private const ushort CrcStart = 0xFFFF;

    /// <summary>The payload, or null when the ID is not a number PromptPay takes.</summary>
    public static string? Payload(string id)
    {
        var digits = new string(id.Where(char.IsAsciiDigit).ToArray());
        var account = digits.Length switch
        {
            PhoneDigits when digits[0] == '0' =>
                Field(PhoneTag, ThaiCountryCode + digits[1..]),
            NationalIdDigits => Field(NationalIdTag, digits),
            WalletDigits => Field(WalletTag, digits),
            _ => null,
        };
        if (account is null)
        {
            return null;
        }

        var body = PayloadFormat + StaticCode + Field(MerchantTag, Application + account)
            + BahtField + CountryField + ChecksumTag;
        return body + Crc(body).ToString("X4", CultureInfo.InvariantCulture);
    }

    private static string Field(string tag, string value) =>
        tag + value.Length.ToString("00", CultureInfo.InvariantCulture) + value;

    /// <summary>CRC-16/CCITT-FALSE over the payload so far, as the standard asks.</summary>
    private static ushort Crc(string text)
    {
        var crc = CrcStart;
        foreach (var value in Encoding.ASCII.GetBytes(text))
        {
            crc ^= (ushort)(value << 8);
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 0x8000) != 0
                    ? (ushort)((crc << 1) ^ CrcPolynomial)
                    : (ushort)(crc << 1);
            }
        }

        return crc;
    }
}
