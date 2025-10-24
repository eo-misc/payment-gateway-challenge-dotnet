using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Persistence;
using PaymentGateway.Api.Tests;

namespace PaymentGateway.Api.Tests;

public abstract class ApiTestsBase : IDisposable
{
    protected readonly ApiFactory Factory;
    protected readonly HttpClient Client;

    protected ApiTestsBase()
    {
        Factory = new ApiFactory();
        Client = Factory.CreateClient();
    }

    protected HttpClient CreateClient(string merchantId)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Merchant-Id", merchantId);
        return client;
    }
    
    protected Payment? GetPayment(string merchantId, Guid paymentId)
    {
        using var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IPaymentsRepository>().Get(merchantId, paymentId);
    }

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
    }
}