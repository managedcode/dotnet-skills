---
title: From LLMs to Agents
description: Understand what makes an AI agent more than a raw LLM call, why the agent abstraction matters, and create your first agent with instructions.
author: TaoChenOSU
ms.topic: article
ms.author: taochen
ms.date: 04/03/2026
ms.service: agent-framework
---

# From LLMs to Agents

The [previous page](llm-fundamentals.md) covered how LLMs work: they take a tokenized sequence of messages, generate new tokens one at a time. But a raw LLM call is **stateless** — it has no memory, no tools wired up, and no built-in way to maintain a conversation. Every call starts from scratch.

An **agent** wraps an LLM with the structure needed to build real applications: a persistent identity, system instructions, tools, memory, and a runtime loop that orchestrates it all. This page explains what that abstraction provides and walks you through creating your first agent.

## When to use this

Understanding the agent abstraction helps when:

- You're deciding whether to use raw LLM calls or Microsoft Agent Framework
- You want to understand the value that Agent Framework provides over direct API calls
- You're designing an application and need to choose the right level of abstraction

## Trade-offs

| Raw LLM calls | Agent Framework |
|----------------|-----------------|
| Full control over every API parameter | Opinionated abstractions that handle common patterns |
| No dependencies beyond the model SDK | Additional dependency on Agent Framework |
| You manage state, tools, and retry logic | Built-in session management, tool dispatch, and middleware for production-grade applications |
| Tightly coupled to one provider | Swap providers without changing application code |

## What a raw LLM call looks like

At its simplest, calling an LLM is a stateless request-response:

```
request:
  messages:
    [system]     "You are a helpful assistant."
    [user]       "What's the capital of France?"

response:
  [assistant]    "The capital of France is Paris."
```

This works for a single question. But for anything beyond that, you quickly hit limitations:

- **No memory** — Chat history management differs by service. Some services support in-service chat history storage, but with raw LLM calls you must manage this yourself. Agent Framework unifies this via the session.
- **No tools** — The model can only generate text. It can't look up data, call APIs, or take actions unless you write all the orchestration code yourself.
- **No identity** — Every call requires you to re-send the system instructions. There's no persistent "agent" — just an API you call.
- **No guardrails** — There's no built-in way to intercept, validate, or modify the model's behavior across calls.
- **No Encapsulation** — Each use site of the LLM needs to have access and knowledge of the tools that needs to be used with the LLM. There is no encapsulation of these inside an opaque agent.
- **Tightly coupled** — Your code is written against a specific provider's API. Switching models means rewriting integration code.

Each of these problems is solvable on its own, but solving all of them for every application is significant engineering work. That's what the agent abstraction handles for you.

## What an agent adds

An agent takes the raw LLM call and wraps it in a structured runtime:

```
┌──────────────────────────────────────────────────┐
│  Agent                                           │
│                                                  │
│  ┌──────────────┐  ┌────────┐  ┌─────────────┐   │
│  │ Instructions │  │ Tools  │  │   Session   │   │
│  └──────────────┘  └────────┘  └─────────────┘   │
│                                                  │
│  ┌──────────────────────────────────────────┐    │
│  │          Middleware Pipeline             │    │
│  └──────────────────────────────────────────┘    │
│                                                  │
│  ┌──────────────────────────────────────────┐    │
│  │       LLM Provider (swappable)           │    │
│  └──────────────────────────────────────────┘    │
└──────────────────────────────────────────────────┘
```

| Layer | What it does |
|-------|--------------|
| **Instructions** | Define the agent's persona, constraints, and output format. Set once, applied to every call. |
| **Tools** | Give the agent the ability to act — call APIs, query databases, run code. The framework handles the tool-call loop automatically. |
| **Session** | Maintain conversation history and any other multi-turn conversation state so the agent remembers what happened before. |
| **Middleware** | Intercept requests and responses for logging, guardrails, caching, or behavioral overrides. |
| **LLM Provider** | Abstract the LLM backend. Switch from Azure OpenAI to another provider without changing your agent code. |

> [!TIP]
> To see the full list of LLM provider options in Agent Framework, refer to [Providers](../integrations/by-component/model-providers/index.md). To see the full agentic pipeline in Agent Framework, refer to [Agent Pipeline](../concepts/agents/agent-pipeline.md).

## Your first agent: instructions only

The simplest possible agent has just two things: a **model client** and **instructions** — just an LLM with a persona. This is the right starting point for simple tasks such as question answering or text summarization, where the LLM's internal knowledge is sufficient.

> [!IMPORTANT]
> An agent with instructions only will respond using **only** the knowledge acquired during the training stage of the LLM, and the instructions provided. For example, if the question is "What is the capital of France?", the agent can answer "Paris" because it learned this fact during training. Therefore, the agent at this point only acts as a wrapper around the LLM with a static persona.

> [!TIP]
> At this stage, you probably don't need a very strong model. If the questions require logical reasoning or complex understanding, you may need a reasoning model.

Please refer to [Your First Agent](../get-started/your-first-agent.md) for a step-by-step guide to creating and running your first agent in Agent Framework with instructions only.

Please refer to [Multi-turn Conversations](../get-started/multi-turn.md) for guidance on handling conversations that span multiple interactions with the agent, i.e. adding **session management**.

## Next steps

To make the agent more capable, the first thing you may want to do is add **tools**. Tools give the agent the ability to act — call APIs, query databases, run code.

> [!div class="nextstepaction"]
> [Adding Tools](adding-tools.md)

**Go deeper:**

- [Running Agents](../concepts/agents/running-agents.md) — streaming, invocation patterns
- [Providers](../integrations/by-component/model-providers/index.md) — choose your LLM provider
