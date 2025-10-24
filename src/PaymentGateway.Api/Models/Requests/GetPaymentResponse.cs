using PaymentGateway.Api.Enums;

namespace PaymentGateway.Api.Models.Requests;

public class GetPaymentResponse
{
    public Guid Id { get; set; }
    public PaymentStatus Status { get; set; }
    public required string CardNumberLastFour { get; set; }
    public required string ExpiryMonth { get; set; }
    public required string ExpiryYear { get; set; }
    public required string Currency { get; set; }
    public int Amount { get; set; }
}