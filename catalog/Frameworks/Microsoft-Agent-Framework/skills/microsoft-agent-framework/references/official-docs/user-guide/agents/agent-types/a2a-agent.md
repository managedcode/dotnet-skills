---
title: A2A Agents
description: Learn how to use Microsoft Agent Framework with a remote A2A service.
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: tutorial
ms.author: westey
ms.date: 09/24/2025
ms.service: agent-framework
---

# A2A Agents

Microsoft Agent Framework supports using a remote agent that is exposed via the A2A protocol in your application using the same `AIAgent` abstraction as any other agent.

::: zone pivot="programming-language-csharp"

## Getting Started

Add the required NuGet packages to your project.

```dotnetcli
dotnet add package Microsoft.Agents.AI.A2A --prerelease
```

## Create an A2A Agent using the well known agent card location

The following scenario uses the well-known agent card location.
It passes the root URI of the A2A agent host to the `A2ACardResolver` constructor, and the resolver will look for the agent card at `https://your-a2a-agent-host/.well-known/agent-card.json`.

First, create an `A2ACardResolver` with the URI of the remote A2A agent host.

```csharp
using System;
using A2A;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.A2A;

A2ACardResolver agentCardResolver = new(new Uri("https://your-a2a-agent-host"));
```

Create an instance of the `AIAgent` for the remote A2A agent using the `GetAIAgentAsync` helper method.

```csharp
AIAgent agent = await agentCardResolver.GetAIAgentAsync();
```

## Create an A2A Agent using the Direct Configuration / Private Discovery mechanism

It's also possible to point directly at the agent URL if it's known. This can be useful for tightly coupled systems, private agents, or development purposes, where clients are directly configured with Agent Card information and agent URL.

In this case, you construct an `A2AClient` directly with the URL of the agent.

```csharp
A2AClient a2aClient = new(new Uri("https://your-a2a-agent-host/echo"));
```

And then you can create an instance of the `AIAgent` using the `AsAIAgent` method.

```csharp
AIAgent agent = a2aClient.AsAIAgent();
```

## Using the Agent

The agent is a standard `AIAgent` and supports all standard agent operations.

For more information on how to run and interact with agents, see the [Agent getting started tutorials](../../../tutorials/overview.md).

::: zone-end
::: zone pivot="programming-language-python"

## Getting Started

Add the required Python packages to your project.

```bash
pip install agent-framework-a2a --pre
```

## Create an A2A Agent

The following scenario uses the well-known agent card location.
It passes the base URL of the A2A agent host to the `A2ACardResolver` constructor, and the resolver looks for the agent card at `https://your-a2a-agent-host/.well-known/agent.json`.

First, create an `A2ACardResolver` with the URL of the remote A2A agent host.

```python
import httpx
from a2a.client import A2ACardResolver

# Create httpx client for HTTP communication
async with httpx.AsyncClient(timeout=60.0) as http_client:
    resolver = A2ACardResolver(httpx_client=http_client, base_url="https://your-a2a-agent-host")
```

Get the agent card and create an instance of the `A2AAgent` for the remote A2A agent.

```python
from agent_framework.a2a import A2AAgent

# Get agent card from the well-known location
agent_card = await resolver.get_agent_card(relative_card_path="/.well-known/agent.json")

# Create A2A agent instance
agent = A2AAgent(
    name=agent_card.name,
    description=agent_card.description,
    agent_card=agent_card,
    url="https://your-a2a-agent-host"
)
```

## Create an A2A Agent using URL

It's also possible to point directly at the agent URL if it's known. This can be useful for tightly coupled systems, private agents, or development purposes, where clients are directly configured with Agent Card information and agent URL.

In this case, you construct an `A2AAgent` directly with the URL of the agent.

```python
from agent_framework.a2a import A2AAgent

# Create A2A agent with direct URL configuration
agent = A2AAgent(
    name="My A2A Agent",
    description="A directly configured A2A agent",
    url="https://your-a2a-agent-host/echo"
)
```

## Using the Agent

The A2A agent supports all standard agent operations.

For more information on how to run and interact with agents, see the [Agent getting started tutorials](../../../tutorials/overview.md).

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Custom Agent](./custom-agent.md)
