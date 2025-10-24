using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Mvc;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

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
            BankUnavailableResult r     => StatusCode(502, new { error = r.Message }),
            _                           => StatusCode(500)
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