# Payment Gateway API

A payment gateway API implementation that processes card payments with merchant isolation and idempotency support.

> See [Assumptions](src/PaymentGateway.Api/assumptions.md) for implementation decisions and trade-offs.

## API Endpoints

### POST /api/payments

Creates a new payment transaction.

**Required Headers:**
- `Merchant-Id`: Merchant identifier
- `Idempotency-Key`: Unique key for idempotent requests

**Request Body:**
```json
{
  "cardNumber": "4242424242428111",
  "expiryMonth": "12",
  "expiryYear": "2025",
  "currency": "GBP",
  "amount": 1050,
  "cvv": "123"
}
```

**Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Authorized",
  "cardNumberLastFour": "8111",
  "expiryMonth": "12",
  "expiryYear": "2025",
  "currency": "GBP",
  "amount": 1050
}
```

### GET /api/payments/{paymentId}

Retrieves a payment by ID.

**Required Headers:**
- `Merchant-Id`: Merchant identifier

**Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Authorized",
  "cardNumberLastFour": "8111",
  "expiryMonth": "12",
  "expiryYear": "2025",
  "currency": "GBP",
  "amount": 1050
}
```

## Status Codes

- **200 OK**: Successful request (returns for both Authorized and Declined payments)
- **400 Bad Request**: Validation error
- **404 Not Found**: Payment not found or belongs to different merchant
- **409 Conflict**: Idempotency conflict
- **502 Bad Gateway**: Bank service unavailable

## Payment Statuses

- **Authorized**: Payment approved by bank
- **Declined**: Payment rejected by bank
- **Rejected**: Payment failed validation

## Validation Rules

### Card Number
- Must be exactly 16 digits
- Numeric only, no spaces or dashes

### Expiry Month
- Must be 2 digits (01-12)

### Expiry Year
- Must be 4 digits
- Range: 2000-2099
- Card must not be expired

### Currency
- Must be exactly 3 uppercase letters
- Supported: GBP, USD, EUR

### Amount
- Must be positive integer (greater than 0)

### CVV
- Must be exactly 3 digits

## Idempotency

The API prevents duplicate payment processing through idempotency keys.

- Same key + same request data = returns cached response with `Idempotent-Replay: true` header
- Same key + different request data = returns 409 Conflict
- Concurrent requests with same key = second request returns 409 Conflict
- Failed requests can be retried with same key
- Idempotency keys are scoped per merchant

## Test Card Numbers

The following card endings produce specific behaviors:

| Card Ending | Result |
|------------|--------|
| ...8111 | Authorized |
| ...8112 | Declined |
| ...8888 | Declined |
| ...8880 | Bank unavailable (502) |

## Example Requests

### Create Payment (Authorized)
```bash
curl -X POST http://localhost:5000/api/payments \
  -H "Merchant-Id: merchant-123" \
  -H "Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000" \
  -H "Content-Type: application/json" \
  -d '{
    "cardNumber": "4242424242428111",
    "expiryMonth": "06",
    "expiryYear": "2026",
    "currency": "USD",
    "amount": 2500,
    "cvv": "456"
  }'
```

### Create Payment (Declined)
```bash
curl -X POST http://localhost:5000/api/payments \
  -H "Merchant-Id: merchant-456" \
  -H "Idempotency-Key: 660e8400-e29b-41d4-a716-446655440001" \
  -H "Content-Type: application/json" \
  -d '{
    "cardNumber": "4242424242428112",
    "expiryMonth": "03",
    "expiryYear": "2025",
    "currency": "EUR",
    "amount": 999,
    "cvv": "789"
  }'
```

### Get Payment
```bash
curl -X GET http://localhost:5000/api/payments/7d6f8e9a-1234-5678-9abc-def012345678 \
  -H "Merchant-Id: merchant-123"
```

## Error Response Examples

### Validation Error (400)
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "CardNumber": [
      "Card number must be exactly 16 digits"
    ]
  }
}
```

### Idempotency Conflict (409)
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.8",
  "title": "Conflict",
  "status": 409,
  "detail": "A different request was already processed with this idempotency key"
}
```

## Implementation Notes

- Card numbers are masked in responses (only last 4 digits shown)
- Payments are persisted for both authorized and declined transactions
- Merchants can only retrieve their own payments
- Card expiry validation checks if card is expired at time of request
- Idempotency prevents duplicate bank calls for same request