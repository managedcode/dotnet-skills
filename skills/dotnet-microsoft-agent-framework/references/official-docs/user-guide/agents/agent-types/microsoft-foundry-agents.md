---
title: Microsoft Foundry Agents
description: Learn how to use Microsoft Agent Framework with Azure AI Foundry — persistent service agents, Chat Completions models, and Responses models.
zone_pivot_groups: programming-languages
ms.topic: concept
ms.date: 03/17/2026
ms.service: agent-framework
---

# Microsoft Foundry Agents

Microsoft Agent Framework supports three ways to work with Azure AI Foundry:

| Mode | API | History | NuGet |
| --- | --- | --- | --- |
| **Persistent (service-managed) agents** | Azure AI Foundry Agents SDK | Service-owned threads | `Microsoft.Agents.AI.AzureAI.Persistent` |
| **Foundry Models — Chat Completions** | OpenAI Chat Completions | Local or custom store | `Microsoft.Agents.AI.OpenAI` |
| **Foundry Models — Responses** | OpenAI Responses | Local or custom store | `Microsoft.Agents.AI.OpenAI` |

Choose the persistent agent path when you need service-managed threads, managed tools (code interpreter, file search, web search), or operational agent lifecycle managed by the platform.

Choose the Foundry Models path (Chat Completions or Responses) when you want to bring your own state, keep portability, and use the broadest range of open-source and partner models from the Foundry model catalog through an OpenAI-compatible API.

::: zone pivot="programming-language-csharp"

## Persistent Azure AI Foundry Agents

### Getting Started

Add the required NuGet packages.

```dotnetcli
dotnet add package Azure.Identity
dotnet add package Microsoft.Agents.AI.AzureAI.Persistent --prerelease
```

### Create and Run a Persistent Agent

```csharp
using System;
using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.Agents.AI;

var persistentAgentsClient = new PersistentAgentsClient(
    "https://<myresource>.services.ai.azure.com/api/projects/<myproject>",
    new AzureCliCredential());

// Create a persistent agent resource
var agentMetadata = await persistentAgentsClient.Administration.CreateAgentAsync(
    model: "gpt-4o-mini",
    name: "Joker",
    instructions: "You are good at telling jokes.");

// Retrieve it as an AIAgent
AIAgent agent = await persistentAgentsClient.GetAIAgentAsync(agentMetadata.Value.Id);

Console.WriteLine(await agent.RunAsync("Tell me a joke about a pirate."));
```

### Using Agent Framework Helpers

You can create and return an `AIAgent` in one step:

```csharp
AIAgent agent = await persistentAgentsClient.CreateAIAgentAsync(
    model: "gpt-4o-mini",
    name: "Joker",
    instructions: "You are good at telling jokes.");
```

### Reusing Existing Foundry Agents

Retrieve an existing agent by its ID:

```csharp
AIAgent agent = await persistentAgentsClient.GetAIAgentAsync("<agent-id>");
```

## Foundry Models — Chat Completions

### Getting Started

Add the required NuGet packages.

```powershell
dotnet add package Azure.Identity
dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
```

### Create an OpenAI Chat Completion Agent with Foundry Models

```csharp
using System;
using System.ClientModel.Primitives;
using Azure.Identity;
using Microsoft.Agents.AI;
using OpenAI;

var clientOptions = new OpenAIClientOptions()
{
    Endpoint = new Uri("https://<myresource>.services.ai.azure.com/openai/v1/")
};

#pragma warning disable OPENAI001
OpenAIClient client = new OpenAIClient(
    new BearerTokenPolicy(new AzureCliCredential(), "https://ai.azure.com/.default"),
    clientOptions);
#pragma warning restore OPENAI001
// Or: new OpenAIClient(new ApiKeyCredential("<your_api_key>"), clientOptions);

var chatCompletionClient = client.GetChatClient("gpt-4o-mini");

AIAgent agent = chatCompletionClient.AsAIAgent(
    instructions: "You are good at telling jokes.",
    name: "Joker");

Console.WriteLine(await agent.RunAsync("Tell me a joke about a pirate."));
```

## Foundry Models — Responses

### Create an OpenAI Responses Agent with Foundry Models

Use the same client setup as above, then use the Responses client:

```csharp
#pragma warning disable OPENAI001
var responseClient = client.GetOpenAIResponseClient("gpt-4o-mini");
#pragma warning restore OPENAI001

AIAgent agent = responseClient.AsAIAgent(
    instructions: "You are good at telling jokes.",
    name: "Joker");

Console.WriteLine(await agent.RunAsync("Tell me a joke about a pirate."));
```

::: zone-end
::: zone pivot="programming-language-python"

## Persistent Azure AI Foundry Agents (Python)

### Environment Variables

```bash
export AZURE_AI_PROJECT_ENDPOINT="https://<your-project>.services.ai.azure.com/api/projects/<project-id>"
export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4o-mini"
```

### Installation

```bash
pip install agent-framework-azure-ai --pre
```

### Basic Agent Creation

```python
import asyncio
from agent_framework.azure import AzureAIAgentClient
from azure.identity.aio import AzureCliCredential

async def main():
    async with (
        AzureCliCredential() as credential,
        AzureAIAgentClient(async_credential=credential).as_agent(
            name="HelperAgent",
            instructions="You are a helpful assistant."
        ) as agent,
    ):
        result = await agent.run("Hello!")
        print(result.text)

asyncio.run(main())
```

### Function Tools

```python
import asyncio
from typing import Annotated
from agent_framework.azure import AzureAIAgentClient
from azure.identity.aio import AzureCliCredential
from pydantic import Field

def get_weather(
    location: Annotated[str, Field(description="The location to get weather for.")],
) -> str:
    """Get the weather for a given location."""
    return f"The weather in {location} is sunny."

async def main():
    async with (
        AzureCliCredential() as credential,
        AzureAIAgentClient(async_credential=credential).as_agent(
            name="WeatherAgent",
            instructions="You are a weather assistant.",
            tools=get_weather
        ) as agent,
    ):
        result = await agent.run("What's the weather in Seattle?")
        print(result.text)

asyncio.run(main())
```

### Streaming Responses

```python
import asyncio
from agent_framework.azure import AzureAIAgentClient
from azure.identity.aio import AzureCliCredential

async def main():
    async with (
        AzureCliCredential() as credential,
        AzureAIAgentClient(async_credential=credential).as_agent(
            name="StreamingAgent",
            instructions="You are a helpful assistant."
        ) as agent,
    ):
        print("Agent: ", end="", flush=True)
        async for chunk in agent.run_stream("Tell me a short story"):
            if chunk.text:
                print(chunk.text, end="", flush=True)
        print()

asyncio.run(main())
```

### Foundry Models (Python)

For Foundry Models via Chat Completions or Responses from Python, use the `AzureAIAgentClient` with the appropriate Foundry Models endpoint instead of the Foundry Agents project endpoint.

```python
import asyncio
from agent_framework.azure import AzureAIAgentClient
from azure.identity.aio import AzureCliCredential

async def main():
    async with (
        AzureCliCredential() as credential,
        AzureAIAgentClient(
            project_endpoint="https://<myresource>.services.ai.azure.com/openai/v1/",
            model_deployment_name="gpt-4o-mini",
            async_credential=credential
        ).as_agent(
            name="Joker",
            instructions="You are good at telling jokes."
        ) as agent,
    ):
        result = await agent.run("Tell me a joke about a pirate.")
        print(result.text)

asyncio.run(main())
```

::: zone-end

## Key Differences

| Concern | Persistent Agent | Foundry Models (CC/Responses) |
| --- | --- | --- |
| Thread storage | Service-owned | Local or custom store |
| Hosted tools | Yes (code interpreter, file search) | No (function tools only) |
| Agent lifecycle | Managed by Azure AI Foundry | In-process |
| Portability | Lower | Higher |
| Best for | Managed resources, file access, code exec | Broadest model range, own state |

## Using Any Agent

Every agent created through these paths is a standard `AIAgent` and supports all standard `AIAgent` operations including multi-turn conversations, function tools, middleware, and streaming.

See the [Agent getting started tutorials](../../../tutorials/overview.md) for more information.

## Source

Consolidated from upstream "Microsoft Foundry Agents | Microsoft Learn":
- `https://learn.microsoft.com/agent-framework/user-guide/agents/agent-types/azure-ai-foundry-agent`
- `https://learn.microsoft.com/agent-framework/user-guide/agents/agent-types/azure-ai-foundry-models-chat-completion-agent`
- `https://learn.microsoft.com/agent-framework/user-guide/agents/agent-types/azure-ai-foundry-models-responses-agent`
