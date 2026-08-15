---
title: Adding Middleware
description: Understand why and when agents need middleware, how the middleware pipeline works, and the types of cross-cutting concerns middleware addresses.
author: taochen
ms.topic: article
ms.author: taochen
ms.date: 04/04/2026
ms.service: agent-framework
---

# Adding Middleware

The [previous page](adding-skills.md) showed how skills package reusable domain expertise — instructions, reference material, and scripts — into self-contained units that any agent can load on demand. But as you deploy agents into production, a new category of problems emerges: problems that cut across *every* interaction regardless of what the agent does.

You need to log every request and response. You need guardrails that block harmful content before the model sees it. You need to enforce rate limits, catch exceptions gracefully, and inject telemetry — all without touching the agent's core logic. Copy-pasting these concerns into every agent (or every tool, or every skill) doesn't scale and creates maintenance nightmares.

**Middleware** solves this. Middleware lets you wrap the agent's [**execution pipeline**](../concepts/agents/agent-pipeline.md) with reusable behaviors that intercept, inspect, and modify requests and responses at well-defined points. Think of middleware as a series of concentric layers around the agent — each layer gets a chance to act on the input before it reaches the agent, and on the output before it reaches the caller.

## When to use this

Add middleware to your agent when:

- You need **guardrails** to block harmful, off-topic, or policy-violating content before or after the model processes it.
- You want **centralized logging or telemetry** for all agent interactions without modifying each agent individually.
- You need to **modify requests or responses** — enriching prompts, transforming outputs, or replacing results entirely — without changing agent logic.
- You want to **enforce policies** such as rate limiting, content filtering, or authentication checks that apply to every run.
- You need to **handle exceptions** consistently — retrying on transient failures, returning graceful fallback responses, or logging errors for diagnostics.
- You want to **share state** across the pipeline — for example, tracking request timing or accumulating metrics that multiple middleware components need.

> [!TIP]
> Agent Framework includes built-in instrumentation for tracing and metrics. See [Observability](../agents/observability.md) for details.

## How the middleware pipeline works

When you call your agent's run method, the request doesn't go directly to the model. Instead, it flows through a pipeline of middleware layers, each of which can inspect or modify the request, delegate to the next layer, and then inspect or modify the response on the way back.

```
┌─────────────────────────────────────────────────────────┐
│  Caller: agent.run("What's the weather?")               │
└──────────────┬──────────────────────────────────────────┘
               ▼
┌─────────────────────────────────────────────────────────┐
│  Middleware 1 (Logging)                                  │
│  • Logs the incoming request                            │
│  • Calls next middleware                                │
│  • Logs the outgoing response                           │
└──────────────┬──────────────────────────────────────────┘
               ▼
┌─────────────────────────────────────────────────────────┐
│  Middleware 2 (Guardrails)                               │
│  • Checks input against content policy                  │
│  • If blocked → returns early with rejection message    │
│  • If allowed → calls next middleware                   │
│  • Checks output against content policy                 │
└──────────────┬──────────────────────────────────────────┘
               ▼
┌─────────────────────────────────────────────────────────┐
│  Agent core (model invocation, tool calls, etc.)        │
└─────────────────────────────────────────────────────────┘
```

Key points:

1. **Each middleware decides whether to continue.** A middleware can call the next layer in the chain to proceed normally, or it can short-circuit the pipeline by returning a response directly — for example, when a guardrail blocks a request.
2. **Middleware sees both directions.** A middleware runs code *before* delegating (to inspect or modify the input) and *after* the response comes back (to inspect or modify the output). This is the classic "onion" pattern.
3. **Multiple middleware chain together.** When you register several middleware components, they nest: the first registered middleware is the outermost layer, and the last registered is the innermost layer closest to the agent.

> [!TIP]
> For a detailed view of how middleware fits into the full agent execution pipeline — including context providers and chat client layers — see the [Agent Pipeline Architecture](../concepts/agents/agent-pipeline.md).

## What middleware can do

Agent Framework supports middleware at three layers of the pipeline — agent run, function calling, and chat client — giving you fine-grained control over where you intercept execution. Common patterns include:

| Pattern | Example | Reference |
|---------|---------|-----------|
| Guardrails & termination | Block harmful content, limit conversation length | [Termination & Guardrails](../concepts/agents/middleware/termination.md) |
| Exception handling | Retry on transient failures, return fallback responses | [Exception Handling](../concepts/agents/middleware/exception-handling.md) |
| Result overrides | Redact sensitive data, enrich or replace agent output | [Result Overrides](../concepts/agents/middleware/result-overrides.md) |
| Shared state | Pass request IDs or timing data between middleware | [Shared State](../concepts/agents/middleware/shared-state.md) |
| Runtime context | Vary behavior based on session, user, or per-run config | [Runtime Context](../concepts/agents/middleware/runtime-context.md) |
| Scoping | Apply middleware to all runs or just a single run | [Agent vs Run Scope](../concepts/agents/middleware/agent-vs-run-scope.md) |

For a complete walkthrough of defining and registering middleware, see [Defining Middleware](../concepts/agents/middleware/defining-middleware.md). For the full architecture overview, see the [Middleware Overview](../concepts/agents/middleware/index.md).

## Considerations

| Consideration | Details |
|---------------|---------|
| **Separation of concerns** | Middleware keeps cross-cutting logic out of your agent code, your tools, and your skills. Each middleware component has a single responsibility — logging, guardrails, error handling — that you can add, remove, or reorder independently. |
| **Order dependence** | Middleware forms a chain. The order you register middleware matters: a logging middleware that runs first will see the raw input, while one that runs last will see input already modified by earlier middleware. Plan your pipeline order deliberately. |
| **Debugging complexity** | When middleware modifies inputs or outputs, debugging requires understanding the full pipeline. A response might look wrong not because of the agent but because a middleware transformed it. Good logging middleware (placed early in the chain) helps diagnose these cases. |
| **Performance overhead** | Each middleware layer adds processing time to every request. For lightweight operations like logging, this is negligible. For expensive operations like calling an external content-moderation API, the latency adds up — especially when multiple such middleware are chained. |

## Next steps

Now that your agent has tools, skills, and middleware, the next step is **context providers** — components that inject memory, user profiles, and dynamic knowledge into the agent's context window before each run.

> [!div class="nextstepaction"]
> [Context Providers](adding-context-providers.md)

**Go deeper:**

- [Middleware Overview](../concepts/agents/middleware/index.md) — full reference for all middleware types
- [Agent Pipeline Architecture](../concepts/agents/agent-pipeline.md) — how middleware fits into the execution pipeline
