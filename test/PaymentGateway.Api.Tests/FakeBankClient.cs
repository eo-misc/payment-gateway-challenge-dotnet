using System.Net;

using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

internal sealed class CountingBankClient : IBankClient
{
    public int Calls => _calls;
    private int _calls;
    
    public Task<BankPaymentResponse> ProcessPaymentAsync(BankPaymentRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);

        //simulate a 400 response (where our application supplies bad details to the bank client)
        // a cvv of 999 triggers this
        if (request.Cvv == "999")
        {
            throw new HttpRequestException("Bad request simulated", null, HttpStatusCode.BadRequest);
        }
        //simulate the behaviour of mountebank
        var lastChar = request.CardNumber[^1];
        if (lastChar == '0')
        {
            throw new HttpRequestException("Bank unavailable", null, HttpStatusCode.ServiceUnavailable);
        }
        bool isAuthorized = new HashSet<Char>(){'1','3','5','7','9'}.Contains(lastChar);
        return Task.FromResult(new BankPaymentResponse()
        {
            Authorized = isAuthorized,
            AuthorizationCode = Guid.NewGuid().ToString(),
        });
    }
}

internal sealed class SlowBankClient : IBankClient
{
    private readonly int _delayMs;
    public int Calls => _calls;
    private int _calls;

    public SlowBankClient(int delayMs)
    {
        _delayMs = delayMs;
    }

    public async Task<BankPaymentResponse> ProcessPaymentAsync(BankPaymentRequest request, CancellationToken cancellationToken)
    {
        // simulate slow processing
        await Task.Delay(_delayMs, cancellationToken);

        Interlocked.Increment(ref _calls);

        // follow same logic as CountingBankClient
        var lastChar = request.CardNumber[^1];
        if (lastChar == '0')
        {
            throw new HttpRequestException("Bank unavailable", null, HttpStatusCode.ServiceUnavailable);
        }
        bool isAuthorized = new HashSet<char>(){'1','3','5','7','9'}.Contains(lastChar);

        return new BankPaymentResponse
        {
            Authorized = isAuthorized,
            AuthorizationCode = Guid.NewGuid().ToString()
        };
    }
}

internal sealed class FailOnceBankClient : IBankClient
{
    public int Calls => _calls;
    private int _calls;

    public Task<BankPaymentResponse> ProcessPaymentAsync(BankPaymentRequest request, CancellationToken cancellationToken)
    {
        var currentCall = Interlocked.Increment(ref _calls);

        // fail on first call only
        if (currentCall == 1)
        {
            throw new HttpRequestException("Bank unavailable", null, HttpStatusCode.ServiceUnavailable);
        }

        // succeed on subsequent calls
        return Task.FromResult(new BankPaymentResponse
        {
            Authorized = true,
            AuthorizationCode = Guid.NewGuid().ToString()
        });
    }
}