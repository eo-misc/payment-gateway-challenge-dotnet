using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Services;

public interface IPaymentRequestValidator
{
    IDictionary<string, string[]> Validate(PostPaymentRequest request);
}