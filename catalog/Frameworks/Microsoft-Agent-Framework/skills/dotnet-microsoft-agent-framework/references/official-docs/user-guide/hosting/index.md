---
title: Hosting Overview
description: Learn how to host AI agents in ASP.NET Core applications using the Agent Framework hosting libraries.
author: dmkorolev
ms.service: agent-framework
ms.topic: overview
ms.date: 11/11/2025
ms.author: dmkorolev
---

# Hosting AI Agents in ASP.NET Core

The Agent Framework provides a comprehensive set of hosting libraries that enable you to seamlessly integrate AI agents into ASP.NET Core applications. These libraries simplify the process of registering, configuring, and exposing agents through various protocols and interfaces.

## Overview
As you may already know from the [AI Agents Overview](../../overview/agent-framework-overview.md#ai-agents), `AIAgent` is the fundamental concept of the Agent Framework. It defines an "LLM wrapper" that processes user inputs, makes decisions, calls tools, and performs additional work to execute actions and generate responses.

However, exposing AI agents from your ASP.NET Core application is not trivial. The Agent Framework hosting libraries solve this by registering AI agents in a dependency injection container, allowing you to resolve and use them in your application services. Additionally, the hosting libraries enable you to manage agent dependencies, such as tools and thread storage, from the same dependency injection container.

Agents can be hosted alongside your application infrastructure, independent of the protocols they use. Similarly, workflows can be hosted and leverage your application's common infrastructure.

## Core Hosting Library

The `Microsoft.Agents.AI.Hosting` library is the foundation for hosting AI agents in ASP.NET Core. It provides the primary APIs for agent registration and configuration.

In the context of ASP.NET Core applications, `IHostApplicationBuilder` is the fundamental type that represents the builder for hosted applications and services. It manages configuration, logging, lifetime, and more. The Agent Framework hosting libraries provide extensions for `IHostApplicationBuilder` to register and configure AI agents and workflows.

### Key APIs

Before configuring agents or workflows, developer needs the `IChatClient` registered in the dependency injection container.
In the examples below, it is registered as keyed singleton under name `chat-model`. This is an example of `IChatClient` registration:
```csharp
// endpoint is of 'https://<your-own-foundry-endpoint>.openai.azure.com/' format
// deploymentName is `gpt-4o-mini` for example

IChatClient chatClient = new AzureOpenAIClient(
        new Uri(endpoint),
        new DefaultAzureCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient();
builder.Services.AddSingleton(chatClient);
```

#### AddAIAgent

Register an AI agent with dependency injection:

```csharp
var pirateAgent = builder.AddAIAgent(
    "pirate",
    instructions: "You are a pirate. Speak like a pirate",
    description: "An agent that speaks like a pirate.",
    chatClientServiceKey: "chat-model");
```

The `AddAIAgent()` method returns an `IHostedAgentBuilder`, which provides a set of extension methods for configuring the `AIAgent`.
For example, you can add tools to the agent:
```csharp
var pirateAgent = builder.AddAIAgent("pirate", instructions: "You are a pirate. Speak like a pirate")
    .WithAITool(new MyTool()); // MyTool is a custom type derived from `AITool`
```

You can also configure the thread store (storage for conversation data):
```csharp
var pirateAgent = builder.AddAIAgent("pirate", instructions: "You are a pirate. Speak like a pirate")
    .WithInMemoryThreadStore();
```

#### AddWorkflow

Register workflows that coordinate multiple agents. A workflow is essentially a "graph" where each node is an `AIAgent`, and the agents communicate with each other.

In this example, we register two agents that work sequentially. The user input is first sent to `agent-1`, which produces a response and sends it to `agent-2`. The workflow then outputs the final response. There is also a `BuildConcurrent` method that creates a concurrent agent workflow.

```csharp
builder.AddAIAgent("agent-1", instructions: "you are agent 1!");
builder.AddAIAgent("agent-2", instructions: "you are agent 2!");

var workflow = builder.AddWorkflow("my-workflow", (sp, key) =>
{
    var agent1 = sp.GetRequiredKeyedService<AIAgent>("agent-1");
    var agent2 = sp.GetRequiredKeyedService<AIAgent>("agent-2");
    return AgentWorkflowBuilder.BuildSequential(key, [agent1, agent2]);
});
```

#### Expose Workflow as AIAgent

`AIAgent`s benefit from integration APIs that expose them via well-known protocols (such as A2A, OpenAI, and others):
- [OpenAI Integration](openai-integration.md) - Expose agents via OpenAI-compatible APIs
- [A2A Integration](agent-to-agent-integration.md) - Enable agent-to-agent communication

Currently, workflows do not provide similar integration capabilities. To use these integrations with a workflow, you can convert the workflow into a standalone agent that can be used like any other agent:

```csharp
var workflowAsAgent = builder
    .AddWorkflow("science-workflow", (sp, key) => { ... })
    .AddAsAIAgent();  // Now the workflow can be used as an agent
```

## Implementation Details

The hosting libraries act as protocol adapters that bridge the gap between external communication protocols and the Agent Framework's internal `AIAgent` implementation. When you use a hosting integration library (such as OpenAI Responses or A2A), the library retrieves the registered `AIAgent` from dependency injection and wraps it with protocol-specific middleware. This middleware handles the translation of incoming requests from the external protocol format into Agent Framework models, invokes the `AIAgent` to process the request, and then translates the agent's response back into the protocol's expected output format. This architecture allows you to use public communication protocols seamlessly with `AIAgent` while keeping your agent implementation protocol-agnostic and focused on business logic.

## Hosting Integration Libraries

The Agent Framework includes specialized hosting libraries for different integration scenarios:

- [OpenAI Integration](openai-integration.md) - Expose agents via OpenAI-compatible APIs
- [A2A Integration](agent-to-agent-integration.md) - Enable agent-to-agent communication

## See Also

- [AI Agents Overview](../../overview/agent-framework-overview.md)
- [Workflows](../../user-guide/workflows/overview.md)
- [Tools and Capabilities](../../tutorials/agents/function-tools.md)