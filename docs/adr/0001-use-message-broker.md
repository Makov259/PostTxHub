# ADR-001: Use a message broker between the API and processing

**Status:** Accepted
**Date:** 2026-10-27

## Context
The API receives financial transactions over HTTP. If we validate, process and save
each one inside the request:
- When the database is slow or down, the request fails and the transaction is lost
  unless the client retries.
- A traffic spike (e.g. 100,000 transactions in a minute) hits the database directly
  and can overload it.
- The client waits for all processing to finish before getting a response.

We need the API to accept transactions quickly and safely, even when processing is
slow or temporarily unavailable.

## Options considered
1. **Process inside the HTTP request.** Simplest. But the API is only as available as
   the database, and spikes go straight to it.
2. **Database table as a queue.** The API inserts rows, a worker polls them. Durable,
   but polling adds load and delay, and it's custom queueing logic we'd have to maintain.
3. **In-memory queue in the API.** Fast and simple, but everything in it is lost if the
   API crashes or restarts, and it can't be shared across multiple API instances.
4. **Message broker.** The API publishes a persistent message and returns 202; a
   separate worker consumes and processes it.

## Decision
Use a message broker (option 4). The API only validates and publishes; a worker does
the processing. Which broker is decided in ADR-002.

## Consequences
**Good:**
- The API responds fast (202) no matter how slow processing is.
- Spikes are absorbed by the queue instead of overloading the database.
- If the worker or database is down, messages wait safely in the queue until it recovers.
- API and worker can be scaled independently.

**Bad / trade-offs:**
- One more system to run, monitor and secure.
- Eventual consistency: 202 means "accepted", not "done". The client needs another
  way to learn the final status.
- Brokers deliver at-least-once, so the same message can arrive twice. Consumers must
  be idempotent (ADR in week 3).
- End-to-end latency per transaction is slightly higher, and debugging spans multiple
  services (solved later with correlation IDs).