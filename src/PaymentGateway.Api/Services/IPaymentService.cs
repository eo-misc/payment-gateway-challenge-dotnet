using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Services;

public interface IPaymentService
{
    Task<ProcessResult> ProcessPaymentAsync(PostPaymentRequest request, string merchantId, string idempotencyKey, CancellationToken cancellationToken);
    
    RetrieveResult RetrievePayment(Guid id, string merchantId, CancellationToken cancellationToken);
}