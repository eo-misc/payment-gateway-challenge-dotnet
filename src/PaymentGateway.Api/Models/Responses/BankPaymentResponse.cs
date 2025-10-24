using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Models.Responses;

public sealed record BankPaymentResponse
{
    [JsonPropertyName("authorized")]
    public required bool Authorized { get; init; }

    [JsonPropertyName("authorization_code")]
    public required string AuthorizationCode { get; init; }
}
