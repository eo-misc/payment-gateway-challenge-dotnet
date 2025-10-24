namespace PaymentGateway.Api.Enums;
public enum IdempotencyState { InProgress, Completed }

public enum IdempotencyStartOutcome
{
    Started,                      
    ReplayCompletedSameFingerprint,
    ConflictMismatchFingerprint,  
    InProgressSameFingerprint      
}