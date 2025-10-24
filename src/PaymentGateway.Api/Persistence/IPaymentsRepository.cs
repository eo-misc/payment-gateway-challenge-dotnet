using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.Persistence;

public interface IPaymentsRepository
{
    void Upsert(Payment payment);
    Payment? Get(string merchantId, Guid paymentId);
}