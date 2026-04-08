---
title: OpenAI Assistants Agents
description: Learn how to use Microsoft Agent Framework with OpenAI Assistants service.
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: tutorial
ms.author: westey
ms.date: 09/24/2025
ms.service: agent-framework
---

# OpenAI Assistants Agents

Microsoft Agent Framework supports creating agents that use the [OpenAI Assistants](https://platform.openai.com/docs/api-reference/assistants/createAssistant) service.

> [!WARNING]
> The OpenAI Assistants API is deprecated and will be shut down. For more information see the [OpenAI documentation](https://platform.openai.com/docs/assistants/migration).

::: zone pivot="programming-language-csharp"

## Getting Started

Add the required NuGet packages to your project.

```dotnetcli
dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
```

## Create an OpenAI Assistants Agent

As a first step you need to create a client to connect to the OpenAI service.

```csharp
using System;
using Microsoft.Agents.AI;
using OpenAI;

OpenAIClient client = new OpenAIClient("<your_api_key>");
```

OpenAI supports multiple services that all provide model-calling capabilities.
This example uses the Assistants client to create an Assistants-based agent.

```csharp
#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates.
var assistantClient = client.GetAssistantClient();
#pragma warning restore OPENAI001
```

To use the OpenAI Assistants service, you need create an assistant resource in the service.
This can be done using either the OpenAI SDK or using Microsoft Agent Framework helpers.

### Using the OpenAI SDK

Create an assistant and retrieve it as an `AIAgent` using the client.

```csharp
// Create a server-side assistant
var createResult = await assistantClient.CreateAssistantAsync(
    "gpt-4o-mini",
    new() { Name = "Joker", Instructions = "You are good at telling jokes." });

// Retrieve the assistant as an AIAgent
AIAgent agent1 = await assistantClient.GetAIAgentAsync(createResult.Value.Id);

// Invoke the agent and output the text result.
Console.WriteLine(await agent1.RunAsync("Tell me a joke about a pirate."));
```

### Using Agent Framework helpers

You can also create and return an `AIAgent` in one step:

```csharp
AIAgent agent2 = await assistantClient.CreateAIAgentAsync(
    model: "gpt-4o-mini",
    name: "Joker",
    instructions: "You are good at telling jokes.");
```

## Reusing OpenAI Assistants

You can reuse existing OpenAI Assistants by retrieving them using their IDs.

```csharp
AIAgent agent3 = await assistantClient.GetAIAgentAsync("<agent-id>");
```

## Using the Agent

The agent is a standard `AIAgent` and supports all standard agent operations.

For more information on how to run and interact with agents, see the [Agent getting started tutorials](../../../tutorials/overview.md).

::: zone-end
::: zone pivot="programming-language-python"

## Prerequisites

Install the Microsoft Agent Framework package.

```bash
pip install agent-framework --pre
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
from agent_framework.openai import OpenAIAssistantsClient
```

## Create an OpenAI Assistants Agent

### Basic Agent Creation

The simplest way to create an agent is by using the `OpenAIAssistantsClient` which automatically creates and manages assistants:

```python
async def basic_example():
    # Create an agent with automatic assistant creation and cleanup
    async with OpenAIAssistantsClient().as_agent(
        instructions="You are a helpful assistant.",
        name="MyAssistant"
    ) as agent:
        result = await agent.run("Hello, how are you?")
        print(result.text)
```

### Using Explicit Configuration

You can provide explicit configuration instead of relying on environment variables:

```python
async def explicit_config_example():
    async with OpenAIAssistantsClient(
        ai_model_id="gpt-4o-mini",
        api_key="your-api-key-here",
    ).as_agent(
        instructions="You are a helpful assistant.",
    ) as agent:
        result = await agent.run("What's the weather like?")
        print(result.text)
```

### Using an Existing Assistant

You can reuse existing OpenAI assistants by providing their IDs:

```python
from openai import AsyncOpenAI

async def existing_assistant_example():
    # Create OpenAI client directly
    client = AsyncOpenAI()

    # Create or get an existing assistant
    assistant = await client.beta.assistants.create(
        model="gpt-4o-mini",
        name="WeatherAssistant",
        instructions="You are a weather forecasting assistant."
    )

    try:
        # Use the existing assistant with Agent Framework
        async with ChatAgent(
            chat_client=OpenAIAssistantsClient(
                async_client=client,
                assistant_id=assistant.id
            ),
            instructions="You are a helpful weather agent.",
        ) as agent:
            result = await agent.run("What's the weather like in Seattle?")
            print(result.text)
    finally:
        # Clean up the assistant
        await client.beta.assistants.delete(assistant.id)
```

## Agent Features

### Function Tools

You can equip your assistant with custom functions:

```python
from typing import Annotated
from pydantic import Field

def get_weather(
    location: Annotated[str, Field(description="The location to get the weather for.")]
) -> str:
    """Get the weather for a given location."""
    return f"The weather in {location} is sunny with 25°C."

async def tools_example():
    async with ChatAgent(
        chat_client=OpenAIAssistantsClient(),
        instructions="You are a helpful weather assistant.",
        tools=get_weather,  # Provide tools to the agent
    ) as agent:
        result = await agent.run("What's the weather like in Tokyo?")
        print(result.text)
```

### Code Interpreter

Enable your assistant to execute Python code:

```python
from agent_framework import HostedCodeInterpreterTool

async def code_interpreter_example():
    async with ChatAgent(
        chat_client=OpenAIAssistantsClient(),
        instructions="You are a helpful assistant that can write and execute Python code.",
        tools=HostedCodeInterpreterTool(),
    ) as agent:
        result = await agent.run("Calculate the factorial of 100 using Python code.")
        print(result.text)
```

### File Search

Enable your assistant to search through uploaded documents:

```python
from agent_framework import HostedFileSearchTool, HostedVectorStoreContent

async def create_vector_store(client: OpenAIAssistantsClient) -> tuple[str, HostedVectorStoreContent]:
    """Create a vector store with sample documents."""
    file = await client.client.files.create(
        file=("todays_weather.txt", b"The weather today is sunny with a high of 75F."), 
        purpose="user_data"
    )
    vector_store = await client.client.vector_stores.create(
        name="knowledge_base",
        expires_after={"anchor": "last_active_at", "days": 1},
    )
    result = await client.client.vector_stores.files.create_and_poll(
        vector_store_id=vector_store.id, 
        file_id=file.id
    )
    if result.last_error is not None:
        raise Exception(f"Vector store file processing failed with status: {result.last_error.message}")

    return file.id, HostedVectorStoreContent(vector_store_id=vector_store.id)

async def delete_vector_store(client: OpenAIAssistantsClient, file_id: str, vector_store_id: str) -> None:
    """Delete the vector store after using it."""
    await client.client.vector_stores.delete(vector_store_id=vector_store_id)
    await client.client.files.delete(file_id=file_id)

async def file_search_example():
    print("=== OpenAI Assistants Client Agent with File Search Example ===\n")

    client = OpenAIAssistantsClient()
    async with ChatAgent(
        chat_client=client,
        instructions="You are a helpful assistant that searches files in a knowledge base.",
        tools=HostedFileSearchTool(),
    ) as agent:
        query = "What is the weather today? Do a file search to find the answer."
        file_id, vector_store = await create_vector_store(client)

        print(f"User: {query}")
        print("Agent: ", end="", flush=True)
        async for chunk in agent.run_stream(
            query, tool_resources={"file_search": {"vector_store_ids": [vector_store.vector_store_id]}}
        ):
            if chunk.text:
                print(chunk.text, end="", flush=True)
        print()  # New line after streaming
        
        await delete_vector_store(client, file_id, vector_store.vector_store_id)
```

### Thread Management

Maintain conversation context across multiple interactions:

```python
async def thread_example():
    async with OpenAIAssistantsClient().as_agent(
        name="Assistant",
        instructions="You are a helpful assistant.",
    ) as agent:
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

### Working with Existing Assistants

You can reuse existing OpenAI assistants by providing their IDs:

```python
from openai import AsyncOpenAI

async def existing_assistant_example():
    # Create OpenAI client directly
    client = AsyncOpenAI()
    
    # Create or get an existing assistant
    assistant = await client.beta.assistants.create(
        model="gpt-4o-mini",
        name="WeatherAssistant",
        instructions="You are a weather forecasting assistant."
    )
    
    try:
        # Use the existing assistant with Agent Framework
        async with OpenAIAssistantsClient(
            async_client=client,
            assistant_id=assistant.id
        ).as_agent() as agent:
            result = await agent.run("What's the weather like in Seattle?")
            print(result.text)
    finally:
        # Clean up the assistant
        await client.beta.assistants.delete(assistant.id)
```

### Streaming Responses

Get responses as they are generated for better user experience:

```python
async def streaming_example():
    async with OpenAIAssistantsClient().as_agent(
        instructions="You are a helpful assistant.",
    ) as agent:
        print("Assistant: ", end="", flush=True)
        async for chunk in agent.run_stream("Tell me a story about AI."):
            if chunk.text:
                print(chunk.text, end="", flush=True)
        print()  # New line after streaming is complete
```

## Using the Agent

The agent is a standard `BaseAgent` and supports all standard agent operations.

For more information on how to run and interact with agents, see the [Agent getting started tutorials](../../../tutorials/overview.md).

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Chat Client Agents](./chat-client-agent.md)
