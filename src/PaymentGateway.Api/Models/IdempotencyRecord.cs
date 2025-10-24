using PaymentGateway.Api.Enums;

namespace PaymentGateway.Api.Models;

public sealed record IdempotencyRecord(
    string MerchantId,
    string Key,
    string Fingerprint,
    IdempotencyState State,
    Guid? PaymentId,
    DateTimeOffset CreatedUtc
);