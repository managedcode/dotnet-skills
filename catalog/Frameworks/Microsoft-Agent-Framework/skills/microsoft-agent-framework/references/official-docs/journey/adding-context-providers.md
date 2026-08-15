---
title: Adding Context Providers
description: Understand what context providers are, why agents need them, and how they inject memory, knowledge, and dynamic data into the agent's context window.
author: TaoChenOSU
ms.topic: article
ms.author: taochen
ms.date: 04/06/2026
ms.service: agent-framework
---

# Adding Context Providers

The [previous page](adding-middleware.md) showed how middleware wraps the agent's execution pipeline with cross-cutting concerns — logging, guardrails, error handling — without touching the agent's core logic. But middleware deals with *how* the agent runs, not *what* the agent knows. So far, the agent's knowledge comes from two places: its training data and whatever the user says in the current turn.

That's a problem. A useful agent needs more than that. It needs to recall what the user said three turns ago, know the user's preferences, or pull relevant facts from a knowledge base — all *before* it starts generating a response. Tools can fetch information, but they're reactive: the model must decide to call them. If the model doesn't realize it needs context, it won't ask for it.

**Context providers** solve this. They're components that run before and after each agent invocation, proactively injecting relevant information into the context window and optionally extracting state from the response to be stored for future use. They give your agent memory, personalization, and access to external knowledge — without changing the agent's instructions or code.

## When to use this

Add context providers to your agent when:

- The agent needs **conversation history** — it should remember what was said in previous turns, not just the current message.
- You want to inject **user-specific data** — profiles, preferences, account details, or session state — so the agent can personalize its responses.
- You need **retrieval-augmented generation (RAG)** — automatically fetching relevant documents or facts from a knowledge base before each response.
- The agent requires **dynamic instructions** — context that changes between invocations based on the time of day, the user's location, or other runtime conditions.
- You want to **decouple data sourcing from agent logic** — the agent doesn't need to know *where* context comes from, only that it's available.

## Why not just use tools?

Tools and context providers both give agents access to external information, but they work in fundamentally different ways:

| Aspect | Tools | Context providers |
|--------|-------|-------------------|
| **Trigger** | Reactive — the model decides when to call a tool | Proactive — runs automatically before every invocation |
| **Control** | Model-driven: the model chooses which tool, when, and with what arguments | Developer-driven: you decide what context is always available |
| **Visibility** | The model must know a tool exists and judge that it's relevant | Context is injected transparently — the model sees it as part of the prompt |
| **Use case** | On-demand actions and lookups: "search the web," "query the database" | Always-present context: conversation history, user profiles, preloaded knowledge |
| **Token cost** | Tokens spent only when the tool is called | Tokens spent on every invocation (the context is always in the prompt) |

Neither is strictly better. Many agents use both: context providers for information that should *always* be present (history, user profile, core knowledge), and tools for information the agent should fetch *on demand* (live search results, database queries, API calls).

> [!TIP]
> A good rule of thumb: if the agent should have this information *every single time* it runs, use a context provider. If the agent should fetch it *only when relevant*, use a tool.

## How context providers work

Context providers participate in a two-phase lifecycle around each agent invocation:

```
┌──────────────────────────────────────────────────────────────┐
│  Caller: agent.run("What's the return policy?")              │
└──────────────┬───────────────────────────────────────────────┘
               ▼
┌──────────────────────────────────────────────────────────────┐
│  BEFORE RUN — each context provider injects context          │
│                                                              │
│  • History provider loads past conversation messages         │
│  • Memory provider retrieves relevant facts/preferences      │
│  • RAG provider searches knowledge base and adds results     │
│  • Custom provider injects user profile, time, location      │
└──────────────┬───────────────────────────────────────────────┘
               ▼
┌──────────────────────────────────────────────────────────────┐
│  Agent core — model sees original input + all injected       │
│  context and generates a response                            │
└──────────────┬───────────────────────────────────────────────┘
               ▼
┌──────────────────────────────────────────────────────────────┐
│  AFTER RUN — each context provider processes the response    │
│                                                              │
│  • History provider saves the new messages                   │
│  • Memory provider extracts facts to remember for later      │
│  • Custom provider updates session state                     │
└──────────────────────────────────────────────────────────────┘
```

Key points:

1. **Context providers run automatically.** You register them once when creating the agent. After that, they participate in every invocation without any extra code on your part.
2. **Multiple providers compose together.** You can register several context providers — a history provider, a RAG provider, and a custom provider — and they all contribute to the same context window. Their contributions are merged in registration order.
3. **Providers have two hooks.** The *before* hook injects context (messages, instructions, tools) into the prompt. The *after* hook processes the response — storing messages, extracting memories, or updating state.
4. **Providers are session-aware.** Context providers receive the current session, so they can load and store data scoped to a specific conversation. See [Sessions](../concepts/agents/conversations/session.md) for how session management works.

> [!TIP]
> For a detailed view of where context providers sit in the full agent execution pipeline — alongside middleware and the chat client — see the [Agent Pipeline Architecture](../concepts/agents/agent-pipeline.md).

## Managing the context window

Every piece of context you inject consumes tokens from the model's context window. History grows with each turn. RAG results add document chunks. User profiles add metadata. If the total exceeds the model's limit, the oldest or least relevant information gets truncated — potentially losing important context.

Context window management is a critical consideration when using context providers: **Compaction** strategies summarize or trim older history to stay within token limits while preserving key information. See [Compaction](../concepts/agents/conversations/compaction.md).

> [!TIP]
> For hands-on experience with memory and context providers, see [Step 4: Memory](../get-started/memory.md) in the Get Started tutorial.

> [!IMPORTANT]
> It is not recommended to maintain a very long context window, as the performance of the model may degrade as the context window grows. If the agent starts to experience degraded performance, consider using compaction strategies to reduce the context size.

## Considerations

| Consideration | Details |
|---------------|---------|
| **Token budget** | Every injected context consumes tokens. Monitor total context size carefully — especially when combining multiple providers. If context grows unbounded, important information gets truncated silently. |
| **Retrieval latency** | Context providers that query external services (databases, search indexes, APIs) add latency to every invocation. Use caching, connection pooling, and async operations to keep retrieval fast. |
| **Relevance** | Injecting irrelevant context doesn't just waste tokens — it can actively degrade the model's responses by diluting the signal. Make sure your providers inject focused, relevant information. |
| **Staleness** | Cached or preloaded context can become outdated. Design providers to refresh data at appropriate intervals, and consider whether slightly stale context is acceptable for your use case. |
| **Composability** | When multiple providers contribute to the same context window, their contributions can interact in unexpected ways. Test providers together, not just individually, to ensure the combined context makes sense. |

## Next steps

Now that your agent has tools, skills, middleware, and context providers, the next step is **agents as tools** — composing agents by using one agent as a tool for another, enabling specialization and delegation.

> [!div class="nextstepaction"]
> [Agents as Tools](agents-as-tools.md)

**Go deeper:**

- [Context Providers reference](../concepts/agents/conversations/context-providers.md) — built-in and custom provider patterns
- [Conversations & Memory overview](../concepts/agents/conversations/index.md) — sessions, history, and storage
- [RAG](../agents/rag.md) — retrieval-augmented generation patterns
- [Compaction](../concepts/agents/conversations/compaction.md) — managing context window size
- [Storage](../concepts/agents/conversations/storage.md) — persisting conversation data
- [Agent Pipeline Architecture](../concepts/agents/agent-pipeline.md) — how context providers fit in the execution pipeline
- [Step 4: Memory](../get-started/memory.md) — hands-on tutorial
