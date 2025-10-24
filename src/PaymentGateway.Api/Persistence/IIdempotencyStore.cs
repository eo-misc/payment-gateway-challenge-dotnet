using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.Persistence;

public sealed record IdempotencyStartResult(
    IdempotencyStartOutcome Outcome,
    IdempotencyRecord Record
);

public interface IIdempotencyStore
{
    IdempotencyStartResult Start(
        string merchantId,
        string key,
        string fingerprint,
        DateTimeOffset now);

    void Complete(
        string merchantId,
        string key,
        Guid paymentId);
    
    void Delete(
        string merchantId,
        string key);

    // WE assume no TTLS for this - we would use dynamo or redis TTL funcancellationTokenionality to auto delete Idempotency records
}