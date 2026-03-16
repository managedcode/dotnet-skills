# Architecture and Agent Selection

## Start With The Smallest Correct Abstraction

Route the problem before you touch packages or SDK helpers.

| Situation | Default | Why | Escalate When |
| --- | --- | --- | --- |
| The task is deterministic, auditable, and easy to encode | Plain `.NET` code | Lowest latency, lowest cost, easiest to test | You truly need model reasoning, tool choice, or fuzzy planning |
| One model-backed decision maker with a bounded tool set is enough | `AIAgent` over `IChatClient` | Smallest useful agent surface in `.NET` | The control flow becomes multi-step or multi-actor |
| The flow must stay typed, inspectable, and resumable | `Workflow` | Executors, edges, requests, and checkpoints are explicit | You also need remote protocols or agent-like reuse |
| The process is long-running and Azure-hosted | Durable agents on Azure Functions | Durable Task gives replay, persistence, and recovery | You do not need serverless durability or long-lived execution |
| External clients need a standard protocol | ASP.NET Core hosting adapters | Protocol concerns stay outside your core agent logic | The in-process agent or workflow still is not chosen |

## Decision Order

1. Decide whether the task should stay deterministic.
2. Decide whether one agent is enough or whether you need a typed workflow.
3. Decide where state lives: local messages, service-owned threads, custom stores, or workflow state.
4. Decide whether any remote protocol is needed at all.
5. Only then choose provider SDKs and hosting packages.

If you reverse this order, you usually end up with the wrong abstraction and then rationalize it afterward.

## The Core Runtime Model

- `AIAgent` is the base runtime abstraction.
- `AIAgent` instances are designed to be stateless and reusable.
- `AgentThread` carries conversation state and provider-specific thread state.
- `AgentResponse` and `AgentResponseUpdate` can contain much more than final text:
  - tool calls
  - tool results
  - reasoning-like progress
  - metadata
  - provider-specific content
- `Workflow` is not "many prompts in a row". It is an explicit execution graph with typed executors and routing rules.

## Agent Selection Matrix

| Choice | Best When | State Model | Main Tradeoff |
| --- | --- | --- | --- |
| `ChatClientAgent` | You already have an `IChatClient` and want the simplest `.NET` composition | Depends on the underlying service | Broadest surface, but capability details vary by provider |
| Responses-based agent | You want richer eventing, background responses, or forward-looking OpenAI-compatible behavior | Service-backed or local, depending on mode | More moving pieces than plain chat completions |
| Chat Completions agent | You want straightforward client-managed conversations | Usually local or custom-store history | Less future-facing than Responses |
| Hosted agent service | The managed service itself is the requirement | Service-owned | Less control over threading, tools, and portability |
| Custom `AIAgent` | Built-in wrappers are insufficient | You own the model | Highest flexibility, highest maintenance burden |
| A workflow wrapped as an agent | A larger graph must be consumed through an agent-like API | Workflow thread plus checkpoint state | Easy to hide complexity if you do not document it |

## Agent Versus Workflow

Choose an agent when:

- one model-backed actor can own the decision making
- the tool set is small and coherent
- retry logic is simple
- you do not need explicit branching or parallel fan-out

Choose a workflow when:

- branching logic matters to correctness
- multiple specialists must coordinate predictably
- you need request and response with external systems or humans
- checkpointing and resume are part of the design, not a future wish
- you need auditable execution paths

Typical smell that should push you to workflows:

- one agent has 20+ tools
- prompts encode routing logic instead of code doing it
- you need to explain "then it usually calls X, unless Y, except after approval"
- you need to pause for a human or another system and continue later

## Durable Agents Are A Hosting Decision

Durable agents are not the default "serious production" mode. They are the right choice only when you need one or more of these:

- Azure Functions hosting
- long-running execution that must survive restarts
- deterministic orchestration replay
- durable thread persistence as part of the hosting model

Do not choose durable agents just because:

- the feature sounds enterprise-grade
- the task might take more than a few seconds
- you want "future proofing"

For normal web apps and services, standard agents plus standard workflows are usually the better baseline.

## Protocol Adapters Come Last

Protocol adapters are wrappers around your in-process design. They are not the design itself.

| Protocol Surface | Use It For | It Does Not Replace |
| --- | --- | --- |
| OpenAI-compatible hosting | Calling your agent from existing OpenAI-style clients | The underlying agent or workflow choice |
| A2A | Agent-to-agent interoperability and discovery | MCP, AG-UI, or workflow design |
| AG-UI | Rich web or mobile UI interactions over a standard protocol | A2A, MCP, or your actual domain logic |
| MCP | Tools and contextual data exchange | Remote agent protocols or human UI protocols |
| DevUI | Local debugging and sample-style testing | Production hosting |

## Practical Baseline For Most `.NET` Teams

If you are building a new `.NET` agentic feature and do not have a service-imposed architecture yet:

1. Start with an `IChatClient`.
2. Wrap it as a `ChatClientAgent`.
3. Add only the function tools you actually need.
4. Use an `AgentThread` and serialize it.
5. Add middleware for policy and logging.
6. Escalate to a workflow only when the flow becomes explicit and typed.
7. Add OpenAI/A2A/AG-UI hosting only after the in-process behavior is already correct.

## Architecture Smells

- Choosing a provider first and then forcing the runtime model to fit it.
- Treating `AgentThread` as a reusable universal object across providers.
- Keeping business state in singleton services or agent fields instead of thread or workflow state.
- Using prompts to fake branching, retries, approvals, or escalation logic that should be explicit.
- Adding every available tool to one agent because "the model will decide".
- Treating hosted services and local `IChatClient` agents as if they have the same guarantees.

## Source Pages

- `references/official-docs/overview/agent-framework-overview.md`
- `references/official-docs/user-guide/agents/agent-types/index.md`
- `references/official-docs/user-guide/agents/running-agents.md`
- `references/official-docs/user-guide/workflows/overview.md`
- `references/official-docs/user-guide/workflows/as-agents.md`
- `references/official-docs/user-guide/hosting/index.md`
