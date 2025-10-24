using System.Diagnostics;
using System.Net;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Mappers;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Persistence;

namespace PaymentGateway.Api.Services;

public class PaymentService : IPaymentService
{
    private static readonly ActivitySource ActivitySource = new("PaymentGateway.Api");

    private readonly IBankClient _bankClient;
    private readonly IPaymentsRepository _paymentsRepository;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IBankClient bankClient,
        IPaymentsRepository paymentsRepository,
        ILogger<PaymentService> logger)
    {
        _bankClient = bankClient;
        _paymentsRepository = paymentsRepository;
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
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
            {
                // Bank returned 400 - this shouldn't happen if our validation is correct
                // Treat as bank error to avoid leaking details
                activity?.SetTag("bank.bad_request", true);
                activity?.SetStatus(ActivityStatusCode.Error, "Bank returned 400");
                _logger.LogError(ex, "Bank unexpectedly returned 400 BadRequest");

                return new BankUnavailableResult("An error occurred");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Bank timeout");
                _logger.LogWarning("Bank request timed out");

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
}
