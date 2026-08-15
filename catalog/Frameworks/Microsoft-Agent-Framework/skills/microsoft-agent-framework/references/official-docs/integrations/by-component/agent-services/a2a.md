---
title: A2A agent service
description: Connect to remote A2A agents and use them through the standard Agent Framework agent interface.
zone_pivot_groups: programming-languages
author: sergeymenshykh
ms.topic: reference
ms.author: semenshi
ms.date: 07/01/2026
ms.service: agent-framework
---

# A2A agent service

The `A2AAgent` enables your application to connect to remote agents that are exposed via the [Agent-to-Agent (A2A) protocol](https://a2a-protocol.org/latest/). It wraps any A2A-compliant endpoint as a standard `AIAgent`, so you can use familiar methods like `RunAsync` and `RunStreamingAsync` to interact with remote agents regardless of what framework or technology they were built with.

To expose an Agent Framework agent as an A2A server, see [Host agents with A2A](../../../hosting/self-hosting/a2a/server.md).

::: zone pivot="programming-language-csharp"

## Getting Started

Add the required NuGet package to your project:

```dotnetcli
dotnet add package Microsoft.Agents.AI.A2A --prerelease
```

## Agent Discovery

Before communicating with a remote A2A agent, you need to discover it and create an `AIAgent` instance. The A2A protocol defines three [discovery strategies](https://a2a-protocol.org/latest/topics/agent-discovery/), each supported by the Agent Framework.

### Well-Known URI

A2A agents can make their [Agent Card](https://a2a-protocol.org/latest/specification/#5-agent-discovery-the-agent-card) discoverable at a standardized path: `https://{domain}/.well-known/agent-card.json`. Use the `A2ACardResolver` to fetch the card and create an agent in a single call:

```csharp
using A2A;
using Microsoft.Agents.AI;

// Initialize a resolver pointing at the remote agent's host.
A2ACardResolver resolver = new(new Uri("https://a2a-agent.example.com"));

// Resolve the agent card and create an AIAgent in one step.
AIAgent agent = await resolver.GetAIAgentAsync();

// Use the agent.
Console.WriteLine(await agent.RunAsync("Hello!"));
```

> [!TIP]
> `GetAIAgentAsync` also accepts an optional `A2AClientOptions` parameter for [protocol selection](#protocol-selection).

### Catalog-Based Discovery

In enterprise environments or public marketplaces, Agent Cards are often managed by a central registry. If you already have an `AgentCard` obtained from such a registry, convert it directly to an `AIAgent`:

```csharp
using A2A;
using Microsoft.Agents.AI;

// Assume agentCard was retrieved from a registry or catalog.
AgentCard agentCard = await GetAgentCardFromRegistryAsync("travel-planner");

AIAgent agent = agentCard.AsAIAgent();

Console.WriteLine(await agent.RunAsync("Plan a trip to Paris."));
```

### Direct Configuration

For tightly coupled systems or development scenarios where the agent endpoint is known ahead of time, create an `A2AClient` directly and convert it to an `AIAgent`:

```csharp
using A2A;
using Microsoft.Agents.AI;

// Create a client pointing at the known agent endpoint.
A2AClient a2aClient = new(new Uri("https://a2a-agent.example.com"));

AIAgent agent = a2aClient.AsAIAgent(name: "my-agent", description: "A helpful assistant.");

Console.WriteLine(await agent.RunAsync("What can you help me with?"));
```

## Protocol Selection

A2A agents can expose multiple protocol bindings such as HTTP+JSON and JSON-RPC. By default, HTTP+JSON is preferred over JSON-RPC. Use `A2AClientOptions.PreferredBindings` to explicitly control which protocol binding is used:

> [!NOTE]
> The remote A2A agent must be available at an endpoint that supports the selected protocol binding.

```csharp
using A2A;
using Microsoft.Agents.AI;

A2ACardResolver agentCardResolver = new(new Uri("https://a2a-agent.example.com"));

AgentCard agentCard = await agentCardResolver.GetAgentCardAsync();

// Prefer HTTP+JSON protocol binding. For JSON-RPC, set PreferredBindings = [ProtocolBindingNames.JsonRpc]
A2AClientOptions options = new()
{
    PreferredBindings = [ProtocolBindingNames.HttpJson]
};

AIAgent agent = agentCard.AsAIAgent(options: options);

Console.WriteLine(await agent.RunAsync("Tell me a joke about a pirate."));
```

## Streaming

A2A supports streaming responses via Server-Sent Events. Use `RunStreamingAsync` to receive updates in real time as the remote agent processes the request:

```csharp
using A2A;
using Microsoft.Agents.AI;

A2ACardResolver resolver = new(new Uri("https://a2a-agent.example.com"));
AIAgent agent = await resolver.GetAIAgentAsync();

await foreach (var update in agent.RunStreamingAsync("Write a short story about a robot."))
{
    if (!string.IsNullOrEmpty(update.Text))
    {
        Console.Write(update.Text);
    }
}
```

## Background Responses

A2A agents support [background responses](../../../agents/background-responses.md) for handling long-running operations. When a remote A2A agent returns a task instead of an immediate message, the Agent Framework provides a continuation token that you can use to poll for results or reconnect to interrupted streams.

### Polling for Task Completion

For non-streaming scenarios, use `AllowBackgroundResponses` to receive a continuation token and poll until the task completes:

```csharp
using A2A;
using Microsoft.Agents.AI;

A2ACardResolver resolver = new(new Uri("https://a2a-agent.example.com"));
AIAgent agent = await resolver.GetAIAgentAsync();

AgentSession session = await agent.CreateSessionAsync();

// AllowBackgroundResponses must be true so the server returns immediately with a continuation token
// instead of blocking until the task is complete.
AgentRunOptions options = new() { AllowBackgroundResponses = true };

// Start the initial run with a long-running task.
AgentResponse response = await agent.RunAsync(
    "Conduct a comprehensive analysis of quantum computing applications in cryptography.",
    session,
    options: options);

// Poll until the response is complete.
while (response.ContinuationToken is { } token)
{
    // Wait before polling again.
    await Task.Delay(TimeSpan.FromSeconds(2));

    // Continue with the token.
    response = await agent.RunAsync(session, options: new AgentRunOptions { ContinuationToken = token });
}

Console.WriteLine(response);
```

### Stream Reconnection

In streaming scenarios, each update may include a continuation token. If the stream is interrupted, use the token to reconnect and obtain the response stream from the beginning:

```csharp
using A2A;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

A2ACardResolver resolver = new(new Uri("https://a2a-agent.example.com"));
AIAgent agent = await resolver.GetAIAgentAsync();

AgentSession session = await agent.CreateSessionAsync();

ResponseContinuationToken? continuationToken = null;

await foreach (var update in agent.RunStreamingAsync(
    "Conduct a comprehensive analysis of quantum computing applications in cryptography.",
    session))
{
    // Save the continuation token to reconnect later if the stream is interrupted.
    // Continuation tokens are only returned for long-running tasks. If the A2A agent
    // returns a message instead of a task, the continuation token will not be initialized.
    if (update.ContinuationToken is { } token)
    {
        continuationToken = token;
    }
}

// If the stream was interrupted and a continuation token was captured,
// reconnect to the response stream using the saved continuation token.
if (continuationToken is not null)
{
    await foreach (var update in agent.RunStreamingAsync(
        session,
        options: new() { ContinuationToken = continuationToken }))
    {
        if (!string.IsNullOrEmpty(update.Text))
        {
            Console.WriteLine(update.Text);
        }
    }
}
```

> [!NOTE]
> A2A agents support stream reconnection (obtaining the same response stream from the beginning), not stream resumption from a specific point in the stream.

## Tools

`A2AAgent` is a transport-level wrapper around a remote A2A agent. Whatever tools the remote agent uses live on the remote side and are invisible to your code. Agent Framework tool types (function tools, code interpreter, file search, hosted/local MCP, etc.) are not configured on the `A2AAgent` itself — to extend the remote agent's capabilities, change the remote agent's configuration.

::: zone-end

::: zone pivot="programming-language-python"

## Getting Started

Install the A2A package:

```bash
pip install agent-framework-a2a --pre
```

## Initialization

`A2AAgent` can be initialized in three ways depending on how much you know about the remote agent ahead of time.

### Direct URL

For development or tightly coupled systems where the endpoint is known:

```python
from agent_framework.a2a import A2AAgent

async with A2AAgent(name="remote", url="https://a2a-agent.example.com") as agent:
    response = await agent.run("Hello!")
    print(response.messages[0].text)
```

When only a URL is provided, `A2AAgent` creates a minimal agent card internally and connects using JSON-RPC.

### Agent Card

If you have an `AgentCard` from a registry or catalog, pass it directly:

```python
from agent_framework.a2a import A2AAgent

async with A2AAgent(agent_card=agent_card) as agent:
    response = await agent.run("Plan a trip to Paris.")
    print(response.messages[0].text)
```

When an `AgentCard` is provided, `A2AAgent` defaults `name` and `description` from the card. It negotiates transport using the card's `supported_interfaces`.

### Well-Known URI (A2ACardResolver)

Use `A2ACardResolver` from the `a2a-sdk` to discover the remote agent at the standard well-known path (`/.well-known/agent.json`):

```python
import httpx
from a2a.client import A2ACardResolver
from agent_framework.a2a import A2AAgent

async with httpx.AsyncClient(timeout=60.0) as http_client:
    resolver = A2ACardResolver(httpx_client=http_client, base_url="https://a2a-agent.example.com")
    agent_card = await resolver.get_agent_card()

async with A2AAgent(agent_card=agent_card) as agent:
    response = await agent.run("What can you help me with?")
    print(response.messages[0].text)
```

## Streaming

Use `stream=True` to receive updates in real time as the remote agent processes the request:

```python
from agent_framework.a2a import A2AAgent

async with A2AAgent(name="remote", url="https://a2a-agent.example.com") as agent:
    stream = agent.run("Write a short story about a robot.", stream=True)
    async for update in stream:
        for content in update.contents:
            if content.text:
                print(content.text, end="", flush=True)

    final = await stream.get_final_response()
    print(f"\n({len(final.messages)} message(s))")
```

## Long-Running Tasks

By default, `A2AAgent` waits for the remote agent to finish before returning. For long-running tasks, set `background=True` to surface a continuation token you can use to poll or subscribe later:

```python
from agent_framework.a2a import A2AAgent

async with A2AAgent(name="worker", url="https://a2a-agent.example.com") as agent:
    # Start a long-running task
    response = await agent.run("Process this large dataset", background=True)

    if response.continuation_token:
        # Poll for completion later
        result = await agent.poll_task(response.continuation_token)
        print(result)
```

You can also resubscribe to the SSE stream instead of polling:

```python
# Resubscribe to the task's event stream
response = await agent.run(continuation_token=response.continuation_token)
```

## Conversation Identity (context_id)

`A2AAgent` stores durable protocol state in `AgentSession.service_session_id` as an `A2AServiceSessionId` mapping:

| Field | Type | Purpose |
|---|---|---|
| `context_id` | `str` | Identifies the A2A conversation. |
| `task_id` | `str \| None` | Tracks the most recent remote task, when the response created one. |
| `task_state` | `TaskState \| None` | Records the latest task state so the next request can continue an input-required task or reference a completed task. |

Create a session with structured state when your application already knows the A2A context:

```python
from agent_framework import AgentSession
from agent_framework.a2a import A2AAgent, A2AServiceSessionId

async with A2AAgent(name="remote", url="https://a2a-agent.example.com") as agent:
    session = AgentSession(
        service_session_id=A2AServiceSessionId(
            context_id="my-conversation-1",
            task_id=None,
            task_state=None,
        )
    )

    # The A2A message uses context_id="my-conversation-1".
    response = await agent.run("Hello!", session=session)

    # A2AAgent updates task_id and task_state from the response.
    response = await agent.run("Follow-up question", session=session)
```

You can also start with `AgentSession()` and let `A2AAgent` populate the structured mapping from the first response. Persist the regular session with `session.to_dict()` and restore it with `AgentSession.from_dict(...)`; the A2A context, task ID, and task state remain together.

For a task in `TASK_STATE_INPUT_REQUIRED`, the next message sets that `task_id` to continue the same task. For other task states, the previous task ID is sent through `reference_task_ids` so the remote agent can refine or continue from the earlier result.

## Authentication

Use an `AuthInterceptor` for secured A2A endpoints:

```python
from a2a.client.auth.interceptor import AuthInterceptor
from agent_framework.a2a import A2AAgent

class BearerAuth(AuthInterceptor):
    def __init__(self, token: str):
        self.token = token

    async def intercept(self, request):
        request.headers["Authorization"] = f"Bearer {self.token}"
        return request

async with A2AAgent(
    name="secure-agent",
    url="https://secure-a2a-agent.example.com",
    auth_interceptor=BearerAuth("your-token"),
) as agent:
    response = await agent.run("Hello!")
```

## Timeout Configuration

`A2AAgent` accepts a `timeout` parameter for controlling request timeouts:

```python
import httpx
from agent_framework.a2a import A2AAgent

# Simple timeout (applies to all components)
async with A2AAgent(name="remote", url="https://example.com", timeout=120.0) as agent:
    ...

# Fine-grained timeout
async with A2AAgent(
    name="remote",
    url="https://example.com",
    timeout=httpx.Timeout(connect=10.0, read=120.0, write=10.0, pool=5.0),
) as agent:
    ...
```

When no timeout is specified, the defaults are: 10s connect, 60s read, 10s write, 5s pool.

## Tools

`A2AAgent` is a transport-level wrapper around a remote A2A agent. Whatever tools the remote agent uses live on the remote side and are invisible to your code. Agent Framework tool types (function tools, code interpreter, file search, hosted/local MCP, etc.) are not configured on the `A2AAgent` itself — to extend the remote agent's capabilities, change the remote agent's configuration.

If you want a Foundry agent to call an A2A agent as a tool, see the [`get_a2a_tool` factory on `FoundryChatClient`](../model-providers/microsoft-foundry.md#agent-to-agent-a2a).

::: zone-end

::: zone pivot="programming-language-go"

Go supports remote A2A agents through the `provider/a2aprovider` package.

Install the Agent Framework and A2A packages:

```bash
go get github.com/microsoft/agent-framework-go
go get github.com/a2aproject/a2a-go/v2
```

## Connect to a remote A2A agent

Resolve the remote agent card, create an A2A client from it, and wrap the client as a standard Agent Framework agent:

```go
import (
    "context"

    "github.com/a2aproject/a2a-go/v2/a2aclient"
    "github.com/a2aproject/a2a-go/v2/a2aclient/agentcard"
    "github.com/microsoft/agent-framework-go/agent"
    "github.com/microsoft/agent-framework-go/provider/a2aprovider"
)

ctx := context.Background()

card, err := agentcard.DefaultResolver.Resolve(ctx, "http://localhost:5000")
if err != nil {
    panic(err)
}

client, err := a2aclient.NewFromCard(ctx, card)
if err != nil {
    panic(err)
}

a := a2aprovider.NewAgent(
    client,
    a2aprovider.AgentConfig{
        Config: agent.Config{
            Name:        card.Name,
            Description: card.Description,
        },
    },
)

resp, err := a.RunText(ctx, "Hello!").Collect()
```

The provider stores the A2A `context_id` and task IDs in the Agent Framework session so follow-up messages can preserve conversation continuity.

## Protocol selection

If a remote agent advertises multiple transport bindings, configure the preferred transport when creating the A2A client:

```go
client, err := a2aclient.NewFromCard(
    ctx,
    card,
    a2aclient.WithConfig(a2aclient.Config{
        PreferredTransports: []a2a.TransportProtocol{a2a.TransportProtocolHTTPJSON},
    }),
)
```

Use `a2a.TransportProtocolJSONRPC` when you want to prefer JSON-RPC.

## Long-running tasks

A2A tasks surface through Agent Framework continuation tokens. Start the run with an explicit session and `agent.AllowBackgroundResponses(true)`, then poll by calling `Run` with no new messages and the continuation token:

```go
session, err := a.CreateSession(ctx)
if err != nil {
    panic(err)
}

resp, err := a.RunText(
    ctx,
    "Process this large dataset.",
    agent.WithSession(session),
    agent.AllowBackgroundResponses(true),
).Collect()
if err != nil {
    panic(err)
}

for resp.ContinuationToken != "" {
    resp, err = a.Run(
        ctx,
        nil,
        agent.WithSession(session),
        agent.WithContinuationToken(resp.ContinuationToken),
    ).Collect()
    if err != nil {
        panic(err)
    }
}
```

For interrupted streaming runs, capture `update.ContinuationToken` from the last received update and pass it to a later streaming run with `agent.WithContinuationToken(token)` and `agent.Stream(true)`.

## Use remote A2A agents as tools

Resolve each remote agent, wrap it with `a2aprovider.NewAgent`, and convert it to a tool with `agenttool.New`.

```go
tools := make([]tool.Tool, 0, len(agentURLs))

for _, agentURL := range agentURLs {
    card, err := agentcard.DefaultResolver.Resolve(ctx, agentURL)
    if err != nil {
        panic(err)
    }

    client, err := a2aclient.NewFromCard(ctx, card)
    if err != nil {
        panic(err)
    }

    remoteAgent := a2aprovider.NewAgent(client, a2aprovider.AgentConfig{
        Config: agent.Config{
            Name:        card.Name,
            Description: card.Description,
        },
    })

    tools = append(tools, agenttool.New(remoteAgent, agenttool.Config{}))
}
```

> [!TIP]
> See the [A2A provider sample](https://github.com/microsoft/agent-framework-go/blob/main/examples/02-agents/providers/a2a/main.go) and [A2A agents as tools sample](https://github.com/microsoft/agent-framework-go/blob/main/examples/02-agents/a2a/as_function_tools/main.go) for complete runnable examples.

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Host agents with A2A](../../../hosting/self-hosting/a2a/server.md)

**Go deeper:**

- [Custom agents](../../../concepts/agents/custom-agents.md)
- [A2A protocol specification](https://a2a-protocol.org/latest/)
