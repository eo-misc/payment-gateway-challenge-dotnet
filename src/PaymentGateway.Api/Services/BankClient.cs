using System.Net.Http.Json;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public class BankClient : IBankClient
{
    private readonly HttpClient _http;
    public BankClient(HttpClient http) => _http = http;

    public async Task<BankPaymentResponse> ProcessPaymentAsync(
        BankPaymentRequest request,
        CancellationToken cancellationToken)
    {
        using var resp = await _http.PostAsJsonAsync("/payments", request, cancellationToken);
        resp.EnsureSuccessStatusCode();

        var payload = await resp.Content.ReadFromJsonAsync<BankPaymentResponse>(cancellationToken);
        if (payload is null) throw new InvalidOperationException("Empty 200 from bank.");
        return payload;
    }
}