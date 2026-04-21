---
title: State Management with AG-UI
description: Learn how to synchronize state between client and server using AG-UI protocol
zone_pivot_groups: programming-languages
author: moonbox3
ms.topic: tutorial
ms.author: evmattso
ms.date: 11/07/2025
ms.service: agent-framework
---

# State Management with AG-UI

This tutorial shows you how to implement state management with AG-UI, enabling bidirectional synchronization of state between the client and server. This is essential for building interactive applications like generative UI, real-time dashboards, or collaborative experiences.

## Prerequisites

Before you begin, ensure you understand:

- [Getting Started with AG-UI](getting-started.md)
- [Backend Tool Rendering](backend-tool-rendering.md)
<!-- - [Human-in-the-Loop](human-in-the-loop.md) -->

## What is State Management?

State management in AG-UI enables:

- **Shared State**: Both client and server maintain a synchronized view of application state
- **Bidirectional Sync**: State can be updated from either client or server
- **Real-time Updates**: Changes are streamed immediately using state events
- **Predictive Updates**: State updates stream as the LLM generates tool arguments (optimistic UI)
- **Structured Data**: State follows a JSON schema for validation

### Use Cases

State management is valuable for:

- **Generative UI**: Build UI components based on agent-controlled state
- **Form Building**: Agent populates form fields as it gathers information
- **Progress Tracking**: Show real-time progress of multi-step operations
- **Interactive Dashboards**: Display data that updates as the agent processes it
- **Collaborative Editing**: Multiple users see consistent state updates

::: zone pivot="programming-language-csharp"

## Creating State-Aware Agents in C#

### Define Your State Model

First, define classes for your state structure:

```csharp
using System.Text.Json.Serialization;

namespace RecipeAssistant;

// State response wrapper
internal sealed class RecipeResponse
{
    [JsonPropertyName("recipe")]
    public RecipeState Recipe { get; set; } = new();
}

// Recipe state model
internal sealed class RecipeState
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("cuisine")]
    public string Cuisine { get; set; } = string.Empty;

    [JsonPropertyName("ingredients")]
    public List<string> Ingredients { get; set; } = [];

    [JsonPropertyName("steps")]
    public List<string> Steps { get; set; } = [];

    [JsonPropertyName("prep_time_minutes")]
    public int PrepTimeMinutes { get; set; }

    [JsonPropertyName("cook_time_minutes")]
    public int CookTimeMinutes { get; set; }

    [JsonPropertyName("skill_level")]
    public string SkillLevel { get; set; } = string.Empty;
}

// JSON serialization context
[JsonSerializable(typeof(RecipeResponse))]
[JsonSerializable(typeof(RecipeState))]
[JsonSerializable(typeof(System.Text.Json.JsonElement))]
internal sealed partial class RecipeSerializerContext : JsonSerializerContext;
```

### Implement State Management Middleware

Create middleware that handles state management by detecting when the client sends state and coordinating the agent's responses:

```csharp
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

internal sealed class SharedStateAgent : DelegatingAIAgent
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public SharedStateAgent(AIAgent innerAgent, JsonSerializerOptions jsonSerializerOptions)
        : base(innerAgent)
    {
        this._jsonSerializerOptions = jsonSerializerOptions;
    }

    public override Task<AgentResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return this.RunStreamingAsync(messages, thread, options, cancellationToken)
            .ToAgentResponseAsync(cancellationToken);
    }

    public override async IAsyncEnumerable<AgentResponseUpdate> RunStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Check if the client sent state in the request
        if (options is not ChatClientAgentRunOptions { ChatOptions.AdditionalProperties: { } properties } chatRunOptions ||
            !properties.TryGetValue("ag_ui_state", out object? stateObj) ||
            stateObj is not JsonElement state ||
            state.ValueKind != JsonValueKind.Object)
        {
            // No state management requested, pass through to inner agent
            await foreach (var update in this.InnerAgent.RunStreamingAsync(messages, thread, options, cancellationToken).ConfigureAwait(false))
            {
                yield return update;
            }
            yield break;
        }

        // Check if state has properties (not empty {})
        bool hasProperties = false;
        foreach (JsonProperty _ in state.EnumerateObject())
        {
            hasProperties = true;
            break;
        }

        if (!hasProperties)
        {
            // Empty state - treat as no state
            await foreach (var update in this.InnerAgent.RunStreamingAsync(messages, thread, options, cancellationToken).ConfigureAwait(false))
            {
                yield return update;
            }
            yield break;
        }

        // First run: Generate structured state update
        var firstRunOptions = new ChatClientAgentRunOptions
        {
            ChatOptions = chatRunOptions.ChatOptions.Clone(),
            AllowBackgroundResponses = chatRunOptions.AllowBackgroundResponses,
            ContinuationToken = chatRunOptions.ContinuationToken,
            ChatClientFactory = chatRunOptions.ChatClientFactory,
        };

        // Configure JSON schema response format for structured state output
        firstRunOptions.ChatOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema<RecipeResponse>(
            schemaName: "RecipeResponse",
            schemaDescription: "A response containing a recipe with title, skill level, cooking time, preferences, ingredients, and instructions");

        // Add current state to the conversation - state is already a JsonElement
        ChatMessage stateUpdateMessage = new(
            ChatRole.System,
            [
                new TextContent("Here is the current state in JSON format:"),
                new TextContent(JsonSerializer.Serialize(state, this._jsonSerializerOptions.GetTypeInfo(typeof(JsonElement)))),
                new TextContent("The new state is:")
            ]);

        var firstRunMessages = messages.Append(stateUpdateMessage);

        // Collect all updates from first run
        var allUpdates = new List<AgentResponseUpdate>();
        await foreach (var update in this.InnerAgent.RunStreamingAsync(firstRunMessages, thread, firstRunOptions, cancellationToken).ConfigureAwait(false))
        {
            allUpdates.Add(update);

            // Yield all non-text updates (tool calls, etc.)
            bool hasNonTextContent = update.Contents.Any(c => c is not TextContent);
            if (hasNonTextContent)
            {
                yield return update;
            }
        }

        var response = allUpdates.ToAgentResponse();

        // Try to deserialize the structured state response
        if (response.TryDeserialize(this._jsonSerializerOptions, out JsonElement stateSnapshot))
        {
            // Serialize and emit as STATE_SNAPSHOT via DataContent
            byte[] stateBytes = JsonSerializer.SerializeToUtf8Bytes(
                stateSnapshot,
                this._jsonSerializerOptions.GetTypeInfo(typeof(JsonElement)));
            yield return new AgentResponseUpdate
            {
                Contents = [new DataContent(stateBytes, "application/json")]
            };
        }
        else
        {
            yield break;
        }

        // Second run: Generate user-friendly summary
        var secondRunMessages = messages.Concat(response.Messages).Append(
            new ChatMessage(
                ChatRole.System,
                [new TextContent("Please provide a concise summary of the state changes in at most two sentences.")]));

        await foreach (var update in this.InnerAgent.RunStreamingAsync(secondRunMessages, thread, options, cancellationToken).ConfigureAwait(false))
        {
            yield return update;
        }
    }
}
```

### Configure the Agent with State Management

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Azure.AI.OpenAI;
using Azure.Identity;

AIAgent CreateRecipeAgent(JsonSerializerOptions jsonSerializerOptions)
{
    string endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
        ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
    string deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
        ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT_NAME is not set.");

    AzureOpenAIClient azureClient = new AzureOpenAIClient(
        new Uri(endpoint),
        new DefaultAzureCredential());

    var chatClient = azureClient.GetChatClient(deploymentName);

    // Create base agent
    AIAgent baseAgent = chatClient.AsIChatClient().AsAIAgent(
        name: "RecipeAgent",
        instructions: """
            You are a helpful recipe assistant. When users ask you to create or suggest a recipe,
            respond with a complete RecipeResponse JSON object that includes:
            - recipe.title: The recipe name
            - recipe.cuisine: Type of cuisine (e.g., Italian, Mexican, Japanese)
            - recipe.ingredients: Array of ingredient strings with quantities
            - recipe.steps: Array of cooking instruction strings
            - recipe.prep_time_minutes: Preparation time in minutes
            - recipe.cook_time_minutes: Cooking time in minutes
            - recipe.skill_level: One of "beginner", "intermediate", or "advanced"

            Always include all fields in the response. Be creative and helpful.
            """);

    // Wrap with state management middleware
    return new SharedStateAgent(baseAgent, jsonSerializerOptions);
}
```

### Map the Agent Endpoint

```csharp
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient().AddLogging();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Add(RecipeSerializerContext.Default));
builder.Services.AddAGUI();

WebApplication app = builder.Build();

var jsonOptions = app.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value;
AIAgent recipeAgent = CreateRecipeAgent(jsonOptions.SerializerOptions);
app.MapAGUI("/", recipeAgent);

await app.RunAsync();
```

### Key Concepts

- **State Detection**: Middleware checks for `ag_ui_state` in `ChatOptions.AdditionalProperties` to detect when the client is requesting state management
- **Two-Phase Response**: First generates structured state (JSON schema), then generates a user-friendly summary
- **Structured State Models**: Define C# classes for your state structure with JSON property names
- **JSON Schema Response Format**: Use `ChatResponseFormat.ForJsonSchema<T>()` to ensure structured output
- **STATE_SNAPSHOT Events**: Emitted as `DataContent` with `application/json` media type, which the AG-UI framework automatically converts to STATE_SNAPSHOT events
- **State Context**: Current state is injected as a system message to provide context to the agent

### How It Works

1. Client sends request with state in `ChatOptions.AdditionalProperties["ag_ui_state"]`
2. Middleware detects state and performs first run with JSON schema response format
3. Middleware adds current state as context in a system message
4. Agent generates structured state update matching your state model
5. Middleware serializes state and emits as `DataContent` (becomes STATE_SNAPSHOT event)
6. Middleware performs second run to generate user-friendly summary
7. Client receives both the state snapshot and the natural language summary

> [!TIP]
> The two-phase approach separates state management from user communication. The first phase ensures structured, reliable state updates while the second phase provides natural language feedback to the user.

### Client Implementation (C#)

> [!IMPORTANT]
> The C# client implementation is not included in this tutorial. The server-side state management is complete, but clients need to:
> 1. Initialize state with an empty object (not null): `RecipeState? currentState = new RecipeState();`
> 2. Send state as `DataContent` in a `ChatRole.System` message
> 3. Receive state snapshots as `DataContent` with `mediaType = "application/json"`
>
> The AG-UI hosting layer automatically extracts state from `DataContent` and places it in `ChatOptions.AdditionalProperties["ag_ui_state"]` as a `JsonElement`.

For a complete client implementation example, see the Python client pattern below which demonstrates the full bidirectional state flow.

::: zone-end

::: zone pivot="programming-language-python"

## Define State Models

First, define Pydantic models for your state structure. This ensures type safety and validation:

```python
from enum import Enum
from pydantic import BaseModel, Field


class SkillLevel(str, Enum):
    """The skill level required for the recipe."""
    BEGINNER = "Beginner"
    INTERMEDIATE = "Intermediate"
    ADVANCED = "Advanced"


class CookingTime(str, Enum):
    """The cooking time of the recipe."""
    FIVE_MIN = "5 min"
    FIFTEEN_MIN = "15 min"
    THIRTY_MIN = "30 min"
    FORTY_FIVE_MIN = "45 min"
    SIXTY_PLUS_MIN = "60+ min"


class Ingredient(BaseModel):
    """An ingredient with its details."""
    icon: str = Field(..., description="Emoji icon representing the ingredient (e.g., 🥕)")
    name: str = Field(..., description="Name of the ingredient")
    amount: str = Field(..., description="Amount or quantity of the ingredient")


class Recipe(BaseModel):
    """A complete recipe."""
    title: str = Field(..., description="The title of the recipe")
    skill_level: SkillLevel = Field(..., description="The skill level required")
    special_preferences: list[str] = Field(
        default_factory=list, description="Dietary preferences (e.g., Vegetarian, Gluten-free)"
    )
    cooking_time: CookingTime = Field(..., description="The estimated cooking time")
    ingredients: list[Ingredient] = Field(..., description="Complete list of ingredients")
    instructions: list[str] = Field(..., description="Step-by-step cooking instructions")
```

## State Schema

Define a state schema to specify the structure and types of your state:

```python
state_schema = {
    "recipe": {"type": "object", "description": "The current recipe"},
}
```

> [!NOTE]
> The state schema uses a simple format with `type` and optional `description`. The actual structure is defined by your Pydantic models.

## Predictive State Updates

Predictive state updates stream tool arguments to the state as the LLM generates them, enabling optimistic UI updates:

```python
predict_state_config = {
    "recipe": {"tool": "update_recipe", "tool_argument": "recipe"},
}
```

This configuration maps the `recipe` state field to the `recipe` argument of the `update_recipe` tool. When the agent calls the tool, the arguments stream to the state in real-time as the LLM generates them.

## Define State Update Tool

Create a tool function that accepts your Pydantic model:

```python
from agent_framework import ai_function


@ai_function
def update_recipe(recipe: Recipe) -> str:
    """Update the recipe with new or modified content.

    You MUST write the complete recipe with ALL fields, even when changing only a few items.
    When modifying an existing recipe, include ALL existing ingredients and instructions plus your changes.
    NEVER delete existing data - only add or modify.

    Args:
        recipe: The complete recipe object with all details

    Returns:
        Confirmation that the recipe was updated
    """
    return "Recipe updated."
```

> [!IMPORTANT]
> The tool function's parameter name (`recipe`) must match the `tool_argument` in your `predict_state_config`.

## Create the Agent with State Management

Here's a complete server implementation with state management:

```python
"""AG-UI server with state management."""

from agent_framework import ChatAgent
from agent_framework.azure import AzureOpenAIChatClient
from agent_framework_ag_ui import (
    AgentFrameworkAgent,
    RecipeConfirmationStrategy,
    add_agent_framework_fastapi_endpoint,
)
from azure.identity import AzureCliCredential
from fastapi import FastAPI

# Create the chat agent with tools
agent = ChatAgent(
    name="recipe_agent",
    instructions="""You are a helpful recipe assistant that creates and modifies recipes.

    CRITICAL RULES:
    1. You will receive the current recipe state in the system context
    2. To update the recipe, you MUST use the update_recipe tool
    3. When modifying a recipe, ALWAYS include ALL existing data plus your changes in the tool call
    4. NEVER delete existing ingredients or instructions - only add or modify
    5. After calling the tool, provide a brief conversational message (1-2 sentences)

    When creating a NEW recipe:
    - Provide all required fields: title, skill_level, cooking_time, ingredients, instructions
    - Use actual emojis for ingredient icons (🥕 🧄 🧅 🍅 🌿 🍗 🥩 🧀)
    - Leave special_preferences empty unless specified
    - Message: "Here's your recipe!" or similar

    When MODIFYING or IMPROVING an existing recipe:
    - Include ALL existing ingredients + any new ones
    - Include ALL existing instructions + any new/modified ones
    - Update other fields as needed
    - Message: Explain what you improved (e.g., "I upgraded the ingredients to premium quality")
    - When asked to "improve", enhance with:
      * Better ingredients (upgrade quality, add complementary flavors)
      * More detailed instructions
      * Professional techniques
      * Adjust skill_level if complexity changes
      * Add relevant special_preferences

    Example improvements:
    - Upgrade "chicken" → "organic free-range chicken breast"
    - Add herbs: basil, oregano, thyme
    - Add aromatics: garlic, shallots
    - Add finishing touches: lemon zest, fresh parsley
    - Make instructions more detailed and professional
    """,
    chat_client=AzureOpenAIChatClient(
        credential=AzureCliCredential(),
        endpoint=endpoint,
        deployment_name=deployment_name,
    ),
    tools=[update_recipe],
)

# Wrap agent with state management
recipe_agent = AgentFrameworkAgent(
    agent=agent,
    name="RecipeAgent",
    description="Creates and modifies recipes with streaming state updates",
    state_schema={
        "recipe": {"type": "object", "description": "The current recipe"},
    },
    predict_state_config={
        "recipe": {"tool": "update_recipe", "tool_argument": "recipe"},
    },
    confirmation_strategy=RecipeConfirmationStrategy(),
)

# Create FastAPI app
app = FastAPI(title="AG-UI Recipe Assistant")
add_agent_framework_fastapi_endpoint(app, recipe_agent, "/")

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="127.0.0.1", port=8888)
```

### Key Concepts

- **Pydantic Models**: Define structured state with type safety and validation
- **State Schema**: Simple format specifying state field types
- **Predictive State Config**: Maps state fields to tool arguments for streaming updates
- **State Injection**: Current state is automatically injected as system messages to provide context
- **Complete Updates**: Tools must write the complete state, not just deltas
- **Confirmation Strategy**: Customize approval messages for your domain (recipe, document, task planning, etc.)

## Understanding State Events

### State Snapshot Event

A complete snapshot of the current state, emitted when the tool completes:

```json
{
    "type": "STATE_SNAPSHOT",
    "snapshot": {
        "recipe": {
            "title": "Classic Pasta Carbonara",
            "skill_level": "Intermediate",
            "special_preferences": ["Authentic Italian"],
            "cooking_time": "30 min",
            "ingredients": [
                {"icon": "🍝", "name": "Spaghetti", "amount": "400g"},
                {"icon": "🥓", "name": "Guanciale or bacon", "amount": "200g"},
                {"icon": "🥚", "name": "Egg yolks", "amount": "4"},
                {"icon": "🧀", "name": "Pecorino Romano", "amount": "100g grated"},
                {"icon": "🧂", "name": "Black pepper", "amount": "To taste"}
            ],
            "instructions": [
                "Bring a large pot of salted water to boil",
                "Cut guanciale into small strips and fry until crispy",
                "Beat egg yolks with grated Pecorino and black pepper",
                "Cook spaghetti until al dente",
                "Reserve 1 cup pasta water, then drain pasta",
                "Remove pan from heat, add hot pasta to guanciale",
                "Quickly stir in egg mixture, adding pasta water to create creamy sauce",
                "Serve immediately with extra Pecorino and black pepper"
            ]
        }
    }
}
```

### State Delta Event

Incremental state updates using JSON Patch format, emitted as the LLM streams tool arguments:

```json
{
    "type": "STATE_DELTA",
    "delta": [
        {
            "op": "replace",
            "path": "/recipe",
            "value": {
                "title": "Classic Pasta Carbonara",
                "skill_level": "Intermediate",
                "cooking_time": "30 min",
                "ingredients": [
                    {"icon": "🍝", "name": "Spaghetti", "amount": "400g"}
                ],
                "instructions": ["Bring a large pot of salted water to boil"]
            }
        }
    ]
}
```

> [!NOTE]
> State delta events stream in real-time as the LLM generates the tool arguments, providing optimistic UI updates. The final state snapshot is emitted when the tool completes execution.

## Client Implementation

The `agent_framework_ag_ui` package provides `AGUIChatClient` for connecting to AG-UI servers, bringing Python client experience to parity with .NET:

```python
"""AG-UI client with state management."""

import asyncio
import json
import os
from typing import Any

import jsonpatch
from agent_framework import ChatAgent, ChatMessage, Role
from agent_framework_ag_ui import AGUIChatClient


async def main():
    """Example client with state tracking."""
    server_url = os.environ.get("AGUI_SERVER_URL", "http://127.0.0.1:8888/")
    print(f"Connecting to AG-UI server at: {server_url}\n")

    # Create AG-UI chat client
    chat_client = AGUIChatClient(server_url=server_url)

    # Wrap with ChatAgent for convenient API
    agent = ChatAgent(
        name="ClientAgent",
        chat_client=chat_client,
        instructions="You are a helpful assistant.",
    )

    # Get a thread for conversation continuity
    thread = agent.get_new_thread()

    # Track state locally
    state: dict[str, Any] = {}

    try:
        while True:
            message = input("\nUser (:q to quit, :state to show state): ")
            if not message.strip():
                continue

            if message.lower() in (":q", "quit"):
                break

            if message.lower() == ":state":
                print(f"\nCurrent state: {json.dumps(state, indent=2)}")
                continue

            print()
            # Stream the agent response with state
            async for update in agent.run_stream(message, thread=thread):
                # Handle text content
                if update.text:
                    print(update.text, end="", flush=True)

                # Handle state updates
                for content in update.contents:
                    # STATE_SNAPSHOT events come as DataContent with application/json
                    if hasattr(content, 'media_type') and content.media_type == 'application/json':
                        # Parse state snapshot
                        state_data = json.loads(content.data.decode() if isinstance(content.data, bytes) else content.data)
                        state = state_data
                        print("\n[State Snapshot Received]")

                    # STATE_DELTA events are handled similarly
                    # Apply JSON Patch deltas to maintain state
                    if hasattr(content, 'delta') and content.delta:
                        patch = jsonpatch.JsonPatch(content.delta)
                        state = patch.apply(state)
                        print("\n[State Delta Applied]")

            print(f"\n\nCurrent state: {json.dumps(state, indent=2)}")
            print()

    except KeyboardInterrupt:
        print("\n\nExiting...")


if __name__ == "__main__":
    # Install dependencies: pip install agent-framework-ag-ui jsonpatch --pre
    asyncio.run(main())
```

### Key Benefits

The `AGUIChatClient` provides:

- **Simplified Connection**: Automatic handling of HTTP/SSE communication
- **Thread Management**: Built-in thread ID tracking for conversation continuity
- **Agent Integration**: Works seamlessly with `ChatAgent` for familiar API
- **State Handling**: Automatic parsing of state events from the server
- **Parity with .NET**: Consistent experience across languages

> [!TIP]
> Use `AGUIChatClient` with `ChatAgent` to get the full benefit of the agent framework's features like conversation history, tool execution, and middleware support.

## Using Confirmation Strategies

The `confirmation_strategy` parameter allows you to customize approval messages for your domain:

```python
from agent_framework_ag_ui import RecipeConfirmationStrategy

recipe_agent = AgentFrameworkAgent(
    agent=agent,
    state_schema={"recipe": {"type": "object", "description": "The current recipe"}},
    predict_state_config={"recipe": {"tool": "update_recipe", "tool_argument": "recipe"}},
    confirmation_strategy=RecipeConfirmationStrategy(),
)
```

Available strategies:
- `DefaultConfirmationStrategy()` - Generic messages for any agent
- `RecipeConfirmationStrategy()` - Recipe-specific messages
- `DocumentWriterConfirmationStrategy()` - Document editing messages
- `TaskPlannerConfirmationStrategy()` - Task planning messages

You can also create custom strategies by inheriting from `ConfirmationStrategy` and implementing the required methods.

## Example Interaction

With the server and client running:

```
User (:q to quit, :state to show state): I want to make a classic Italian pasta carbonara

[Run Started]
[Calling Tool: update_recipe]
[State Updated]
[State Updated]
[State Updated]
[Tool Result: Recipe updated.]
Here's your recipe!
[Run Finished]

============================================================
CURRENT STATE
============================================================

recipe:
  title: Classic Pasta Carbonara
  skill_level: Intermediate
  special_preferences: ['Authentic Italian']
  cooking_time: 30 min
  ingredients:
    - 🍝 Spaghetti: 400g
    - 🥓 Guanciale or bacon: 200g
    - 🥚 Egg yolks: 4
    - 🧀 Pecorino Romano: 100g grated
    - 🧂 Black pepper: To taste
  instructions:
    1. Bring a large pot of salted water to boil
    2. Cut guanciale into small strips and fry until crispy
    3. Beat egg yolks with grated Pecorino and black pepper
    4. Cook spaghetti until al dente
    5. Reserve 1 cup pasta water, then drain pasta
    6. Remove pan from heat, add hot pasta to guanciale
    7. Quickly stir in egg mixture, adding pasta water to create creamy sauce
    8. Serve immediately with extra Pecorino and black pepper

============================================================
```

> [!TIP]
> Use the `:state` command to view the current state at any time during the conversation.

## Predictive State Updates in Action

When using predictive state updates with `predict_state_config`, the client receives `STATE_DELTA` events as the LLM generates tool arguments in real-time, before the tool executes:

```json
// Agent starts generating tool call for update_recipe
// Client receives STATE_DELTA events as the recipe argument streams:

// First delta - partial recipe with title
{
  "type": "STATE_DELTA",
  "delta": [{"op": "replace", "path": "/recipe", "value": {"title": "Classic Pasta"}}]
}

// Second delta - title complete with more fields
{
  "type": "STATE_DELTA",
  "delta": [{"op": "replace", "path": "/recipe", "value": {
    "title": "Classic Pasta Carbonara",
    "skill_level": "Intermediate"
  }}]
}

// Third delta - ingredients starting to appear
{
  "type": "STATE_DELTA",
  "delta": [{"op": "replace", "path": "/recipe", "value": {
    "title": "Classic Pasta Carbonara",
    "skill_level": "Intermediate",
    "cooking_time": "30 min",
    "ingredients": [
      {"icon": "🍝", "name": "Spaghetti", "amount": "400g"}
    ]
  }}]
}

// ... more deltas as the LLM generates the complete recipe
```

This enables the client to show optimistic UI updates in real-time as the agent is thinking, providing immediate feedback to users.

## State with Human-in-the-Loop

You can combine state management with approval workflows by setting `require_confirmation=True`:

```python
recipe_agent = AgentFrameworkAgent(
    agent=agent,
    state_schema={"recipe": {"type": "object", "description": "The current recipe"}},
    predict_state_config={"recipe": {"tool": "update_recipe", "tool_argument": "recipe"}},
    require_confirmation=True,  # Require approval for state changes
    confirmation_strategy=RecipeConfirmationStrategy(),
)
```

When enabled:

1. State updates stream as the agent generates tool arguments (predictive updates via `STATE_DELTA` events)
2. Agent requests approval before executing the tool (via `FUNCTION_APPROVAL_REQUEST` event)
3. If approved, the tool executes and final state is emitted (via `STATE_SNAPSHOT` event)
4. If rejected, the predictive state changes are discarded

## Advanced State Patterns

### Complex State with Multiple Fields

You can manage multiple state fields with different tools:

```python
from pydantic import BaseModel


class TaskStep(BaseModel):
    """A single task step."""
    description: str
    status: str = "pending"
    estimated_duration: str = "5 min"


@ai_function
def generate_task_steps(steps: list[TaskStep]) -> str:
    """Generate task steps for a given task."""
    return f"Generated {len(steps)} steps."


@ai_function
def update_preferences(preferences: dict[str, Any]) -> str:
    """Update user preferences."""
    return "Preferences updated."


# Configure with multiple state fields
agent_with_multiple_state = AgentFrameworkAgent(
    agent=agent,
    state_schema={
        "steps": {"type": "array", "description": "List of task steps"},
        "preferences": {"type": "object", "description": "User preferences"},
    },
    predict_state_config={
        "steps": {"tool": "generate_task_steps", "tool_argument": "steps"},
        "preferences": {"tool": "update_preferences", "tool_argument": "preferences"},
    },
)
```

### Using Wildcard Tool Arguments

When a tool returns complex nested data, use `"*"` to map all tool arguments to state:

```python
@ai_function
def create_document(title: str, content: str, metadata: dict[str, Any]) -> str:
    """Create a document with title, content, and metadata."""
    return "Document created."


# Map all tool arguments to document state
predict_state_config = {
    "document": {"tool": "create_document", "tool_argument": "*"}
}
```

This maps the entire tool call (all arguments) to the `document` state field.

## Best Practices

### Use Pydantic Models

Define structured models for type safety:

```python
class Recipe(BaseModel):
    """Use Pydantic models for structured, validated state."""
    title: str
    skill_level: SkillLevel
    ingredients: list[Ingredient]
    instructions: list[str]
```

Benefits:
- **Type Safety**: Automatic validation of data types
- **Documentation**: Field descriptions serve as documentation
- **IDE Support**: Auto-completion and type checking
- **Serialization**: Automatic JSON conversion

### Complete State Updates

Always write the complete state, not just deltas:

```python
@ai_function
def update_recipe(recipe: Recipe) -> str:
    """
    You MUST write the complete recipe with ALL fields.
    When modifying a recipe, include ALL existing ingredients and
    instructions plus your changes. NEVER delete existing data.
    """
    return "Recipe updated."
```

This ensures state consistency and proper predictive updates.

### Match Parameter Names

Ensure tool parameter names match `tool_argument` configuration:

```python
# Tool parameter name
def update_recipe(recipe: Recipe) -> str:  # Parameter name: 'recipe'
    ...

# Must match in predict_state_config
predict_state_config = {
    "recipe": {"tool": "update_recipe", "tool_argument": "recipe"}  # Same name
}
```

### Provide Context in Instructions

Include clear instructions about state management:

```python
agent = ChatAgent(
    instructions="""
    CRITICAL RULES:
    1. You will receive the current recipe state in the system context
    2. To update the recipe, you MUST use the update_recipe tool
    3. When modifying a recipe, ALWAYS include ALL existing data plus your changes
    4. NEVER delete existing ingredients or instructions - only add or modify
    """,
    ...
)
```

### Use Confirmation Strategies

Customize approval messages for your domain:

```python
from agent_framework_ag_ui import RecipeConfirmationStrategy

recipe_agent = AgentFrameworkAgent(
    agent=agent,
    confirmation_strategy=RecipeConfirmationStrategy(),  # Domain-specific messages
)
```

## Next Steps

You've now learned all the core AG-UI features! Next you can:

- Explore the [Agent Framework documentation](../../overview/agent-framework-overview.md)
- Build a complete application combining all AG-UI features
- Deploy your AG-UI service to production

## Additional Resources

- [AG-UI Overview](index.md)
- [Getting Started](getting-started.md)
- [Backend Tool Rendering](backend-tool-rendering.md)
<!-- - [Human-in-the-Loop](human-in-the-loop.md) -->

::: zone-end
