using System.Diagnostics;
using System.Net;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Helpers;
using PaymentGateway.Api.Mappers;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Persistence;
using Polly.CircuitBreaker;

namespace PaymentGateway.Api.Services;

public class PaymentService : IPaymentService
{
    private static readonly ActivitySource ActivitySource = new("PaymentGateway.Api");

    private readonly IBankClient _bankClient;
    private readonly IPaymentsRepository _paymentsRepository;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly IPaymentRequestValidator _validator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IBankClient bankClient,
        IPaymentsRepository paymentsRepository,
        IIdempotencyStore idempotencyStore,
        IPaymentRequestValidator validator,
        TimeProvider timeProvider,
        ILogger<PaymentService> logger)
    {
        _bankClient = bankClient;
        _paymentsRepository = paymentsRepository;
        _idempotencyStore = idempotencyStore;
        _validator = validator;
        _timeProvider = timeProvider;
        _logger = logger;
    }
    
    public async Task<ProcessResult> ProcessPaymentAsync(
        PostPaymentRequest request,
        string merchantId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("ProcessPayment", ActivityKind.Internal);
        activity?.SetTag("merchant.id", merchantId);
        activity?.SetTag("payment.amount", request.Amount);
        activity?.SetTag("payment.currency", request.Currency);

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["MerchantId"] = merchantId,
            ["IdempotencyKey"] = idempotencyKey
        });

        var validationErrors = _validator.Validate(request);
        if (validationErrors.Count > 0)
        {
            _logger.LogWarning("Payment validation failed with {ErrorCount} errors", validationErrors.Count);
            return new RejectedResult(validationErrors);
        }

        var requestFingerprint = StringHelpers.ComputeFingerprint(request);
        var idemStartResult = _idempotencyStore.Start(merchantId, idempotencyKey, requestFingerprint, _timeProvider.GetUtcNow());
        switch (idemStartResult.Outcome)
        {
            case IdempotencyStartOutcome.ReplayCompletedSameFingerprint:
                {
                    var existingId = idemStartResult.Record.PaymentId!.Value;
                    var existing = _paymentsRepository.Get(merchantId, existingId)!;

                    activity?.SetTag("payment.id", existing.Id);
                    activity?.SetTag("payment.status", existing.Status.ToString());

                    var replayResponse = existing.ToPostPaymentResponse();
                    return existing.Status == PaymentStatus.Authorized
                        ? new AuthorizedResult(replayResponse, IsReplay: true)
                        : new DeclinedResult(replayResponse, IsReplay: true);
                }

            case IdempotencyStartOutcome.InProgressSameFingerprint:
                return new ConflictInProgressResult("Another request with this idempotency key is being processed.", RetryAfterSeconds: 30);

            case IdempotencyStartOutcome.ConflictMismatchFingerprint:
                return new ConflictMismatchResult("Idempotency key was used with different parameters.");

            case IdempotencyStartOutcome.Started:
                break;
            default:
                throw new InvalidOperationException("Unknown idempotency outcome.");
        }
        
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            CardNumberLastFour = request.CardNumber[^4..],
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = request.Currency,
            Amount = request.Amount
        };

        activity?.SetTag("payment.id", payment.Id);

        var shouldCleanupIdempotency = true;
        try
        {
            var bankRequest = new BankPaymentRequest
            {
                CardNumber = request.CardNumber,
                ExpiryDate = $"{request.ExpiryMonth}/{request.ExpiryYear}",
                Currency = request.Currency,
                Amount = request.Amount,
                Cvv = request.Cvv
            };

            var bankResponse = await _bankClient.ProcessPaymentAsync(bankRequest, cancellationToken);
            payment.Status = bankResponse.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined;

            _paymentsRepository.Upsert(payment);
            _idempotencyStore.Complete(merchantId, idempotencyKey, payment.Id);

            activity?.SetTag("payment.status", payment.Status.ToString());

            var response = payment.ToPostPaymentResponse();
            shouldCleanupIdempotency = false;

            return payment.Status == PaymentStatus.Authorized
                ? new AuthorizedResult(response, IsReplay: false)
                : new DeclinedResult(response, IsReplay: false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Bank unavailable");
            _logger.LogWarning(ex, "Bank service unavailable after retries");
            return new BankUnavailableResult("Bank service unavailable");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Bank timeout");
            _logger.LogWarning("Bank request timed out");
            return new BankUnavailableResult("Bank timeout");
        }
        catch (BrokenCircuitException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Circuit breaker open");
            _logger.LogWarning("Circuit breaker opened due to bank failures");
            return new BankUnavailableResult("Bank timeout");
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Unexpected error processing payment");
            throw;
        }
        finally
        {
            if (shouldCleanupIdempotency)
            {
                _idempotencyStore.Delete(merchantId, idempotencyKey);
            }
        }
    }
    
    public RetrieveResult RetrievePayment(Guid id, string merchantId, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("RetrievePayment");
        activity?.SetTag("payment.id", id);
        activity?.SetTag("merchant.id", merchantId);

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["MerchantId"] = merchantId,
            ["PaymentId"] = id
        });

        var payment = _paymentsRepository.Get(merchantId, id);
        if (payment == null)
        {
            activity?.SetTag("retrieval.found", false);
            return new NotFoundResult("Payment not found");
        }

        activity?.SetTag("retrieval.found", true);
        activity?.SetTag("payment.status", payment.Status.ToString());

        var response = payment.ToGetPaymentResponse();
        return new FoundResult(response);
    }
}
