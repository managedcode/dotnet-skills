# Communication, execution, and placement selection

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


## Choose Execution and Scaling

| Primitive | Purpose | Decision rule |
|---|---|---|
| Standard grain | Serialize behavior for one identity | Default for stateful domain entities and digital twins |
| `[StatelessWorker]` | Scale fungible work locally and across silos | Use when activations are interchangeable and their local state need not agree |
| `[ReadOnly]` | Permit compatible reads to interleave | Use only when the method cannot mutate grain state or external invariants |
| `[Reentrant]` | Allow turns from other calls while the grain awaits | Use for call cycles or measured concurrency needs after auditing every invariant |
| `[AlwaysInterleave]` / `[MayInterleave]` | Selectively admit interleaving | Prefer narrow scheduling exceptions over making the whole grain reentrant |
| Placement strategy/filter | Constrain or optimize activation location | Keep the resource-optimized default unless locality, hardware, zone, compliance, or role requirements are proven |
| Placement hint | Suggest a target silo for activation or migration | Choose an active compatible silo and scope the request-context hint; it does not move an existing activation |
| Heterogeneous silo/versioning | Run different grain sets or versions during rollout | Use explicit compatibility/version selection for safe rolling deployments |

Turn-based execution is single-threaded, not magically race-free. Reentrancy allows another turn to run while the first awaits; any state observed before the `await` can be stale afterward. Avoid blocking calls, `.Result`, `.Wait()`, thread-affine work, and unbounded CPU loops on the grain scheduler.

