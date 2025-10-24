using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace PaymentGateway.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public FakeTimeProvider TimeProvider { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<TimeProvider>(TimeProvider);
        });

        base.ConfigureWebHost(builder);
    }
}