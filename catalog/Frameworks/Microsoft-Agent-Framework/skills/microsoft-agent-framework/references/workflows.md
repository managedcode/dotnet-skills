# Workflows

## Workflows Exist To Make Control Flow Explicit

Use a workflow when the correctness of the system depends on an explicit execution graph rather than a model deciding everything on the fly.

Typical reasons:

- typed multi-step execution
- predictable branching
- fan-out and aggregation
- human-in-the-loop pauses
- checkpoint and resume
- durable orchestration
- multi-agent collaboration that must stay inspectable

If a single agent with a small tool surface can solve the task, stay with an agent.

## Core Concepts

| Concept | Meaning | Why It Matters |
| --- | --- | --- |
| Executor | A typed processing node | Owns one step of the workflow |
| Edge | A routing rule between executors | Makes branching and handoff explicit |
| Workflow | The execution graph | Defines the process structure |
| Superstep | A unit of progress between checkpoint points | Determines checkpoint timing |
| `InputPort` | The boundary for external requests and responses | Enables HITL and system callbacks |
| Shared state | Workflow-wide durable data | Avoids abusing agent state for process state |
| Checkpoint | A saved execution snapshot | Enables recovery, resume, and rehydration |

## Builder Selection

Use `WorkflowBuilder` when:

- you need custom executors
- the graph is not just agent orchestration
- you want explicit control over edges and message types

Use `AgentWorkflowBuilder` when:

- you are primarily coordinating agents
- the orchestration matches built-in agent patterns
- you want sequential or concurrent pipeline helpers

### `AgentWorkflowBuilder.BuildConcurrent`

```csharp
var workflow = AgentWorkflowBuilder.BuildConcurrent(agents);
```

- Accepts `IEnumerable<AIAgent>` and an optional custom aggregator `Func<IList<List<ChatMessage>>, List<ChatMessage>>`.
- Handles fan-out and fan-in automatically without requiring custom executor classes.
- Default aggregator returns the last message from each responding agent.
- After `InProcessExecution.StreamAsync`, send `TurnToken(emitEvents: true)` via `run.TrySendMessageAsync` to kick off agents.
- Use manual `WorkflowBuilder` with `AddFanOutEdge`/`AddFanInEdge` only when you need a custom dispatcher or aggregation logic beyond what the built-in overload supports.

## Workflow Patterns

| Pattern | Best For | Main Risk |
| --- | --- | --- |
| Sequential | staged refinement and pipelines | hidden accumulation of low-quality output between stages |
| Concurrent | parallel analysis and aggregation | weak aggregation logic or duplicated work |
| Handoff | routing to the right specialist | opaque routing if criteria stay implicit |
| Group Chat | managed multi-agent discussion | noisy collaboration without clear stopping rules |
| Magentic | planner-led decomposition | overkill for simple bounded tasks |

These are workflow patterns, not prompt slogans. If you cannot explain the message flow in code, you probably do not have a real workflow design yet.

## Request And Response

Request and response is the first-class way to model:

- human approval
- external callbacks
- asynchronous system input
- pauses that must survive beyond one model run

`InputPort` is the key primitive.

```csharp
var inputPort = InputPort.Create<ApprovalRequest, ApprovalResponse>("approval");

var workflow = new WorkflowBuilder(inputPort)
    .AddEdge(inputPort, reviewerExecutor)
    .AddEdge(reviewerExecutor, inputPort)
    .Build<ApprovalRequest>();
```

Operationally:

1. an executor emits a request
2. the host sees a `RequestInfoEvent`
3. the outer system resolves the request
4. the response is sent back into the workflow
5. the waiting executor resumes

If approval, escalation, or external data truly changes the control flow, this is cleaner than stuffing everything into tools and prompts.

## Checkpoints

Checkpoints are captured at superstep boundaries and include:

- executor state
- pending messages
- pending requests and responses
- shared states

For custom executors, checkpointing is not free. You must explicitly save and restore internal executor state.

Use checkpoints when:

- runs are long-lived
- resume matters
- failures must not discard progress
- the workflow crosses system boundaries

## Shared State Versus Executor State

Use shared state only for data that belongs to the workflow as a whole.

Keep executor-local state local when:

- it belongs to one step only
- it should not be a shared mutable dependency
- you need clearer reasoning about checkpoint behavior

This separation matters because workflows become hard to reason about when every executor reads and writes one giant state bag.

## Workflow As Agent

Wrap the workflow as an agent when:

- a hosting layer expects an `AIAgent`
- another system only knows how to talk to agents
- you need to expose the workflow through OpenAI-compatible endpoints, A2A, or similar surfaces

Do not wrap a workflow as an agent just to hide complexity from your own codebase. Keep the graph explicit in code and docs.

## Observability

Workflow observability is not optional once you have:

- concurrency
- branching
- approvals
- retries
- multiple specialists

At minimum, be able to answer:

- which executor ran
- what message it received
- why a branch was chosen
- whether a request is pending
- which checkpoint corresponds to which execution stage

## Declarative Workflows And `.NET`

The official docs currently position declarative workflows as Python-first.

For `.NET`:

- treat those docs as conceptual guidance
- do not invent a declarative `.NET` API surface that the docs do not actually publish
- keep production `.NET` implementations programmatic unless official `.NET` declarative support is documented

## Anti-Patterns

- Using one giant workflow because it feels "enterprise".
- Encoding routing rules in prompt text instead of edges.
- Using workflow state as a dumping ground for every executor's scratch data.
- Forgetting to checkpoint custom executor state.
- Wrapping a workflow as an agent and then forgetting the actual workflow still exists underneath.

## Source Pages

- `references/official-docs/user-guide/workflows/overview.md`
- `references/official-docs/user-guide/workflows/core-concepts/overview.md`
- `references/official-docs/user-guide/workflows/requests-and-responses.md`
- `references/official-docs/user-guide/workflows/checkpoints.md`
- `references/official-docs/user-guide/workflows/as-agents.md`
- `references/official-docs/user-guide/workflows/orchestrations/overview.md`
