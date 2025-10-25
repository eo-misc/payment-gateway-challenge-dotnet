using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Services;

public class PaymentRequestValidator(TimeProvider timeProvider) : IPaymentRequestValidator
{
    public IDictionary<string, string[]> Validate(PostPaymentRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        ValidateCardNumber(request.CardNumber, errors);
        ValidateExpiryMonth(request.ExpiryMonth, errors);
        ValidateExpiryYear(request.ExpiryYear, errors);
        ValidateCurrency(request.Currency, errors);
        ValidateAmount(request.Amount, errors);
        ValidateCvv(request.Cvv, errors);
        ValidateExpiryDate(request.ExpiryMonth, request.ExpiryYear, errors);

        return errors;
    }

    private static void ValidateCardNumber(string cardNumber, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(cardNumber) ||
            cardNumber.Length < 14 ||
            cardNumber.Length > 19 ||
            !cardNumber.All(char.IsAsciiDigit))
        {
            errors[nameof(PostPaymentRequest.CardNumber)] = new[] { "Card number must be 14-19 numeric characters" };
        }
    }

    private static void ValidateExpiryMonth(string expiryMonth, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(expiryMonth))
        {
            errors[nameof(PostPaymentRequest.ExpiryMonth)] = new[] { "Expiry month is required" };
            return;
        }

        if (!int.TryParse(expiryMonth, out var month) || month < 1 || month > 12)
        {
            errors[nameof(PostPaymentRequest.ExpiryMonth)] = new[] { "Expiry month must be between 1 and 12" };
        }
    }

    private static void ValidateExpiryYear(string expiryYear, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(expiryYear))
        {
            errors[nameof(PostPaymentRequest.ExpiryYear)] = new[] { "Expiry year is required" };
            return;
        }

        if (expiryYear.Length != 4 || !int.TryParse(expiryYear, out var year) || year < 2000 || year > 2099)
        {
            errors[nameof(PostPaymentRequest.ExpiryYear)] = new[] { "Expiry year must be 4 digits between 2000 and 2099" };
        }
    }

    private static void ValidateCurrency(string currency, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            errors[nameof(PostPaymentRequest.Currency)] = new[] { "Currency is required" };
            return;
        }

        if (currency.Length != 3 || !currency.All(char.IsLetter) || !currency.All(char.IsUpper))
        {
            errors[nameof(PostPaymentRequest.Currency)] = new[] { "Currency must be a 3-letter uppercase ISO code" };
            return;
        }

        if (!Currency.AcceptedCurrencies.Contains(currency))
        {
            errors[nameof(PostPaymentRequest.Currency)] = new[] { $"Currency must be one of: {string.Join(", ", Currency.AcceptedCurrencies)}" };
        }
    }

    private static void ValidateAmount(int amount, Dictionary<string, string[]> errors)
    {
        if (amount <= 0)
        {
            errors[nameof(PostPaymentRequest.Amount)] = new[] { "Amount must be greater than 0" };
        }
    }

    private static void ValidateCvv(string cvv, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(cvv))
        {
            errors[nameof(PostPaymentRequest.Cvv)] = new[] { "CVV is required" };
            return;
        }

        if ((cvv.Length != 3 && cvv.Length != 4) || !cvv.All(char.IsAsciiDigit))
        {
            errors[nameof(PostPaymentRequest.Cvv)] = new[] { "CVV must be 3 or 4 digits" };
        }
    }

    private void ValidateExpiryDate(string expiryMonth, string expiryYear, Dictionary<string, string[]> errors)
    {
        // Only validate expiry date if month and year format validation passed
        if (errors.ContainsKey(nameof(PostPaymentRequest.ExpiryMonth)) ||
            errors.ContainsKey(nameof(PostPaymentRequest.ExpiryYear)))
        {
            return;
        }

        var month = int.Parse(expiryMonth);
        var year = int.Parse(expiryYear);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Card expires at the end of the expiry month
        // Compare year first, then month - if current year/month is greater, card has expired
        if (now.Year > year || (now.Year == year && now.Month > month))
        {
            errors["ExpiryDate"] = new[] { "Card has expired" };
        }
    }
}
