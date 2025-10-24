We assume that it is okay to use a Singleton InMemory repository here for this demo app
We assume that we don't need to pass cancellation tokens to repository as we are using in memory db

Assume no Luhn check is required as it is not defined in requirements

We assume that the MerchantId passed in the headers is validated somehow and that the incoming requests are authenticated.
MerchantId might be stored against an API key provided with each request. 

Idempotency is implemented in memory - no need to use async or cancellation tokens. If implementing this solution in production 
we may instead use a distributed cache. I believe using idempotency key as unique index in a SQL table, and attempting an insert is an even better, 
truly idempotent solution. 