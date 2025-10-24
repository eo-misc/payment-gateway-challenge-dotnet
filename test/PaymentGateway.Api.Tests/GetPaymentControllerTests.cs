using System.Net;
using System.Net.Http.Json;

using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Tests;

public class GetPaymentControllerTests : ApiTestsBase
{
    [Fact]
    public async Task Get_GivenRecordExistsForMerchant_RetrievesRecordWith200OK()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var merchantId = "merchant-123";

        var existingPayment = new Payment
        {
            Id = paymentId,
            MerchantId = merchantId,
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = "1234",
            ExpiryMonth = "04",
            ExpiryYear = "2025",
            Currency = "GBP",
            Amount = 100
        };

        SeedPayment(existingPayment);

        var client = CreateClient(merchantId: merchantId);

        // Act
        var response = await client.GetAsync($"/api/payments/{paymentId}");
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(PaymentStatus.Authorized, paymentResponse.Status);
        Assert.NotEqual(Guid.Empty, paymentResponse.Id);
        Assert.Equal("1234", paymentResponse.CardNumberLastFour);
        Assert.Equal("04", paymentResponse.ExpiryMonth);
        Assert.Equal("2025", paymentResponse.ExpiryYear);
        Assert.Equal("GBP", paymentResponse.Currency);
        Assert.Equal(100, paymentResponse.Amount);
    }
    
    [Fact]
    public async Task Get_GivenRecordDoesntExistsForMerchant_Returns404NotFound()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var merchantId = "merchant-123";

        var existingPayment = new Payment
        {
            Id = paymentId,
            MerchantId = merchantId,
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = "1234",
            ExpiryMonth = "04",
            ExpiryYear = "2025",
            Currency = "GBP",
            Amount = 100
        };

        SeedPayment(existingPayment);

        var client = CreateClient(merchantId: merchantId);
        var otherPaymentId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/api/payments/{otherPaymentId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("merchant-123", "merchant-456")]
    [InlineData("merchant-A", "merchant-B")]
    public async Task Get_GivenRecordExistsForDifferentMerchant_Returns404NotFound(string ownerMerchantId, string requestingMerchantId)
    {
        // Arrange
        var paymentId = Guid.NewGuid();

        var existingPayment = new Payment
        {
            Id = paymentId,
            MerchantId = ownerMerchantId,
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = "1234",
            ExpiryMonth = "04",
            ExpiryYear = "2025",
            Currency = "GBP",
            Amount = 100
        };

        SeedPayment(existingPayment);

        var client = CreateClient(merchantId: requestingMerchantId);

        // Act
        var response = await client.GetAsync($"/api/payments/{paymentId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
}