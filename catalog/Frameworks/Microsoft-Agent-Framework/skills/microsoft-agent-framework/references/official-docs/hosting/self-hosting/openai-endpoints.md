---
title: OpenAI Integration
description: Learn how to expose Microsoft Agent Framework agents using OpenAI-compatible protocols including Chat Completions and Responses APIs.
zone_pivot_groups: programming-languages
author: dmkorolev
ms.service: agent-framework
ms.topic: tutorial
ms.date: 07/01/2026
ms.author: dmkorolev
---

# OpenAI-Compatible Endpoints

The Agent Framework supports OpenAI-compatible protocols for both **hosting** agents behind standard APIs and **connecting** to any OpenAI-compatible endpoint.

## What Are OpenAI Protocols?

Two OpenAI protocols are supported:

- **Chat Completions API** — Standard stateless request/response format for chat interactions
- **Responses API** — Advanced format that supports conversations, streaming, and long-running agent processes

**The Responses API is now the default and recommended approach** according to OpenAI's documentation. It provides a more comprehensive and feature-rich interface for building AI applications with built-in conversation management, streaming capabilities, and support for long-running processes.

Use the **Responses API** when:
- Building new applications (recommended default)
- You need server-side conversation management. However, that is not a requirement: you can still use Responses API in stateless mode.
- You want persistent conversation history
- You're building long-running agent processes
- You need advanced streaming capabilities with detailed event types
- You want to track and manage individual responses (e.g., retrieve a specific response by ID, check its status, or cancel a running response)

Use the **Chat Completions API** when:
- Migrating existing applications that rely on the Chat Completions format
- You need simple, stateless request/response interactions
- State management is handled entirely by your client
- You're integrating with existing tools that only support Chat Completions
- You need maximum compatibility with legacy systems

::: zone pivot="programming-language-csharp"

## Hosting Agents as OpenAI Endpoints (.NET)

The `Microsoft.Agents.AI.Hosting.OpenAI` library enables you to expose AI agents through OpenAI-compatible HTTP endpoints, supporting both the Chat Completions and Responses APIs. This allows you to integrate your agents with any OpenAI-compatible client or tool.

**NuGet Package:**
- [Microsoft.Agents.AI.Hosting.OpenAI](https://www.nuget.org/packages/Microsoft.Agents.AI.Hosting.OpenAI)

## Chat Completions API

The Chat Completions API provides a simple, stateless interface for interacting with agents using the standard OpenAI chat format.

### Setting up an agent in ASP.NET Core with ChatCompletions integration

Here's a complete example exposing an agent via the Chat Completions API:

#### Prerequisites

#### 1. Create an ASP.NET Core Web API project

Create a new ASP.NET Core Web API project or use an existing one.

#### 2. Install required dependencies

Install the following packages:

  ## [.NET CLI](#tab/dotnet-cli)

  Run the following commands in your project directory to install the required NuGet packages:

  ```bash
  # Hosting.A2A.AspNetCore for OpenAI ChatCompletions/Responses protocol(s) integration
  dotnet add package Microsoft.Agents.AI.Hosting.OpenAI --prerelease

  # Libraries to connect to Azure OpenAI
  dotnet add package Azure.AI.OpenAI --prerelease
  dotnet add package Azure.Identity
  dotnet add package Microsoft.Extensions.AI
  dotnet add package Microsoft.Extensions.AI.OpenAI --prerelease

  # Swagger to test app
  dotnet add package Microsoft.AspNetCore.OpenApi
  dotnet add package Swashbuckle.AspNetCore
  ```

  ---


#### 3. Configure Azure OpenAI connection

The application requires an Azure OpenAI connection. Configure the endpoint and deployment name using `dotnet user-secrets` or environment variables.
You can also simply edit the `appsettings.json`, but that's not recommended for the apps deployed in production since some of the data can be considered to be secret.

  ## [User-Secrets](#tab/user-secrets)
  ```bash
  dotnet user-secrets set "AZURE_OPENAI_ENDPOINT" "https://<your-openai-resource>.openai.azure.com/"
  dotnet user-secrets set "AZURE_OPENAI_DEPLOYMENT_NAME" "gpt-4o-mini"
  ```
  ## [ENV Windows](#tab/env-windows)
  ```powershell
  $env:AZURE_OPENAI_ENDPOINT = "https://<your-openai-resource>.openai.azure.com/"
  $env:AZURE_OPENAI_DEPLOYMENT_NAME = "gpt-4o-mini"
  ```
  ## [ENV unix](#tab/env-unix)
  ```bash
  export AZURE_OPENAI_ENDPOINT="https://<your-openai-resource>.openai.azure.com/"
  export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o-mini"
  ```
  ## [appsettings](#tab/appsettings)
  ```json
    "AZURE_OPENAI_ENDPOINT": "https://<your-openai-resource>.openai.azure.com/",
    "AZURE_OPENAI_DEPLOYMENT_NAME": "gpt-4o-mini"
  ```

  ---


#### 4. Add the code to Program.cs

Replace the contents of `Program.cs` with the following code:

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

string endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"]
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
string deploymentName = builder.Configuration["AZURE_OPENAI_DEPLOYMENT_NAME"]
    ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT_NAME is not set.");

// Register the chat client
IChatClient chatClient = new AzureOpenAIClient(
        new Uri(endpoint),
        new DefaultAzureCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient();
builder.Services.AddSingleton(chatClient);

builder.AddOpenAIChatCompletions();

// Register an agent
var pirateAgent = builder.AddAIAgent("pirate", instructions: "You are a pirate. Speak like a pirate.");

var app = builder.Build();

app.MapOpenApi();
app.UseSwagger();
app.UseSwaggerUI();

// Expose the agent via OpenAI ChatCompletions protocol
app.MapOpenAIChatCompletions(pirateAgent);

app.Run();
```

### Testing the Chat Completions Endpoint

Once the application is running, you can test the agent using the OpenAI SDK or HTTP requests:

#### Using HTTP Request

```http
POST {{baseAddress}}/pirate/v1/chat/completions
Content-Type: application/json
{
  "model": "pirate",
  "stream": false,
  "messages": [
    {
      "role": "user",
      "content": "Hey mate!"
    }
  ]
}
```
_Note: Replace `{{baseAddress}}` with your server endpoint._

Here is a sample response:
```json
{
	"id": "chatcmpl-nxAZsM6SNI2BRPMbzgjFyvWWULTFr",
	"object": "chat.completion",
	"created": 1762280028,
	"model": "gpt-5",
	"choices": [
		{
			"index": 0,
			"finish_reason": "stop",
			"message": {
				"role": "assistant",
				"content": "Ahoy there, matey! How be ye farin' on this fine day?"
			}
		}
	],
	"usage": {
		"completion_tokens": 18,
		"prompt_tokens": 22,
		"total_tokens": 40,
		"completion_tokens_details": {
			"accepted_prediction_tokens": 0,
			"audio_tokens": 0,
			"reasoning_tokens": 0,
			"rejected_prediction_tokens": 0
		},
		"prompt_tokens_details": {
			"audio_tokens": 0,
			"cached_tokens": 0
		}
	},
	"service_tier": "default"
}
```

The response includes the message ID, content, and usage statistics.

Chat Completions also supports **streaming**, where output is returned in chunks as soon as content is available.
This capability enables displaying output progressively. You can enable streaming by specifying `"stream": true`.
The output format consists of Server-Sent Events (SSE) chunks as defined in the OpenAI Chat Completions specification.

```http
POST {{baseAddress}}/pirate/v1/chat/completions
Content-Type: application/json
{
  "model": "pirate",
  "stream": true,
  "messages": [
    {
      "role": "user",
      "content": "Hey mate!"
    }
  ]
}
```

And the output we get is a set of ChatCompletions chunks:
```
data: {"id":"chatcmpl-xwKgBbFtSEQ3OtMf21ctMS2Q8lo93","choices":[],"object":"chat.completion.chunk","created":0,"model":"gpt-5"}

data: {"id":"chatcmpl-xwKgBbFtSEQ3OtMf21ctMS2Q8lo93","choices":[{"index":0,"finish_reason":"stop","delta":{"content":"","role":"assistant"}}],"object":"chat.completion.chunk","created":0,"model":"gpt-5"}

...

data: {"id":"chatcmpl-xwKgBbFtSEQ3OtMf21ctMS2Q8lo93","choices":[],"object":"chat.completion.chunk","created":0,"model":"gpt-5","usage":{"completion_tokens":34,"prompt_tokens":23,"total_tokens":57,"completion_tokens_details":{"accepted_prediction_tokens":0,"audio_tokens":0,"reasoning_tokens":0,"rejected_prediction_tokens":0},"prompt_tokens_details":{"audio_tokens":0,"cached_tokens":0}}}
```

The streaming response contains similar information, but delivered as Server-Sent Events.

## Responses API

The Responses API provides advanced features including conversation management, streaming, and support for long-running agent processes.

### Setting up an agent in ASP.NET Core with Responses API integration

Here's a complete example using the Responses API:

#### Prerequisites

Follow the same prerequisites as the Chat Completions example (steps 1-3).

#### 4. Add the code to Program.cs

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

string endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"]
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
string deploymentName = builder.Configuration["AZURE_OPENAI_DEPLOYMENT_NAME"]
    ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT_NAME is not set.");

// Register the chat client
IChatClient chatClient = new AzureOpenAIClient(
        new Uri(endpoint),
        new DefaultAzureCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient();
builder.Services.AddSingleton(chatClient);

builder.AddOpenAIResponses();
builder.AddOpenAIConversations();

// Register an agent
var pirateAgent = builder.AddAIAgent("pirate", instructions: "You are a pirate. Speak like a pirate.");

var app = builder.Build();

app.MapOpenApi();
app.UseSwagger();
app.UseSwaggerUI();

// Expose the agent via OpenAI Responses protocol
app.MapOpenAIResponses(pirateAgent);
app.MapOpenAIConversations();

app.Run();
```

### Testing the Responses API

The Responses API is similar to Chat Completions but is stateful, allowing you to pass a `conversation` parameter.
Like Chat Completions, it supports the `stream` parameter, which controls the output format: either a single JSON response or a stream of events.
The Responses API defines its own streaming event types, including `response.created`, `response.output_item.added`, `response.output_item.done`, `response.completed`, and others.

> [!IMPORTANT]
> OpenAI response and conversation IDs are opaque service-side IDs. OpenAI scopes `resp_*` and `conv_*` IDs to the backing API key or project by default, so they are usually constrained when that boundary matches your app or tenant boundary. Risk appears when a hosted agent uses one backing key or project for multiple end users, echoes raw OpenAI IDs to clients, and accepts those IDs back without checking ownership. Store them server-side, map them from your own client session IDs, and verify the authenticated user or tenant before passing them as `previous_response_id` or `conversation`.

#### Create a Conversation and Response

You can send a Responses request directly, or you can first create a conversation using the Conversations API
and then link subsequent requests to that conversation.

To begin, create a new conversation:
```http
POST http://localhost:5209/v1/conversations
Content-Type: application/json
{
  "items": [
    {
        "type": "message",
        "role": "user",
        "content": "Hello!"
      }
  ]
}
```

The response includes the conversation ID:
```json
{
  "id": "conv_E9Ma6nQpRzYxRHxRRqoOWWsDjZVyZfKxlHhfCf02Yxyy9N2y",
  "object": "conversation",
  "created_at": 1762881679,
  "metadata": {}
}
```

Next, send a request and specify the conversation parameter.
_(To receive the response as streaming events, set `"stream": true` in the request.)_
```http
POST http://localhost:5209/pirate/v1/responses
Content-Type: application/json
{
  "stream": false,
  "conversation": "conv_E9Ma6nQpRzYxRHxRRqoOWWsDjZVyZfKxlHhfCf02Yxyy9N2y",
  "input": [
    {
      "type": "message",
      "role": "user",
      "content": [
        {
            "type": "input_text",
            "text": "are you a feminist?"
        }
      ]
    }
  ]
}
```

The agent returns the response and saves the conversation items to storage for later retrieval:
```json
{
  "id": "resp_FP01K4bnMsyQydQhUpovK6ysJJroZMs1pnYCUvEqCZqGCkac",
  "conversation": "conv_E9Ma6nQpRzYxRHxRRqoOWWsDjZVyZfKxlHhfCf02Yxyy9N2y",
  "object": "response",
  "created_at": 1762881518,
  "status": "completed",
  "incomplete_details": null,
  "output": [
    {
      "role": "assistant",
      "content": [
        {
          "type": "output_text",
          "text": "Arrr, matey! As a pirate, I be all about respect for the crew, no matter their gender! We sail these seas together, and every hand on deck be valuable. A true buccaneer knows that fairness and equality be what keeps the ship afloat. So, in me own way, I’d say I be supportin’ all hearty souls who seek what be right! What say ye?"
        }
      ],
      "type": "message",
      "status": "completed",
      "id": "msg_1FAQyZcWgsBdmgJgiXmDyavWimUs8irClHhfCf02Yxyy9N2y"
    }
  ],
  "usage": {
    "input_tokens": 26,
    "input_tokens_details": {
      "cached_tokens": 0
    },
    "output_tokens": 85,
    "output_tokens_details": {
      "reasoning_tokens": 0
    },
    "total_tokens": 111
  },
  "tool_choice": null,
  "temperature": 1,
  "top_p": 1
}
```

The response includes conversation and message identifiers, content, and usage statistics.

To retrieve the conversation items, send this request:
```http
GET http://localhost:5209/v1/conversations/conv_E9Ma6nQpRzYxRHxRRqoOWWsDjZVyZfKxlHhfCf02Yxyy9N2y/items?include=string
```

This returns a JSON response containing both input and output messages:
```JSON
{
  "object": "list",
  "data": [
    {
      "role": "assistant",
      "content": [
        {
          "type": "output_text",
          "text": "Arrr, matey! As a pirate, I be all about respect for the crew, no matter their gender! We sail these seas together, and every hand on deck be valuable. A true buccaneer knows that fairness and equality be what keeps the ship afloat. So, in me own way, I’d say I be supportin’ all hearty souls who seek what be right! What say ye?",
          "annotations": [],
          "logprobs": []
        }
      ],
      "type": "message",
      "status": "completed",
      "id": "msg_1FAQyZcWgsBdmgJgiXmDyavWimUs8irClHhfCf02Yxyy9N2y"
    },
    {
      "role": "user",
      "content": [
        {
          "type": "input_text",
          "text": "are you a feminist?"
        }
      ],
      "type": "message",
      "status": "completed",
      "id": "msg_iLVtSEJL0Nd2b3ayr9sJWeV9VyEASMlilHhfCf02Yxyy9N2y"
    }
  ],
  "first_id": "msg_1FAQyZcWgsBdmgJgiXmDyavWimUs8irClHhfCf02Yxyy9N2y",
  "last_id": "msg_lUpquo0Hisvo6cLdFXMKdYACqFRWcFDrlHhfCf02Yxyy9N2y",
  "has_more": false
}
```

## Exposing Multiple Agents

You can expose multiple agents simultaneously using both protocols:

```csharp
var mathAgent = builder.AddAIAgent("math", instructions: "You are a math expert.");
var scienceAgent = builder.AddAIAgent("science", instructions: "You are a science expert.");

// Add both protocols
builder.AddOpenAIChatCompletions();
builder.AddOpenAIResponses();

var app = builder.Build();

// Expose both agents via Chat Completions
app.MapOpenAIChatCompletions(mathAgent);
app.MapOpenAIChatCompletions(scienceAgent);

// Expose both agents via Responses
app.MapOpenAIResponses(mathAgent);
app.MapOpenAIResponses(scienceAgent);
```

Agents will be available at:
- Chat Completions: `/math/v1/chat/completions` and `/science/v1/chat/completions`
- Responses: `/math/v1/responses` and `/science/v1/responses`

## Custom Endpoints

You can customize the endpoint paths:

```csharp
// Custom path for Chat Completions
app.MapOpenAIChatCompletions(mathAgent, path: "/api/chat");

// Custom path for Responses
app.MapOpenAIResponses(scienceAgent, responsesPath: "/api/responses");
```

::: zone-end

::: zone pivot="programming-language-python"

## Connecting to OpenAI-Compatible Endpoints (Python)

The Python `OpenAIChatCompletionClient` and `OpenAIChatClient` both support a `base_url` parameter, enabling you to connect to **any** OpenAI-compatible endpoint — including self-hosted agents, local inference servers (Ollama, LM Studio, vLLM), or third-party OpenAI-compatible APIs.

```bash
pip install agent-framework
```

### Chat Completions Client

Use `OpenAIChatCompletionClient` with `base_url` to point to any Chat Completions-compatible server:

```python
import asyncio
from agent_framework import Agent, tool
from agent_framework.openai import OpenAIChatCompletionClient

@tool(approval_mode="never_require")
def get_weather(location: str) -> str:
    """Get the weather for a location."""
    return f"Weather in {location}: sunny, 22°C"

async def main():
    # Point to any OpenAI-compatible endpoint
    agent = Agent(
        client=OpenAIChatCompletionClient(
            base_url="http://localhost:11434/v1/",  # e.g. Ollama
            api_key="not-needed",                   # placeholder for local servers
            model="llama3.2",
        ),
        name="WeatherAgent",
        instructions="You are a helpful weather assistant.",
        tools=get_weather,
    )

    response = await agent.run("What's the weather in Seattle?")
    print(response)

asyncio.run(main())
```

### Responses Client

Use `OpenAIChatClient` with `base_url` for endpoints that support the Responses API:

```python
import asyncio
from agent_framework import Agent
from agent_framework.openai import OpenAIChatClient

async def main():
    agent = Agent(
        client=OpenAIChatClient(
            base_url="https://your-hosted-agent.example.com/v1/",
            api_key="your-api-key",
            model="gpt-4o-mini",
        ),
        name="Assistant",
        instructions="You are a helpful assistant.",
    )

    # Non-streaming
    response = await agent.run("Hello!")
    print(response)

    # Streaming
    async for chunk in agent.run("Tell me a joke", stream=True):
        if chunk.text:
            print(chunk.text, end="", flush=True)

asyncio.run(main())
```

### Common OpenAI-Compatible Servers

The `base_url` approach works with any server exposing the OpenAI Chat Completions format:

| Server | Base URL | Notes |
|--------|----------|-------|
| [Ollama](https://ollama.com/) | `http://localhost:11434/v1/` | Local inference, no API key needed |
| [LM Studio](https://lmstudio.ai/) | `http://localhost:1234/v1/` | Local inference with GUI |
| [vLLM](https://docs.vllm.ai/) | `http://localhost:8000/v1/` | High-throughput serving |
| [Microsoft Foundry](https://ai.azure.com/) | Your deployment endpoint | Uses Azure credentials |
| Hosted Agent Framework agents | Your agent endpoint | .NET agents exposed via `MapOpenAIChatCompletions` |

> [!NOTE]
> You can also set the `OPENAI_BASE_URL` environment variable instead of passing `base_url` directly. The client will use it automatically.

### Using Azure OpenAI Clients

Use the same generic OpenAI clients for Azure OpenAI by passing explicit Azure routing inputs instead of `base_url`:

```python
import os
from agent_framework import Agent
from agent_framework.openai import OpenAIChatClient
from azure.identity import AzureCliCredential

agent = Agent(
    client=OpenAIChatClient(
        model=os.environ["AZURE_OPENAI_CHAT_MODEL"],
        azure_endpoint=os.environ["AZURE_OPENAI_ENDPOINT"],
        api_version=os.getenv("AZURE_OPENAI_API_VERSION"),
        credential=AzureCliCredential(),
    ),
    name="Assistant",
    instructions="You are a helpful assistant.",
)
```

Configure with environment variables:
```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
export AZURE_OPENAI_CHAT_MODEL="gpt-4o-mini"
export AZURE_OPENAI_API_VERSION="your-api-version"
```

`OpenAIChatClient` prefers `AZURE_OPENAI_CHAT_MODEL`; `AZURE_OPENAI_MODEL` remains the shared fallback if you need one.

::: zone-end

::: zone pivot="programming-language-go"

Go uses the `provider/openaiprovider` package with the official OpenAI Go client. Use `openaiprovider.NewChatCompletionsAgent` for Chat Completions-compatible endpoints and configure Azure OpenAI with the OpenAI client's Azure options.

```go
import (
  "github.com/microsoft/agent-framework-go/provider/openaiprovider"

  "github.com/openai/openai-go/v3"
  "github.com/openai/openai-go/v3/azure"
)

a := openaiprovider.NewChatCompletionsAgent(
  openai.NewClient(
    azure.WithEndpoint(endpoint, apiVersion),
    azure.WithTokenCredential(token),
  ),
  openaiprovider.AgentConfig{
    Model: deployment,
    Instructions: "You are a helpful assistant.",
  },
)
```

> [!TIP]
> See the [OpenAI provider sample](https://github.com/microsoft/agent-framework-go/blob/main/examples/02-agents/providers/openai/main.go) and [Azure OpenAI provider sample](https://github.com/microsoft/agent-framework-go/blob/main/examples/02-agents/providers/azure/main.go) for complete runnable examples.

::: zone-end
## See Also

- [Integrations Overview](../../integrations/index.md)
- [A2A hosting](./a2a/server.md)
- [OpenAI Chat Completions API Reference](https://developers.openai.com/api/reference/chat-completions/overview)
- [OpenAI Responses API Reference](https://developers.openai.com/api/reference/responses/overview)

## Next steps

> [!div class="nextstepaction"]
> [Hyperlight CodeAct](../../integrations/by-component/context-providers/hyperlight.md)
