---
title: OpenAI Responses Agents
description: Learn how to use Microsoft Agent Framework with OpenAI Responses service.
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: tutorial
ms.author: westey
ms.date: 09/24/2025
ms.service: agent-framework
---

# OpenAI Responses Agents

Microsoft Agent Framework supports creating agents that use the [OpenAI responses](https://platform.openai.com/docs/api-reference/responses/create) service.

::: zone pivot="programming-language-csharp"

## Getting Started

Add the required NuGet packages to your project.

```dotnetcli
dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
```

## Create an OpenAI Responses Agent

As a first step you need to create a client to connect to the OpenAI service.

```csharp
using System;
using Microsoft.Agents.AI;
using OpenAI;

OpenAIClient client = new OpenAIClient("<your_api_key>");
```

OpenAI supports multiple services that all provide model-calling capabilities.
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
OPENAI_RESPONSES_MODEL_ID="gpt-4o"  # or your preferred Responses-compatible model
```

Alternatively, you can use a `.env` file in your project root:

```env
OPENAI_API_KEY=your-openai-api-key
OPENAI_RESPONSES_MODEL_ID=gpt-4o
```

## Getting Started

Import the required classes from Agent Framework:

```python
import asyncio
from agent_framework import ChatAgent
from agent_framework.openai import OpenAIResponsesClient
```

## Create an OpenAI Responses Agent

### Basic Agent Creation

The simplest way to create a responses agent:

```python
async def basic_example():
    # Create an agent using OpenAI Responses
    agent = OpenAIResponsesClient().as_agent(
        name="WeatherBot",
        instructions="You are a helpful weather assistant.",
    )

    result = await agent.run("What's a good way to check the weather?")
    print(result.text)
```

### Using Explicit Configuration

You can provide explicit configuration instead of relying on environment variables:

```python
async def explicit_config_example():
    agent = OpenAIResponsesClient(
        ai_model_id="gpt-4o",
        api_key="your-api-key-here",
    ).as_agent(
        instructions="You are a helpful assistant.",
    )

    result = await agent.run("Tell me about AI.")
    print(result.text)
```

## Basic Usage Patterns

### Streaming Responses

Get responses as they are generated for better user experience:

```python
async def streaming_example():
    agent = OpenAIResponsesClient().as_agent(
        instructions="You are a creative storyteller.",
    )

    print("Agent: ", end="", flush=True)
    async for chunk in agent.run_stream("Tell me a short story about AI."):
        if chunk.text:
            print(chunk.text, end="", flush=True)
    print()  # New line after streaming
```

## Agent Features

### Reasoning Models

Use advanced reasoning capabilities with models like GPT-5:

```python
from agent_framework import HostedCodeInterpreterTool, TextContent, TextReasoningContent

async def reasoning_example():
    agent = OpenAIResponsesClient(ai_model_id="gpt-5").as_agent(
        name="MathTutor",
        instructions="You are a personal math tutor. When asked a math question, "
                    "write and run code to answer the question.",
        tools=HostedCodeInterpreterTool(),
        default_options={"reasoning": {"effort": "high", "summary": "detailed"}},
    )

    print("Agent: ", end="", flush=True)
    async for chunk in agent.run_stream("Solve: 3x + 11 = 14"):
        if chunk.contents:
            for content in chunk.contents:
                if isinstance(content, TextReasoningContent):
                    # Reasoning content in gray text
                    print(f"\033[97m{content.text}\033[0m", end="", flush=True)
                elif isinstance(content, TextContent):
                    print(content.text, end="", flush=True)
    print()
```

### Structured Output

Get responses in structured formats:

```python
from pydantic import BaseModel
from agent_framework import AgentResponse

class CityInfo(BaseModel):
    """A structured output for city information."""
    city: str
    description: str

async def structured_output_example():
    agent = OpenAIResponsesClient().as_agent(
        name="CityExpert",
        instructions="You describe cities in a structured format.",
    )

    # Non-streaming structured output
    result = await agent.run("Tell me about Paris, France", options={"response_format": CityInfo})

    if result.value:
        city_data = result.value
        print(f"City: {city_data.city}")
        print(f"Description: {city_data.description}")

    # Streaming structured output
    structured_result = await AgentRunResponse.from_agent_response_generator(
        agent.run_stream("Tell me about Tokyo, Japan", options={"response_format": CityInfo}),
        output_format_type=CityInfo,
    )

    if structured_result.value:
        tokyo_data = structured_result.value
        print(f"City: {tokyo_data.city}")
        print(f"Description: {tokyo_data.description}")
```

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
    agent = OpenAIResponsesClient().as_agent(
        instructions="You are a helpful weather assistant.",
        tools=get_weather,
    )

    result = await agent.run("What's the weather like in Tokyo?")
    print(result.text)
```

### Code Interpreter

Enable your agent to execute Python code:

```python
from agent_framework import HostedCodeInterpreterTool

async def code_interpreter_example():
    agent = OpenAIResponsesClient().as_agent(
        instructions="You are a helpful assistant that can write and execute Python code.",
        tools=HostedCodeInterpreterTool(),
    )

    result = await agent.run("Calculate the factorial of 100 using Python code.")
    print(result.text)
```

#### Code Interpreter with File Upload

For data analysis tasks, you can upload files and analyze them with code:

```python
import os
import tempfile
from agent_framework import HostedCodeInterpreterTool
from openai import AsyncOpenAI

async def code_interpreter_with_files_example():
    print("=== OpenAI Code Interpreter with File Upload ===")

    # Create the OpenAI client for file operations
    openai_client = AsyncOpenAI()

    # Create sample CSV data
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

    # Upload file to OpenAI
    print("Uploading file to OpenAI...")
    with open(temp_file_path, "rb") as file:
        uploaded_file = await openai_client.files.create(
            file=file,
            purpose="assistants",  # Required for code interpreter
        )

    print(f"File uploaded with ID: {uploaded_file.id}")

    # Create agent using OpenAI Responses client
    agent = ChatAgent(
        chat_client=OpenAIResponsesClient(async_client=openai_client),
        instructions="You are a helpful assistant that can analyze data files using Python code.",
        tools=HostedCodeInterpreterTool(inputs=[{"file_id": uploaded_file.id}]),
    )

    # Test the code interpreter with the uploaded file
    query = "Analyze the employee data in the uploaded CSV file. Calculate average salary by department."
    print(f"User: {query}")
    result = await agent.run(query)
    print(f"Agent: {result.text}")

    # Clean up: delete the uploaded file
    await openai_client.files.delete(uploaded_file.id)
    print(f"Cleaned up uploaded file: {uploaded_file.id}")

    # Clean up temporary local file
    os.unlink(temp_file_path)
    print(f"Cleaned up temporary file: {temp_file_path}")
```

### Thread Management

Maintain conversation context across multiple interactions:

```python
async def thread_example():
    agent = OpenAIResponsesClient().as_agent(
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

### File Search

Enable your agent to search through uploaded documents and files:

```python
from agent_framework import HostedFileSearchTool, HostedVectorStoreContent

async def file_search_example():
    client = OpenAIResponsesClient()

    # Create a file with sample content
    file = await client.client.files.create(
        file=("todays_weather.txt", b"The weather today is sunny with a high of 75F."),
        purpose="user_data"
    )

    # Create a vector store for document storage
    vector_store = await client.client.vector_stores.create(
        name="knowledge_base",
        expires_after={"anchor": "last_active_at", "days": 1},
    )

    # Add file to vector store and wait for processing
    result = await client.client.vector_stores.files.create_and_poll(
        vector_store_id=vector_store.id,
        file_id=file.id
    )

    # Check if processing was successful
    if result.last_error is not None:
        raise Exception(f"Vector store file processing failed with status: {result.last_error.message}")

    # Create vector store content reference
    vector_store_content = HostedVectorStoreContent(vector_store_id=vector_store.id)

    # Create agent with file search capability
    agent = ChatAgent(
        chat_client=client,
        instructions="You are a helpful assistant that can search through files to find information.",
        tools=[HostedFileSearchTool(inputs=vector_store_content)],
    )

    # Test the file search
    message = "What is the weather today? Do a file search to find the answer."
    print(f"User: {message}")

    response = await agent.run(message)
    print(f"Agent: {response}")

    # Cleanup
    await client.client.vector_stores.delete(vector_store.id)
    await client.client.files.delete(file.id)
```

### Web Search

Enable real-time web search capabilities:

```python
from agent_framework import HostedWebSearchTool

async def web_search_example():
    agent = OpenAIResponsesClient().as_agent(
        name="SearchBot",
        instructions="You are a helpful assistant that can search the web for current information.",
        tools=HostedWebSearchTool(),
    )

    result = await agent.run("What are the latest developments in artificial intelligence?")
    print(result.text)
```

### Image Analysis

Analyze and understand images with multi-modal capabilities:

```python
from agent_framework import ChatMessage, TextContent, UriContent

async def image_analysis_example():
    agent = OpenAIResponsesClient().as_agent(
        name="VisionAgent",
        instructions="You are a helpful agent that can analyze images.",
    )

    # Create message with both text and image content
    message = ChatMessage(
        role="user",
        contents=[
            TextContent(text="What do you see in this image?"),
            UriContent(
                uri="your-image-uri",
                media_type="image/jpeg",
            ),
        ],
    )

    result = await agent.run(message)
    print(result.text)
```

### Image Generation

Generate images using the Responses API:

```python
from agent_framework import DataContent, HostedImageGenerationTool, ImageGenerationToolResultContent, UriContent

async def image_generation_example():
    agent = OpenAIResponsesClient().as_agent(
        instructions="You are a helpful AI that can generate images.",
        tools=[
            HostedImageGenerationTool(
                options={
                    "size": "1024x1024",
                    "output_format": "webp",
                }
            )
        ],
    )

    result = await agent.run("Generate an image of a sunset over the ocean.")

    # Check for generated images in the response
    for message in result.messages:
        for content in message.contents:
            if isinstance(content, ImageGenerationToolResultContent) and content.outputs:
                for output in content.outputs:
                    if isinstance(output, (DataContent, UriContent)) and output.uri:
                        print(f"Image generated: {output.uri}")
```

### MCP Tools

Connect to MCP servers from within the agent for extended capabilities:

```python
from agent_framework import MCPStreamableHTTPTool

async def local_mcp_example():
    agent = OpenAIResponsesClient().as_agent(
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

#### Hosted MCP Tools

Use hosted MCP tools to leverage server-side capabilities:

```python
from agent_framework import HostedMCPTool

async def hosted_mcp_example():
    agent = OpenAIResponsesClient().as_agent(
        name="DocsBot",
        instructions="You are a helpful assistant with access to various tools.",
        tools=HostedMCPTool(
            name="Microsoft Learn MCP",
            url="https://learn.microsoft.com/api/mcp",
        ),
    )

    result = await agent.run("How do I create an Azure storage account?")
    print(result.text)
```

## Using the Agent

The agent is a standard `BaseAgent` and supports all standard agent operations.

For more information on how to run and interact with agents, see the [Agent getting started tutorials](../../../tutorials/overview.md).

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [OpenAI Assistant Agents](./openai-assistants-agent.md)
