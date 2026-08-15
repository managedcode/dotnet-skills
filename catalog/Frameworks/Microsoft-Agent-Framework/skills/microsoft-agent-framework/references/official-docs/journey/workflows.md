---
title: Workflows
description: Orchestrate multi-agent, multi-step processes with explicit control over execution order, state, and human-in-the-loop patterns.
author: TaoChenOSU
ms.topic: article
ms.author: taochen
ms.date: 04/06/2026
ms.service: agent-framework
---

# Workflows

> [!TIP]
> Before reaching for workflows, we recommend you first try simpler patterns to see if they meet your needs. They are easier to set up and debug. Workflows are most useful when you need guaranteed execution order that a single agent can't reliably provide on its own.

The journey so far has covered increasingly powerful ways to build with agents. You've seen how a single agent can [use tools](adding-tools.md), [load skills](adding-skills.md), [run through middleware](adding-middleware.md), and [draw on rich context](adding-context-providers.md). You've composed agents by [using one as a tool for another](agents-as-tools.md) and connected them across service boundaries with [A2A](agent-to-agent.md).

All of these patterns share a common trait: **the LLM decides what happens next.** The model picks which tool to call, whether to delegate, and when to stop. That's powerful for open-ended tasks where the right path depends on the conversation — but it's a liability when the process itself has rules.

Consider scenarios like these:

- A **document-review pipeline** where a draft must be written, reviewed, revised, and approved — in that order, every time.
- A **customer-onboarding flow** that collects information, runs a compliance check, provisions accounts, and sends a welcome email — some steps in parallel, some gated by human approval.
- An **analytics workflow** that gathers data from multiple sources, merges the results, and generates a report — where a failure halfway through should resume from the last checkpoint, not start over.

In each case, the *structure* of the process is known ahead of time. The steps, their ordering, the decision points — these aren't things you want the model to figure out at runtime. You want to **define the graph explicitly** and let agents (or any other logic) execute within it.

That's what [**workflows**](../concepts/workflows/index.md) provide.

## The intelligence spectrum

Agent applications don't have to be fully autonomous or fully rule-based — there's a spectrum in between, and workflows let you choose where to land.

```
Fully intelligent                                              Fully deterministic
(model decides everything)                                     (code decides everything)
◄──────────────────────────────────────────────────────────────►
│                         │                         │
│  Single agent with      │  Workflow with agent    │  Workflow with only
│  tools — the model      │  executors — the graph  │  deterministic executors
│  picks every step       │  controls the process,  │  — no LLM involved,
│                         │  agents handle the      │  pure business logic
│                         │  reasoning-heavy steps  │
```

At the left end, a single agent with tools handles everything — the model decides what to do, when to delegate, and when to stop. This is the most flexible approach, but also the least predictable. At the right end, a workflow with purely deterministic executors is essentially a traditional pipeline — fully predictable, but with no AI reasoning at all.

Most real-world applications live **somewhere in the middle**. A workflow defines the structure — which steps run, in what order, with what gates — while individual executors within that workflow use agents for the steps that benefit from LLM reasoning. You get the predictability of an explicit process with the intelligence of AI where it matters.

The key insight is that **you control the dial**. For each step in your process, you decide:

- Should the **model** figure out what to do? → Use an [agent executor](../workflows/agents-in-workflows.md).
- Should the **code** determine the outcome? → Use a deterministic executor with regular business logic.
- Should a **human** make the call? → Use a [human-in-the-loop](../workflows/human-in-the-loop.md) gate.

This is the real power of workflows: not replacing agents, but giving you explicit control over **how much intelligence** goes into each part of your application.

## Choosing the right pattern

The patterns from earlier in this journey and workflows aren't competing approaches — they're different points on the spectrum. The key question is: **who should decide what happens next?**

| Question | If the answer is "the model" | If the answer is "the developer" |
|----------|------------------------------|----------------------------------|
| Which subtask to tackle next? | [Agents as tools](agents-as-tools.md) — the outer agent routes dynamically | [Workflows](../concepts/workflows/index.md) — the graph defines the path |
| Whether to involve another agent? | [Agents as tools](agents-as-tools.md) — model-driven delegation | [Agents in workflows](../workflows/agents-in-workflows.md) — the graph wires agents together |
| When to ask a human? | [Tool approval](../agents/tools/tool-approval.md) — reactive, per-tool | [Human-in-the-loop](../workflows/human-in-the-loop.md) — explicit gates at defined points |
| How to handle partial failure? | Retry logic in tool implementations | [Checkpoints](../workflows/checkpoints.md) — resume from the last saved state |

In practice, most production systems **combine both**. A workflow defines the high-level process, and individual executors within that workflow use agents for the steps that benefit from LLM reasoning. The [agents in workflows](../workflows/agents-in-workflows.md) page shows exactly how to do this.

## Built-in orchestration patterns

For common multi-agent coordination scenarios, Agent Framework provides [built-in orchestration patterns](../workflows/orchestrations/index.md) — prebuilt workflow templates that you can use directly or customize:

| Pattern | When to use it |
|---------|----------------|
| [**Sequential**](../workflows/orchestrations/sequential.md) | Agents execute one after another in a defined order — each builds on the previous agent's output |
| [**Concurrent**](../workflows/orchestrations/concurrent.md) | Agents execute in parallel — useful when tasks are independent and you want to reduce latency |
| [**Handoff**](../workflows/orchestrations/handoff.md) | Agents transfer control to each other based on context — good for routing to specialists |
| [**Group Chat**](../workflows/orchestrations/group-chat.md) | Agents collaborate in a shared conversation — useful for debate, review, or brainstorming |
| [**Magentic**](../workflows/orchestrations/magentic.md) | A manager agent dynamically coordinates specialized agents — balances structure with flexibility |

These orchestrations handle the boilerplate of agent coordination so you can focus on the agents themselves.

## Workflows as agents

One of the most powerful composition patterns is wrapping a workflow so it looks like a regular agent. The [workflows as agents](../workflows/as-agents.md) feature lets you take a complex multi-step workflow and expose it through the standard agent interface. Other agents can call it as a tool, A2A clients can invoke it over HTTP, and consumers don't need to know they're talking to a workflow at all.

## Journey recap

You've now seen the full spectrum of agent development patterns:

| Pattern | Best for |
|---------|----------|
| [LLM Fundamentals](llm-fundamentals.md) | Understanding the foundation |
| [From LLMs to Agents](from-llms-to-agents.md) | The agent abstraction |
| [Adding Tools](adding-tools.md) | Agents that act on external systems |
| [Adding Skills](adding-skills.md) | Reusable, modular agent behaviors |
| [Adding Middleware](adding-middleware.md) | Cross-cutting concerns and guardrails |
| [Context Providers](adding-context-providers.md) | Memory, personalization, and RAG |
| [Agents as Tools](agents-as-tools.md) | Simple agent composition and delegation |
| [Agent-to-Agent (A2A)](agent-to-agent.md) | Cross-service agent communication |
| [Workflows](workflows.md) | Complex, multi-step orchestration with explicit control |

Each pattern adds capability — and complexity. The best agent systems use the simplest pattern that meets their requirements, and reach for more powerful patterns only when the scenario demands it.

## Next steps

**Go deeper:**

- [Workflows](../concepts/workflows/index.md) — core concepts and architecture
- [Executors](../concepts/workflows/executors.md) and [Edges](../concepts/workflows/edges.md) — building blocks of the workflow graph
- [Agents in Workflows](../workflows/agents-in-workflows.md) — integrating AI agents into workflow steps
- [Orchestrations](../workflows/orchestrations/index.md) — prebuilt multi-agent patterns (sequential, concurrent, handoff, group chat, magentic)
- [Human-in-the-Loop](../workflows/human-in-the-loop.md) — approval gates and external input
- [Checkpoints & Resuming](../workflows/checkpoints.md) — long-running workflow recovery
- [State Management](../concepts/workflows/state.md) — sharing data across executors
- [Workflows as Agents](../workflows/as-agents.md) — exposing workflows through the agent interface
