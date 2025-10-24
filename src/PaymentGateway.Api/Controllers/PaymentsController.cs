using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Helpers;
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
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        merchantId = StringHelpers.NormalizeMerchantId(merchantId);
        if (merchantId is null)
            return BadRequest("Invalid or missing Merchant-Id header");
        
        idempotencyKey = StringHelpers.NormalizeIdempotencyKey(idempotencyKey);
        if (idempotencyKey is null)
            return BadRequest("Invalid Idempotency-Key header");
        
        var result = await _paymentService.ProcessPaymentAsync(request, merchantId, idempotencyKey, cancellationToken);

        return result switch
        {
            AuthorizedResult r => OkWithIdemHeaders(r.Payment, idempotencyKey, r.IsReplay),
            DeclinedResult  r  => OkWithIdemHeaders(r.Payment, idempotencyKey, r.IsReplay),
            RejectedResult r            => BadRequest(new ValidationProblemDetails(r.Errors)),
            ConflictInProgressResult r  => StatusCode(409, new ProblemDetails { Title = "Conflict", Detail = r.Message, Status = 409 }),
            ConflictMismatchResult r    => StatusCode(409, new ProblemDetails { Title = "Conflict", Detail = r.Message, Status = 409 }),
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
        merchantId = StringHelpers.NormalizeMerchantId(merchantId);
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
    
    private ActionResult<PostPaymentResponse> OkWithIdemHeaders(PostPaymentResponse body, string key, bool replay)
    {
        Response.Headers["Idempotency-Key"] = key;
        if (replay) Response.Headers["Idempotent-Replay"] = "true";
        return Ok(body);
    }
}