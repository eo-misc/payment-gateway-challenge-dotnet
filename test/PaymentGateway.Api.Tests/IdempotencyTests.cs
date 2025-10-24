using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Persistence;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

public class IdempotencyTests
{
    [Fact]
    public async Task Post_GivenSameIdempotencyKeyAndSamePayloadAfterAuthorized_Then200AuthorizedWithReplay()
    {
        // arrange
        var mockBankClient = new CountingBankClient();

        var webApplicationFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(webHostBuilder => webHostBuilder.ConfigureServices(services =>
            {
                services.RemoveAll<IBankClient>();
                services.AddSingleton<IBankClient>(mockBankClient);
            }));

        var httpClient = webApplicationFactory.CreateClient();
        var testMerchantId = "m-123";
        var idempotencyKey = Guid.NewGuid().ToString();

        httpClient.DefaultRequestHeaders.Add("Merchant-Id", testMerchantId);
        httpClient.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);

        var paymentRequest = new PostPaymentRequest
        {
            CardNumber = "4242424242424241",
            ExpiryMonth = "12",
            ExpiryYear = "2028",
            Currency = "GBP",
            Amount = 1050,
            Cvv = "123"
        };

        // act 1: first call processes and persists
        var firstResponse = await httpClient.PostAsJsonAsync("/api/payments", paymentRequest);
        var firstPaymentResult = await firstResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // assert first call
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.NotNull(firstPaymentResult);
        Assert.Equal(PaymentStatus.Authorized, firstPaymentResult!.Status);
        Assert.NotEqual(Guid.Empty, firstPaymentResult.Id);
        Assert.Equal(1, mockBankClient.Calls);

        // act 2: same key + same payload → replay
        var secondResponse = await httpClient.PostAsJsonAsync("/api/payments", paymentRequest);
        var secondPaymentResult = await secondResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // assert replay behavior
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotNull(secondPaymentResult);
        Assert.Equal(PaymentStatus.Authorized, secondPaymentResult!.Status);
        Assert.Equal(firstPaymentResult.Id, secondPaymentResult.Id);  // same payment
        Assert.Equal(1, mockBankClient.Calls);  // bank NOT called again

        var hasReplayHeader = secondResponse.Headers.TryGetValues("Idempotent-Replay", out var replayHeaderValues);
        Assert.Equal("true", hasReplayHeader ? replayHeaderValues.Single() : null);
    }
    
    [Fact]
    public async Task Post_GivenSameIdempotencyKeyAndSamePayloadAfterDeclined_Then200DeclinedWithReplay()
    {
        // arrange
        var mockBankClient = new CountingBankClient();

        var webApplicationFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(webHostBuilder => webHostBuilder.ConfigureServices(services =>
            {
                services.RemoveAll<IBankClient>();
                services.AddSingleton<IBankClient>(mockBankClient);
                services.RemoveAll<IIdempotencyStore>();
                services.AddSingleton<IIdempotencyStore, IdempotencyStore>();
                services.RemoveAll<IPaymentsRepository>();
                services.AddSingleton<IPaymentsRepository, PaymentsRepository>();
            }));

        var httpClient = webApplicationFactory.CreateClient();
        var testMerchantId = "m-123";
        var idempotencyKey = Guid.NewGuid().ToString();

        httpClient.DefaultRequestHeaders.Add("Merchant-Id", testMerchantId);
        httpClient.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);

        var paymentRequest = new PostPaymentRequest
        {
            CardNumber = "4242424242424242",
            ExpiryMonth = "12",
            ExpiryYear = "2028",
            Currency = "GBP",
            Amount = 1050,
            Cvv = "123"
        };

        // act 1: first call processes and persists
        var firstResponse = await httpClient.PostAsJsonAsync("/api/payments", paymentRequest);
        var firstPaymentResult = await firstResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // assert first call
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.NotNull(firstPaymentResult);
        Assert.Equal(PaymentStatus.Declined, firstPaymentResult!.Status);
        Assert.NotEqual(Guid.Empty, firstPaymentResult.Id);
        Assert.Equal(1, mockBankClient.Calls);

        // act 2: same key + same payload → replay
        var secondResponse = await httpClient.PostAsJsonAsync("/api/payments", paymentRequest);
        var secondPaymentResult = await secondResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // assert replay behavior
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotNull(secondPaymentResult);
        Assert.Equal(PaymentStatus.Declined, secondPaymentResult!.Status);
        Assert.Equal(firstPaymentResult.Id, secondPaymentResult.Id);  // same payment
        Assert.Equal(1, mockBankClient.Calls);  // bank NOT called again

        var hasReplayHeader = secondResponse.Headers.TryGetValues("Idempotent-Replay", out var replayHeaderValues);
        Assert.Equal("true", hasReplayHeader ? replayHeaderValues.Single() : null);
    }

    [Fact]
    public async Task Post_GivenSameIdempotencyKeyAndSamePayloadWhileFirstInProgress_Then409ConflictInProgress()
    {
        // arrange
        var slowBankClient = new SlowBankClient(delayMs: 500); // simulate slow bank processing

        var webApplicationFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(webHostBuilder => webHostBuilder.ConfigureServices(services =>
            {
                services.RemoveAll<IBankClient>();
                services.AddSingleton<IBankClient>(slowBankClient);
            }));

        var httpClient = webApplicationFactory.CreateClient();
        var testMerchantId = "m-123";
        var idempotencyKey = Guid.NewGuid().ToString();

        httpClient.DefaultRequestHeaders.Add("Merchant-Id", testMerchantId);
        httpClient.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);

        var paymentRequest = new PostPaymentRequest
        {
            CardNumber = "4242424242424241",
            ExpiryMonth = "12",
            ExpiryYear = "2028",
            Currency = "GBP",
            Amount = 1050,
            Cvv = "123"
        };

        // act: send first request (will be slow)
        var firstTask = httpClient.PostAsJsonAsync("/api/payments", paymentRequest);

        // wait a bit to ensure first request is being processed but not complete
        await Task.Delay(100);

        // send second request while first is still in progress
        var secondResponse = await httpClient.PostAsJsonAsync("/api/payments", paymentRequest);

        // wait for first to complete
        var firstResponse = await firstTask;

        // assert
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);

        var conflictContent = await secondResponse.Content.ReadAsStringAsync();
        Assert.Contains("Another request with this idempotency key is being processed", conflictContent);
    }

    [Fact]
    public async Task Post_GivenSameIdempotencyKeyAndDifferentPayloadAfterCompleted_Then409ConflictDifferentPayload()
    {
        // arrange
        var mockBankClient = new CountingBankClient();

        var webApplicationFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(webHostBuilder => webHostBuilder.ConfigureServices(services =>
            {
                services.RemoveAll<IBankClient>();
                services.AddSingleton<IBankClient>(mockBankClient);
            }));

        var httpClient = webApplicationFactory.CreateClient();
        var testMerchantId = "m-123";
        var idempotencyKey = Guid.NewGuid().ToString();

        httpClient.DefaultRequestHeaders.Add("Merchant-Id", testMerchantId);
        httpClient.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);

        var firstPaymentRequest = new PostPaymentRequest
        {
            CardNumber = "4242424242424241",
            ExpiryMonth = "12",
            ExpiryYear = "2028",
            Currency = "GBP",
            Amount = 1050,
            Cvv = "123"
        };

        // act 1: first call processes successfully
        var firstResponse = await httpClient.PostAsJsonAsync("/api/payments", firstPaymentRequest);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var differentPaymentRequest = new PostPaymentRequest
        {
            CardNumber = "4242424242424241",
            ExpiryMonth = "12",
            ExpiryYear = "2028",
            Currency = "GBP",
            Amount = 2000,  // different amount
            Cvv = "123"
        };

        // act 2: same idempotency key but different payload
        var secondResponse = await httpClient.PostAsJsonAsync("/api/payments", differentPaymentRequest);

        // assert conflict
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Post_GivenDifferentIdempotencyKeySamePayload_ThenProcessesIndependently()
    {
        // arrange
        var mockBankClient = new CountingBankClient();

        var webApplicationFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(webHostBuilder => webHostBuilder.ConfigureServices(services =>
            {
                services.RemoveAll<IBankClient>();
                services.AddSingleton<IBankClient>(mockBankClient);
            }));

        var httpClient = webApplicationFactory.CreateClient();
        var testMerchantId = "m-123";

        var paymentRequest = new PostPaymentRequest
        {
            CardNumber = "4242424242424241",
            ExpiryMonth = "12",
            ExpiryYear = "2028",
            Currency = "GBP",
            Amount = 1050,
            Cvv = "123"
        };

        // act: send same request with different idempotency keys
        httpClient.DefaultRequestHeaders.Add("Merchant-Id", testMerchantId);

        httpClient.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var firstResponse = await httpClient.PostAsJsonAsync("/api/payments", paymentRequest);
        var firstPaymentResult = await firstResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        httpClient.DefaultRequestHeaders.Remove("Idempotency-Key");
        httpClient.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var secondResponse = await httpClient.PostAsJsonAsync("/api/payments", paymentRequest);
        var secondPaymentResult = await secondResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // assert both processed independently
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotEqual(firstPaymentResult!.Id, secondPaymentResult!.Id);  // different payment IDs
        Assert.Equal(2, mockBankClient.Calls);  // bank called twice
    }

    [Fact]
    public async Task Post_GivenSameIdempotencyKeySamePayloadDifferentMerchant_ThenProcessesIndependently()
    {
        // arrange
        var mockBankClient = new CountingBankClient();

        var webApplicationFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(webHostBuilder => webHostBuilder.ConfigureServices(services =>
            {
                services.RemoveAll<IBankClient>();
                services.AddSingleton<IBankClient>(mockBankClient);
            }));

        var idempotencyKey = Guid.NewGuid().ToString();

        var paymentRequest = new PostPaymentRequest
        {
            CardNumber = "4242424242424241",
            ExpiryMonth = "12",
            ExpiryYear = "2028",
            Currency = "GBP",
            Amount = 1050,
            Cvv = "123"
        };

        // act: send same request with same idempotency key but different merchants
        var httpClient1 = webApplicationFactory.CreateClient();
        httpClient1.DefaultRequestHeaders.Add("Merchant-Id", "merchant-1");
        httpClient1.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        var firstResponse = await httpClient1.PostAsJsonAsync("/api/payments", paymentRequest);
        var firstPaymentResult = await firstResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        var httpClient2 = webApplicationFactory.CreateClient();
        httpClient2.DefaultRequestHeaders.Add("Merchant-Id", "merchant-2");
        httpClient2.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        var secondResponse = await httpClient2.PostAsJsonAsync("/api/payments", paymentRequest);
        var secondPaymentResult = await secondResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // assert both processed independently
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotEqual(firstPaymentResult!.Id, secondPaymentResult!.Id);  // different payment IDs
        Assert.Equal(2, mockBankClient.Calls);  // bank called twice
    }

    [Fact]
    public async Task Post_GivenSameIdempotencyKeyDifferentPayloadDifferentMerchant_ThenProcessesIndependently()
    {
        // arrange
        var mockBankClient = new CountingBankClient();

        var webApplicationFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(webHostBuilder => webHostBuilder.ConfigureServices(services =>
            {
                services.RemoveAll<IBankClient>();
                services.AddSingleton<IBankClient>(mockBankClient);
            }));

        var idempotencyKey = Guid.NewGuid().ToString();

        var paymentRequest1 = new PostPaymentRequest
        {
            CardNumber = "4242424242424241",
            ExpiryMonth = "12",
            ExpiryYear = "2028",
            Currency = "GBP",
            Amount = 1050,
            Cvv = "123"
        };

        var paymentRequest2 = new PostPaymentRequest
        {
            CardNumber = "4242424242424241",
            ExpiryMonth = "12",
            ExpiryYear = "2028",
            Currency = "GBP",
            Amount = 2000,  // different amount
            Cvv = "123"
        };

        // act: send different requests with same idempotency key but different merchants
        var httpClient1 = webApplicationFactory.CreateClient();
        httpClient1.DefaultRequestHeaders.Add("Merchant-Id", "merchant-1");
        httpClient1.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        var firstResponse = await httpClient1.PostAsJsonAsync("/api/payments", paymentRequest1);
        var firstPaymentResult = await firstResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        var httpClient2 = webApplicationFactory.CreateClient();
        httpClient2.DefaultRequestHeaders.Add("Merchant-Id", "merchant-2");
        httpClient2.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        var secondResponse = await httpClient2.PostAsJsonAsync("/api/payments", paymentRequest2);
        var secondPaymentResult = await secondResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // assert both processed independently
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotEqual(firstPaymentResult!.Id, secondPaymentResult!.Id);  // different payment IDs
        Assert.Equal(2, mockBankClient.Calls);  // bank called twice
    }

    [Fact]
    public async Task Post_GivenMissingIdempotencyKey_Then400BadRequest()
    {
        // arrange
        var webApplicationFactory = new WebApplicationFactory<Program>();
        var httpClient = webApplicationFactory.CreateClient();

        httpClient.DefaultRequestHeaders.Add("Merchant-Id", "m-123");
        // intentionally not adding idempotency key

        var paymentRequest = new PostPaymentRequest
        {
            CardNumber = "4242424242424241",
            ExpiryMonth = "12",
            ExpiryYear = "2028",
            Currency = "GBP",
            Amount = 1050,
            Cvv = "123"
        };

        // act
        var response = await httpClient.PostAsJsonAsync("/api/payments", paymentRequest);

        // assert - idempotency key is required
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid Idempotency-Key", content);
    }

    [Fact]
    public async Task Post_GivenEmptyIdempotencyKey_Then400BadRequest()
    {
        // arrange
        var webApplicationFactory = new WebApplicationFactory<Program>();
        var httpClient = webApplicationFactory.CreateClient();

        httpClient.DefaultRequestHeaders.Add("Merchant-Id", "m-123");
        httpClient.DefaultRequestHeaders.Add("Idempotency-Key", "");  // empty string

        var paymentRequest = new PostPaymentRequest
        {
            CardNumber = "4242424242424241",
            ExpiryMonth = "12",
            ExpiryYear = "2028",
            Currency = "GBP",
            Amount = 1050,
            Cvv = "123"
        };

        // act
        var response = await httpClient.PostAsJsonAsync("/api/payments", paymentRequest);

        // assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid Idempotency-Key", content);
    }

    [Fact]
    public async Task Post_GivenInvalidIdempotencyKeyFormat_Then400BadRequest()
    {
        // arrange
        var webApplicationFactory = new WebApplicationFactory<Program>();
        var httpClient = webApplicationFactory.CreateClient();

        httpClient.DefaultRequestHeaders.Add("Merchant-Id", "m-123");
        httpClient.DefaultRequestHeaders.Add("Idempotency-Key", "invalid@key#with$special%chars");  // invalid format

        var paymentRequest = new PostPaymentRequest
        {
            CardNumber = "4242424242424241",
            ExpiryMonth = "12",
            ExpiryYear = "2028",
            Currency = "GBP",
            Amount = 1050,
            Cvv = "123"
        };

        // act
        var response = await httpClient.PostAsJsonAsync("/api/payments", paymentRequest);

        // assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid Idempotency-Key", content);
    }

    [Fact]
    public async Task Post_GivenSameIdempotencyKeyAfterBankFailure_ThenAllowsRetry()
    {
        // arrange - use a mock bank that fails on first call, succeeds on retry
        var failOnceBank = new FailOnceBankClient();

        var webApplicationFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(webHostBuilder => webHostBuilder.ConfigureServices(services =>
            {
                services.RemoveAll<IBankClient>();
                services.AddSingleton<IBankClient>(failOnceBank);
            }));

        var httpClient = webApplicationFactory.CreateClient();
        var testMerchantId = "m-123";
        var idempotencyKey = Guid.NewGuid().ToString();

        httpClient.DefaultRequestHeaders.Add("Merchant-Id", testMerchantId);
        httpClient.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);

        var paymentRequest = new PostPaymentRequest
        {
            CardNumber = "4242424242424241",  // valid card
            ExpiryMonth = "12",
            ExpiryYear = "2028",
            Currency = "GBP",
            Amount = 1050,
            Cvv = "123"
        };

        // act 1: first call fails due to bank unavailable
        var firstResponse = await httpClient.PostAsJsonAsync("/api/payments", paymentRequest);

        // assert first call returns 502 Bad Gateway (bank unavailable)
        Assert.Equal(HttpStatusCode.BadGateway, firstResponse.StatusCode);
        Assert.Equal(1, failOnceBank.Calls);

        // act 2: retry with same idempotency key and same request after bank failure
        var secondResponse = await httpClient.PostAsJsonAsync("/api/payments", paymentRequest);
        var secondPaymentResult = await secondResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // assert retry succeeds with same idempotency key
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotNull(secondPaymentResult);
        Assert.Equal(PaymentStatus.Authorized, secondPaymentResult!.Status);
        Assert.Equal(2, failOnceBank.Calls);  // bank called again for retry

        // act 3: third call with same key should replay successful response
        var thirdResponse = await httpClient.PostAsJsonAsync("/api/payments", paymentRequest);
        var thirdPaymentResult = await thirdResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // assert third call is a replay
        Assert.Equal(HttpStatusCode.OK, thirdResponse.StatusCode);
        Assert.Equal(secondPaymentResult.Id, thirdPaymentResult!.Id);  // same payment ID
        Assert.Equal(2, failOnceBank.Calls);  // bank NOT called again

        var hasReplayHeader = thirdResponse.Headers.TryGetValues("Idempotent-Replay", out var replayHeaderValues);
        Assert.Equal("true", hasReplayHeader ? replayHeaderValues.Single() : null);
    }
}