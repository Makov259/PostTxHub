# PostTxHub

A post-transaction processing hub: the back-office part of a bank that receives
already-approved card payments, transfers and refunds from other systems and makes
sure each one is processed exactly once and never lost.

## Stack
ASP.NET Core (.NET 10) · RabbitMQ · PostgreSQL · Docker Compose

## How it works (so far)
1. `POST /transactions` validates the transaction and returns `202 Accepted`.
2. The API publishes it to RabbitMQ (persistent message, publisher confirms).
3. It waits in the `transactions.received` queue for the worker (in progress).

## Run it
```bash
docker compose up -d
dotnet run --project src/PostTxHub.Api
```
Then send requests from `src/PostTxHub.Api/PostTxHub.Api.http`.
RabbitMQ dashboard: http://localhost:15672 (posttx / posttx_dev)

## Design decisions
- [ADR-001: Use a message broker](docs/adr/0001-use-message-broker.md)
- [ADR-002: RabbitMQ instead of Kafka](docs/adr/0002-rabbitmq-over-kafka.md)