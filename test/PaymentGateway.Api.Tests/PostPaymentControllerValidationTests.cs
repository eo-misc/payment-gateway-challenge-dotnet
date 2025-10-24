using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Tests;

public partial class PaymentsControllerTests
{
    [Theory]
    [InlineData(null!)]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("12345678901234567890")]
    [InlineData("1234567890123a")]
    [InlineData("1234-5678-9012-3456")]
    public async Task Post_GivenInvalidCardNumber_ThenReturns400Rejected(string cardNumber)
    {
        var request = new PostPaymentRequest
        {
            CardNumber = cardNumber,
            ExpiryMonth = "04",
            ExpiryYear = "2025",
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        var client = CreateClient(merchantId: "merchant-123", idempotencyKey: Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync("/api/payments", request);
        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problemDetails);
        Assert.True(problemDetails.Errors.ContainsKey("CardNumber"));
        Assert.NotEmpty(problemDetails.Errors["CardNumber"]);
    }

    [Theory]
    [InlineData(null!)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("13")]
    [InlineData("99")]
    [InlineData("ABC")]
    public async Task Post_GivenInvalidExpiryMonth_ThenReturns400Rejected(string expiryMonth)
    {
        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248111",
            ExpiryMonth = expiryMonth,
            ExpiryYear = "2030",
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        var client = CreateClient(merchantId: "merchant-123", idempotencyKey: Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync("/api/payments", request);
        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problemDetails);
        Assert.True(problemDetails.Errors.ContainsKey("ExpiryMonth"));
        Assert.NotEmpty(problemDetails.Errors["ExpiryMonth"]);
    }

    [Theory]
    [InlineData(null!)]
    [InlineData("")]
    [InlineData("003")]
    [InlineData("03")]
    [InlineData("ABC")]
    [InlineData("1999")]
    [InlineData("2100")]
    [InlineData("3000")]
    public async Task Post_GivenInvalidExpiryYear_ThenReturns400Rejected(string expiryYear)
    {
        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248111",
            ExpiryMonth = "12",
            ExpiryYear = expiryYear,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        var client = CreateClient(merchantId: "merchant-123", idempotencyKey: Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync("/api/payments", request);
        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problemDetails);
        Assert.True(problemDetails.Errors.ContainsKey("ExpiryYear"));
        Assert.NotEmpty(problemDetails.Errors["ExpiryYear"]);
    }

    [Theory]
    [InlineData(null!)]
    [InlineData("")]
    [InlineData("GB")]
    [InlineData("GBPP")]
    [InlineData("gbp")]
    [InlineData("Gbp")]
    [InlineData("123")]
    [InlineData("GB$")]
    public async Task Post_GivenInvalidCurrencyFormat_ThenReturns400Rejected(string currency)
    {
        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248111",
            ExpiryMonth = "12",
            ExpiryYear = "2030",
            Currency = currency,
            Amount = 100,
            Cvv = "123"
        };

        var client = CreateClient(merchantId: "merchant-123", idempotencyKey: Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync("/api/payments", request);
        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problemDetails);
        Assert.True(problemDetails.Errors.ContainsKey("Currency"));
        Assert.NotEmpty(problemDetails.Errors["Currency"]);
    }

    [Theory]
    [InlineData("JPY")]
    [InlineData("CAD")]
    [InlineData("AUD")]
    [InlineData("CHF")]
    public async Task Post_GivenUnsupportedCurrency_ThenReturns400Rejected(string currency)
    {
        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248111",
            ExpiryMonth = "12",
            ExpiryYear = "2030",
            Currency = currency,
            Amount = 100,
            Cvv = "123"
        };

        var client = CreateClient(merchantId: "merchant-123", idempotencyKey: Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync("/api/payments", request);
        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problemDetails);
        Assert.True(problemDetails.Errors.ContainsKey("Currency"));
        Assert.NotEmpty(problemDetails.Errors["Currency"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(int.MinValue)]
    public async Task Post_GivenInvalidAmount_ThenReturns400Rejected(int amount)
    {
        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248111",
            ExpiryMonth = "12",
            ExpiryYear = "2030",
            Currency = "GBP",
            Amount = amount,
            Cvv = "123"
        };

        var client = CreateClient(merchantId: "merchant-123", idempotencyKey: Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync("/api/payments", request);
        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problemDetails);
        Assert.True(problemDetails.Errors.ContainsKey("Amount"));
        Assert.NotEmpty(problemDetails.Errors["Amount"]);
    }

    [Theory]
    [InlineData(null!)]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("12345")]
    [InlineData("12a")]
    [InlineData("abc")]
    public async Task Post_GivenInvalidCvv_ThenReturns400Rejected(string cvv)
    {
        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248111",
            ExpiryMonth = "12",
            ExpiryYear = "2030",
            Currency = "GBP",
            Amount = 100,
            Cvv = cvv
        };

        var client = CreateClient(merchantId: "merchant-123", idempotencyKey: Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync("/api/payments", request);
        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problemDetails);
        Assert.True(problemDetails.Errors.ContainsKey("Cvv"));
        Assert.NotEmpty(problemDetails.Errors["Cvv"]);
    }

    [Theory]
    [InlineData("01", "2024", 2024, 2, 1)]
    [InlineData("02", "2024", 2024, 3, 1)]
    [InlineData("06", "2024", 2024, 7, 1)]
    [InlineData("12", "2023", 2024, 1, 1)]
    [InlineData("01", "2000", 2024, 1, 1)]
    public async Task Post_GivenExpiredCard_ThenReturns400Rejected(
        string expiryMonth,
        string expiryYear,
        int currentYear,
        int currentMonth,
        int currentDay)
    {
        TimeProvider.SetUtcNow(new DateTimeOffset(currentYear, currentMonth, currentDay, 0, 0, 0, TimeSpan.Zero));

        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248111",
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        var client = CreateClient(merchantId: "merchant-123", idempotencyKey: Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync("/api/payments", request);
        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problemDetails);
        Assert.True(problemDetails.Errors.ContainsKey("ExpiryDate"));
        Assert.NotEmpty(problemDetails.Errors["ExpiryDate"]);
    }

    [Theory]
    [InlineData("02", "2024", 2024, 2, 1)]
    [InlineData("02", "2024", 2024, 2, 29)]
    [InlineData("03", "2024", 2024, 3, 31)]
    [InlineData("04", "2024", 2024, 4, 30)]
    [InlineData("12", "2024", 2024, 12, 31)]
    public async Task Post_GivenFutureExpiry_ThenReturns200AndNotRejected(
        string expiryMonth,
        string expiryYear,
        int currentYear,
        int currentMonth,
        int currentDay)
    {
        TimeProvider.SetUtcNow(new DateTimeOffset(currentYear, currentMonth, currentDay, 0, 0, 0, TimeSpan.Zero));

        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248111",
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        var client = CreateClient(merchantId: "merchant-123", idempotencyKey: Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync("/api/payments", request);
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
