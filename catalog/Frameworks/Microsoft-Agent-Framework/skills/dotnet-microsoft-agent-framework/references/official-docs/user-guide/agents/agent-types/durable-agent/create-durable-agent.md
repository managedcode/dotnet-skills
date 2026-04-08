---
title: Durable Agents
description: Learn how to use the durable task extension for Microsoft Agent Framework to build stateful AI agents with serverless hosting.
zone_pivot_groups: programming-languages
author: anthonychu
ms.topic: tutorial
ms.author: antchu
ms.date: 11/05/2025
ms.service: agent-framework
---

# Durable Agents

The durable task extension for Microsoft Agent Framework enables you to build stateful AI agents and multi-agent deterministic orchestrations in a serverless environment on Azure.

[Azure Functions](/azure/azure-functions/functions-overview) is a serverless compute service that lets you run code on-demand without managing infrastructure. The durable task extension for Microsoft Agent Framework builds on this foundation to provide durable state management, meaning your agent's conversation history and execution state are reliably persisted and survive failures, restarts, and long-running operations.

The extension manages agent thread state and orchestration coordination, allowing you to focus on your agent logic instead of infrastructure concerns for reliability.

## Key Features

The durable task extension provides the following key features:

- **Serverless hosting**: Deploy and host agents in Azure Functions with automatically generated HTTP endpoints for agent interactions
- **Stateful agent threads**: Maintain persistent threads with conversation history that survive across multiple interactions
- **Deterministic orchestrations**: Coordinate multiple agents reliably with fault-tolerant workflows that can run for days or weeks, supporting sequential, parallel, and human-in-the-loop patterns
- **Observability and debugging**: Visualize agent conversations, orchestration flows, and execution history through the built-in Durable Task Scheduler dashboard

## Getting Started

::: zone pivot="programming-language-csharp"

In a .NET Azure Functions project, add the required NuGet packages.

```bash
dotnet add package Azure.AI.OpenAI --prerelease
dotnet add package Azure.Identity
dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
dotnet add package Microsoft.Agents.AI.Hosting.AzureFunctions --prerelease
```

> [!NOTE]
> In addition to these packages, ensure your project uses version 2.2.0 or later of the [Microsoft.Azure.Functions.Worker](https://www.nuget.org/packages/Microsoft.Azure.Functions.Worker/) package.

::: zone-end

::: zone pivot="programming-language-python"

In a Python Azure Functions project, install the required Python packages.

```bash
pip install azure-identity
pip install agent-framework-azurefunctions --pre
```

::: zone-end

## Serverless Hosting

With the durable task extension, you can deploy and host Microsoft Agent Framework agents in Azure Functions with built-in HTTP endpoints and orchestration-based invocation. Azure Functions provides event-driven, pay-per-invocation pricing with automatic scaling and minimal infrastructure management.

When you configure a durable agent, the durable task extension automatically creates HTTP endpoints for your agent and manages all the underlying infrastructure for storing conversation state, handling concurrent requests, and coordinating multi-agent workflows.

::: zone pivot="programming-language-csharp"

```csharp
using System;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AzureFunctions;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? "gpt-4o-mini";

// Create an AI agent following the standard Microsoft Agent Framework pattern
AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions: "You are good at telling jokes.",
        name: "Joker");

// Configure the function app to host the agent with durable thread management
// This automatically creates HTTP endpoints and manages state persistence
using IHost app = FunctionsApplication
    .CreateBuilder(args)
    .ConfigureFunctionsWebApplication()
    .ConfigureDurableAgents(options =>
        options.AddAIAgent(agent)
    )
    .Build();
app.Run();
```

::: zone-end

::: zone pivot="programming-language-python"

```python
import os
from agent_framework.azure import AzureOpenAIChatClient, AgentFunctionApp
from azure.identity import DefaultAzureCredential

endpoint = os.getenv("AZURE_OPENAI_ENDPOINT")
deployment_name = os.getenv("AZURE_OPENAI_DEPLOYMENT_NAME", "gpt-4o-mini")

# Create an AI agent following the standard Microsoft Agent Framework pattern
agent = AzureOpenAIChatClient(
    endpoint=endpoint,
    deployment_name=deployment_name,
    credential=DefaultAzureCredential()
).as_agent(
    instructions="You are good at telling jokes.",
    name="Joker"
)

# Configure the function app to host the agent with durable thread management
# This automatically creates HTTP endpoints and manages state persistence
app = AgentFunctionApp(agents=[agent])
```

::: zone-end

### When to Use Durable Agents

Choose durable agents when you need:

- **Full code control**: Deploy and manage your own compute environment while maintaining serverless benefits
- **Complex orchestrations**: Coordinate multiple agents with deterministic, reliable workflows that can run for days or weeks
- **Event-driven orchestration**: Integrate with Azure Functions triggers (HTTP, timers, queues, etc.) and bindings for event-driven agent workflows
- **Automatic conversation state**: Agent conversation history is automatically managed and persisted without requiring explicit state handling in your code

This serverless hosting approach differs from managed service-based agent hosting (such as Azure AI Foundry Agent Service), which provides fully managed infrastructure without requiring you to deploy or manage Azure Functions apps. Durable agents are ideal when you need the flexibility of code-first deployment combined with the reliability of durable state management.

When hosted in the [Azure Functions Flex Consumption](/azure/azure-functions/flex-consumption-plan) hosting plan, agents can scale to thousands of instances or to zero instances when not in use, allowing you to pay only for the compute you need.

## Stateful Agent Threads with Conversation History

Agents maintain persistent threads that survive across multiple interactions. Each thread is identified by a unique thread ID and stores the complete conversation history in durable storage managed by the [Durable Task Scheduler](/azure/azure-functions/durable/durable-task-scheduler/durable-task-scheduler).

This pattern enables conversational continuity where agent state is preserved through process crashes and restarts, allowing full conversation history to be maintained across user threads. The durable storage ensures that even if your Azure Functions instance restarts or scales to a different instance, the conversation seamlessly continues from where it left off.

The following example demonstrates multiple HTTP requests to the same thread, showing how conversation context persists:

```bash
# First interaction - start a new thread
curl -X POST https://your-function-app.azurewebsites.net/api/agents/Joker/run \
  -H "Content-Type: text/plain" \
  -d "Tell me a joke about pirates"

# Response includes thread ID in x-ms-thread-id header and joke as plain text
# HTTP/1.1 200 OK
# Content-Type: text/plain
# x-ms-thread-id: @dafx-joker@263fa373-fa01-4705-abf2-5a114c2bb87d
#
# Why don't pirates shower before they walk the plank? Because they'll just wash up on shore later!

# Second interaction - continue the same thread with context
curl -X POST "https://your-function-app.azurewebsites.net/api/agents/Joker/run?thread_id=@dafx-joker@263fa373-fa01-4705-abf2-5a114c2bb87d" \
  -H "Content-Type: text/plain" \
  -d "Tell me another one about the same topic"

# Agent remembers the pirate context from the first message and responds with plain text
# What's a pirate's favorite letter? You'd think it's R, but it's actually the C!
```

Agent state is maintained in durable storage, enabling distributed execution across multiple instances. Any instance can resume an agent's execution after interruptions or failures, ensuring continuous operation.

## Next Steps

Learn about advanced capabilities of the durable task extension:

> [!div class="nextstepaction"]
> [Durable Agent Features](features.md)

For a step-by-step tutorial on building and running a durable agent:

> [!div class="nextstepaction"]
> [Create and run a durable agent](../../../../tutorials/agents/create-and-run-durable-agent.md)

## Related Content

- [Durable Task Scheduler Overview](/azure/azure-functions/durable/durable-task-scheduler/durable-task-scheduler)
- [Azure Functions Flex Consumption Plan](/azure/azure-functions/flex-consumption-plan)
- [Microsoft Agent Framework Overview](../../../../overview/agent-framework-overview.md)
