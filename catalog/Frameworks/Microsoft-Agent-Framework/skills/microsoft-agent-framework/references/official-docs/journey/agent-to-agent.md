---
title: Agent-to-Agent (A2A)
description: Enable agents to communicate across service and organizational boundaries using the A2A protocol.
author: TaoChenOSU
ms.topic: article
ms.author: taochen
ms.date: 04/06/2026
ms.service: agent-framework
---

# Agent-to-Agent (A2A)

The [previous page](agents-as-tools.md) showed how to compose agents within a single process — one agent calls another as a function tool, and the framework handles the rest. That pattern works well when all your agents live in the same application, share the same runtime, and are maintained by the same team.

But real-world agent systems often need to communicate across boundaries. **Agent-to-Agent (A2A)** is an [open protocol](https://a2a-protocol.org/latest/) designed for exactly this. It defines a standard way for agents to discover each other, exchange messages, and coordinate on tasks — over HTTP, across any boundary, in any language or framework. Agent Framework provides an [A2A agent service](../integrations/by-component/agent-services/a2a.md) for calling remote agents and [A2A hosting](../hosting/self-hosting/a2a/server.md) for exposing agents.

## When to use this

Use A2A when your agents need to cross a boundary that in-process composition can't handle:

- **Service boundaries.** Your travel-booking agent runs as a microservice, and your expense-filing agent runs as another. They can't call each other as in-process function tools — they need a network protocol.
- **Team boundaries.** A partner team owns a "compliance-review" agent. You don't have access to their code, their model, or their deployment — you just need to send it a request and get a response.
- **Organizational boundaries.** A third-party provider offers a specialized agent (document processing, legal review, medical triage). You need a standard way to discover it, understand what it can do, and communicate with it — regardless of what framework or language it's built with.
- **Independent evolution.** Your agents need different release cycles, different teams, or different languages — without tightly coupling their implementations.

> [!TIP]
> If your agents all live in the same process and are maintained by the same team, [agents as tools](agents-as-tools.md) is simpler and has less overhead. A2A adds value when you cross a process, service, or organizational boundary.

## Considerations

| Consideration | Details |
|---------------|---------|
| **Interoperability** | A2A is framework-agnostic. Your .NET agent can call a Python agent, a LangChain agent, or any agent that implements the protocol. This is A2A's primary value — it's the "HTTP of agent communication." |
| **Network overhead** | Every A2A call is an HTTP request. This adds latency compared to in-process agent-as-tool calls. For performance-sensitive paths, keep agents co-located or use A2A only where a boundary truly exists. |
| **Operational complexity** | Remote agents are distributed services. You need to handle network failures, timeouts, retries, and versioning — the same concerns you'd have with any service-to-service communication. |
| **Discovery at runtime** | Agent cards make discovery dynamic, but you still need to know where to look. In production, you'll typically configure known agent endpoints or use a registry. |
| **Conversation state** | The remote agent manages its own conversation state (keyed by context ID). Your agent doesn't see the remote agent's internal reasoning — only its responses. If the remote agent restarts and loses state, your conversation context may be lost. |

## Next steps

Now that your agents can communicate across any boundary, the final step in the journey is **workflows** — explicit, graph-based orchestration for multi-step, multi-agent processes where you need full control over execution order, state, and recoverability.

> [!div class="nextstepaction"]
> [Workflows](workflows.md)

**Go deeper:**

- [A2A agent service](../integrations/by-component/agent-services/a2a.md) — discover and invoke remote A2A agents
- [A2A hosting](../hosting/self-hosting/a2a/server.md) — expose Agent Framework agents through A2A
- [Agents as Tools](agents-as-tools.md) — the simpler in-process composition pattern
