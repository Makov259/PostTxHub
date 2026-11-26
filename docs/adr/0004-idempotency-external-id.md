# ADR-004: Catch client duplicates with a unique ExternalId

## Status
Accepted

## Context
Clients are other systems, and they retry. If a client sends a payment, the network
drops our `202`, and the client sends it again, the API stamps a **new** `TransactionId`
on the second request. ADR-003 only catches broker redeliveries (same `TransactionId`),
so the same payment would be saved twice.

Two IDs, two jobs:
- `ExternalId`: the client's name for the **payment**. Same on every retry.
- `TransactionId`: our name for one **arrival** at the API. New every time.

Only the client knows that two requests are the same payment, so the client's ID is
the idempotency key.

## Options considered
1. **Generate the ID on the server.** Every retry gets a new ID, so retries look like
   new payments. This is the problem, not a fix.
2. **SELECT first, insert if not found.** Two workers can check at the same moment,
   both see nothing, and both insert. A race condition.
3. **Unique index on `ExternalId` and react to the violation.** The database is the
   single referee. Two inserts can never both win.

## Decision
Option 3.
- `HasIndex(t => t.ExternalId).IsUnique()`, applied with the `AddUniqueExternalId` migration.
- Both kinds of duplicate raise Postgres error 23505. The worker tells them apart by
  `ConstraintName`:
  - `PK_Transactions`: broker redelivery, same message. Ack.
  - `IX_Transactions_ExternalId`: client duplicate. Load the saved row and compare
    Type, Amount, Currency and AccountId.
    - **Same data:** a harmless retry. Info log, ack.
    - **Different data:** a conflict (one receipt, two amounts). Error log, reject.
      The message lands in the dead-letter queue (see ADR-005). The first saved
      payment is never overwritten.

**Verified by tests:**
- Same request sent twice: `202` both times, one row, "Client retry" logged.
- Same `ExternalId`, different amount: "Conflict" logged, message rejected, the
  original row unchanged, queue back to 0.

## Consequences
**Good:**
- One payment, one row, no matter how many times the client retries.
- Conflicts are visible (error log, DLQ) instead of silently dropped.

**Bad / trade-offs:**
- The API answers `202` before the worker checks, so a client is **never told**
  about a conflict, and receives a `TransactionId` that will never exist in the table.
  Real payment APIs (e.g. Stripe's `Idempotency-Key`) check at the door and return
  the original response.
- The key is unique across all clients. A real system would scope it per client.
- Keys never expire.
