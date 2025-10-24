using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

using NotFoundResult = PaymentGateway.Api.Models.NotFoundResult;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController : Controller
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost]
    public async Task<ActionResult<PostPaymentResponse>> PostPaymentAsync(
        [FromBody] PostPaymentRequest request,
        [FromHeader(Name = "Merchant-Id")] string? merchantId,
        CancellationToken cancellationToken)
    {
        merchantId = NormalizeMerchantId(merchantId);
        if (merchantId is null)
            return BadRequest("Invalid or missing Merchant-Id header");
        
        var result = await _paymentService.ProcessPaymentAsync(request, merchantId, cancellationToken);

        return result switch
        {
            AuthorizedResult r => Ok(r.Payment),
            DeclinedResult  r  => Ok(r.Payment),
            RejectedResult r            => BadRequest(new ValidationProblemDetails(r.Errors)),
            BankUnavailableResult r     => StatusCode(502, new { error = r.Message }),
            _                           => StatusCode(500)
        };
    }
    
    [HttpGet("{id:guid}")]
    public ActionResult<GetPaymentResponse?> GetPaymentAsync(
        Guid id,
        [FromHeader(Name = "Merchant-Id")] string? merchantId,
        CancellationToken cancellationToken)
    {
        merchantId = NormalizeMerchantId(merchantId);
        if (merchantId == null)
            return BadRequest("Invalid or missing Merchant-Id header");

        var result = _paymentService.RetrievePayment(id, merchantId, cancellationToken);

        return result switch
        {
            FoundResult r => Ok(r.Payment),
            NotFoundResult r => NotFound(r.message),
            _ => StatusCode(500)
        };
    }
    
    private static readonly Regex Token = new("^[A-Za-z0-9._-]{1,64}$", RegexOptions.Compiled);
    
    private static string? NormalizeMerchantId(string? merchantId)
    {
        if (string.IsNullOrWhiteSpace(merchantId))
            return null;

        merchantId = merchantId.Trim();
        return Token.IsMatch(merchantId) ? merchantId : null;
    }
}