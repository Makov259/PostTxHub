# ADR-002: Use RabbitMQ instead of Kafka

**Status:** Accepted
**Date:** 2026-10-29

## Context
ADR-001 decided we need a message broker. The two most common choices are
RabbitMQ and Kafka. They look similar from far away but work very differently:

- **RabbitMQ is a mailbox.** A message goes into a queue, one worker takes it
  out, confirms it (ack), and it's gone. The broker is "smart": it routes
  messages using exchanges and bindings, tracks which messages are acknowledged,
  and has built-in support for retries and dead-letter queues.
- **Kafka is a logbook.** Every message is written to the end of a log and
  kept for days or weeks, even after it's read. Each reader keeps its own
  bookmark (offset) and can re-read history. The broker is "simple"; consumers
  do more of the work themselves.

What PostTxHub needs:
1. Each transaction processed **once, by one worker**, then done.
2. **Per-message acknowledgement**: a message only leaves the queue after it's saved.
3. **Retries and a dead-letter queue** for messages that keep failing (week 3).
4. **Routing by type**: card, transfer, refund to different handlers (week 6).
5. Moderate volume, and **simple to run** for a single developer (one container).

## Options considered
1. **RabbitMQ.** Matches all five needs out of the box. One container, a web
   dashboard, a mature official .NET client. No message history after ack.
2. **Kafka.** Excellent for very high throughput, replaying history, and many
   independent systems reading the same stream. But per-message ack, retries and
   DLQs must be built by hand, routing is more limited, and running it is heavier.

## Decision
Use RabbitMQ. Our workload is "a list of jobs, each done once", which is exactly
what a queue-based broker is built for.

## Consequences
**Good:**
- Ack, retries, DLQ and routing come built in, so less custom code.
- Easy to run locally and to inspect through the management dashboard.
- Very common in .NET and banking job ads.

**Bad / trade-offs:**
- **No replay.** Once a message is acked it's gone from the broker. Postgres
  is our source of truth, not the broker.
- If many other systems (fraud, analytics, statements) later need to read the
  same stream independently, Kafka would fit better. RabbitMQ Streams or a
  move to Kafka would be the path then.
- Lower maximum throughput than Kafka. That's fine for our volume.

**When I would choose Kafka instead:** very high volume, the need to replay past
events, or several independent consumers each needing the full history.