using System.Net;
using System.Net.Http.Json;

using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Tests;

public partial class PaymentsControllerTests : ApiTestsBase
{
    [Fact]
    public async Task Post_GivenValidPaymentAndBankAuthorizes_ThenReturns200Authorized()
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248111",
            ExpiryMonth = "04",
            ExpiryYear = "2025",
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        var client = CreateClient(merchantId: "merchant-123", idempotencyKey: Guid.NewGuid().ToString());

        // Act
        var response = await client.PostAsJsonAsync("/api/payments", request);
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>(ApiFactory.JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(PaymentStatus.Authorized, paymentResponse.Status);
        Assert.NotEqual(Guid.Empty, paymentResponse.Id);
        Assert.Equal("8111", paymentResponse.CardNumberLastFour);
        Assert.Equal("04", paymentResponse.ExpiryMonth);
        Assert.Equal("2025", paymentResponse.ExpiryYear);
        Assert.Equal("GBP", paymentResponse.Currency);
        Assert.Equal(100, paymentResponse.Amount);
    }
    
    [Fact]
    public async Task Post_GivenValidPaymentAndBankAuthorizes_ThenPersistsPaymentToDatabase()
    {
        // Arrange
        var merchantId = "merchant-123";
        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248111",
            ExpiryMonth = "04",
            ExpiryYear = "2025",
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        var client = CreateClient(merchantId: merchantId,  idempotencyKey: Guid.NewGuid().ToString());

        // Act
        var response = await client.PostAsJsonAsync("/api/payments", request);
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>(ApiFactory.JsonOptions);

        // Assert
        Assert.NotNull(paymentResponse);
        var savedPayment = GetPayment(merchantId, paymentResponse.Id);
        Assert.NotNull(savedPayment);
        Assert.Equal(paymentResponse.Id, savedPayment.Id);
        Assert.Equal(merchantId, savedPayment.MerchantId);
        Assert.Equal(PaymentStatus.Authorized, savedPayment.Status);
        Assert.Equal("8111", savedPayment.CardNumberLastFour);
        Assert.Equal("04", savedPayment.ExpiryMonth);
        Assert.Equal("2025", savedPayment.ExpiryYear);
        Assert.Equal("GBP", savedPayment.Currency);
        Assert.Equal(100, savedPayment.Amount);
    }
    
    [Fact]
    public async Task Post_GivenValidPaymentAndBankDeclines_ThenReturns200Declined()
    {
        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248888",
            ExpiryMonth = "04",
            ExpiryYear = "2025",
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        var client = CreateClient(merchantId: "merchant-123", idempotencyKey: Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync("/api/payments", request);
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>(ApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(PaymentStatus.Declined, paymentResponse.Status);
        Assert.NotEqual(Guid.Empty, paymentResponse.Id);
        Assert.Equal("8888", paymentResponse.CardNumberLastFour);
        Assert.Equal("04", paymentResponse.ExpiryMonth);
        Assert.Equal("2025", paymentResponse.ExpiryYear);
        Assert.Equal("GBP", paymentResponse.Currency);
        Assert.Equal(100, paymentResponse.Amount);
    }
    
    [Fact]
    public async Task Post_GivenValidPaymentAndBankDeclines_ThenPersistsPaymentToDatabase()
    {
        // Arrange
        var merchantId = "merchant-123";
        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248112",
            ExpiryMonth = "04",
            ExpiryYear = "2025",
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        var client = CreateClient(merchantId: merchantId, idempotencyKey: Guid.NewGuid().ToString());

        // Act
        var response = await client.PostAsJsonAsync("/api/payments", request);
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>(ApiFactory.JsonOptions);

        // Assert
        Assert.NotNull(paymentResponse);
        var savedPayment = GetPayment(merchantId, paymentResponse.Id);
        Assert.NotNull(savedPayment);
        Assert.Equal(paymentResponse.Id, savedPayment.Id);
        Assert.Equal(merchantId, savedPayment.MerchantId);
        Assert.Equal(PaymentStatus.Declined, savedPayment.Status);
        Assert.Equal("8112", savedPayment.CardNumberLastFour);
        Assert.Equal("04", savedPayment.ExpiryMonth);
        Assert.Equal("2025", savedPayment.ExpiryYear);
        Assert.Equal("GBP", savedPayment.Currency);
        Assert.Equal(100, savedPayment.Amount);
    }
    
    [Fact]
    public async Task Post_GivenValidPaymentAndBankReturns503_ThenReturns502BadGateway()
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248880",
            ExpiryMonth = "04",
            ExpiryYear = "2025",
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        var client = CreateClient(merchantId: "merchant-123", idempotencyKey: Guid.NewGuid().ToString());

        // Act
        var response = await client.PostAsJsonAsync("/api/payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }
}