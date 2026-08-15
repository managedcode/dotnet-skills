---
title: Agent concepts
description: Understand Agent Framework agent types, runtime execution, conversations, middleware, and safety.
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: overview
ms.author: edvan
ms.date: 07/30/2026
ms.service: agent-framework
---

<!--
  Language parity table - keep in sync when adding/removing sections.

  | Section            | C# | Python | Go | Notes                         |
  |--------------------|:--:|:------:|:--:|:-------------------------------|
  | Runtime and types  | ✅ |   ✅   | ✅ | Shared                         |
  | Chat-client agents | ✅ |   ✅   | ✅ | SDK-specific construction     |
  | Conversations      | ✅ |   ✅   | ✅ | Shared                         |
  | Middleware         | ✅ |   ✅   | ✅ | Shared                         |
  | Safety             | ✅ |   ✅   | ✅ | Shared                         |
-->

# Agent concepts

An Agent Framework agent combines an agent abstraction, a model or remote-agent connection, instructions, tools, middleware, context providers, and session state behind a consistent run interface.

## Runtime and execution

- [Running agents](running-agents.md) explains regular and streaming runs, run options, responses, messages, and content.
- [Agent pipeline](agent-pipeline.md) explains how middleware, context providers, model invocation, and tools participate in a run.

## Agent types

- [Custom agents](custom-agents.md) explains the common agent interface and when to implement an agent directly.
- [Model providers](../../integrations/by-component/model-providers/index.md) connect application-owned agents to inference services.
- [Agent services](../../integrations/by-component/agent-services/index.md) connect to managed or protocol-backed remote agents.

## Chat-client agents

Chat-client agents are application-owned agents backed by a model inference client. They support function tools, multi-turn conversations, provider-hosted tools where available, structured outputs, streaming, middleware, and local or service-managed conversation history.

::: zone pivot="programming-language-csharp"

Any inference client that implements [`Microsoft.Extensions.AI.IChatClient`](/dotnet/ai/microsoft-extensions-ai#the-ichatclient-interface) can back a `ChatClientAgent`:

```csharp
using Microsoft.Agents.AI;

AIAgent agent = new ChatClientAgent(
    chatClient,
    instructions: "You are a helpful assistant.");
```

All agent implementations share the `AIAgent` abstraction, so application code and orchestrations can work with chat-client agents, custom agents, and remote-agent proxies through one interface.

For provider capabilities, conversation-history support, and SDK endpoint selection, see [Model providers](../../integrations/by-component/model-providers/index.md).

::: zone-end

::: zone pivot="programming-language-python"

Create a standard `Agent` from any client that implements `SupportsChatGetResponse`:

```python
from agent_framework import Agent

agent = Agent(
    client=client,
    instructions="You are a helpful assistant.",
)
```

The same `Agent` interface works across supported model providers. Direct agent types such as `FoundryAgent`, `A2AAgent`, `GitHubCopilotAgent`, and `ClaudeAgent` connect to managed or remote agent runtimes instead.

For available inference clients, see [Model providers](../../integrations/by-component/model-providers/index.md).

::: zone-end

::: zone pivot="programming-language-go"

Go model-provider packages construct the standard `*agent.Agent` type through provider-specific constructors. This gives application code a consistent run, session, tool, middleware, and streaming interface while each provider owns client initialization.

For constructors and import paths, see [Model providers](../../integrations/by-component/model-providers/index.md).

::: zone-end

## Conversations and memory

[Conversation concepts](conversations/index.md) cover sessions, context providers, storage, compaction, and the built-in chat-history memory provider.

## Middleware

[Middleware concepts](middleware/index.md) cover definition, scope, ordering, shared state, run-time context, termination, errors, and result overrides.

## Safety

[Agent safety](safety.md) describes design patterns for constraining agent behavior and reducing operational risk. Security enforcement with FIDES remains an [Agent Capability](../../agents/security.md).

## Next steps

> [!div class="nextstepaction"]
> [Learn how to run agents](running-agents.md)
