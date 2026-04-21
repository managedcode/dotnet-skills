---
title: OpenAI ChatCompletion Agents
description: Learn how to use Microsoft Agent Framework with OpenAI ChatCompletion service.
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: tutorial
ms.author: westey
ms.date: 09/24/2025
ms.service: agent-framework
---

# OpenAI ChatCompletion Agents

Microsoft Agent Framework supports creating agents that use the [OpenAI ChatCompletion](https://platform.openai.com/docs/api-reference/chat/create) service.

::: zone pivot="programming-language-csharp"

## Getting Started

Add the required NuGet packages to your project.

```dotnetcli
dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
```

## Create an OpenAI ChatCompletion Agent

As a first step you need to create a client to connect to the OpenAI service.

```csharp
using System;
using Microsoft.Agents.AI;
using OpenAI;

OpenAIClient client = new OpenAIClient("<your_api_key>");
```

OpenAI supports multiple services that all provide model-calling capabilities.
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

## Using the Agent

The agent is a standard `AIAgent` and supports all standard `AIAgent` operations.

For more information on how to run and interact with agents, see the [Agent getting started tutorials](../../../tutorials/overview.md).

::: zone-end
::: zone pivot="programming-language-python"

## Prerequisites

Install the Microsoft Agent Framework package.

```bash
pip install agent-framework-core --pre
```

## Configuration

### Environment Variables

Set up the required environment variables for OpenAI authentication:

```bash
# Required for OpenAI API access
OPENAI_API_KEY="your-openai-api-key"
OPENAI_CHAT_MODEL_ID="gpt-4o-mini"  # or your preferred model
```

Alternatively, you can use a `.env` file in your project root:

```env
OPENAI_API_KEY=your-openai-api-key
OPENAI_CHAT_MODEL_ID=gpt-4o-mini
```

## Getting Started

Import the required classes from Agent Framework:

```python
import asyncio
from agent_framework import ChatAgent
from agent_framework.openai import OpenAIChatClient
```

## Create an OpenAI ChatCompletion Agent

### Basic Agent Creation

The simplest way to create a chat completion agent:

```python
async def basic_example():
    # Create an agent using OpenAI ChatCompletion
    agent = OpenAIChatClient().as_agent(
        name="HelpfulAssistant",
        instructions="You are a helpful assistant.",
    )

    result = await agent.run("Hello, how can you help me?")
    print(result.text)
```

### Using Explicit Configuration

You can provide explicit configuration instead of relying on environment variables:

```python
async def explicit_config_example():
    agent = OpenAIChatClient(
        ai_model_id="gpt-4o-mini",
        api_key="your-api-key-here",
    ).as_agent(
        instructions="You are a helpful assistant.",
    )

    result = await agent.run("What can you do?")
    print(result.text)
```

## Agent Features

### Function Tools

Equip your agent with custom functions:

```python
from typing import Annotated
from pydantic import Field

def get_weather(
    location: Annotated[str, Field(description="The location to get weather for")]
) -> str:
    """Get the weather for a given location."""
    # Your weather API implementation here
    return f"The weather in {location} is sunny with 25°C."

async def tools_example():
    agent = ChatAgent(
        chat_client=OpenAIChatClient(),
        instructions="You are a helpful weather assistant.",
        tools=get_weather,  # Add tools to the agent
    )

    result = await agent.run("What's the weather like in Tokyo?")
    print(result.text)
```

### Web Search

Enable real-time web search capabilities:

```python
from agent_framework import HostedWebSearchTool

async def web_search_example():
    agent = OpenAIChatClient(model_id="gpt-4o-search-preview").as_agent(
        name="SearchBot",
        instructions="You are a helpful assistant that can search the web for current information.",
        tools=HostedWebSearchTool(),
    )

    result = await agent.run("What are the latest developments in artificial intelligence?")
    print(result.text)
```

### Model Context Protocol (MCP) Tools

Connect to local MCP servers for extended capabilities:

```python
from agent_framework import MCPStreamableHTTPTool

async def local_mcp_example():
    agent = OpenAIChatClient().as_agent(
        name="DocsAgent",
        instructions="You are a helpful assistant that can help with Microsoft documentation.",
        tools=MCPStreamableHTTPTool(
            name="Microsoft Learn MCP",
            url="https://learn.microsoft.com/api/mcp",
        ),
    )

    result = await agent.run("How do I create an Azure storage account using az cli?")
    print(result.text)
```

### Thread Management

Maintain conversation context across multiple interactions:

```python
async def thread_example():
    agent = OpenAIChatClient().as_agent(
        name="Agent",
        instructions="You are a helpful assistant.",
    )

    # Create a persistent thread for conversation context
    thread = agent.get_new_thread()

    # First interaction
    first_query = "My name is Alice"
    print(f"User: {first_query}")
    first_result = await agent.run(first_query, thread=thread)
    print(f"Agent: {first_result.text}")

    # Second interaction - agent remembers the context
    second_query = "What's my name?"
    print(f"User: {second_query}")
    second_result = await agent.run(second_query, thread=thread)
    print(f"Agent: {second_result.text}")  # Should remember "Alice"
```

### Streaming Responses

Get responses as they are generated for better user experience:

```python
async def streaming_example():
    agent = OpenAIChatClient().as_agent(
        name="StoryTeller",
        instructions="You are a creative storyteller.",
    )
    
    print("Agent: ", end="", flush=True)
    async for chunk in agent.run_stream("Tell me a short story about AI."):
        if chunk.text:
            print(chunk.text, end="", flush=True)
    print()  # New line after streaming
```

## Using the Agent

The agent is a standard `BaseAgent` and supports all standard agent operations.

For more information on how to run and interact with agents, see the [Agent getting started tutorials](../../../tutorials/overview.md).

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Response Agents](./openai-responses-agent.md)
