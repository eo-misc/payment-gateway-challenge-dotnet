namespace PaymentGateway.Api.Models.Requests;

public sealed record PostPaymentRequest
{
    public string? CardNumber { get; set; }
    public string? ExpiryMonth { get; set; }
    public string? ExpiryYear { get; set; }
    public string? Currency { get; set; }
    public int Amount { get; set; }
    public string? Cvv { get; set; }
}