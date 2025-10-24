using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Persistence;

namespace PaymentGateway.Api.Tests;

public abstract class ApiTestsBase : IDisposable
{
    protected readonly ApiFactory Factory;
    protected readonly FakeTimeProvider TimeProvider;
    protected readonly HttpClient Client;

    protected ApiTestsBase()
    {
        Factory = new ApiFactory();
        TimeProvider = Factory.TimeProvider;
        Client = Factory.CreateClient();
    }

    protected HttpClient CreateClient(string merchantId)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Merchant-Id", merchantId);
        return client;
    }

    protected void SeedPayment(Payment payment)
    {
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IPaymentsRepository>().Upsert(payment);
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