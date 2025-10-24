using System.Diagnostics;
using System.Net;
using PaymentGateway.Api.Enums;
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
    private readonly IPaymentRequestValidator _validator;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IBankClient bankClient,
        IPaymentsRepository paymentsRepository,
        IPaymentRequestValidator validator,
        ILogger<PaymentService> logger)
    {
        _bankClient = bankClient;
        _paymentsRepository = paymentsRepository;
        _validator = validator;
        _logger = logger;
    }
    
    public async Task<ProcessResult> ProcessPaymentAsync(
        PostPaymentRequest request,
        string merchantId,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("ProcessPayment", ActivityKind.Internal);
        activity?.SetTag("merchant.id", merchantId);
        activity?.SetTag("payment.amount", request.Amount);
        activity?.SetTag("payment.currency", request.Currency);
        activity?.SetTag("payment.card_last4", request.CardNumber[^4..]);
        
        var validationErrors = _validator.Validate(request);
        if (validationErrors.Count > 0)
        {
            using (_logger.BeginScope(new Dictionary<string, object> { ["merchant.id"] = merchantId }))
                _logger.LogWarning("Validation failed: {Count} errors", validationErrors.Count);

            activity?.SetTag("payment.status", "Rejected");
            return new RejectedResult(validationErrors);
        }
        
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            // Status set after bank call
            CardNumberLastFour = request.CardNumber[^4..],
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = request.Currency,
            Amount = request.Amount
        };

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["merchant.id"] = merchantId,
            ["payment.id"] = payment.Id
        }))
        {
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

                using var bankActivity = ActivitySource.StartActivity("Bank.ProcessPayment", ActivityKind.Client);
                var bankResponse = await _bankClient.ProcessPaymentAsync(bankRequest, cancellationToken);
                bankActivity?.SetTag("bank.authorized", bankResponse.Authorized);

                payment.Status = bankResponse.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined;

                _paymentsRepository.Upsert(payment);

                activity?.SetTag("payment.id", payment.Id);
                activity?.SetTag("payment.status", payment.Status.ToString());

                var response = payment.ToPostPaymentResponse();
                return payment.Status == PaymentStatus.Authorized
                    ? new AuthorizedResult(response,  IsReplay: false)
                    : new DeclinedResult(response,  IsReplay: false);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                activity?.SetTag("bank.unavailable", true);
                activity?.SetStatus(ActivityStatusCode.Error, "Bank 503 after retries");
                _logger.LogWarning(ex, "Bank unavailable (503) after retries");

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
                activity?.SetStatus(ActivityStatusCode.Error, "Bank timeout");
                _logger.LogWarning("Circuit breaker opened");
                // this could raise a 429 instead
                return new BankUnavailableResult("Bank timeout");
            }
            catch (Exception ex)
            {
                activity?.SetTag("exception.type", ex.GetType().FullName);
                activity?.SetTag("exception.message", ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogError(ex, "ProcessPayment failed");
                throw;
            }
        }
    }
    
    public RetrieveResult RetrievePayment(Guid id, string merchantId, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("RetrievePayment");
        activity?.SetTag("payment.id", id);
        activity?.SetTag("merchant.id", merchantId);

        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["merchant.id"] = merchantId,
                   ["payment.id"] = id
               }))
        {
            _logger.LogInformation("Retrieving payment");

            var payment = _paymentsRepository.Get(merchantId, id);
            if (payment == null)
            {
                activity?.SetTag("retrieval.found", false);
                _logger.LogInformation("Payment not found");
                return new NotFoundResult("Payment not found");
            }

            activity?.SetTag("retrieval.found", true);
            activity?.SetTag("payment.status", payment.Status.ToString());

            _logger.LogInformation("Payment retrieved: Status={Status}", payment.Status);

            var response = payment.ToGetPaymentResponse();
            return new FoundResult(response);
        }
    }
}
