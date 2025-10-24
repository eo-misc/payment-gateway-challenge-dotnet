using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Services;

public interface IPaymentService
{
    Task<ProcessResult> ProcessPaymentAsync(PostPaymentRequest request, string merchantId, CancellationToken cancellationToken);
}