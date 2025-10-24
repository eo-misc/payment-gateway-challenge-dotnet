namespace PaymentGateway.Api.Domain;

public static class Currency
{
    public static readonly HashSet<string> AcceptedCurrencies = new() { "GBP", "USD", "EUR" };
}
