---
title: Azure OpenAI Responses Agents
description: Learn how to use Microsoft Agent Framework with Azure OpenAI Responses service.
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: tutorial
ms.author: westey
ms.date: 09/24/2025
ms.service: agent-framework
---

# Azure OpenAI Responses Agents

Microsoft Agent Framework supports creating agents that use the [Azure OpenAI Responses](/azure/ai-foundry/openai/how-to/responses) service.

::: zone pivot="programming-language-csharp"

## Getting Started

Add the required NuGet packages to your project.

```dotnetcli
dotnet add package Azure.AI.OpenAI --prerelease
dotnet add package Azure.Identity
dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
```

## Create an Azure OpenAI Responses Agent

As a first step you need to create a client to connect to the Azure OpenAI service.

```csharp
using System;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using OpenAI;

AzureOpenAIClient client = new AzureOpenAIClient(
    new Uri("https://<myresource>.openai.azure.com/"),
    new AzureCliCredential());
```

Azure OpenAI supports multiple services that all provide model calling capabilities.
Pick the Responses service to create a Responses based agent.

```csharp
#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates.
var responseClient = client.GetOpenAIResponseClient("gpt-4o-mini");
#pragma warning restore OPENAI001
```

Finally, create the agent using the `AsAIAgent` extension method on the `ResponseClient`.

```csharp
AIAgent agent = responseClient.AsAIAgent(
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

## Configuration

### Environment Variables

Before using Azure OpenAI Responses agents, you need to set up these environment variables:

```bash
export AZURE_OPENAI_ENDPOINT="https://<myresource>.openai.azure.com"
export AZURE_OPENAI_RESPONSES_DEPLOYMENT_NAME="gpt-4o-mini"
```

Optionally, you can also set:

```bash
export AZURE_OPENAI_API_VERSION="preview"  # Required for Responses API
export AZURE_OPENAI_API_KEY="<your-api-key>"  # If not using Azure CLI authentication
```

### Installation

Add the Agent Framework package to your project:

```bash
pip install agent-framework-core --pre
```

## Getting Started

### Authentication

Azure OpenAI Responses agents use Azure credentials for authentication. The simplest approach is to use `AzureCliCredential` after running `az login`:

```python
from azure.identity import AzureCliCredential

credential = AzureCliCredential()
```

## Create an Azure OpenAI Responses Agent

### Basic Agent Creation

The simplest way to create an agent is using the `AzureOpenAIResponsesClient` with environment variables:

```python
import asyncio
from agent_framework.azure import AzureOpenAIResponsesClient
from azure.identity import AzureCliCredential

async def main():
    agent = AzureOpenAIResponsesClient(credential=AzureCliCredential()).as_agent(
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
from agent_framework.azure import AzureOpenAIResponsesClient
from azure.identity import AzureCliCredential

async def main():
    agent = AzureOpenAIResponsesClient(
        endpoint="https://<myresource>.openai.azure.com",
        deployment_name="gpt-4o-mini",
        api_version="preview",
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

### Reasoning Models

Azure OpenAI Responses agents support advanced reasoning models like o1 for complex problem-solving:

```python
import asyncio
from agent_framework.azure import AzureOpenAIResponsesClient
from azure.identity import AzureCliCredential

async def main():
    agent = AzureOpenAIResponsesClient(
        deployment_name="o1-preview",  # Use reasoning model
        credential=AzureCliCredential()
    ).as_agent(
        instructions="You are a helpful assistant that excels at complex reasoning.",
        name="ReasoningAgent"
    )
    
    result = await agent.run("Solve this logic puzzle: If A > B, B > C, and C > D, and we know D = 5, B = 10, what can we determine about A?")
    print(result.text)

asyncio.run(main())
```

### Structured Output

Get structured responses from Azure OpenAI Responses agents:

```python
import asyncio
from typing import Annotated
from agent_framework.azure import AzureOpenAIResponsesClient
from azure.identity import AzureCliCredential
from pydantic import BaseModel, Field

class WeatherForecast(BaseModel):
    location: Annotated[str, Field(description="The location")]
    temperature: Annotated[int, Field(description="Temperature in Celsius")]
    condition: Annotated[str, Field(description="Weather condition")]
    humidity: Annotated[int, Field(description="Humidity percentage")]

async def main():
    agent = AzureOpenAIResponsesClient(credential=AzureCliCredential()).as_agent(
        instructions="You are a weather assistant that provides structured forecasts.",
        response_format=WeatherForecast
    )
    
    result = await agent.run("What's the weather like in Paris today?")
    weather_data = result.value
    print(f"Location: {weather_data.location}")
    print(f"Temperature: {weather_data.temperature}°C")
    print(f"Condition: {weather_data.condition}")
    print(f"Humidity: {weather_data.humidity}%")

asyncio.run(main())
```

### Function Tools

You can provide custom function tools to Azure OpenAI Responses agents:

```python
import asyncio
from typing import Annotated
from agent_framework.azure import AzureOpenAIResponsesClient
from azure.identity import AzureCliCredential
from pydantic import Field

def get_weather(
    location: Annotated[str, Field(description="The location to get the weather for.")],
) -> str:
    """Get the weather for a given location."""
    return f"The weather in {location} is sunny with a high of 25°C."

async def main():
    agent = AzureOpenAIResponsesClient(credential=AzureCliCredential()).as_agent(
        instructions="You are a helpful weather assistant.",
        tools=get_weather
    )

    result = await agent.run("What's the weather like in Seattle?")
    print(result.text)

asyncio.run(main())
```

### Code Interpreter

Azure OpenAI Responses agents support code execution through the hosted code interpreter:

```python
import asyncio
from agent_framework import ChatAgent, HostedCodeInterpreterTool
from agent_framework.azure import AzureOpenAIResponsesClient
from azure.identity import AzureCliCredential

async def main():
    async with ChatAgent(
        chat_client=AzureOpenAIResponsesClient(credential=AzureCliCredential()),
        instructions="You are a helpful assistant that can write and execute Python code.",
        tools=HostedCodeInterpreterTool()
    ) as agent:
        result = await agent.run("Calculate the factorial of 20 using Python code.")
        print(result.text)

asyncio.run(main())
```

#### Code Interpreter with File Upload

For data analysis tasks, you can upload files and analyze them with code:

```python
import asyncio
import os
import tempfile
from agent_framework import ChatAgent, HostedCodeInterpreterTool
from agent_framework.azure import AzureOpenAIResponsesClient
from azure.identity import AzureCliCredential
from openai import AsyncAzureOpenAI

async def create_sample_file_and_upload(openai_client: AsyncAzureOpenAI) -> tuple[str, str]:
    """Create a sample CSV file and upload it to Azure OpenAI."""
    csv_data = """name,department,salary,years_experience
Alice Johnson,Engineering,95000,5
Bob Smith,Sales,75000,3
Carol Williams,Engineering,105000,8
David Brown,Marketing,68000,2
Emma Davis,Sales,82000,4
Frank Wilson,Engineering,88000,6
"""
    
    # Create temporary CSV file
    with tempfile.NamedTemporaryFile(mode="w", suffix=".csv", delete=False) as temp_file:
        temp_file.write(csv_data)
        temp_file_path = temp_file.name

    # Upload file to Azure OpenAI
    print("Uploading file to Azure OpenAI...")
    with open(temp_file_path, "rb") as file:
        uploaded_file = await openai_client.files.create(
            file=file,
            purpose="assistants",  # Required for code interpreter
        )

    print(f"File uploaded with ID: {uploaded_file.id}")
    return temp_file_path, uploaded_file.id

async def cleanup_files(openai_client: AsyncAzureOpenAI, temp_file_path: str, file_id: str) -> None:
    """Clean up both local temporary file and uploaded file."""
    # Clean up: delete the uploaded file
    await openai_client.files.delete(file_id)
    print(f"Cleaned up uploaded file: {file_id}")

    # Clean up temporary local file
    os.unlink(temp_file_path)
    print(f"Cleaned up temporary file: {temp_file_path}")

async def main():
    print("=== Azure OpenAI Code Interpreter with File Upload ===")

    # Initialize Azure OpenAI client for file operations
    credential = AzureCliCredential()

    async def get_token():
        token = credential.get_token("https://cognitiveservices.azure.com/.default")
        return token.token

    openai_client = AsyncAzureOpenAI(
        azure_ad_token_provider=get_token,
        api_version="2024-05-01-preview",
    )

    temp_file_path, file_id = await create_sample_file_and_upload(openai_client)

    # Create agent using Azure OpenAI Responses client
    async with ChatAgent(
        chat_client=AzureOpenAIResponsesClient(credential=credential),
        instructions="You are a helpful assistant that can analyze data files using Python code.",
        tools=HostedCodeInterpreterTool(inputs=[{"file_id": file_id}]),
    ) as agent:
        # Test the code interpreter with the uploaded file
        query = "Analyze the employee data in the uploaded CSV file. Calculate average salary by department."
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Agent: {result.text}")

    await cleanup_files(openai_client, temp_file_path, file_id)

asyncio.run(main())
```

### File Search

Enable your agent to search through uploaded documents and files:

```python
import asyncio
from agent_framework import ChatAgent, HostedFileSearchTool, HostedVectorStoreContent
from agent_framework.azure import AzureOpenAIResponsesClient
from azure.identity import AzureCliCredential

async def create_vector_store(client: AzureOpenAIResponsesClient) -> tuple[str, HostedVectorStoreContent]:
    """Create a vector store with sample documents."""
    file = await client.client.files.create(
        file=("todays_weather.txt", b"The weather today is sunny with a high of 75F."), 
        purpose="assistants"
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

async def delete_vector_store(client: AzureOpenAIResponsesClient, file_id: str, vector_store_id: str) -> None:
    """Delete the vector store after using it."""
    await client.client.vector_stores.delete(vector_store_id=vector_store_id)
    await client.client.files.delete(file_id=file_id)

async def main():
    print("=== Azure OpenAI Responses Client with File Search Example ===\n")

    # Initialize Responses client
    client = AzureOpenAIResponsesClient(credential=AzureCliCredential())

    file_id, vector_store = await create_vector_store(client)

    async with ChatAgent(
        chat_client=client,
        instructions="You are a helpful assistant that can search through files to find information.",
        tools=[HostedFileSearchTool(inputs=vector_store)],
    ) as agent:
        query = "What is the weather today? Do a file search to find the answer."
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Agent: {result}\n")

    await delete_vector_store(client, file_id, vector_store.vector_store_id)

asyncio.run(main())
```

### Model Context Protocol (MCP) Tools

#### Local MCP Tools

Connect to local MCP servers for extended capabilities:

```python
import asyncio
from agent_framework import ChatAgent, MCPStreamableHTTPTool
from agent_framework.azure import AzureOpenAIResponsesClient
from azure.identity import AzureCliCredential

async def main():
    """Example showing local MCP tools for Azure OpenAI Responses Agent."""
    # Create Azure OpenAI Responses client
    responses_client = AzureOpenAIResponsesClient(credential=AzureCliCredential())
    
    # Create agent
    agent = responses_client.as_agent(
        name="DocsAgent",
        instructions="You are a helpful assistant that can help with Microsoft documentation questions.",
    )

    # Connect to the MCP server (Streamable HTTP)
    async with MCPStreamableHTTPTool(
        name="Microsoft Learn MCP",
        url="https://learn.microsoft.com/api/mcp",
    ) as mcp_tool:
        # First query — expect the agent to use the MCP tool if it helps
        first_query = "How to create an Azure storage account using az cli?"
        first_result = await agent.run(first_query, tools=mcp_tool)
        print("\n=== Answer 1 ===\n", first_result.text)

        # Follow-up query (connection is reused)
        second_query = "What is Microsoft Agent Framework?"
        second_result = await agent.run(second_query, tools=mcp_tool)
        print("\n=== Answer 2 ===\n", second_result.text)

asyncio.run(main())
```

#### Hosted MCP Tools

Use hosted MCP tools with approval workflows:

```python
import asyncio
from agent_framework import ChatAgent, HostedMCPTool
from agent_framework.azure import AzureOpenAIResponsesClient
from azure.identity import AzureCliCredential

async def main():
    """Example showing hosted MCP tools without approvals."""
    credential = AzureCliCredential()
    
    async with ChatAgent(
        chat_client=AzureOpenAIResponsesClient(credential=credential),
        name="DocsAgent",
        instructions="You are a helpful assistant that can help with microsoft documentation questions.",
        tools=HostedMCPTool(
            name="Microsoft Learn MCP",
            url="https://learn.microsoft.com/api/mcp",
            # Auto-approve all function calls for seamless experience
            approval_mode="never_require",
        ),
    ) as agent:
        # First query
        first_query = "How to create an Azure storage account using az cli?"
        print(f"User: {first_query}")
        first_result = await agent.run(first_query)
        print(f"Agent: {first_result.text}\n")
        
        print("\n=======================================\n")
        
        # Second query
        second_query = "What is Microsoft Agent Framework?"
        print(f"User: {second_query}")
        second_result = await agent.run(second_query)
        print(f"Agent: {second_result.text}\n")

asyncio.run(main())
```

### Image Analysis

Azure OpenAI Responses agents support multimodal interactions including image analysis:

```python
import asyncio
from agent_framework import ChatMessage, TextContent, UriContent
from agent_framework.azure import AzureOpenAIResponsesClient
from azure.identity import AzureCliCredential

async def main():
    print("=== Azure Responses Agent with Image Analysis ===")

    # Create an Azure Responses agent with vision capabilities
    agent = AzureOpenAIResponsesClient(credential=AzureCliCredential()).as_agent(
        name="VisionAgent",
        instructions="You are a helpful agent that can analyze images.",
    )

    # Create a message with both text and image content
    user_message = ChatMessage(
        role="user",
        contents=[
            TextContent(text="What do you see in this image?"),
            UriContent(
                uri="https://upload.wikimedia.org/wikipedia/commons/thumb/d/dd/Gfp-wisconsin-madison-the-nature-boardwalk.jpg/2560px-Gfp-wisconsin-madison-the-nature-boardwalk.jpg",
                media_type="image/jpeg",
            ),
        ],
    )

    # Get the agent's response
    print("User: What do you see in this image? [Image provided]")
    result = await agent.run(user_message)
    print(f"Agent: {result.text}")

asyncio.run(main())
```

### Using Threads for Context Management

Maintain conversation context across multiple interactions:

```python
import asyncio
from agent_framework.azure import AzureOpenAIResponsesClient
from azure.identity import AzureCliCredential

async def main():
    agent = AzureOpenAIResponsesClient(credential=AzureCliCredential()).as_agent(
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
from agent_framework.azure import AzureOpenAIResponsesClient
from azure.identity import AzureCliCredential

async def main():
    agent = AzureOpenAIResponsesClient(credential=AzureCliCredential()).as_agent(
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
> [OpenAI Chat Completion Agents](./openai-chat-completion-agent.md)
