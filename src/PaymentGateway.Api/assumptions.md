# Assumptions

## Data Storage

For this implementation, I've used an in-memory singleton repository to store payment data. This approach is sufficient for demonstrating the core functionality of the payment gateway. In a production environment, this would be replaced with a persistent database solution. Since we're using in-memory storage, I haven't implemented cancellation tokens for repository operations as they would add unnecessary complexity for this demo.

## Card Validation

The requirements don't specify implementing Luhn algorithm validation for card numbers. Given that this is a gateway that forwards requests to a bank simulator, I've assumed the bank would handle comprehensive card validation. The API validates the basic format (16 digits, numeric only) but doesn't verify the card number's mathematical validity.

## Authentication and Authorization

I've assumed that merchant authentication happens at a layer above this API. In a real implementation, the Merchant-Id header would be validated against an authentication system, potentially tied to API keys or OAuth tokens. For this demo, any Merchant-Id value is accepted, but the system maintains proper isolation between different merchants' data.

## Idempotency Implementation

The idempotency mechanism is implemented using in-memory storage for simplicity. This means idempotency keys are lost on application restart, which is acceptable for a demo but wouldn't be suitable for production. In a real-world scenario, I would recommend either:

1. Using a distributed cache like Redis for idempotency key storage
2. Implementing idempotency keys as unique constraints in a database table, which provides truly atomic idempotent guarantees through database-level conflict detection

The current implementation correctly handles concurrent requests and payload validation, demonstrating the core concepts even though the storage isn't persistent. 

In a production system idempotency keys may have a strongly enforced format but for ease of testing we accept and normalize strings under 64 chars.