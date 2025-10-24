using PaymentGateway.Api.Enums;

namespace PaymentGateway.Api.Models;

public class Payment
{
    public required Guid Id { get; set; }
    public required string MerchantId { get; set; }
    public PaymentStatus Status { get; set; }
    public required string CardNumberLastFour { get; set; }
    public required string ExpiryMonth { get; set; }
    public required string ExpiryYear { get; set; }
    public required string Currency { get; set; }
    public required int Amount { get; set; }
}
