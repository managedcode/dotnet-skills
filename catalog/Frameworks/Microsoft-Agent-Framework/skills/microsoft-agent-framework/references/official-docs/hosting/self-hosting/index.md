---
title: Self-host Agent Framework applications
description: Build an application-owned server and add one or more Agent Framework protocols.
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 07/22/2026
ms.service: agent-framework
---

# Self-host Agent Framework applications

:::zone pivot="programming-language-csharp"

> [!NOTE]
> Self-hosting protocol helpers for .NET are coming soon. The hosting model will let your application own its server, state, and protocol integrations.

:::zone-end

:::zone pivot="programming-language-go"

> [!NOTE]
> Self-hosting protocol helpers are not currently available for Go.

:::zone-end

:::zone pivot="programming-language-python"

Self-hosting lets you run an Agent Framework agent or workflow in your own web application, container, service, or runtime. Your application controls routing, identity, authorization, request policy, storage, deployment, and scaling. Add one or more protocol integrations to that server based on the clients you need to support.

Use this option when you need to integrate an agent endpoint with your existing application infrastructure. If you want Microsoft Foundry to run the agent for you, see [Foundry Hosted Agents](../foundry-hosted-agent.md). If you need Azure Functions triggers or durable execution, see [Durable Extension](../azure-functions.md).

The design of these packages is such that is allows for maximum flexibility for the developer. This means that if you want to build a host that exposes a agent with the Responses API, and abuse the parameters for other purposes (i.e. map `temperature` to `top_p`), you can do that. If you don't want to store sessions, you can do that, if you want to allow the caller to control the full agent run, you can do that too. We will not get in the way, we provide helpers for the common cases, and make you responsible for the rest, to allow you to build the exact host that you need.

> [!IMPORTANT]
> `agent-framework-hosting`, `agent-framework-hosting-responses`, `agent-framework-hosting-telegram`, `agent-framework-a2a`, `agent-framework-hosting-a2a`, and `agent-framework-hosting-mcp` are prerelease Python packages. Install prerelease versions explicitly and review release notes before updating a production deployment.

```bash
pip install --pre agent-framework-hosting
```

## What the hosting helpers provide

The generic hosting package provides shared execution state for an application-owned server:

- `AgentState` pairs an agent target with a `SessionStore` and creates sessions when the application selects a new key.
- `SessionStore` stores, retrieves, and deletes sessions by an application-selected ID. Its default store is process-local and has no eviction policy.
- `WorkflowState` resolves a workflow target. Your application owns checkpoint storage and any mapping from a client continuation ID to a checkpoint.

`AgentState` is not a server or protocol registry. Your application selects an authorized session key, resolves the target, and saves the post-run state. It can use the same target and shared application infrastructure for one or several protocol endpoints.

## Customize session storage

`SessionStore` is a small async storage class with `get`, `set`, and `delete` methods. The default implementation keeps sessions in process memory. Subclass it and override those methods to store `AgentSession` objects in Redis, a database, blob storage, or another application-owned store, then pass the instance to `AgentState(session_store=...)`.

`SessionStore` and [history providers](../../concepts/agents/conversations/storage.md) persist separate parts of an agent conversation. A session store saves one session object per session ID, including session metadata and provider state. A dedicated `HistoryProvider` stores the conversation separately, typically as one record per message. This separation is recommended for durable hosts because appending individual messages is generally more efficient than rewriting a growing session object after every turn. A history provider is defined per agent, by passing the desired history provider class to the `context_providers` parameter.

> [!NOTE]
> The default history provider: `InMemoryHistoryProvider` is the exception: it stores the full conversation in `AgentSession.state`. When that provider is used, `SessionStore` persists the conversation inside the session object. For longer conversations or production storage, use a dedicated history provider so the session store can remain focused on lightweight session state.

## Bring your own framework or client library

The hosting packages aren't tied to a web framework or client library. The samples use FastAPI and `aiogram` because they provide concise runnable examples, not because the helpers require them.

- For HTTP endpoints, use the routing and request/response APIs of your application framework, such as FastAPI, Starlette, Django, Flask, Azure Functions, or another framework.
- For protocol clients such as Telegram, use any client library that can supply a protocol update and execute the operations produced by the helper.

The application selects its framework and client library; the Agent Framework packages only convert protocol data and manage optional execution state. They don't register routes, authenticate callers, authorize access to state, choose allowed model options, or provide durable storage.

## Add protocols to your server

Choose one or more protocol integrations:

| Protocol | Package and integration |
|---|---|---|
| [OpenAI Responses](responses.md) | `agent-framework-hosting-responses` |
| [Telegram](telegram.md) | `agent-framework-hosting-telegram` |
| [A2A](a2a/index.md) | `agent-framework-a2a` or `agent-framework-hosting-a2a` |
| [MCP](mcp.md) | `agent-framework-hosting-mcp` |

Each protocol page describes its setup. However they are designed to allow you to build a single host with one or more protocols enabled and a callable target; either an agent or a workflow. Since we do not limit you to one web framework, you can choose the one you want, and setup the host with those protocols with ease.

## Secure session continuation

Treat every protocol-provided identifier as untrusted input. Before using an ID to load a session, checkpoint, task, or other state:

1. Authenticate the caller.
2. Authorize the caller to access the referenced state.
3. Partition durable state by the authenticated tenant, user, or workspace.
4. Persist session and checkpoint state only after the run or stream has completed.

This self-hosting pattern lets your application implement only the protocol endpoints and policies it needs; it doesn't attempt to implement the complete API surface of every supported protocol.

## Next steps

> [!div class="nextstepaction"]
> [Add the OpenAI Responses protocol](responses.md)

**Go deeper:**

- [Telegram](telegram.md)
- [A2A](a2a/index.md)
- [MCP](mcp.md)
- [Foundry Hosted Agents](../foundry-hosted-agent.md)

:::zone-end
