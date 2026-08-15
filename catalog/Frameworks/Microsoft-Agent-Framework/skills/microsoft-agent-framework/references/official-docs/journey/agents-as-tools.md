---
title: Agents as Tools
description: Compose agents by using one agent as a tool for another — enabling specialization and delegation.
author: TaoChenOSU
ms.topic: article
ms.author: taochen
ms.date: 05/27/2026
ms.service: agent-framework
---

# Agents as Tools

The [previous page](adding-context-providers.md) showed how context providers give agents memory and dynamic knowledge — information that's proactively injected before every invocation. At this point, you have a **single** agent that can use tools, load skills, run through middleware, and draw on rich context. That's powerful, but it's still one agent doing everything.

What happens when your agent's responsibilities grow beyond what a single set of instructions can handle well? As an agent accumulates tools, **tool selection degrades** — models are better at choosing among a handful of well-described tools than sorting through dozens. As instructions broaden, **focus degrades** — a system prompt that tries to cover travel booking, expense reporting, and calendar management gives the model too many roles to juggle.

[**Agents as tools**](../agents/tools/index.md#using-an-agent-as-a-function-tool) solve this by letting you compose agents: one agent (the *outer* agent) can call another agent (the *inner* agent) as if it were a regular function tool. Each inner agent has a tight scope — its own instructions, its own tools, its own expertise. The outer agent decides when to delegate and what to ask for — exactly the same way it decides when to call any other tool.

## When to use this

Use agents as tools when:

- You want to **delegate a specialized subtask** to a focused agent — for example, a general assistant that calls a dedicated "travel-booking agent" when the user asks about flights.
- The outer agent should decide **when and whether** to involve the inner agent, based on the conversation — the delegation is model-driven, not hard-coded.
- You don't need explicit control over the **execution order** between agents — you're fine with the outer agent orchestrating things through its own reasoning.

> [!TIP]
> Each agent can also use a different model depending on its specialization and requirements. More complex agents might use larger models for reasoning, while simpler agents might use smaller, faster models for efficiency.

## Considerations

| Consideration | Details |
|---------------|---------|
| **Simplicity** | Agent-as-tool is the lightest multi-agent pattern. You convert an agent to a tool and hand it to another agent. It's the natural next step when one agent isn't enough. |
| **Latency** | Each delegation is a full agent invocation: the outer agent calls the inner agent, which calls the LLM, which may call tools of its own. Nested invocations add up. Keep inner agents focused so they resolve quickly. |
| **Routing is model-driven** | The outer agent's LLM decides when to call the inner agent, just like it decides when to call any tool. This means routing can be unpredictable — if the tool description is vague, the model may call the wrong agent or skip it entirely. Clear, specific descriptions are critical. |
| **Limited visibility** | The outer agent sees the inner agent's final text response — it doesn't see the inner agent's intermediate reasoning, tool calls, or context. If you need observability into inner agent behavior, use [tracing](../agents/observability.md). |
| **Context isolation** | The inner agent runs with its own instructions and tools. It doesn't automatically inherit the outer agent's conversation history or context. You communicate with it through the tool call arguments, just like any other function tool. |

## How it works

Agents as tools builds on the [tool-calling loop](adding-tools.md#how-the-tool-calling-loop-works) you already know. The only difference is that the "function" being called is itself an agent.

```
┌──────────────────────────────────────────────────────────┐
│  User: "Book me a flight to Paris and file the expense"  │
└──────────────┬───────────────────────────────────────────┘
               ▼
┌──────────────────────────────────────────────────────────┐
│  Outer agent reasons about the request                   │
│  → decides to call the travel-booking agent first        │
└──────────────┬───────────────────────────────────────────┘
               ▼
┌──────────────────────────────────────────────────────────┐
│  Inner agent (travel-booking) runs as a tool:            │
│  • receives: "Book a flight to Paris"                    │
│  • uses its own tools (search_flights, book_flight)      │
│  • returns: "Booked Flight AF123, $450"                  │
└──────────────┬───────────────────────────────────────────┘
               ▼
┌──────────────────────────────────────────────────────────┐
│  Outer agent receives the tool result                    │
│  → decides to call the expense-filing agent next         │
└──────────────┬───────────────────────────────────────────┘
               ▼
┌──────────────────────────────────────────────────────────┐
│  Inner agent (expense-filing) runs as a tool:            │
│  • receives: "File expense for Flight AF123, $450"       │
│  • uses its own tools (create_expense, attach_receipt)   │
│  • returns: "Expense report filed"                       │
└──────────────┬───────────────────────────────────────────┘
               ▼
┌──────────────────────────────────────────────────────────┐
│  Outer agent synthesizes both results:                   │
│  "Done! Booked Flight AF123 to Paris for $450 and filed  │
│   expense report."                                       │
└──────────────────────────────────────────────────────────┘
```

Key points:

1. **The inner agent looks like a function tool.** From the outer agent's perspective, calling an inner agent is no different from calling `get_weather()` or `search_database()`. The framework handles converting the agent to a tool with a name, description, and input parameter.
2. **The inner agent runs independently.** It has its own instructions, tools, and LLM invocations. It doesn't see the outer agent's full conversation — only the input passed through the tool call.
3. **The outer agent sees only the final result.** The inner agent's intermediate steps (tool calls, reasoning, retries) are invisible to the outer agent. It receives a text response, just like any tool result.

## Next steps

Now that you can compose agents within a single process, the next step is **Agent-to-Agent (A2A)** — enabling agents to communicate across service and organizational boundaries using a standard protocol.

> [!div class="nextstepaction"]
> [Agent-to-Agent (A2A)](agent-to-agent.md)

**Go deeper:**

- [Tools Overview — Using an Agent as a Function Tool](../agents/tools/index.md#using-an-agent-as-a-function-tool) — code examples for C#, Python, and Go
- [Function Tools](../agents/tools/function-tools.md) — the tool type that agent-as-tool builds on
- [Observability](../agents/observability.md) — tracing inner agent behavior
