using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Models;

public abstract record RetrieveResult;

public sealed record FoundResult(GetPaymentResponse Payment) : RetrieveResult;
public sealed record NotFoundResult(string message) : RetrieveResult;
