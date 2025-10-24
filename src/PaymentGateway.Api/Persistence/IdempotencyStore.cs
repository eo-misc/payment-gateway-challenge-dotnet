using System.Collections.Concurrent;

using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.Persistence;

public class IdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<(string MerchantId, string Key), IdempotencyRecord> _store = new();
    
    // no stale in progress recovery here - time constraint
    public IdempotencyStartResult Start(string merchantId, string key, string fingerprint, DateTimeOffset now)
    {
        var merchantIdKey = (merchantId, key);
        //try to add a new idempotency record to our store with an idem key. If exists already and in progress then return conflict, if complete then replay
        var candidate = new IdempotencyRecord(merchantId, key, fingerprint, IdempotencyState.InProgress, null, now);
        var idempotencyRecord = _store.GetOrAdd(merchantIdKey, candidate);
        var isNew = ReferenceEquals(idempotencyRecord, candidate); //idemRecord is immutable
        
        var sameFingerprint = string.Equals(idempotencyRecord.Fingerprint, fingerprint, StringComparison.Ordinal);
        
        var outcome = isNew
            ? IdempotencyStartOutcome.Started
            : (idempotencyRecord.State, sameFingerprint) switch
            {
                (IdempotencyState.Completed,  true) => IdempotencyStartOutcome.ReplayCompletedSameFingerprint,
                (IdempotencyState.InProgress, true) => IdempotencyStartOutcome.InProgressSameFingerprint,
                _                                   => IdempotencyStartOutcome.ConflictMismatchFingerprint // same idem key is provided with different fingerprint.
            };
        
        return new IdempotencyStartResult(outcome, idempotencyRecord);
    }

    public void Complete(string merchantId, string key, Guid paymentId)
    {
        var merchantIdKey = (merchantId, key);

        // Only complete if it exists
        if (_store.TryGetValue(merchantIdKey, out var current))
        {
            _store.AddOrUpdate(merchantIdKey,
                _ => current with { State = IdempotencyState.Completed, PaymentId = paymentId }, // never hit
                (_, existing) => existing with { State = IdempotencyState.Completed, PaymentId = paymentId });
        }
    }


    public void Delete(string merchantId, string key)
    {
        _store.TryRemove((merchantId, key), out _);
    }
}