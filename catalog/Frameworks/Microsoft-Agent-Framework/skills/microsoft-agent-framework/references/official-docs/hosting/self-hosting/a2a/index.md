---
title: Self-host A2A agents
description: Choose an opinionated A2A executor, an app-owned adapter, or conversion helpers for Agent Framework agents and workflows.
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 07/23/2026
ms.service: agent-framework
---

# Self-host A2A agents

:::zone pivot="programming-language-csharp"

Use the .NET A2A hosting packages to expose an Agent Framework agent through ASP.NET Core. See [Host agents with A2A](server.md) for a complete multi-language server guide.

:::zone-end

:::zone pivot="programming-language-go"

Use the Go `provider/a2aprovider` package with the official A2A Go server handlers. See [Host agents with A2A](server.md) for a complete server example.

:::zone-end

:::zone pivot="programming-language-python"

Agent Framework provides two Python packages for hosting agents and workflows through the official [A2A SDK](https://pypi.org/project/a2a-sdk/):

| Package | Integration model | Use it when |
|---|---|---|
| `agent-framework-a2a` | An opinionated `A2AExecutor` that converts requests, runs an agent, and publishes A2A task events and artifacts. | You want the standard Agent Framework-to-A2A behavior and only need to assemble the A2A SDK server. |
| `agent-framework-hosting-a2a` | Incremental building blocks for an app-owned executor. Start with the foundational agent or workflow converters, and optionally use `AgentA2AAdapter` or `WorkflowA2AAdapter`, which build on those converters to add native card generation and mode validation. | Your application needs to own session mapping, task transitions, event delivery, artifact boundaries, output conversion, or a multi-protocol host. |

Both packages use native A2A SDK types and server components. Your application supplies the request handler, task store, routes or SDK application builder, authentication, and deployment. With `agent-framework-hosting-a2a`, the application can construct the agent card directly or let an adapter generate it.

## Use the opinionated A2A executor

Install `agent-framework-a2a` when the built-in server adapter matches your lifecycle:

```bash
pip install --pre agent-framework-a2a starlette uvicorn
```

`A2AExecutor` implements the A2A SDK's `AgentExecutor`. It reads the user input from the A2A request context, creates an Agent Framework session from the A2A context ID, runs the agent in streaming or non-streaming mode, converts supported output content, and publishes task status and artifact events through the SDK's `TaskUpdater`.

Compose it with the A2A SDK's `DefaultRequestHandler`, task store, agent card, and Starlette application or another supported server integration. Configure streaming with `A2AExecutor(agent, stream=True)`, pass stable agent run options through `run_kwargs`, or subclass `A2AExecutor` and override `handle_events` when you need a different output mapping.

`A2AExecutor` is scoped to an A2A endpoint and manages its A2A execution and session mapping directly. Use the hosting packages when the same agent must be available through several protocols in one application.

For the complete server setup, see [Expose an Agent Framework agent over A2A](server.md#exposing-an-agent-framework-agent-over-a2a).

## Use an adapter in an app-owned executor

Install the hosting package when your application owns the native A2A executor but wants Agent Framework to generate the public card and validate conversions:

```bash
pip install --pre agent-framework-hosting-a2a starlette uvicorn
```

`AgentA2AAdapter` accepts an agent or `AgentState`. Its asynchronous `get_card` method derives the public name and description, uses conservative text modes by default, and can infer native A2A skills from Agent Framework `SkillsProvider` instances. Server capabilities and supported interfaces remain explicit because they describe the application endpoint rather than the agent's `run` method.

The adapter exposes `a2a_to_run` and `a2a_from_run` methods that validate values against the configured card modes by default. The application still owns the A2A executor, task lifecycle, event queue, artifact boundaries, session policy, authentication, routes, and deployment.

This executor uses one adapter for inbound conversion, agent state, and outbound conversion:

:::code language="python" source="~/../agent-framework-code/python/samples/04-hosting/a2a/agent_framework_to_a2a.py" range="33-100":::

The server setup creates the adapter, generates its native `AgentCard`, and composes the app-owned executor with the A2A SDK request handler:

:::code language="python" source="~/../agent-framework-code/python/samples/04-hosting/a2a/agent_framework_to_a2a.py" range="103-138":::

## Build an app-owned A2A executor

Use the standalone hosting helpers when your application also needs direct control over card creation:

```bash
pip install --pre agent-framework-hosting-a2a starlette uvicorn
```

The helpers are framework-neutral:

- `a2a_to_run` converts an A2A `Message` to Agent Framework run arguments.
- `a2a_from_run` converts Agent Framework responses and streaming updates to A2A `Part` values.

Your executor selects session keys and owns task transitions, event queues, artifact IDs, message boundaries, and outbound delivery. `a2a_from_run` returns a flat part list so the application can group those parts into A2A messages or artifacts and apply message-level metadata.

The hosting setup also supports multi-protocol applications. Share the same agent target and `AgentState` infrastructure across A2A, OpenAI Responses, Telegram, and MCP routes, while each protocol endpoint keeps its own conversion, authorization, and session-key policy. This lets clients reach one agent through different protocols at the same time without creating a separate agent deployment for each endpoint.

Compose the helpers in a native A2A SDK executor. This sample creates and updates A2A tasks, converts the inbound message into an Agent Framework run, persists the updated `AgentState` session after the stream finishes, and publishes returned parts as artifacts.

:::code language="python" source="~/../agent-framework-code/python/samples/04-hosting/a2a/a2a_server.py" range="57-124":::

The sample uses Starlette and Uvicorn, but the helpers are not tied to either. Use your application framework or an A2A SDK application builder to serve the A2A agent card and JSON-RPC routes:

:::code language="python" source="~/../agent-framework-code/python/samples/04-hosting/a2a/a2a_server.py" range="171-192":::

## Host a workflow with an adapter

`WorkflowA2AAdapter` provides the same card-generation and conversion boundary for a workflow or `WorkflowState`. It infers conservative input and output modes from the workflow's declared types, or you can supply explicit modes for an application-specific representation.

The standalone `a2a_to_workflow_run` and `a2a_from_workflow_run` helpers provide typed workflow input and output conversion. The adapter exposes them as asynchronous `a2a_to_run` and synchronous `a2a_from_run` methods that validate against its effective card modes. Input conversion accepts one A2A text, raw, or data part for the workflow's single start-executor input type, and output conversion maps completed public workflow outputs to native A2A parts. Call `get_card` before validated output conversion when the adapter must infer output modes.

The application remains responsible for the native A2A executor and for streaming progress, task status, artifacts, checkpoints, and human-in-the-loop continuation. Pending human-input requests aren't converted automatically, so the host must implement its own continuation policy.

## Secure sessions and task state

`A2AExecutor` uses the A2A context ID as the Agent Framework session ID. The adapter-based and helper-based samples combine the A2A tenant and context ID to demonstrate an application-selected mapping. In every approach, a production host must authenticate the caller before it reaches the A2A request handler, derive the tenant and subject from that trusted identity, and authorize all task, context, continuation, and cancellation IDs.

> [!IMPORTANT]
> The A2A SDK's default task and push-configuration stores are in-memory and scope ownership by user name. For a multi-tenant service, use an `owner_resolver` that derives ownership from the same trusted tenant and subject, and use durable task and session stores when replicas can restart or scale out.

For a complete helper-based server and multi-agent examples, see the [A2A hosting samples](https://github.com/microsoft/agent-framework/tree/main/python/samples/04-hosting/a2a). For A2A clients and protocol capabilities, see the [A2A agent service](../../../integrations/by-component/agent-services/a2a.md).

## Next steps

> [!div class="nextstepaction"]
> [Host agents with A2A](server.md)

**Go deeper:**

- [Self-hosting overview](../index.md)
- [OpenAI Responses](../responses.md)
- [Telegram](../telegram.md)

:::zone-end
