using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Models;

public abstract record ProcessResult;

public sealed record AuthorizedResult(PostPaymentResponse Payment, bool IsReplay) : ProcessResult;

public sealed record DeclinedResult(PostPaymentResponse Payment, bool IsReplay) : ProcessResult;

public sealed record BankUnavailableResult(string Message) : ProcessResult;

public sealed record RejectedResult(IDictionary<string, string[]> Errors) : ProcessResult;

