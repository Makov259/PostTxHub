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
5. Temporary failures wait in retry queues (1s, 5s, 30s) and come back. Anything that
   can't be processed ends up in `transactions.dead`, never silently dropped.

## Reliability (tested)
- **Worker crash between save and ack:** the message is redelivered and recognized as a
  duplicate through its primary key, so it's never lost and never saved twice.
- **Client retries:** a unique `ExternalId` means the same payment sent twice is saved once.
  Same `ExternalId` with different data is rejected as a conflict.
- **Postgres down:** the message retries after 1s, 5s and 30s without blocking the worker,
  and is saved as soon as the database is back.
- **Dead-letter queue:** broken messages, conflicts and messages out of retries land in
  `transactions.dead` for a human to inspect.

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
- [ADR-004: Catch client duplicates with a unique ExternalId](docs/adr/0004-idempotency-external-id.md)
- [ADR-005: Retry with growing delays, then a dead-letter queue](docs/adr/0005-retries-and-dead-letter-queue.md)

## Roadmap
Transactional outbox, routing by transaction type, CI, integration tests.