---
title: Backend Tool Rendering with AG-UI
description: Learn how to add function tools that execute on the backend with results streamed to clients
zone_pivot_groups: programming-languages
author: moonbox3
ms.topic: tutorial
ms.author: evmattso
ms.date: 11/07/2025
ms.service: agent-framework
---

# Backend Tool Rendering with AG-UI

::: zone pivot="programming-language-csharp"

This tutorial shows you how to add function tools to your AG-UI agents. Function tools are custom C# methods that the agent can call to perform specific tasks like retrieving data, performing calculations, or interacting with external systems. With AG-UI, these tools execute on the backend and their results are automatically streamed to the client.

## Prerequisites

Before you begin, ensure you have completed the [Getting Started](getting-started.md) tutorial and have:

- .NET 8.0 or later
- `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` package installed
- Azure OpenAI service configured
- Basic understanding of AG-UI server and client setup

## What is Backend Tool Rendering?

Backend tool rendering means:

- Function tools are defined on the server
- The AI agent decides when to call these tools
- Tools execute on the backend (server-side)
- Tool call events and results are streamed to the client in real-time
- The client receives updates about tool execution progress

## Creating an AG-UI Server with Function Tools

Here's a complete server implementation demonstrating how to register tools with complex parameter types:

```csharp
// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using System.Text.Json.Serialization;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient().AddLogging();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Add(SampleJsonSerializerContext.Default));
builder.Services.AddAGUI();

WebApplication app = builder.Build();

string endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"]
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
string deploymentName = builder.Configuration["AZURE_OPENAI_DEPLOYMENT_NAME"]
    ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT_NAME is not set.");

// Define request/response types for the tool
internal sealed class RestaurantSearchRequest
{
    public string Location { get; set; } = string.Empty;
    public string Cuisine { get; set; } = "any";
}

internal sealed class RestaurantSearchResponse
{
    public string Location { get; set; } = string.Empty;
    public string Cuisine { get; set; } = string.Empty;
    public RestaurantInfo[] Results { get; set; } = [];
}

internal sealed class RestaurantInfo
{
    public string Name { get; set; } = string.Empty;
    public string Cuisine { get; set; } = string.Empty;
    public double Rating { get; set; }
    public string Address { get; set; } = string.Empty;
}

// JSON serialization context for source generation
[JsonSerializable(typeof(RestaurantSearchRequest))]
[JsonSerializable(typeof(RestaurantSearchResponse))]
internal sealed partial class SampleJsonSerializerContext : JsonSerializerContext;

// Define the function tool
[Description("Search for restaurants in a location.")]
static RestaurantSearchResponse SearchRestaurants(
    [Description("The restaurant search request")] RestaurantSearchRequest request)
{
    // Simulated restaurant data
    string cuisine = request.Cuisine == "any" ? "Italian" : request.Cuisine;

    return new RestaurantSearchResponse
    {
        Location = request.Location,
        Cuisine = request.Cuisine,
        Results =
        [
            new RestaurantInfo
            {
                Name = "The Golden Fork",
                Cuisine = cuisine,
                Rating = 4.5,
                Address = $"123 Main St, {request.Location}"
            },
            new RestaurantInfo
            {
                Name = "Spice Haven",
                Cuisine = cuisine == "Italian" ? "Indian" : cuisine,
                Rating = 4.7,
                Address = $"456 Oak Ave, {request.Location}"
            },
            new RestaurantInfo
            {
                Name = "Green Leaf",
                Cuisine = "Vegetarian",
                Rating = 4.3,
                Address = $"789 Elm Rd, {request.Location}"
            }
        ]
    };
}

// Get JsonSerializerOptions from the configured HTTP JSON options
Microsoft.AspNetCore.Http.Json.JsonOptions jsonOptions = app.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value;

// Create tool with serializer options
AITool[] tools =
[
    AIFunctionFactory.Create(
        SearchRestaurants,
        serializerOptions: jsonOptions.SerializerOptions)
];

// Create the AI agent with tools
ChatClient chatClient = new AzureOpenAIClient(
        new Uri(endpoint),
        new DefaultAzureCredential())
    .GetChatClient(deploymentName);

ChatClientAgent agent = chatClient.AsIChatClient().AsAIAgent(
    name: "AGUIAssistant",
    instructions: "You are a helpful assistant with access to restaurant information.",
    tools: tools);

// Map the AG-UI agent endpoint
app.MapAGUI("/", agent);

await app.RunAsync();
```

### Key Concepts

- **Server-side execution**: Tools execute in the server process
- **Automatic streaming**: Tool calls and results are streamed to clients in real-time

> [!IMPORTANT]
> When creating tools with complex parameter types (objects, arrays, etc.), you must provide the `serializerOptions` parameter to `AIFunctionFactory.Create()`. The serializer options should be obtained from the application's configured `JsonOptions` via `IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>` to ensure consistency with the rest of the application's JSON serialization.

### Running the Server

Set environment variables and run:

```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o-mini"
dotnet run --urls http://localhost:8888
```

## Observing Tool Calls in the Client

The basic client from the Getting Started tutorial displays the agent's final text response. However, you can extend it to observe tool calls and results as they're streamed from the server.

### Displaying Tool Execution Details

To see tool calls and results in real-time, extend the client's streaming loop to handle `FunctionCallContent` and `FunctionResultContent`:

```csharp
// Inside the streaming loop from getting-started.md
await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(messages, thread))
{
    ChatResponseUpdate chatUpdate = update.AsChatResponseUpdate();

    // ... existing run started code ...

    // Display streaming content
    foreach (AIContent content in update.Contents)
    {
        switch (content)
        {
            case TextContent textContent:
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write(textContent.Text);
                Console.ResetColor();
                break;

            case FunctionCallContent functionCallContent:
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n[Function Call - Name: {functionCallContent.Name}]");
                
                // Display individual parameters
                if (functionCallContent.Arguments != null)
                {
                    foreach (var kvp in functionCallContent.Arguments)
                    {
                        Console.WriteLine($"  Parameter: {kvp.Key} = {kvp.Value}");
                    }
                }
                Console.ResetColor();
                break;

            case FunctionResultContent functionResultContent:
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"\n[Function Result - CallId: {functionResultContent.CallId}]");
                
                if (functionResultContent.Exception != null)
                {
                    Console.WriteLine($"  Exception: {functionResultContent.Exception}");
                }
                else
                {
                    Console.WriteLine($"  Result: {functionResultContent.Result}");
                }
                Console.ResetColor();
                break;

            case ErrorContent errorContent:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[Error: {errorContent.Message}]");
                Console.ResetColor();
                break;
        }
    }
}
```

### Expected Output with Tool Calls

When the agent calls backend tools, you'll see:

```
User (:q or quit to exit): What's the weather like in Amsterdam?

[Run Started - Thread: thread_abc123, Run: run_xyz789]

[Function Call - Name: SearchRestaurants]
  Parameter: Location = Amsterdam
  Parameter: Cuisine = any

[Function Result - CallId: call_def456]
  Result: {"Location":"Amsterdam","Cuisine":"any","Results":[...]}

The weather in Amsterdam is sunny with a temperature of 22°C. Here are some 
great restaurants in the area: The Golden Fork (Italian, 4.5 stars)...
[Run Finished - Thread: thread_abc123]
```

### Key Concepts

- **`FunctionCallContent`**: Represents a tool being called with its `Name` and `Arguments` (parameter key-value pairs)
- **`FunctionResultContent`**: Contains the tool's `Result` or `Exception`, identified by `CallId`

## Next Steps

Now that you can add function tools, you can:

- **[Frontend tools](frontend-tools.md)**: Add frontend tools.
<!-- - **[Implement Human-in-the-Loop](human-in-the-loop.md)**: Add approval workflows for sensitive operations -->
<!-- - **[Manage State](state-management.md)**: Implement shared state for generative UI applications -->
- **[Test with Dojo](testing-with-dojo.md)**: Use AG-UI's Dojo app to test your agents

## Additional Resources

- [AG-UI Overview](index.md)
- [Getting Started Tutorial](getting-started.md)
- [Agent Framework Documentation](../../overview/agent-framework-overview.md)

::: zone-end

::: zone pivot="programming-language-python"

This tutorial shows you how to add function tools to your AG-UI agents. Function tools are custom Python functions that the agent can call to perform specific tasks like retrieving data, performing calculations, or interacting with external systems. With AG-UI, these tools execute on the backend and their results are automatically streamed to the client.

## Prerequisites

Before you begin, ensure you have completed the [Getting Started](getting-started.md) tutorial and have:

- Python 3.10 or later
- `agent-framework-ag-ui` installed
- Azure OpenAI service configured
- Basic understanding of AG-UI server and client setup

> [!NOTE]
> These samples use `DefaultAzureCredential` for authentication. Make sure you're authenticated with Azure (e.g., via `az login`). For more information, see the [Azure Identity documentation](/python/api/azure-identity/azure.identity.defaultazurecredential).

## What is Backend Tool Rendering?

Backend tool rendering means:

- Function tools are defined on the server
- The AI agent decides when to call these tools
- Tools execute on the backend (server-side)
- Tool call events and results are streamed to the client in real-time
- The client receives updates about tool execution progress

This approach provides:

- **Security**: Sensitive operations stay on the server
- **Consistency**: All clients use the same tool implementations
- **Transparency**: Clients can display tool execution progress
- **Flexibility**: Update tools without changing client code

## Creating Function Tools

### Basic Function Tool

You can turn any Python function into a tool using the `@ai_function` decorator:

```python
from typing import Annotated
from pydantic import Field
from agent_framework import ai_function


@ai_function
def get_weather(
    location: Annotated[str, Field(description="The city")],
) -> str:
    """Get the current weather for a location."""
    # In a real application, you would call a weather API
    return f"The weather in {location} is sunny with a temperature of 22°C."
```

### Key Concepts

- **`@ai_function` decorator**: Marks a function as available to the agent
- **Type annotations**: Provide type information for parameters
- **`Annotated` and `Field`**: Add descriptions to help the agent understand parameters
- **Docstring**: Describes what the function does (helps the agent decide when to use it)
- **Return value**: The result returned to the agent (and streamed to the client)

### Multiple Function Tools

You can provide multiple tools to give the agent more capabilities:

```python
from typing import Any
from agent_framework import ai_function


@ai_function
def get_weather(
    location: Annotated[str, Field(description="The city.")],
) -> str:
    """Get the current weather for a location."""
    return f"The weather in {location} is sunny with a temperature of 22°C."


@ai_function
def get_forecast(
    location: Annotated[str, Field(description="The city.")],
    days: Annotated[int, Field(description="Number of days to forecast")] = 3,
) -> dict[str, Any]:
    """Get the weather forecast for a location."""
    return {
        "location": location,
        "days": days,
        "forecast": [
            {"day": 1, "weather": "Sunny", "high": 24, "low": 18},
            {"day": 2, "weather": "Partly cloudy", "high": 22, "low": 17},
            {"day": 3, "weather": "Rainy", "high": 19, "low": 15},
        ],
    }
```

## Creating an AG-UI Server with Function Tools

Here's a complete server implementation with function tools:

```python
"""AG-UI server with backend tool rendering."""

import os
from typing import Annotated, Any

from agent_framework import ChatAgent, ai_function
from agent_framework.azure import AzureOpenAIChatClient
from agent_framework_ag_ui import add_agent_framework_fastapi_endpoint
from azure.identity import AzureCliCredential
from fastapi import FastAPI
from pydantic import Field


# Define function tools
@ai_function
def get_weather(
    location: Annotated[str, Field(description="The city")],
) -> str:
    """Get the current weather for a location."""
    # Simulated weather data
    return f"The weather in {location} is sunny with a temperature of 22°C."


@ai_function
def search_restaurants(
    location: Annotated[str, Field(description="The city to search in")],
    cuisine: Annotated[str, Field(description="Type of cuisine")] = "any",
) -> dict[str, Any]:
    """Search for restaurants in a location."""
    # Simulated restaurant data
    return {
        "location": location,
        "cuisine": cuisine,
        "results": [
            {"name": "The Golden Fork", "rating": 4.5, "price": "$$"},
            {"name": "Bella Italia", "rating": 4.2, "price": "$$$"},
            {"name": "Spice Garden", "rating": 4.7, "price": "$$"},
        ],
    }


# Read required configuration
endpoint = os.environ.get("AZURE_OPENAI_ENDPOINT")
deployment_name = os.environ.get("AZURE_OPENAI_DEPLOYMENT_NAME")

if not endpoint:
    raise ValueError("AZURE_OPENAI_ENDPOINT environment variable is required")
if not deployment_name:
    raise ValueError("AZURE_OPENAI_DEPLOYMENT_NAME environment variable is required")

chat_client = AzureOpenAIChatClient(
    credential=AzureCliCredential(),
    endpoint=endpoint,
    deployment_name=deployment_name,    
)

# Create agent with tools
agent = ChatAgent(
    name="TravelAssistant",
    instructions="You are a helpful travel assistant. Use the available tools to help users plan their trips.",
    chat_client=chat_client,
    tools=[get_weather, search_restaurants],
)

# Create FastAPI app
app = FastAPI(title="AG-UI Travel Assistant")
add_agent_framework_fastapi_endpoint(app, agent, "/")

if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="127.0.0.1", port=8888)
```

## Understanding Tool Events

When the agent calls a tool, the client receives several events:

### Tool Call Events

```python
# 1. TOOL_CALL_START - Tool execution begins
{
    "type": "TOOL_CALL_START",
    "toolCallId": "call_abc123",
    "toolCallName": "get_weather"
}

# 2. TOOL_CALL_ARGS - Tool arguments (may stream in chunks)
{
    "type": "TOOL_CALL_ARGS",
    "toolCallId": "call_abc123",
    "delta": "{\"location\": \"Paris, France\"}"
}

# 3. TOOL_CALL_END - Arguments complete
{
    "type": "TOOL_CALL_END",
    "toolCallId": "call_abc123"
}

# 4. TOOL_CALL_RESULT - Tool execution result
{
    "type": "TOOL_CALL_RESULT",
    "toolCallId": "call_abc123",
    "content": "The weather in Paris, France is sunny with a temperature of 22°C."
}
```

## Enhanced Client for Tool Events

Here's an enhanced client using `AGUIChatClient` that displays tool execution:

```python
"""AG-UI client with tool event handling."""

import asyncio
import os

from agent_framework import ChatAgent, ToolCallContent, ToolResultContent
from agent_framework_ag_ui import AGUIChatClient


async def main():
    """Main client loop with tool event display."""
    server_url = os.environ.get("AGUI_SERVER_URL", "http://127.0.0.1:8888/")
    print(f"Connecting to AG-UI server at: {server_url}\n")

    # Create AG-UI chat client
    chat_client = AGUIChatClient(server_url=server_url)
    
    # Create agent with the chat client
    agent = ChatAgent(
        name="ClientAgent",
        chat_client=chat_client,
        instructions="You are a helpful assistant.",
    )

    # Get a thread for conversation continuity
    thread = agent.get_new_thread()

    try:
        while True:
            message = input("\nUser (:q or quit to exit): ")
            if not message.strip():
                continue

            if message.lower() in (":q", "quit"):
                break

            print("\nAssistant: ", end="", flush=True)
            async for update in agent.run_stream(message, thread=thread):
                # Display text content
                if update.text:
                    print(f"\033[96m{update.text}\033[0m", end="", flush=True)
                
                # Display tool calls and results
                for content in update.contents:
                    if isinstance(content, ToolCallContent):
                        print(f"\n\033[95m[Calling tool: {content.name}]\033[0m")
                    elif isinstance(content, ToolResultContent):
                        result_text = content.result if isinstance(content.result, str) else str(content.result)
                        print(f"\033[94m[Tool result: {result_text}]\033[0m")

            print("\n")

    except KeyboardInterrupt:
        print("\n\nExiting...")
    except Exception as e:
        print(f"\n\033[91mError: {e}\033[0m")


if __name__ == "__main__":
    asyncio.run(main())
```

## Example Interaction

With the enhanced server and client running:

```
User (:q or quit to exit): What's the weather like in Paris and suggest some Italian restaurants?

[Run Started]
[Tool Call: get_weather]
[Tool Result: The weather in Paris, France is sunny with a temperature of 22°C.]
[Tool Call: search_restaurants]
[Tool Result: {"location": "Paris", "cuisine": "Italian", "results": [...]}]
Based on the current weather in Paris (sunny, 22°C) and your interest in Italian cuisine,
I'd recommend visiting Bella Italia, which has a 4.2 rating. The weather is perfect for
outdoor dining!
[Run Finished]
```

## Tool Implementation Best Practices

### Error Handling

Handle errors gracefully in your tools:

```python
@ai_function
def get_weather(
    location: Annotated[str, Field(description="The city.")],
) -> str:
    """Get the current weather for a location."""
    try:
        # Call weather API
        result = call_weather_api(location)
        return f"The weather in {location} is {result['condition']} with temperature {result['temp']}°C."
    except Exception as e:
        return f"Unable to retrieve weather for {location}. Error: {str(e)}"
```

### Rich Return Types

Return structured data when appropriate:

```python
@ai_function
def analyze_sentiment(
    text: Annotated[str, Field(description="The text to analyze")],
) -> dict[str, Any]:
    """Analyze the sentiment of text."""
    # Perform sentiment analysis
    return {
        "text": text,
        "sentiment": "positive",
        "confidence": 0.87,
        "scores": {
            "positive": 0.87,
            "neutral": 0.10,
            "negative": 0.03,
        },
    }
```

### Descriptive Documentation

Provide clear descriptions to help the agent understand when to use tools:

```python
@ai_function
def book_flight(
    origin: Annotated[str, Field(description="Departure city and airport code, e.g., 'New York, JFK'")],
    destination: Annotated[str, Field(description="Arrival city and airport code, e.g., 'London, LHR'")],
    date: Annotated[str, Field(description="Departure date in YYYY-MM-DD format")],
    passengers: Annotated[int, Field(description="Number of passengers")] = 1,
) -> dict[str, Any]:
    """
    Book a flight for specified passengers from origin to destination.
    
    This tool should be used when the user wants to book or reserve airline tickets.
    Do not use this for searching flights - use search_flights instead.
    """
    # Implementation
    pass
```

## Tool Organization with Classes

For related tools, organize them in a class:

```python
from agent_framework import ai_function


class WeatherTools:
    """Collection of weather-related tools."""
    
    def __init__(self, api_key: str):
        self.api_key = api_key
    
    @ai_function
    def get_current_weather(
        self,
        location: Annotated[str, Field(description="The city.")],
    ) -> str:
        """Get current weather for a location."""
        # Use self.api_key to call API
        return f"Current weather in {location}: Sunny, 22°C"
    
    @ai_function
    def get_forecast(
        self,
        location: Annotated[str, Field(description="The city.")],
        days: Annotated[int, Field(description="Number of days")] = 3,
    ) -> dict[str, Any]:
        """Get weather forecast for a location."""
        # Use self.api_key to call API
        return {"location": location, "forecast": [...]}


# Create tools instance
weather_tools = WeatherTools(api_key="your-api-key")

# Create agent with class-based tools
agent = ChatAgent(
    name="WeatherAgent",
    instructions="You are a weather assistant.",
    chat_client=AzureOpenAIChatClient(...),
    tools=[
        weather_tools.get_current_weather,
        weather_tools.get_forecast,
    ],
)
```

## Next Steps

Now that you understand backend tool rendering, you can:

<!-- - **[Add Human-in-the-Loop](human-in-the-loop.md)**: Require user approval before executing sensitive tools -->
<!-- - **[Manage State](state-management.md)**: Share state between client and server for richer interactions -->
- **[Create Advanced Tools](../../tutorials/agents/function-tools.md)**: Learn more about creating function tools with Agent Framework

## Additional Resources

- [AG-UI Overview](index.md)
- [Getting Started with AG-UI](getting-started.md)
- [Function Tools Tutorial](../../tutorials/agents/function-tools.md)

::: zone-end
