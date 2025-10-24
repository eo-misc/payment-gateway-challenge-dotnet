namespace PaymentGateway.Api.Models.Requests;

public sealed record PostPaymentRequest
{
    public required string CardNumber { get; set; }
    public required string ExpiryMonth { get; set; }
    public required string ExpiryYear { get; set; }
    public required string Currency { get; set; }
    public int Amount { get; set; }
    public required string Cvv { get; set; }
}