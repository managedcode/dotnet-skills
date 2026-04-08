---
title: Azure OpenAI ChatCompletion Agents
description: Learn how to use Microsoft Agent Framework with Azure OpenAI ChatCompletion service.
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: tutorial
ms.author: westey
ms.date: 09/24/2025
ms.service: agent-framework
---

# Azure OpenAI ChatCompletion Agents

Microsoft Agent Framework supports creating agents that use the [Azure OpenAI ChatCompletion](/azure/ai-foundry/openai/how-to/chatgpt) service.

::: zone pivot="programming-language-csharp"

## Getting Started

Add the required NuGet packages to your project.

```dotnetcli
dotnet add package Azure.AI.OpenAI --prerelease
dotnet add package Azure.Identity
dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
```

## Create an Azure OpenAI ChatCompletion Agent

As a first step you need to create a client to connect to the Azure OpenAI service.

```csharp
using System;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using OpenAI;

AzureOpenAIClient client = new AzureOpenAIClient(
    new Uri("https://<myresource>.openai.azure.com"),
    new AzureCliCredential());
```

Azure OpenAI supports multiple services that all provide model calling capabilities.
Pick the ChatCompletion service to create a ChatCompletion based agent.

```csharp
var chatCompletionClient = client.GetChatClient("gpt-4o-mini");
```

Finally, create the agent using the `AsAIAgent` extension method on the `ChatCompletionClient`.

```csharp
AIAgent agent = chatCompletionClient.AsAIAgent(
    instructions: "You are good at telling jokes.",
    name: "Joker");
// Invoke the agent and output the text result.
Console.WriteLine(await agent.RunAsync("Tell me a joke about a pirate."));
```

## Agent Features

### Function Tools

You can provide custom function tools to Azure OpenAI ChatCompletion agents:

```csharp
using System;
using System.ComponentModel;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-4o-mini";

[Description("Get the weather for a given location.")]
static string GetWeather([Description("The location to get the weather for.")] string location)
    => $"The weather in {location} is cloudy with a high of 15°C.";

// Create the chat client and agent, and provide the function tool to the agent.
AIAgent agent = new AzureOpenAIClient(
    new Uri(endpoint),
    new AzureCliCredential())
     .GetChatClient(deploymentName)
     .AsAIAgent(instructions: "You are a helpful assistant", tools: [AIFunctionFactory.Create(GetWeather)]);

// Non-streaming agent interaction with function tools.
Console.WriteLine(await agent.RunAsync("What is the weather like in Amsterdam?"));
```

### Streaming Responses

Get responses as they are generated using streaming:

```csharp
AIAgent agent = chatCompletionClient.AsAIAgent(
    instructions: "You are good at telling jokes.",
    name: "Joker");

// Invoke the agent with streaming support.
await foreach (var update in agent.RunStreamingAsync("Tell me a joke about a pirate."))
{
    Console.Write(update);
}
```

## Using the Agent

The agent is a standard `AIAgent` and supports all standard `AIAgent` operations.

For more information on how to run and interact with agents, see the [Agent getting started tutorials](../../../tutorials/overview.md).

::: zone-end
::: zone pivot="programming-language-python"

## Configuration

### Environment Variables

Before using Azure OpenAI ChatCompletion agents, you need to set up these environment variables:

```bash
export AZURE_OPENAI_ENDPOINT="https://<myresource>.openai.azure.com"
export AZURE_OPENAI_CHAT_DEPLOYMENT_NAME="gpt-4o-mini"
```

Optionally, you can also set:

```bash
export AZURE_OPENAI_API_VERSION="2024-10-21"  # Default API version
export AZURE_OPENAI_API_KEY="<your-api-key>"  # If not using Azure CLI authentication
```

### Installation

Add the Agent Framework package to your project:

```bash
pip install agent-framework-core --pre
```

## Getting Started

### Authentication

Azure OpenAI agents use Azure credentials for authentication. The simplest approach is to use `AzureCliCredential` after running `az login`:

```python
from azure.identity import AzureCliCredential

credential = AzureCliCredential()
```

## Create an Azure OpenAI ChatCompletion Agent

### Basic Agent Creation

The simplest way to create an agent is using the `AzureOpenAIChatClient` with environment variables:

```python
import asyncio
from agent_framework.azure import AzureOpenAIChatClient
from azure.identity import AzureCliCredential

async def main():
    agent = AzureOpenAIChatClient(credential=AzureCliCredential()).as_agent(
        instructions="You are good at telling jokes.",
        name="Joker"
    )

    result = await agent.run("Tell me a joke about a pirate.")
    print(result.text)

asyncio.run(main())
```

### Explicit Configuration

You can also provide configuration explicitly instead of using environment variables:

```python
import asyncio
from agent_framework.azure import AzureOpenAIChatClient
from azure.identity import AzureCliCredential

async def main():
    agent = AzureOpenAIChatClient(
        endpoint="https://<myresource>.openai.azure.com",
        deployment_name="gpt-4o-mini",
        credential=AzureCliCredential()
    ).as_agent(
        instructions="You are good at telling jokes.",
        name="Joker"
    )

    result = await agent.run("Tell me a joke about a pirate.")
    print(result.text)

asyncio.run(main())
```

## Agent Features

### Function Tools

You can provide custom function tools to Azure OpenAI ChatCompletion agents:

```python
import asyncio
from typing import Annotated
from agent_framework.azure import AzureOpenAIChatClient
from azure.identity import AzureCliCredential
from pydantic import Field

def get_weather(
    location: Annotated[str, Field(description="The location to get the weather for.")],
) -> str:
    """Get the weather for a given location."""
    return f"The weather in {location} is sunny with a high of 25°C."

async def main():
    agent = AzureOpenAIChatClient(credential=AzureCliCredential()).as_agent(
        instructions="You are a helpful weather assistant.",
        tools=get_weather
    )

    result = await agent.run("What's the weather like in Seattle?")
    print(result.text)

asyncio.run(main())
```

### Using Threads for Context Management

Maintain conversation context across multiple interactions:

```python
import asyncio
from agent_framework.azure import AzureOpenAIChatClient
from azure.identity import AzureCliCredential

async def main():
    agent = AzureOpenAIChatClient(credential=AzureCliCredential()).as_agent(
        instructions="You are a helpful programming assistant."
    )
    
    # Create a new thread for conversation context
    thread = agent.get_new_thread()
    
    # First interaction
    result1 = await agent.run("I'm working on a Python web application.", thread=thread, store=True)
    print(f"Assistant: {result1.text}")
    
    # Second interaction - context is preserved
    result2 = await agent.run("What framework should I use?", thread=thread, store=True)
    print(f"Assistant: {result2.text}")

asyncio.run(main())
```

### Streaming Responses

Get responses as they are generated using streaming:

```python
import asyncio
from agent_framework.azure import AzureOpenAIChatClient
from azure.identity import AzureCliCredential

async def main():
    agent = AzureOpenAIChatClient(credential=AzureCliCredential()).as_agent(
        instructions="You are a helpful assistant."
    )

    print("Agent: ", end="", flush=True)
    async for chunk in agent.run_stream("Tell me a short story about a robot"):
        if chunk.text:
            print(chunk.text, end="", flush=True)
    print()

asyncio.run(main())
```

## Using the Agent

The agent is a standard `BaseAgent` and supports all standard agent operations.

For more information on how to run and interact with agents, see the [Agent getting started tutorials](../../../tutorials/overview.md).

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [OpenAI Response Agents](./azure-openai-responses-agent.md)
