---
title: A2A SDK v1 Migration Guide
description: Learn how to migrate existing Agent Framework A2A Agent and A2A Hosting code after the A2A SDK was updated from v0.3 to v1.
zone_pivot_groups: programming-languages
author: sergeymenshykh
ms.topic: article
ms.author: semenshi
ms.date: 04/24/2026
ms.service: agent-framework
---

# A2A SDK v1 Migration Guide

The Agent Framework's A2A integration packages have been updated to use A2A SDK v1, replacing the previous v0.3 dependency. This is a **breaking change** that affects both the A2A Agent (client-side) and A2A Hosting (server-side) packages.

This guide covers the changes you need to make to migrate your existing code.

> [!NOTE]
> This guide covers changes to the Agent Framework's A2A abstraction layer.

::: zone pivot="programming-language-csharp"

## Quick reference

| Area | Old | New |
|------|-----|-----|
| Server registration | Not needed (handled by `MapA2A`) | `builder.AddA2AServer("agent-name")` |
| Endpoint mapping | `app.MapA2A(agent, path, agentCard)` (various overloads) | `app.MapA2AHttpJson("agent-name", path)`<br>`app.MapA2AJsonRpc("agent-name", path)` |
| Agent card | Inline parameter in `MapA2A()` | `app.MapWellKnownAgentCard(card)` |
| Hosting options | `A2AHostingOptions` | `A2AServerRegistrationOptions` |
| Protocol selection | JSON-RPC only, not configurable | HTTP+JSON preferred, JSON-RPC fallback. Configurable via `A2AClientOptions.PreferredBindings` |

## A2A Agent

**Package:** [Microsoft.Agents.AI.A2A](https://www.nuget.org/packages/Microsoft.Agents.AI.A2A)

### Factory method signature changes

The factory methods for creating an `AIAgent` from A2A endpoints (`A2ACardResolver.GetAIAgentAsync()`, `AgentCard.AsAIAgent()`, `A2AClient.AsAIAgent()`) now accept an optional `A2AClientOptions` parameter for configuring client behavior. This parameter did not exist before.

**Before:**

```csharp
AIAgent agent = await resolver.GetAIAgentAsync();
```

**After:**

```csharp
A2AClientOptions options = new()
{
    PreferredBindings = [ProtocolBindingNames.HttpJson]
};

AIAgent agent = await resolver.GetAIAgentAsync(options: options);
```

### Protocol selection

> [!IMPORTANT]
> The default protocol has changed. Previously, the A2A Agent always used JSON-RPC (via `A2AClient`). Now, the default is **HTTP+JSON** with JSON-RPC as a fallback. If the remote agent supports both bindings, requests will silently switch to HTTP+JSON. Set `A2AClientOptions.PreferredBindings` to `[ProtocolBindingNames.JsonRpc]` to preserve the previous behavior.

Protocol selection is a new capability.

You can explicitly control which protocol binding is used via `A2AClientOptions.PreferredBindings`:

```csharp
A2AClientOptions options = new()
{
    // Explicitly prefer JSON-RPC to maintain previous behavior
    PreferredBindings = [ProtocolBindingNames.JsonRpc]
};

AIAgent agent = await resolver.GetAIAgentAsync(options: options);
```

> [!NOTE]
> The remote A2A agent must support the selected protocol binding.

## A2A Hosting

**Packages:**

- [Microsoft.Agents.AI.Hosting.A2A](https://www.nuget.org/packages/Microsoft.Agents.AI.Hosting.A2A) - Core hosting logic (server registration, request handling, session management).
- [Microsoft.Agents.AI.Hosting.A2A.AspNetCore](https://www.nuget.org/packages/Microsoft.Agents.AI.Hosting.A2A.AspNetCore) - ASP.NET Core endpoint mapping for A2A protocol bindings. This package transitively includes the core package.

### Server registration

A2A server registration is now a separate, explicit step. Previously, `MapA2A` handled server setup, endpoint mapping, and agent card serving in one call. Now you register the A2A server during service configuration, map endpoints, and serve the agent card separately.

**Before:**

`MapA2A` combined all three concerns. It had overloads for different ways to reference the agent, with optional `AgentCard` and `Action<ITaskManager>` parameters:

```csharp
// Using an IHostedAgentBuilder
app.MapA2A(agentBuilder, "/a2a/weather-agent");
app.MapA2A(agentBuilder, "/a2a/weather-agent", agentCard);
app.MapA2A(agentBuilder, "/a2a/weather-agent", configureTaskManager);
app.MapA2A(agentBuilder, "/a2a/weather-agent", agentCard, configureTaskManager);

// Using an agent name string
app.MapA2A("weather-agent", "/a2a/weather-agent");
app.MapA2A("weather-agent", "/a2a/weather-agent", agentCard);
app.MapA2A("weather-agent", "/a2a/weather-agent", configureTaskManager);
app.MapA2A("weather-agent", "/a2a/weather-agent", agentCard, configureTaskManager);

// Using an AIAgent instance
app.MapA2A(agent, "/a2a/weather-agent");
app.MapA2A(agent, "/a2a/weather-agent", agentCard);
app.MapA2A(agent, "/a2a/weather-agent", configureTaskManager);
app.MapA2A(agent, "/a2a/weather-agent", agentCard, configureTaskManager);

// Using an ITaskManager directly
app.MapA2A(taskManager, "/a2a/weather-agent");
```

The `AIAgent` class also had a `MapA2A` extension method in the `Microsoft.Agents.AI.Hosting.A2A` package that returned an `ITaskManager`:

```csharp
// Using AIAgent extension method
ITaskManager taskManager = agent.MapA2A();
ITaskManager taskManager = agent.MapA2A(agentCard);
```

> [!NOTE]
> The `ITaskManager` return value is no longer exposed. Use `AddA2AServer(agent)` instead; the underlying `IAgentHandler` is resolved internally by the A2A server.

**After:**

Server registration and endpoint mapping are now separate steps. `AddA2AServer` registers the server, and `MapA2AHttpJson` / `MapA2AJsonRpc` map protocol-specific endpoints:

```csharp
// Using an IHostedAgentBuilder (returned by AddAIAgent)
var agentBuilder = builder.AddAIAgent("weather-agent", instructions: "You are a helpful weather assistant.");
agentBuilder.AddA2AServer();

// Using an agent name string
builder.AddA2AServer("weather-agent");

// Using an AIAgent instance
builder.AddA2AServer(agent);

// Using IServiceCollection directly
builder.Services.AddA2AServer("weather-agent");
builder.Services.AddA2AServer(agent);
```

For details on how `AddA2AServer` works and how to override its defaults, see [A2A Hosting](../hosting/self-hosting/a2a/dotnet.md#how-adda2aserver-works).

### Endpoint mapping

Each mapping method has overloads for `IHostedAgentBuilder`, `AIAgent`, or `string agentName`:

**Before:**

```csharp
app.MapA2A(agentBuilder, path: "/a2a/weather-agent", agentCard: new()
{
    Name = "WeatherAgent",
    Description = "A helpful weather assistant.",
    Version = "1.0"
});
```

**After:**

```csharp
// Using an IHostedAgentBuilder
app.MapA2AHttpJson(agentBuilder, "/a2a/weather-agent");  // HTTP+JSON
app.MapA2AJsonRpc(agentBuilder, "/a2a/weather-agent");   // JSON-RPC

// Using an AIAgent instance
app.MapA2AHttpJson(agent, "/a2a/weather-agent");
app.MapA2AJsonRpc(agent, "/a2a/weather-agent");

// Using an agent name string
app.MapA2AHttpJson("weather-agent", "/a2a/weather-agent");
app.MapA2AJsonRpc("weather-agent", "/a2a/weather-agent");
```

You can map both bindings simultaneously so that clients can choose their preferred transport.

### Agent card

Agent card configuration has moved from an inline parameter on `MapA2A` to a dedicated call. The card is served at the A2A standard well-known path.

**Before:**

```csharp
app.MapA2A(agentBuilder, path: "/a2a/weather-agent", agentCard: new()
{
    Name = "WeatherAgent",
    Description = "A helpful weather assistant.",
    Version = "1.0"
});
```

**After:**

```csharp
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
```

> [!NOTE]
> `MapWellKnownAgentCard` is provided by the A2A SDK package (`A2A.AspNetCore`), not the Agent Framework hosting packages.

> [!TIP]
> Only one agent card can be served per host via the well-known path. Other agents can still be reached directly by URL. See [Agent Discovery](https://a2a-protocol.org/latest/topics/agent-discovery/) for more options.

### Full before and after example

**Before:**

```csharp
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;

var builder = WebApplication.CreateBuilder(args);

var weatherAgentBuilder = builder.AddAIAgent("weather-agent",
    instructions: "You are a helpful weather assistant.",
    description: "A helpful weather assistant.");

var app = builder.Build();

app.MapA2A(weatherAgentBuilder, path: "/a2a/weather-agent", agentCard: new()
{
    Name = "WeatherAgent",
    Description = "A helpful weather assistant.",
    Version = "1.0"
});

app.Run();
```

**After:**

```csharp
using A2A;
using A2A.AspNetCore;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;

var builder = WebApplication.CreateBuilder(args);

// 1. Register the agent (unchanged).
var weatherAgentBuilder = builder.AddAIAgent("weather-agent",
    instructions: "You are a helpful weather assistant.",
    description: "A helpful weather assistant.");

// 2. Register the A2A server for the agent.
weatherAgentBuilder.AddA2AServer();

var app = builder.Build();

// 3. Map A2A protocol endpoints.
app.MapA2AHttpJson(weatherAgentBuilder, "/a2a/weather-agent");  // HTTP+JSON
app.MapA2AJsonRpc(weatherAgentBuilder, "/a2a/weather-agent");   // JSON-RPC

// 4. Serve a minimal agent card for discovery.
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

## Removed and renamed APIs

| Old | New |
|-----|-----|
| `MapA2A(agent, path, agentCard)` | `AddA2AServer("name")` + `MapA2AHttpJson("name", path)` / `MapA2AJsonRpc("name", path)` + `MapWellKnownAgentCard(card)` |
| `Microsoft.Agents.AI.Hosting.A2A.AIAgentExtensions.MapA2A` | Consolidated into `A2AServerServiceCollectionExtensions.AddA2AServer` |
| `A2AHostingOptions` | Renamed to `A2AServerRegistrationOptions` |

::: zone-end

::: zone pivot="programming-language-python"

## A2A Hosting (server-side)

### Server setup

The `A2AStarletteApplication` convenience class has been removed. Build the Starlette app directly using route helpers:

**Before:**

```python
from a2a.server.apps import A2AStarletteApplication
from a2a.server.request_handlers import DefaultRequestHandler
from a2a.server.tasks import InMemoryTaskStore

request_handler = DefaultRequestHandler(
    agent_executor=A2AExecutor(agent),
    task_store=InMemoryTaskStore(),
)

server = A2AStarletteApplication(
    agent_card=public_agent_card,
    http_handler=request_handler,
).build()
```

**After:**

```python
from a2a.server.request_handlers import DefaultRequestHandler
from a2a.server.routes import create_agent_card_routes, create_jsonrpc_routes
from a2a.server.tasks import InMemoryTaskStore
from starlette.applications import Starlette

request_handler = DefaultRequestHandler(
    agent_executor=A2AExecutor(agent),
    task_store=InMemoryTaskStore(),
    agent_card=public_agent_card,
)

server = Starlette(
    routes=[
        *create_agent_card_routes(public_agent_card),
        *create_jsonrpc_routes(request_handler, "/"),
    ]
)
```

> [!IMPORTANT]
> `DefaultRequestHandler` now requires the `agent_card` parameter. `create_jsonrpc_routes` requires a second `rpc_url` argument (typically `"/"`).

### AgentCard construction

The `AgentCard` no longer has a top-level `url` field. Use `supported_interfaces` with `AgentInterface` instead. Field names have moved from camelCase to snake_case.

**Before:**

```python
from a2a.types import AgentCapabilities, AgentCard, AgentSkill

agent_card = AgentCard(
    name="Travel Agent",
    description="Helps plan travel.",
    url="http://localhost:9999/",
    version="1.0.0",
    defaultInputModes=["text"],
    defaultOutputModes=["text"],
    capabilities=AgentCapabilities(streaming=True),
    skills=[...],
)
```

**After:**

```python
from a2a.types import AgentCapabilities, AgentCard, AgentInterface, AgentSkill

agent_card = AgentCard(
    name="Travel Agent",
    description="Helps plan travel.",
    version="1.0.0",
    default_input_modes=["text"],
    default_output_modes=["text"],
    capabilities=AgentCapabilities(streaming=True),
    supported_interfaces=[
        AgentInterface(url="http://localhost:9999/", protocol_binding="JSONRPC"),
    ],
    skills=[...],
)
```

### Full before and after example

**Before:**

```python
import uvicorn
from a2a.server.apps import A2AStarletteApplication
from a2a.server.request_handlers import DefaultRequestHandler
from a2a.server.tasks import InMemoryTaskStore
from a2a.types import AgentCapabilities, AgentCard
from agent_framework import Agent
from agent_framework.a2a import A2AExecutor
from agent_framework.openai import OpenAIChatClient

agent_card = AgentCard(
    name="My Agent",
    url="http://localhost:9999/",
    version="1.0.0",
    defaultInputModes=["text"],
    defaultOutputModes=["text"],
    capabilities=AgentCapabilities(streaming=True),
    skills=[],
)

agent = Agent(
    client=OpenAIChatClient(),
    name="My Agent",
    instructions="You are a helpful assistant.",
)

handler = DefaultRequestHandler(
    agent_executor=A2AExecutor(agent),
    task_store=InMemoryTaskStore(),
)

server = A2AStarletteApplication(
    agent_card=agent_card,
    http_handler=handler,
).build()

uvicorn.run(server, host="0.0.0.0", port=9999)
```

**After:**

```python
import uvicorn
from a2a.server.request_handlers import DefaultRequestHandler
from a2a.server.routes import create_agent_card_routes, create_jsonrpc_routes
from a2a.server.tasks import InMemoryTaskStore
from a2a.types import AgentCapabilities, AgentCard, AgentInterface
from agent_framework import Agent
from agent_framework.a2a import A2AExecutor
from agent_framework.openai import OpenAIChatClient
from starlette.applications import Starlette

agent_card = AgentCard(
    name="My Agent",
    version="1.0.0",
    default_input_modes=["text"],
    default_output_modes=["text"],
    capabilities=AgentCapabilities(streaming=True),
    supported_interfaces=[
        AgentInterface(url="http://localhost:9999/", protocol_binding="JSONRPC"),
    ],
    skills=[],
)

agent = Agent(
    client=OpenAIChatClient(),
    name="My Agent",
    instructions="You are a helpful assistant.",
)

handler = DefaultRequestHandler(
    agent_executor=A2AExecutor(agent),
    task_store=InMemoryTaskStore(),
    agent_card=agent_card,
)

server = Starlette(
    routes=[
        *create_agent_card_routes(agent_card),
        *create_jsonrpc_routes(handler, "/"),
    ]
)

uvicorn.run(server, host="0.0.0.0", port=9999)
```

## Removed and renamed APIs

| Old | New |
|-----|-----|
| `A2AStarletteApplication` | Removed. Use `Starlette` from `starlette.applications` with `create_agent_card_routes` and `create_jsonrpc_routes` |
| `from a2a.server.apps import A2AStarletteApplication` | `from starlette.applications import Starlette` + `from a2a.server.routes import create_agent_card_routes, create_jsonrpc_routes` |
| `DefaultRequestHandler(agent_executor=..., task_store=...)` | `DefaultRequestHandler(agent_executor=..., task_store=..., agent_card=...)` |
| `AgentCard(url=...)` | `AgentCard(supported_interfaces=[AgentInterface(url=..., protocol_binding="JSONRPC")])` |
| `defaultInputModes` / `defaultOutputModes` | `default_input_modes` / `default_output_modes` |
| `TextPart`, `FilePart`, `DataPart` | `Part` (with `text`, `url`, `raw` fields) |
| `TaskState.completed`, `TaskState.failed` | `TaskState.TASK_STATE_COMPLETED`, `TaskState.TASK_STATE_FAILED` |
| `Role("agent")`, `Role("user")` | `Role.ROLE_AGENT`, `Role.ROLE_USER` |
| `client.resubscribe(...)` | `client.subscribe(...)` |

::: zone-end

::: zone pivot="programming-language-go"

> [!NOTE]
> This migration guide applies to the C# and Python Agent Framework A2A packages. For Go A2A clients, see the [A2A agent service](../integrations/by-component/agent-services/a2a.md); for Go servers, see [A2A hosting](../hosting/self-hosting/a2a/server.md).

::: zone-end

## See also

- [A2A agent service](../integrations/by-component/agent-services/a2a.md) - full reference for consuming remote A2A agents
- [A2A hosting](../hosting/self-hosting/a2a/server.md) - expose Agent Framework agents through A2A
- [A2A Hosting](../hosting/self-hosting/a2a/dotnet.md) - full reference for the new hosting API
- [A2A Protocol Specification](https://a2a-protocol.org/latest/)
