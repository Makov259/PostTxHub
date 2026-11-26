# ADR-005: Retry with growing delays, then a dead-letter queue

## Status
Accepted

## Context
In ADR-003, a temporary failure (Postgres down, timeout) made the worker wait 2 seconds
**while holding the message**, then requeue it. Problems:
- The worker sleeps, and every other message waits behind it.
- No limit: a message could retry forever.
- The message has no memory of how many times it was tried.
- The delay never grows, so a long outage gets hammered every 2 seconds.

And permanent failures (broken JSON, conflicts, unknown bugs) were simply dropped.

## Options considered
1. **Sleep in the worker** (the old way). Blocks the consumer. Rejected.
2. **Delayed-message plugin.** Works, but it's an extra plugin to install and run.
3. **Waiting queues built from standard RabbitMQ features:** a per-queue timer (TTL)
   plus a forwarding address (dead-letter exchange). No plugins.

## Decision
Option 3.

**Topology:**
| Queue | Timer | Forwards to |
|---|---|---|
| `transactions.retry.1s` | 1s | `transactions.received` |
| `transactions.retry.5s` | 5s | `transactions.received` |
| `transactions.retry.30s` | 30s | `transactions.received` |
| `transactions.received` | none | `transactions.dead` (on reject) |
| `transactions.dead` | none | nobody, kept for a human |

The retry queues and the DLQ are not bound to the `transactions` exchange, so the
API can never reach them. They are only reached through the default exchange.

**Temporary failure:**
1. Read the attempt count from the `retry-count` header (missing = 0).
2. If attempts remain, publish a copy to the matching waiting queue with
   `retry-count + 1`, then ack the original. The worker never sleeps.
3. When the timer runs out, RabbitMQ moves the copy back to the main queue.
4. After 3 retries, reject the message (nack, no requeue).

**Any reject** (broken message, conflict, out of retries, unknown error) is moved by
the main queue's own forwarding address to `transactions.dead`, with an `x-death`
header recording where and why. No consumer code publishes to the DLQ.

**Publish the copy first, ack second.** A crash in between leaves two copies with
the same `TransactionId`; the second one is caught by the primary key (ADR-003).
The other order could lose the message.

**Verified by tests:**
- Postgres stopped: attempts 1s, 5s, 30s, then "Out of retries", message in the DLQ.
- Postgres restarted during the 30s wait: the message was saved on the next try.
- A non-JSON message published by hand: "Unreadable message", message in the DLQ.

## Consequences
**Good:**
- Nothing is silently lost. Every message is either saved or sitting in the DLQ.
- The worker keeps processing other messages while one waits.
- Delays grow (1s, 5s, 30s), giving a broken dependency room to recover.
- Adding a retry step is one new queue and one entry in `RetryQueues`.

**Bad / trade-offs:**
- Fixed delays per queue. A different delay means a new queue.
- The retry copy is published without publisher confirms on the consumer channel.
- Nobody consumes or alerts on the DLQ yet. Replay is manual (RabbitMQ dashboard,
  "Move messages").
- If Postgres goes down exactly while the conflict check runs, that error escapes
  the catch block and the message stays unacked until the channel closes.
- Changing a queue's arguments requires deleting and re-declaring the queue.
