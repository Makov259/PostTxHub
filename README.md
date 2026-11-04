# PostTxHub

A post-transaction processing hub: the back-office part of a bank that receives
already-approved card payments, transfers and refunds from other systems and makes
sure each one is processed exactly once and never lost.

## Stack
ASP.NET Core (.NET 10) · RabbitMQ · PostgreSQL · EF Core · Docker Compose

## How it works
1. `POST /transactions` validates the transaction, gives it an ID and returns `202 Accepted`.
2. The API publishes it to the `transactions` exchange (persistent message, publisher confirms),
   which routes it to the `transactions.received` queue.
3. A separate **Worker** consumes the queue with manual acks.
4. The Worker saves the transaction to Postgres and **acks only after the commit**,
   so a message never leaves the queue before it's safely stored.

## Reliability (tested)
- **Postgres down:** the message is requeued and retried, then saved once the database is back.
- **Worker crash between save and ack:** the message is redelivered and recognized as a
  duplicate through its primary key, so it's never lost and never saved twice.
- **Broken messages** are rejected instead of blocking the queue.

## Run it
```bash
docker compose up -d
dotnet tool install --global dotnet-ef        # once
dotnet ef database update --project src/PostTxHub.Worker -- --environment Development
dotnet run --project src/PostTxHub.Worker
dotnet run --project src/PostTxHub.Api
```
Then send requests from `src/PostTxHub.Api/PostTxHub.Api.http`.
RabbitMQ dashboard: http://localhost:15672 (posttx / posttx_dev)

## Project structure
- `PostTxHub.Api`: HTTP endpoint, validation, publishing
- `PostTxHub.Worker`: consumer, EF Core, migrations
- `PostTxHub.Contracts`: the message shape shared by both apps
- `PostTxHub.Messaging`: shared RabbitMQ settings and topology

## Design decisions
- [ADR-001: Use a message broker](docs/adr/0001-use-message-broker.md)
- [ADR-002: RabbitMQ instead of Kafka](docs/adr/0002-rabbitmq-over-kafka.md)
- [ADR-003: Ack only after the database commit](docs/adr/0003-ack-after-commit.md)

## Roadmap
Idempotency on `ExternalId`, retries with backoff and a dead-letter queue,
transactional outbox, CI, integration tests.