---
title: Getting Started with AG-UI
description: Step-by-step tutorial to build your first AG-UI server and client with Agent Framework
zone_pivot_groups: programming-languages
author: moonbox3
ms.topic: tutorial
ms.author: evmattso
ms.date: 07/10/2026
ms.service: agent-framework
---

# Getting Started with AG-UI

This tutorial demonstrates how to build both server and client applications using the AG-UI protocol with .NET or Python and Agent Framework. You'll learn how to create an AG-UI server that hosts an AI agent and a client that connects to it for interactive conversations.

## What You'll Build

By the end of this tutorial, you'll have:

- An AG-UI server hosting an AI agent accessible via HTTP
- A client application that connects to the server and streams responses
- Understanding of how the AG-UI protocol works with Agent Framework

::: zone pivot="programming-language-csharp"

## Prerequisites

Before you begin, ensure you have the following:

- .NET 8.0 or later
- [Azure OpenAI service endpoint and deployment configured](/azure/ai-foundry/openai/how-to/create-resource)
- [Azure CLI installed](/cli/azure/install-azure-cli) and [authenticated](/cli/azure/authenticate-azure-cli)
- User has the `Cognitive Services OpenAI Contributor` role for the Azure OpenAI resource

> [!NOTE]
> These samples use Azure OpenAI models. For more information, see [how to deploy Azure OpenAI models with Microsoft Foundry](/azure/ai-foundry/how-to/deploy-models-openai).

> [!NOTE]
> These samples use `DefaultAzureCredential` for authentication. Make sure you're authenticated with Azure (e.g., via `az login`). For more information, see the [Azure Identity documentation](/dotnet/api/overview/azure/identity-readme).

> [!WARNING]
> The AG-UI protocol is still under development and subject to change. We will keep these samples updated as the protocol evolves.

## Step 1: Creating an AG-UI Server

The AG-UI server hosts your AI agent and exposes it via HTTP endpoints using ASP.NET Core.

> [!NOTE]
> The server project requires the `Microsoft.NET.Sdk.Web` SDK. If you're creating a new project from scratch, use `dotnet new web` or ensure your `.csproj` file uses `<Project Sdk="Microsoft.NET.Sdk.Web">` instead of `Microsoft.NET.Sdk`.

### Install Required Packages

Install the necessary packages for the server:

```bash
dotnet add package Microsoft.Agents.AI.Hosting.AGUI.AspNetCore --prerelease
dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
dotnet add package Azure.AI.OpenAI
dotnet add package Azure.Identity
```

> [!NOTE]
> The `Microsoft.Agents.AI.OpenAI` package provides the `AsAIAgent()` extension method that turns an Azure OpenAI (or OpenAI) chat client into an Agent Framework agent.

### Server Code

Create a file named `Program.cs`:

```csharp
// Copyright (c) Microsoft. All rights reserved.

using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using OpenAI.Chat;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddAGUIServer();

string endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"]
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
string deploymentName = builder.Configuration["AZURE_OPENAI_DEPLOYMENT_NAME"]
    ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT_NAME is not set.");

// Build an agent directly from the Azure OpenAI chat client.
AIAgent agent = new AzureOpenAIClient(
        new Uri(endpoint),
        new DefaultAzureCredential())
    .GetChatClient(deploymentName)
    .AsAIAgent(
        name: "AGUIAssistant",
        instructions: "You are a helpful assistant.");

WebApplication app = builder.Build();

// Map the agent to an AG-UI endpoint (HTTP POST + SSE streaming).
app.MapAGUIServer("/", agent);

await app.RunAsync();
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

### Key Concepts

- **`AddAGUIServer`**: Registers AG-UI server services with the dependency injection container
- **`MapAGUIServer`**: Extension method that maps an agent to an AG-UI endpoint with automatic request/response handling and SSE streaming
- **`AsAIAgent`**: Creates an Agent Framework agent directly from the Azure OpenAI chat client with a name and instructions
- **ASP.NET Core Integration**: Uses ASP.NET Core's native async support for streaming responses
- **Instructions**: The agent is created with default instructions, which can be overridden by client messages
- **Configuration**: `AzureOpenAIClient` with `DefaultAzureCredential` provides secure authentication

### Configure and Run the Server

Set the required environment variables:

```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o-mini"
```

Run the server:

```bash
dotnet run --urls http://localhost:8888
```

The server will start listening on `http://localhost:8888`.

> [!NOTE]
> Keep this server running while you set up and run the client in Step 2. Both the server and client need to run simultaneously for the complete system to work.

## Step 2: Creating an AG-UI Client

The AG-UI client connects to the remote server and displays streaming responses.

> [!IMPORTANT]
> Before running the client, ensure the AG-UI server from Step 1 is running at `http://localhost:8888`.

### Install Required Packages

Install the AG-UI client library:

```bash
dotnet add package AGUI.Client --prerelease
dotnet add package Microsoft.Agents.AI --prerelease
```

> [!NOTE]
> The `Microsoft.Agents.AI` package provides the `AsAIAgent()` extension method. The AG-UI client
> type (`AGUIChatClient`) ships in the AG-UI C# SDK package `AGUI.Client`.

### Client Code

Create a file named `Program.cs`:

```csharp
// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Agents.AI;
using AGUI.Client;
using Microsoft.Extensions.AI;

string serverUrl = Environment.GetEnvironmentVariable("AGUI_SERVER_URL") ?? "http://localhost:8888";

Console.WriteLine($"Connecting to AG-UI server at: {serverUrl}\n");

// Create the AG-UI client agent
using HttpClient httpClient = new()
{
    Timeout = TimeSpan.FromSeconds(60)
};

AGUIChatClient chatClient = new(new AGUIChatClientOptions(httpClient, serverUrl));

AIAgent agent = chatClient.AsAIAgent(
    name: "agui-client",
    description: "AG-UI Client Agent");

AgentSession session = await agent.CreateSessionAsync();
List<ChatMessage> messages =
[
    new(ChatRole.System, "You are a helpful assistant.")
];

try
{
    while (true)
    {
        // Get user input
        Console.Write("\nUser (:q or quit to exit): ");
        string? message = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(message))
        {
            Console.WriteLine("Request cannot be empty.");
            continue;
        }

        if (message is ":q" or "quit")
        {
            break;
        }

        messages.Add(new ChatMessage(ChatRole.User, message));

        // Stream the response
        bool isFirstUpdate = true;

        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(messages, session))
        {
            ChatResponseUpdate chatUpdate = update.AsChatResponseUpdate();

            // First update indicates run started
            if (isFirstUpdate)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n[Run Started - Run: {chatUpdate.ResponseId}]");
                Console.ResetColor();
                isFirstUpdate = false;
            }

            // Display streaming text content
            foreach (AIContent content in update.Contents)
            {
                if (content is TextContent textContent)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write(textContent.Text);
                    Console.ResetColor();
                }
                else if (content is ErrorContent errorContent)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[Error: {errorContent.Message}]");
                    Console.ResetColor();
                }
            }
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n[Run Finished]");
        Console.ResetColor();
    }
}
catch (Exception ex)
{
    Console.WriteLine($"\nAn error occurred: {ex.Message}");
}
```

### Key Concepts

- **Server-Sent Events (SSE)**: The protocol uses SSE for streaming responses
- **AGUIChatClient**: Client class that connects to AG-UI servers and implements `IChatClient`
- **AsAIAgent**: Extension method on `AGUIChatClient` to create an agent from the client
- **RunStreamingAsync**: Streams responses as `AgentResponseUpdate` objects
- **AsChatResponseUpdate**: Extension method to access chat-specific properties like `ResponseId`
- **Session Management**: The `AgentSession` maintains conversation context across requests
- **Content Types**: Responses include `TextContent` for messages and `ErrorContent` for errors

### Configure and Run the Client

Optionally set a custom server URL:

```bash
export AGUI_SERVER_URL="http://localhost:8888"
```

Run the client in a separate terminal (ensure the server from Step 1 is running):

```bash
dotnet run
```

## Step 3: Testing the Complete System

With both the server and client running, you can now test the complete system.

### Expected Output

```
$ dotnet run
Connecting to AG-UI server at: http://localhost:8888


User (:q or quit to exit): What is 2 + 2?

[Run Started - Run: run_OXhljyhFd6LNjtYlQGHaYknF]
2 + 2 equals 4.
[Run Finished]

User (:q or quit to exit): Tell me a fun fact about space

[Run Started - Run: run_9fTgYc51ITc5xsGetz1zTKnh]
A fun fact about space is that there are more stars in the observable
universe than grains of sand on all the beaches on Earth.
[Run Finished]

User (:q or quit to exit): :q
```

## Testing with curl (Optional)

You can exercise the server directly with curl before running the client. The AG-UI endpoint accepts a
`RunAgentInput` JSON body. The only required field is `messages`. If you omit `threadId`, the server
generates one for you:

```bash
curl -N http://localhost:8888/ \
  -H "Content-Type: application/json" \
  -H "Accept: text/event-stream" \
  -d '{
    "messages": [
      {"role": "user", "content": "What is 2 + 2?"}
    ]
  }'
```

You should see Server-Sent Events streaming back:

```
data: {"type":"RUN_STARTED","threadId":"b60869bc...","runId":""}

data: {"type":"TEXT_MESSAGE_START","messageId":"...","role":"assistant","name":"AGUIAssistant"}

data: {"type":"TEXT_MESSAGE_CONTENT","messageId":"...","delta":"Two"}

data: {"type":"TEXT_MESSAGE_CONTENT","messageId":"...","delta":" plus"}

...

data: {"type":"TEXT_MESSAGE_END","messageId":"..."}

data: {"type":"RUN_FINISHED","threadId":"b60869bc...","runId":""}
```

Use the route you mapped with `MapAGUIServer` (here `/`) and the port your server listens on
(`dotnet run --urls http://localhost:8888`).

## How It Works

### Server-Side Flow

1. Client sends HTTP POST request with messages
2. ASP.NET Core endpoint receives the request via `MapAGUIServer`
3. Agent processes the messages using Agent Framework
4. Responses are converted to AG-UI events
5. Events are streamed back as Server-Sent Events (SSE)
6. Connection closes when the run completes

### Client-Side Flow

1. `AGUIChatClient` sends HTTP POST request to server endpoint
2. Server responds with SSE stream
3. Client parses incoming events into `AgentResponseUpdate` objects
4. Each update is displayed based on its content type
5. `ConversationId` is captured for conversation continuity
6. Stream completes when run finishes

### Protocol Details

The AG-UI protocol uses:

- HTTP POST for sending requests
- Server-Sent Events (SSE) for streaming responses
- JSON for event serialization
- Thread IDs (as `ConversationId`) for maintaining conversation context
- Run IDs (as `ResponseId`) for tracking individual executions

## Next Steps

Now that you understand the basics of AG-UI, you can:

- **[Add Backend Tools](backend-tool-rendering.md)**: Create custom function tools for your domain
<!-- - **[Implement Human-in-the-Loop](human-in-the-loop.md)**: Add approval workflows for sensitive operations -->
<!-- - **[Manage State](state-management.md)**: Implement shared state for generative UI applications -->

## Additional Resources

- [AG-UI Overview](index.md)
- [Agent Framework Documentation](../../../../overview/index.md)
- [AG-UI Protocol Specification](https://docs.ag-ui.com/)

::: zone-end

::: zone pivot="programming-language-python"

## Prerequisites

Before you begin, ensure you have the following:

- Python 3.10 or later
- [Azure OpenAI service endpoint and deployment configured](/azure/ai-foundry/openai/how-to/create-resource)
- [Azure CLI installed](/cli/azure/install-azure-cli) and [authenticated](/cli/azure/authenticate-azure-cli)
- User has the `Cognitive Services OpenAI Contributor` role for the Azure OpenAI resource

> [!NOTE]
> These samples use Azure OpenAI models. For more information, see [how to deploy Azure OpenAI models with Foundry](/azure/ai-foundry/how-to/deploy-models-openai).

> [!NOTE]
> These samples use `DefaultAzureCredential` for authentication. Make sure you're authenticated with Azure (e.g., via `az login`). For more information, see the [Azure Identity documentation](/python/api/azure-identity/azure.identity.defaultazurecredential).

> [!WARNING]
> The AG-UI protocol is still under development and subject to change. We will keep these samples updated as the protocol evolves.

## Step 1: Creating an AG-UI Server

The AG-UI server hosts your AI agent and exposes it via HTTP endpoints using FastAPI.

### Install Required Packages

Install the necessary packages for the server:

```bash
pip install agent-framework-ag-ui --pre
```

Or using uv:

```bash
uv pip install agent-framework-ag-ui --prerelease=allow
```

This will automatically install `agent-framework-core`, `fastapi`, `uvicorn`, and `sse-starlette` as dependencies.

### Server Code

Create a file named `server.py`:

```python
"""AG-UI server example."""

import os

from agent_framework import Agent
from agent_framework.openai import OpenAIChatCompletionClient
from agent_framework_ag_ui import add_agent_framework_fastapi_endpoint
from azure.identity import AzureCliCredential
from fastapi import FastAPI

# Read required configuration
endpoint = os.environ.get("AZURE_OPENAI_ENDPOINT")
deployment_name = os.environ.get("AZURE_OPENAI_CHAT_COMPLETION_MODEL")

if not endpoint:
    raise ValueError("AZURE_OPENAI_ENDPOINT environment variable is required")
if not deployment_name:
    raise ValueError("AZURE_OPENAI_CHAT_COMPLETION_MODEL environment variable is required")

chat_client = OpenAIChatCompletionClient(
    model=deployment_name,
    azure_endpoint=endpoint,
    api_version=os.getenv("AZURE_OPENAI_API_VERSION"),
    credential=AzureCliCredential(),
)

# Create the AI agent
agent = Agent(
    name="AGUIAssistant",
    instructions="You are a helpful assistant.",
    client=chat_client,
)

# Create FastAPI app
app = FastAPI(title="AG-UI Server")

# Register the AG-UI endpoint
add_agent_framework_fastapi_endpoint(app, agent, "/")

if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="127.0.0.1", port=8888)
```

### Key Concepts

- **`add_agent_framework_fastapi_endpoint`**: Registers the AG-UI endpoint with automatic request/response handling and SSE streaming
- **`Agent`**: The Agent Framework agent that will handle incoming requests
- **FastAPI Integration**: Uses FastAPI's native async support for streaming responses
- **Instructions**: The agent is created with default instructions, which can be overridden by client messages
- **Configuration**: `OpenAIChatCompletionClient` accepts explicit Azure routing inputs such as `model`, `azure_endpoint`, `api_version`, and `credential`, and can also read from environment variables

### Configure and Run the Server

Set the required environment variables:

```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
export AZURE_OPENAI_CHAT_COMPLETION_MODEL="gpt-4o-mini"
```

Run the server:

```bash
python server.py
```

Or using uvicorn directly:

```bash
uvicorn server:app --host 127.0.0.1 --port 8888
```

The server will start listening on `http://127.0.0.1:8888`.

## Step 2: Creating an AG-UI Client

The AG-UI client connects to the remote server and displays streaming responses.

### Install Required Packages

The AG-UI package is already installed, which includes the `AGUIChatClient`:

```bash
# Already installed with agent-framework-ag-ui
pip install agent-framework-ag-ui --pre
```

### Client Code

Create a file named `client.py`:

```python
"""AG-UI client example."""

import asyncio
import os

from agent_framework import Agent
from agent_framework_ag_ui import AGUIChatClient


async def main():
    """Main client loop."""
    # Get server URL from environment or use default
    server_url = os.environ.get("AGUI_SERVER_URL", "http://127.0.0.1:8888/")
    print(f"Connecting to AG-UI server at: {server_url}\n")

    # Create AG-UI chat client
    chat_client = AGUIChatClient(endpoint=server_url)

    # Create agent with the chat client
    agent = Agent(
        name="ClientAgent",
        client=chat_client,
        instructions="You are a helpful assistant.",
    )

    # Get a thread for conversation continuity
    thread = agent.create_session()

    try:
        while True:
            # Get user input
            message = input("\nUser (:q or quit to exit): ")
            if not message.strip():
                print("Request cannot be empty.")
                continue

            if message.lower() in (":q", "quit"):
                break

            # Stream the agent response
            print("\nAssistant: ", end="", flush=True)
            async for update in agent.run(message, session=thread, stream=True):
                # Print text content as it streams
                if update.text:
                    print(f"\033[96m{update.text}\033[0m", end="", flush=True)

            print("\n")

    except KeyboardInterrupt:
        print("\n\nExiting...")
    except Exception as e:
        print(f"\n\033[91mAn error occurred: {e}\033[0m")


if __name__ == "__main__":
    asyncio.run(main())
```

### Key Concepts

- **Server-Sent Events (SSE)**: The protocol uses SSE format (`data: {json}\n\n`)
- **Event Types**: Different events provide metadata and content (UPPERCASE with underscores):
  - `RUN_STARTED`: Agent has started processing
  - `TEXT_MESSAGE_START`: Start of a text message from the agent
  - `TEXT_MESSAGE_CONTENT`: Incremental text streamed from the agent (with `delta` field)
  - `TEXT_MESSAGE_END`: End of a text message
  - `RUN_FINISHED`: Successful completion
  - `RUN_ERROR`: Error information
- **Field Naming**: Event fields use camelCase (e.g., `threadId`, `runId`, `messageId`)
- **Thread Management**: The `threadId` maintains conversation context across requests
- **Client-Side Instructions**: System messages are sent from the client

### Configure and Run the Client

Optionally set a custom server URL:

```bash
export AGUI_SERVER_URL="http://127.0.0.1:8888/"
```

Run the client (in a separate terminal):

```bash
python client.py
```

## Step 3: Testing the Complete System

With both the server and client running, you can now test the complete system.

### Expected Output

```
$ python client.py
Connecting to AG-UI server at: http://127.0.0.1:8888/

User (:q or quit to exit): What is 2 + 2?

[Run Started - Thread: abc123, Run: xyz789]
2 + 2 equals 4.
[Run Finished - Thread: abc123, Run: xyz789]

User (:q or quit to exit): Tell me a fun fact about space

[Run Started - Thread: abc123, Run: def456]
Here's a fun fact: A day on Venus is longer than its year! Venus takes
about 243 Earth days to rotate once on its axis, but only about 225 Earth
days to orbit the Sun.
[Run Finished - Thread: abc123, Run: def456]

User (:q or quit to exit): :q
```

### Color-Coded Output

The client displays different content types with distinct colors:

- **Yellow**: Run started notifications
- **Cyan**: Agent text responses (streamed in real-time)
- **Green**: Run completion notifications
- **Red**: Error messages

## Testing with curl (Optional)

Before running the client, you can test the server manually using curl:

```bash
curl -N http://127.0.0.1:8888/ \
  -H "Content-Type: application/json" \
  -H "Accept: text/event-stream" \
  -d '{
    "messages": [
      {"role": "user", "content": "What is 2 + 2?"}
    ]
  }'
```

You should see Server-Sent Events streaming back:

```
data: {"type":"RUN_STARTED","threadId":"...","runId":"..."}

data: {"type":"TEXT_MESSAGE_START","messageId":"...","role":"assistant"}

data: {"type":"TEXT_MESSAGE_CONTENT","messageId":"...","delta":"The"}

data: {"type":"TEXT_MESSAGE_CONTENT","messageId":"...","delta":" answer"}

...

data: {"type":"TEXT_MESSAGE_END","messageId":"..."}

data: {"type":"RUN_FINISHED","threadId":"...","runId":"..."}
```

For an idle stream, curl may also display `: keepalive` comment lines. These are SSE transport comments, not AG-UI events.

## How It Works

### Server-Side Flow

1. Client sends HTTP POST request with messages
2. FastAPI endpoint receives the request
3. `AgentFrameworkAgent` wrapper orchestrates the execution
4. Agent processes the messages using Agent Framework
5. `AgentFrameworkEventBridge` converts agent updates to AG-UI events
6. Responses are streamed back as Server-Sent Events (SSE)
7. Connection closes when the run completes

### Client-Side Flow

1. Client sends HTTP POST request to server endpoint
2. Server responds with SSE stream
3. Client parses incoming `data:` lines as JSON events
4. Each event is displayed based on its type
5. `threadId` is captured for conversation continuity
6. Stream completes when `RUN_FINISHED` event arrives

### Protocol Details

The AG-UI protocol uses:

- HTTP POST for sending requests
- Server-Sent Events (SSE) for streaming responses
- JSON for event serialization
- Thread IDs for maintaining conversation context
- Run IDs for tracking individual executions
- Event type naming: UPPERCASE with underscores (e.g., `RUN_STARTED`, `TEXT_MESSAGE_CONTENT`)
- Field naming: camelCase (e.g., `threadId`, `runId`, `messageId`)
- SSE keepalive comments every 15 seconds while a stream is idle. Clients that process only `data:` lines ignore
  these comments automatically.

## Common Patterns

### Custom Server Configuration

```python
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

app = FastAPI()

# Add CORS for web clients
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

add_agent_framework_fastapi_endpoint(
    app,
    agent,
    "/agent",
    keepalive_seconds=30,  # Defaults to 15; set to None to disable
)
```

`keepalive_seconds` must be a positive number or `None`.

### Multiple Agents

```python
app = FastAPI()

weather_agent = Agent(name="weather", ...)
finance_agent = Agent(name="finance", ...)

add_agent_framework_fastapi_endpoint(app, weather_agent, "/weather")
add_agent_framework_fastapi_endpoint(app, finance_agent, "/finance")
```

### Error Handling

```python
try:
    async for event in client.send_message(message):
        if event.get("type") == "RUN_ERROR":
            error_msg = event.get("message", "Unknown error")
            print(f"Error: {error_msg}")
            # Handle error appropriately
except httpx.HTTPError as e:
    print(f"HTTP error: {e}")
except Exception as e:
    print(f"Unexpected error: {e}")
```

## Troubleshooting

### Connection Refused

Ensure the server is running before starting the client:

```bash
# Terminal 1
python server.py

# Terminal 2 (after server starts)
python client.py
```

### Authentication Errors

Make sure you're authenticated with Azure:

```bash
az login
```

Verify you have the correct role assignment on the Azure OpenAI resource.

### Streaming Not Working

Check that your client timeout is sufficient:

```python
httpx.AsyncClient(timeout=60.0)  # 60 seconds should be enough
```

For long-running agents, increase the timeout accordingly.

Idle streams emit an SSE keepalive comment every 15 seconds by default. If a proxy closes idle connections sooner,
configure a smaller positive `keepalive_seconds` value when registering the endpoint.

### Thread Context Lost

The client automatically manages thread continuity. If context is lost:

1. Check that `threadId` is being captured from `RUN_STARTED` events
2. Ensure the same client instance is used across messages
3. Verify the server is receiving the `thread_id` in subsequent requests

## Next Steps

Now that you understand the basics of AG-UI, you can:

- **[Add Backend Tools](backend-tool-rendering.md)**: Create custom function tools for your domain
<!-- - **[Implement Human-in-the-Loop](human-in-the-loop.md)**: Add approval workflows for sensitive operations -->
<!-- - **[Manage State](state-management.md)**: Implement shared state for generative UI applications -->

## Additional Resources

- [AG-UI Overview](index.md)
- [Agent Framework Documentation](../../../../overview/index.md)
- [AG-UI Protocol Specification](https://docs.ag-ui.com/)

::: zone-end

::: zone pivot="programming-language-go"

Go supports AG-UI through `provider/aguiprovider` for both servers and clients.

```go
import "github.com/microsoft/agent-framework-go/provider/aguiprovider"

mux := http.NewServeMux()
mux.Handle("/", aguiprovider.NewJSONHTTPHandler(myAgent, aguiprovider.HandlerConfig{}))

if err := http.ListenAndServe(":8888", mux); err != nil {
    log.Fatal(err)
}
```

Use `aguiprovider.NewAgent` when your Go app needs to call an AG-UI server as an agent:

```go
import aguiSSEClient "github.com/ag-ui-protocol/ag-ui/sdks/community/go/pkg/client/sse"

a := aguiprovider.NewAgent(
    aguiSSEClient.NewClient(aguiSSEClient.Config{Endpoint: serverURL}),
    aguiprovider.AgentConfig{},
)
```

> [!TIP]
> See the [AG-UI getting started server](https://github.com/microsoft/agent-framework-go/blob/main/examples/02-agents/agui/step01_getting_started/server/main.go) and [client](https://github.com/microsoft/agent-framework-go/blob/main/examples/02-agents/agui/step01_getting_started/client/main.go) samples for complete runnable examples.

::: zone-end