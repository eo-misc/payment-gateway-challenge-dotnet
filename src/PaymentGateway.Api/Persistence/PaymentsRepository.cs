using System.Collections.Concurrent;
using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.Persistence;

public class PaymentsRepository : IPaymentsRepository
{
    private readonly ConcurrentDictionary<(string MerchantId, Guid PaymentId), Payment> _store = new();

    public void Upsert(Payment payment)
    {
        _store[(payment.MerchantId, payment.Id)] = payment;
    }
    
    public Payment? Get(string merchantId, Guid paymentId)
    {
        _store.TryGetValue((merchantId, paymentId), out var payment);
        return payment;
    }

    internal void Clear() => _store.Clear();
}