using PaymentGateway.Api.Middlewares;
using PaymentGateway.Api.Persistence;
using PaymentGateway.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddExceptionHandler<ExceptionHandlingMiddleware>();
builder.Services.AddProblemDetails();

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddSingleton<IPaymentsRepository,  PaymentsRepository>();
builder.Services.AddSingleton<IIdempotencyStore, IdempotencyStore>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IPaymentRequestValidator, PaymentRequestValidator>();

builder.Services.AddHttpClient<IBankClient, BankClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:8080");
});
// resilience - uncomment for real 
// .AddStandardResilienceHandler(options =>
// {
//     // demo purposes
//     options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
//     options.AttemptTimeout.Timeout = TimeSpan.FromMilliseconds(300);
//     // Standard resilience handler will retry for 5XX errors
//     options.Retry.MaxRetryAttempts = 2; 
//     options.Retry.Delay = TimeSpan.FromMilliseconds(200); // Small initial delay for demo
// });

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

namespace PaymentGateway.Api
{
    public partial class Program { }
}