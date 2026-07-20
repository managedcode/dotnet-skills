---
name: orleans
description: "Design Microsoft Orleans systems from each primitive's purpose and failure model. USE FOR: grains, digital twins, state versus databases, transactions, messaging, streams, timers, reminders, Durable Jobs, stateless workers, grain services, startup, and hosting. DO NOT USE FOR: other actor stacks, batches, relational-only CRUD, or advice without an Orleans decision. INVOKES: inspect version and topology, choose the primitive, implement, and validate."
---

# Microsoft Orleans

## Start With Purpose

Do not begin with an Orleans API. First state:

1. the business identity that owns the behavior;
2. the invariant and what must survive activation or cluster failure;
3. the required consistency, query shape, acknowledgement, durability, replay, fan-out, and timing.

Then select the smallest Orleans primitive whose guarantees match those requirements. Reject Orleans when the problem is primarily shared-memory computation, a finite batch, relational querying, or global coordination with few independent entities.

Inspect package versions for version-sensitive work. Orleans `10.2.1` ships `Microsoft.Orleans.DurableJobs*` and `Microsoft.Orleans.Journaling*` as `10.2.1-alpha.1`; treat them as experimental until that status changes.

## Mental Model

- A **grain** is a virtual actor: a logical entity with stable identity, behavior, and optional state. It is not a process, row, DTO, controller, or background job.
- A **grain reference** is a location-transparent address. Getting a reference does not create a durable record or prove that an activation exists.
- An **activation** is an ephemeral in-memory execution instance. Orleans creates, places, moves, deactivates, and recreates it. Never equate activation lifetime with entity lifetime.
- A normal grain has at most one activation in the cluster by default and processes turns one at a time. This makes the grain a natural owner of per-identity invariants.
- A **silo** hosts activations. Silos form a cluster; external clients or a co-hosted `IGrainFactory` call grains.
- Calls are asynchronous messages even when they look like C# method calls. Network failure, timeout, serialization, retries, and duplicate side effects still matter.
- A grain can model a **digital twin** when its identity and behavior correspond to a device, user, order, room, account, or other real/domain entity. Digital twin is a use case, not the definition of every grain.
- Orleans gives logical ownership and turn-based execution. It does not make external side effects transactional, turn arbitrary data into a queryable database, or provide exactly-once execution by default.

Model a grain as a tiny, always-addressable service per business identity:

```text
identity -> serialized decisions -> bounded current state -> messages/events/work
```

## Workflow

1. Inspect the solution, Orleans version, hosting topology, grain interfaces, providers, and tests.
2. Identify domain identities and invariants. Prefer many independent, bounded entities over global coordinator grains.
3. Choose state, communication, and time-based work from the tables below.
4. Keep default non-reentrant scheduling and placement until a measured requirement justifies a change.
5. Configure providers independently and test the failure model the design depends on.

## Choose the State Owner

| Primitive | Purpose | Choose it when | Do not use it as |
|---|---|---|---|
| Activation fields | Fast, temporary state for one activation | The value is derived, cached, disposable, or safe to rebuild after deactivation/failure | Durable truth |
| `IPersistentState<T>` | Durable current state owned by one grain identity | The grain needs a bounded snapshot loaded on activation and explicitly written after commands | A general query database or cross-grain table |
| External database/repository | Queryable, indexed, relational, bulk, shared, or externally owned data | The system needs joins, search, reporting, set-based updates, independent access, or an existing system of record | A replacement for grain ownership when serialized per-entity decisions are still required |
| Grain plus database/read model | Separate command ownership from query/storage concerns | A grain owns invariants and a small control snapshot while a database owns large records, history, projections, or reporting | Two competing sources of truth without an explicit contract |
| `JournaledGrain<TState,TEvent>` | Persist domain events and reconstruct state | Audit history, business-event replay, log consistency, or multi-cluster event-sourced replication is a requirement | A default persistence choice for ordinary CRUD state |
| `Orleans.Journaling` durable states | Replay durable collection/value operations through a journal | The experimental 10.2 journaling model, durable collections, or durable completion state solves a measured need | Stable default persistence; it is distinct from `JournaledGrain` business event sourcing |
| `ITransactionalState<T>` | ACID, serializable all-or-nothing changes across transactional grain state | A short operation must atomically update multiple grain-owned states and compensation is unacceptable | Long-running workflows or atomicity with arbitrary external systems |
| Saga/process manager | Durable progress with compensation across steps and external systems | Work is long-running, spans services, waits for events, or cannot share one transaction | Instant atomic commit |

### Grain State Versus a Database

Use grain state for bounded current state and invariants owned and accessed by one identity. Use a database/read model for joins, search, reports, scans, bulk updates, large/history data, shared access, or an external system of record. Use both when a grain owns commands while the database owns query shape, but define one authority per field and recovery rules.

Never query or mutate another grain's persistence record behind the grain, or expose provider storage as the public query model merely because it uses SQL/Cosmos/Redis. Before using transactions, try one bounded grain owner; use a saga for external effects or long waits. Read [references/persistence-api.md](references/persistence-api.md) for the full boundary.

## Choose Communication

| Primitive | Purpose | Choose it when | Failure/ownership model |
|---|---|---|---|
| Direct grain call | Typed request/response to a known logical owner | The caller needs completion, a result, or an exception | At-most-once by default; a timeout does not prove whether the target committed a side effect |
| `IAsyncEnumerable<T>` | Stream a response within one caller-initiated request | One caller consumes progressive results with cancellation and backpressure | Call-scoped, not pub/sub, not a durable subscription |
| Orleans stream | Decouple producers and consumers across dynamic, long-lived event flows | Pub/sub, multiple consumers, provider-backed queues, replay, or durable subscriptions are needed | Delivery, ordering, replay, and backpressure depend on the selected provider |
| Broadcast channel | Transient, low-overhead fan-out to implicit grain subscribers | All interested grains need the latest signal and occasional loss/history gaps are acceptable | Best-effort, no message storage, no replay; not a persistent stream |
| Observer | Push callbacks to a connected client or addressable subscriber | A UI/client needs live notifications and can resubscribe after reconnect | Ephemeral and inherently unreliable for clients; expire, unsubscribe, and delete references |
| One-way request | Remove the response path for a specialized lossy notification | The sender needs no result, completion, or error and measured response overhead matters | No acknowledgement and no guarantee the callee received the request |
| `RequestContext` | Flow small request-scoped metadata | Trace, correlation, tenant, or request metadata must follow a call chain | Not durable state; metadata does not flow back in responses |
| Grain call filter | Apply cross-cutting behavior around calls | Authorization, telemetry, argument/result inspection, or exception conversion spans many grains | Keep domain decisions in grains, not global filters |
| Grain extension | Attach a runtime/protocol capability to an addressable grain | Infrastructure needs an additional interface without changing the domain interface | Advanced runtime mechanism; avoid as ordinary domain composition |

Prefer a direct call unless decoupling is a requirement. Do not use a stream merely to avoid calling a known target. Do not use broadcast channels for commands, money movement, audit events, or anything that must be replayed. Do not use observers as a durable event bus.

When adding retries, make side effects idempotent. Default Orleans calls are at-most-once only while neither the runtime nor application retries. Retried calls can arrive more than once, and Orleans does not durably deduplicate them for the application.

## Choose Time-Based and Background Work

| Primitive | Purpose | Choose it when | Do not choose it when |
|---|---|---|---|
| `RegisterGrainTimer` | Periodic or one-shot work tied to the current activation | Work is frequent, local to an active grain, and safe to stop on deactivation or silo failure | The schedule must survive reactivation/restart |
| Reminder | Durable recurring schedule definition associated with a grain identity | Low-frequency recurring work must wake the grain after deactivation or cluster restart | Every missed occurrence must be replayed, timing must be precise, or work is high-frequency |
| Durable Job | Persistent one-time future delivery to a target grain with cancellation/retry metadata | A delayed command, expiry, notification, or workflow step must execute at least once around a due time | Recurring work, exactly-once side effects, or production work that cannot accept the current alpha package status |
| `[StatelessWorker]` | Auto-scaled pool of local stateless grain activations | CPU/transform/routing/pre-aggregation work is not tied to one durable entity | Scheduling or durability; a stateless worker is not a job system |
| `BackgroundService` | Continuous loop owned by each host process | A silo/web host must poll or consume an external source and forward work into grains | One global loop across replicas unless duplicates are safe or externally coordinated |
| `IHostedService` | Host startup/shutdown action or simpler background component | Initialization or bounded host-lifetime work belongs to standard .NET hosting | Per-entity durable work |
| Orleans startup task | Fail-fast hook at a specific silo startup stage | Legacy/framework integration truly requires Orleans lifecycle ordering | General background work; prefer `BackgroundService` or `IHostedService` |
| Silo lifecycle participant | Ordered initialization/shutdown of an Orleans component | A provider or runtime service must start at an exact lifecycle stage | Business scheduling |
| Grain service | Per-silo, cluster-partitioned runtime support service | Every silo hosts a long-lived service and responsibility for grains must be partitioned across silos | An ordinary domain entity, a singleton, or a durable job queue |
| External scheduler/workflow engine | Scheduling/orchestration outside Orleans | Cross-system workflows, cron calendars, human steps, broad operational control, or mature production guarantees dominate | Per-grain work already solved by a stable Orleans primitive |

### Timer, Reminder, or Durable Job

Timer means “while this activation lives”; reminder means “wake this grain on a durable recurring schedule”; Durable Job means “invoke this target once around a future time, at least once.” Reminders persist definitions but miss ticks while the cluster is down. Durable Job handlers must be idempotent, and current `alpha.1` packages are an explicit architecture risk.

`BackgroundService` means one loop per host replica, not one loop per cluster. Use a well-known grain or external lease/leader for one logical collector.

Read [references/scheduling-and-services.md](references/scheduling-and-services.md) before implementing timers, reminders, Durable Jobs, hosted/startup tasks, silo lifecycle participants, or grain services.

## Choose Execution and Scaling

| Primitive | Purpose | Decision rule |
|---|---|---|
| Standard grain | Serialize behavior for one identity | Default for stateful domain entities and digital twins |
| `[StatelessWorker]` | Scale fungible work locally and across silos | Use when activations are interchangeable and their local state need not agree |
| `[ReadOnly]` | Permit compatible reads to interleave | Use only when the method cannot mutate grain state or external invariants |
| `[Reentrant]` | Allow turns from other calls while the grain awaits | Use for call cycles or measured concurrency needs after auditing every invariant |
| `[AlwaysInterleave]` / `[MayInterleave]` | Selectively admit interleaving | Prefer narrow scheduling exceptions over making the whole grain reentrant |
| Placement strategy/filter | Constrain or optimize activation location | Keep the resource-optimized default unless locality, hardware, zone, compliance, or role requirements are proven |
| Heterogeneous silo/versioning | Run different grain sets or versions during rollout | Use explicit compatibility/version selection for safe rolling deployments |

Turn-based execution is single-threaded, not magically race-free. Reentrancy allows another turn to run while the first awaits; any state observed before the `await` can be stale afterward. Avoid blocking calls, `.Result`, `.Wait()`, thread-affine work, and unbounded CPU loops on the grain scheduler.

## Hosting and Provider Boundaries

Keep concerns separate: clustering discovers silos; the grain directory locates activations; grain/reminder/transaction/job storage persist different records; stream providers carry events while `PubSubStore` tracks subscriptions; serialization defines wire and persistence compatibility.

In-memory clustering, storage, reminders, streams, Durable Jobs, and journaling are development/test choices unless loss is explicitly acceptable. Configure production providers, credentials, TLS/networking, server GC, graceful shutdown, and health/readiness for the deployment target. In Aspire, declare backing resources in AppHost and register the keyed clients expected by Orleans providers.

## Contract and API Rules

- Keep grain interfaces coarse-grained and asynchronous: `Task`, `Task<T>`, `ValueTask<T>`, or supported `IAsyncEnumerable<T>`.
- Cancellation is cooperative, not proof that work did not finish.
- Use `[GenerateSerializer]` and stable `[Id(N)]` values on messages/state. Use `[Alias]` for durable type identity and `[Immutable]` only for genuinely immutable values.
- Never reuse removed field IDs; write state explicitly and propagate storage failures.
- Bound external calls and use idempotency plus a saga/outbox when state writes and external effects must be reconciled.

## Validate the Chosen Guarantees

- Grain boundaries follow business identity and avoid hot global coordinators.
- State is bounded, and the grain-state-versus-database choice is explicit.
- Every time primitive matches activation, recurrence, durability, and delivery requirements.
- Durable Job handlers/retried commands are idempotent; reminders tolerate missed ticks.
- Broadcast channels, observers, and one-way calls carry only loss-tolerant signals.
- Provider-backed tests prove delivery, ordering, replay, and failover claims.
- Reentrancy tests cover state across `await`; transactions use transactional storage, `[Reentrant]`, and `PerformRead`/`PerformUpdate`.
- Hosted services are reviewed for per-replica duplication.
- Production uses persistent providers, multi-silo tests, and observability wherever the guarantee depends on them.

## Load References

Open only the references needed for the selected primitive:

- [references/official-docs-index.md](references/official-docs-index.md) — official documentation map
- [references/scheduling-and-services.md](references/scheduling-and-services.md) — scheduled/background/runtime work
- [references/grains.md](references/grains.md) and [references/grain-api.md](references/grain-api.md) — grain design and APIs
- [references/persistence-api.md](references/persistence-api.md) — state, databases, Journaling, event sourcing, transactions
- [references/streaming-api.md](references/streaming-api.md) — streams, broadcast, observers, response streams
- [references/serialization-api.md](references/serialization-api.md) — serialization and versioning
- [references/hosting.md](references/hosting.md) and [references/configuration-api.md](references/configuration-api.md) — hosting, providers, operations
- [references/implementation.md](references/implementation.md) and [references/testing-patterns.md](references/testing-patterns.md) — runtime and tests
- [references/patterns.md](references/patterns.md), [references/anti-patterns.md](references/anti-patterns.md), and [references/examples.md](references/examples.md) — patterns and samples

Prefer Learn for stable APIs. For Durable Jobs and `Orleans.Journaling`, use version-tagged package READMEs/public API because Learn does not yet cover them fully.
