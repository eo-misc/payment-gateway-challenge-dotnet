using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Helpers;

public static class StringHelpers
{
    private static readonly Regex Token = new("^[A-Za-z0-9._-]{1,64}$", RegexOptions.Compiled);

    public static string? NormalizeIdempotencyKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        key = key.Trim();
        return Token.IsMatch(key) ? key : null;
    }

    public static string? NormalizeMerchantId(string? merchantId)
    {
        if (string.IsNullOrWhiteSpace(merchantId))
            return null;

        merchantId = merchantId.Trim();
        return Token.IsMatch(merchantId) ? merchantId : null;
    }
    
    public static string ComputeFingerprint(PostPaymentRequest req)
    {
        var last4   = req.CardNumber[^4..]; // string slice; keeps leading zeros
        var ccy     = req.Currency.Trim().ToUpperInvariant();
        var month   = int.Parse(req.ExpiryMonth, CultureInfo.InvariantCulture); // normalize to int
        var year    = int.Parse(req.ExpiryYear,  CultureInfo.InvariantCulture);
        var amount  = req.Amount; // integer minor units by contract

        var canonical = $"{amount}|{ccy}|{last4}|{month}|{year}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}