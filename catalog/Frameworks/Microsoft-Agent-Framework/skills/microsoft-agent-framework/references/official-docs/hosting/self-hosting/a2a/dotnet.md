---
title: A2A Hosting
description: Learn how to host Agent Framework agents via the A2A protocol in ASP.NET Core.
author: sergeymenshykh
ms.topic: tutorial
ms.author: semenshi
ms.date: 04/23/2026
ms.service: agent-framework
---

# A2A Hosting

The Agent Framework provides hosting packages that expose your AI agents via the [Agent-to-Agent (A2A) protocol](https://a2a-protocol.org/latest/). Once hosted, any A2A-compliant client can discover and communicate with your agents, regardless of what framework or technology the client was built with.

**NuGet Packages:**

- [Microsoft.Agents.AI.Hosting.A2A.AspNetCore](https://www.nuget.org/packages/Microsoft.Agents.AI.Hosting.A2A.AspNetCore) - ASP.NET Core endpoint mapping for A2A protocol bindings. This package transitively includes `Microsoft.Agents.AI.Hosting.A2A`.
- [Microsoft.Agents.AI.Hosting.A2A](https://www.nuget.org/packages/Microsoft.Agents.AI.Hosting.A2A) - Core hosting logic for bridging AI agents to the A2A protocol (server registration, request handling, session management).

## Getting started

Install the ASP.NET Core hosting package (it pulls in the core package automatically):

```dotnetcli
dotnet add package Microsoft.Agents.AI.Hosting.A2A.AspNetCore --prerelease
dotnet add package A2A.AspNetCore --prerelease
dotnet add package Azure.AI.Projects --prerelease
dotnet add package Azure.Identity
dotnet add package Microsoft.Agents.AI.Foundry --prerelease
```

The following example shows a minimal ASP.NET Core application that hosts a single agent via A2A. It uses [Microsoft Foundry](../../../integrations/by-component/model-providers/microsoft-foundry.md) as the AI provider - see [Providers](../../../integrations/by-component/model-providers/index.md) for other options.

```csharp
using A2A;
using A2A.AspNetCore;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

string endpoint = builder.Configuration["AZURE_AI_PROJECT_ENDPOINT"]
    ?? throw new InvalidOperationException("AZURE_AI_PROJECT_ENDPOINT is not set.");
string model = builder.Configuration["AZURE_AI_MODEL"] ?? "gpt-4o-mini";

// 1. Create and register the "weather-agent" agent in the DI container.
builder.Services.AddKeyedSingleton<AIAgent>("weather-agent", (sp, _) =>
{
    return new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential())
        .AsAIAgent(
            model: model,
            instructions: "You are a helpful weather assistant.",
            name: "weather-agent");
});

// 2. Register the A2A server for the "weather-agent" agent.
builder.AddA2AServer("weather-agent");

var app = builder.Build();

// 3. Map A2A protocol endpoints for the "weather-agent" agent.
app.MapA2AHttpJson("weather-agent", "/a2a/weather-agent");

// 4. Serve a minimal agent card for the "weather-agent" agent discovery.
app.MapWellKnownAgentCard(new AgentCard
{
    Name = "WeatherAgent",
    Description = "A helpful weather assistant.",
    SupportedInterfaces =
    [
        new AgentInterface
        {
            Url = "http://localhost:5000/a2a/weather-agent",
            ProtocolBinding = ProtocolBindingNames.HttpJson,
            ProtocolVersion = "1.0",
        }
    ]
});

app.Run();
```

The agent is now reachable at `/a2a/weather-agent` over the A2A HTTP+JSON protocol binding, and its agent card is discoverable at `/.well-known/agent.json`. Any A2A-compliant client can discover and communicate with this agent.

## Protocol bindings

The A2A protocol defines two transport bindings. Both are supported:

| Binding | Method | Description |
|---------|--------|-------------|
| HTTP+JSON | `MapA2AHttpJson` | Standard HTTP requests and Server-Sent Events for streaming. |
| JSON-RPC | `MapA2AJsonRpc` | JSON-RPC 2.0 over HTTP. |

You can map both bindings simultaneously so that clients can choose their preferred transport. Different paths can be used if necessary:

```csharp
app.MapA2AHttpJson("weather-agent", "/a2a/weather-agent");  // HTTP+JSON
app.MapA2AJsonRpc("weather-agent", "/a2a/weather-agent");   // JSON-RPC
```

## Agent card

[Agent cards](https://a2a-protocol.org/latest/specification/#5-agent-discovery-the-agent-card) describe your agent's metadata - name, description, version, and supported interfaces - so that clients can discover and understand its capabilities before sending requests. The [Getting started](#getting-started) section shows a minimal agent card. For production use, provide a fully populated card:

```csharp
using A2A;
using A2A.AspNetCore;

app.MapWellKnownAgentCard(new AgentCard
{
    Name = "WeatherAgent",
    Description = "A helpful weather assistant.",
    Version = "1.0",
    DefaultInputModes = ["text"],
    DefaultOutputModes = ["text"],
    SupportedInterfaces =
    [
        new AgentInterface
        {
            Url = "http://localhost:5000/a2a/weather-agent",
            ProtocolBinding = ProtocolBindingNames.HttpJson,
            ProtocolVersion = "1.0",
        }
    ]
});
```

> [!NOTE]
> `MapWellKnownAgentCard` is provided by the A2A SDK package (`A2A.AspNetCore`), not the Agent Framework hosting packages.

> [!TIP]
> Only one agent card can be served per host, so only one agent is discoverable via the well-known path. Other agents can still be reached directly by URL. See [Agent Discovery](https://a2a-protocol.org/latest/topics/agent-discovery/) for more options.

## How `AddA2AServer` works

The `AddA2AServer` method registers a keyed `A2AServer` singleton in the dependency injection container. When the server is constructed, it resolves or creates several internal components:

| Component | Default | Purpose |
|-----------|---------|---------|
| `IAgentHandler` | `A2AAgentHandler` | Bridges incoming A2A requests to the `AIAgent`. Translates messages, runs the agent, and returns responses as A2A messages. |
| `AgentSessionStore` | `InMemoryAgentSessionStore` | Stores conversation sessions so the agent can maintain context across multiple requests with the same `contextId`. |
| `ITaskStore` | `InMemoryTaskStore` | Tracks task state for long-running A2A operations. |
| `AgentRunMode` | `DisallowBackground` | Controls whether the agent can return background responses (A2A tasks) instead of immediate messages. |

> [!WARNING]
> The default `InMemoryAgentSessionStore` and `InMemoryTaskStore` are intended for development only. State is lost on application restart and is not shared across multiple instances. For production deployments, register durable implementations.

### Overriding defaults

You can replace any of these components by registering keyed services in the DI container before calling `AddA2AServer`. The server resolves keyed services using the agent name as the key.

**Custom session store** - for persistent conversation storage:

```csharp
builder.Services.AddKeyedSingleton<AgentSessionStore>("weather-agent", new MyDurableSessionStore());

builder.AddA2AServer("weather-agent");
```

**Custom task store** - for durable task tracking:

```csharp
builder.Services.AddKeyedSingleton<ITaskStore>("weather-agent", new MyDurableTaskStore());

builder.AddA2AServer("weather-agent");
```

**Custom agent handler** - to take full control of request processing. When a keyed `IAgentHandler` is registered, it replaces the default `A2AAgentHandler` entirely:

```csharp
builder.Services.AddKeyedSingleton<IAgentHandler>("weather-agent", new MyCustomHandler());

builder.AddA2AServer("weather-agent");
```

**Agent run mode** - configure via `A2AServerRegistrationOptions`:

```csharp
builder.AddA2AServer("weather-agent", options =>
{
    options.AgentRunMode = AgentRunMode.DisallowBackground;
});
```

## Multiple agents

You can host multiple agents in a single application. Each agent gets its own A2A server and endpoint:

```csharp
// Register agents in DI.
builder.Services.AddKeyedSingleton<AIAgent>("weather-agent", (sp, _) =>
{
    return new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential())
        .AsAIAgent(model: model, instructions: "You are a helpful weather assistant.", name: "weather-agent");
});

builder.Services.AddKeyedSingleton<AIAgent>("scientist", (sp, _) =>
{
    return new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential())
        .AsAIAgent(model: model, instructions: "You are a scientist.", name: "scientist");
});

// Register A2A servers.
builder.AddA2AServer("weather-agent");
builder.AddA2AServer("scientist");

var app = builder.Build();

// Map endpoints.
app.MapA2AHttpJson("weather-agent", "/a2a/weather-agent");
app.MapA2AHttpJson("scientist", "/a2a/scientist");

app.Run();
```

In this example, neither agent has an agent card, so clients must know the endpoint URLs directly. You can add agent card discovery with `MapWellKnownAgentCard`, but only one agent can be advertised per host - see [Agent card](#agent-card).

## Background responses

> [!NOTE]
> Background responses are not supported yet for A2A-hosted agents. The `AgentRunMode` defaults to `DisallowBackground`, meaning all responses are returned as immediate A2A messages.

## Next steps

> [!div class="nextstepaction"]
> [A2A agent service](../../../integrations/by-component/agent-services/a2a.md)

## See also

- [A2A Protocol Specification](https://a2a-protocol.org/latest/)
- [A2A hosting](server.md)
- [Hosting Overview](../../../get-started/hosting.md)
- [Agents](../../../concepts/agents/index.md)
