---
title: State Management with AG-UI
description: Learn how to synchronize state between client and server using AG-UI protocol
zone_pivot_groups: programming-languages
author: moonbox3
ms.topic: tutorial
ms.author: evmattso
ms.date: 07/10/2026
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

State management in the .NET AG-UI integration is **declarative**: your agent exposes ordinary tools that return your state objects, and you tell the hosting layer which tool results become AG-UI state events by configuring an `AGUIStreamOptions`. You don't write a custom agent or emit protocol content by hand.

### Define Your State Model

First, define classes for your state structure:

```csharp
using System.Text.Json.Serialization;

namespace RecipeAssistant;

// State response wrapper returned by the tool. Its shape is what the client renders as state.
internal sealed class RecipeResponse
{
    [JsonPropertyName("recipe")]
    public Recipe Recipe { get; set; } = new();
}

// Recipe state model.
internal sealed class Recipe
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("skill_level")]
    public string SkillLevel { get; set; } = string.Empty;

    [JsonPropertyName("cooking_time")]
    public string CookingTime { get; set; } = string.Empty;

    [JsonPropertyName("special_preferences")]
    public List<string> SpecialPreferences { get; set; } = [];

    [JsonPropertyName("ingredients")]
    public List<Ingredient> Ingredients { get; set; } = [];

    [JsonPropertyName("instructions")]
    public List<string> Instructions { get; set; } = [];
}

// A single ingredient.
internal sealed class Ingredient
{
    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public string Amount { get; set; } = string.Empty;
}

// JSON serialization context for the tool payloads.
[JsonSerializable(typeof(RecipeResponse))]
[JsonSerializable(typeof(Recipe))]
[JsonSerializable(typeof(Ingredient))]
internal sealed partial class RecipeSerializerContext : JsonSerializerContext;
```

### Emit a State Snapshot from a Tool

Expose a tool that returns the complete state. The agent calls it whenever the recipe should change. The hosting layer turns the tool result into a `STATE_SNAPSHOT` event when you map it:

```csharp
using System.ComponentModel;
using Microsoft.Extensions.AI;

[Description("Generate or update the shared recipe and display it to the user.")]
static RecipeResponse GenerateRecipe(
    [Description("The complete recipe to display.")] Recipe recipe) => new() { Recipe = recipe };

AITool generateRecipe = AIFunctionFactory.Create(
    GenerateRecipe,
    name: "generate_recipe",
    description: "Generate or update the shared recipe and display it to the user.",
    RecipeSerializerContext.Default.Options);
```

### Create the Agent

Build the agent directly from your chat client with <xref:Microsoft.Agents.AI.ChatClientAgentOptions>. Put the system prompt and tools on <xref:Microsoft.Extensions.AI.ChatOptions>:

```csharp
using Microsoft.Agents.AI;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

const string SharedStateSystemPrompt =
    """
    You are a helpful recipe assistant that maintains a shared recipe state with the user.

    IMPORTANT:
    - When the user asks you to create, change, or improve a recipe, call the `generate_recipe`
      tool with a COMPLETE recipe: a title, skill_level, cooking_time, special_preferences, the
      full list of ingredients (each with an icon, name and amount) and the step-by-step
      instructions.
    - Always include every ingredient the recipe needs, keeping any the user already added.
    - When the user only asks a question about the recipe, answer in plain text and do NOT call the tool.
    """;

string endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
string deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT_NAME is not set.");

AIAgent recipeAgent = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
    .GetChatClient(deploymentName)
    .AsAIAgent(new ChatClientAgentOptions
    {
        Name = "RecipeAgent",
        Description = "An agent that maintains a shared recipe state with the user.",
        ChatOptions = new ChatOptions
        {
            Instructions = SharedStateSystemPrompt,
            Tools = [generateRecipe],
        },
    });
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

### Map the Tool Result to a State Event

Create an `AGUIStreamOptions`, register the tool name as a state snapshot, and attach it to the endpoint metadata. `MapAGUIServer` reads the stream options from the endpoint (or from `IOptions<AGUIStreamOptions>` in DI) and emits the state events for you:

```csharp
using AGUI.Server;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Add(RecipeSerializerContext.Default));
builder.Services.AddAGUIServer();

// A `generate_recipe` result becomes a STATE_SNAPSHOT event.
AGUIStreamOptions streamOptions = new AGUIStreamOptions()
    .MapResultAsStateSnapshot("generate_recipe");

WebApplication app = builder.Build();

// Attach the stream options to the endpoint. MapAGUIServer emits the state events for you.
app.MapAGUIServer("/", recipeAgent).WithMetadata(streamOptions);

await app.RunAsync();
```

That's the whole server. There is no custom `DelegatingAIAgent` and no protocol content to build: the tool returns your state object, `MapResultAsStateSnapshot` turns each result into a `STATE_SNAPSHOT`, and the framework streams it to the client.

### Reading Client State

The recipe lives on the client. When the client sends a turn, it includes its current state on the AG-UI `RunAgentInput`. Recover it from the request's <xref:Microsoft.Extensions.AI.ChatOptions> with `TryGetRunAgentInput` and read `RunAgentInput.State` (a `JsonElement`):

```csharp
using System.Text.Json;
using AGUI.Abstractions;
using AGUI.Server;
using Microsoft.Extensions.AI;

static bool TryGetClientState(ChatOptions chatOptions, out JsonElement state)
{
    if (chatOptions.TryGetRunAgentInput(out RunAgentInput? input) &&
        input.State is { ValueKind: not JsonValueKind.Undefined } clientState)
    {
        state = clientState;
        return true;
    }

    state = default;
    return false;
}
```

`TryGetRunAgentInput` reads the input that the hosting layer stashed on `ChatOptions.AdditionalProperties`. You never touch that dictionary directly. Give the model the current recipe by prepending it as a system message before the agent runs (for example, from a lightweight <xref:Microsoft.Agents.AI.DelegatingAIAgent> that only injects context and delegates the run), so edits build on the existing state instead of starting from scratch.

### Key Concepts

- **Tools return state**: A tool returns your state object; you never construct AG-UI events yourself.
- **Declarative mapping**: `AGUIStreamOptions.MapResultAsStateSnapshot(toolName)` / `MapResultAsStateDelta(toolName)` map a tool result to a `STATE_SNAPSHOT` / `STATE_DELTA` event.
- **Endpoint wiring**: Attach the stream options with `.WithMetadata(streamOptions)` on `MapAGUIServer`, or register `IOptions<AGUIStreamOptions>` in DI.
- **Reading state**: `ChatOptions.TryGetRunAgentInput(out var input)` recovers the `RunAgentInput`; `input.State` is the client's current state as a `JsonElement`.

## State Deltas with Agentic Generative UI

`MapResultAsStateSnapshot` replaces the entire state on each turn. For incremental changes, map a tool result to a `STATE_DELTA` with `MapResultAsStateDelta`, returning a [JSON Patch](https://datatracker.ietf.org/doc/html/rfc6902) document.

A common scenario is *agentic generative UI*: the agent builds a plan, then updates the status of individual steps as it works. `create_plan` sends the full plan as a snapshot; `update_plan_step` sends only the changed fields as a delta.

Define the plan model and status enum:

```csharp
using System.Text.Json.Serialization;

internal sealed class Plan
{
    [JsonPropertyName("steps")]
    public List<Step> Steps { get; set; } = [];
}

internal sealed class Step
{
    [JsonPropertyName("description")]
    public required string Description { get; set; }

    [JsonPropertyName("status")]
    public StepStatus Status { get; set; } = StepStatus.Pending;
}

[JsonConverter(typeof(JsonStringEnumConverter<StepStatus>))]
internal enum StepStatus
{
    Pending,
    Completed
}

internal sealed class JsonPatchOperation
{
    [JsonPropertyName("op")]
    public required string Op { get; set; }

    [JsonPropertyName("path")]
    public required string Path { get; set; }

    [JsonPropertyName("value")]
    public object? Value { get; set; }
}
```

The `create_plan` tool returns the full plan; `update_plan_step` returns a list of JSON Patch operations:

```csharp
using System.ComponentModel;

[Description("Create a plan with multiple steps.")]
public static Plan CreatePlan(
    [Description("List of step descriptions to create the plan.")] List<string> steps)
{
    return new Plan
    {
        Steps = [.. steps.Select(s => new Step { Description = s, Status = StepStatus.Pending })]
    };
}

[Description("Update a step in the plan with new description or status.")]
public static List<JsonPatchOperation> UpdatePlanStep(
    [Description("The index of the step to update.")] int index,
    [Description("The new status for the step.")] StepStatus status)
{
    // Status must be lowercase to match AG-UI frontend expectations.
    string statusValue = status == StepStatus.Pending ? "pending" : "completed";

    return
    [
        new JsonPatchOperation { Op = "replace", Path = $"/steps/{index}/status", Value = statusValue }
    ];
}
```

Register both tools on the agent (with `AllowMultipleToolCalls = false` so the model updates one step at a time), and map each tool result to the matching state event: `create_plan` to a snapshot, `update_plan_step` to a delta.

```csharp
using AGUI.Server;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

AITool createPlan = AIFunctionFactory.Create(
    CreatePlan, name: "create_plan", description: "Create a plan with multiple steps.");
AITool updatePlanStep = AIFunctionFactory.Create(
    UpdatePlanStep, name: "update_plan_step", description: "Update a step in the plan with new description or status.");

AIAgent planAgent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    Name = "AgenticUIAgent",
    ChatOptions = new ChatOptions
    {
        Instructions = "Use `create_plan` to set the initial steps, then call `update_plan_step` until every step is completed. Do not describe the plan in text.",
        Tools = [createPlan, updatePlanStep],
        AllowMultipleToolCalls = false,
    },
});

AGUIStreamOptions planStreamOptions = new AGUIStreamOptions()
    .MapResultAsStateSnapshot("create_plan")   // full plan -> STATE_SNAPSHOT
    .MapResultAsStateDelta("update_plan_step"); // JSON Patch -> STATE_DELTA

app.MapAGUIServer("/agentic_generative_ui", planAgent).WithMetadata(planStreamOptions);
```

> [!NOTE]
> `STATE_SNAPSHOT` replaces the entire state; `STATE_DELTA` applies a JSON Patch to the existing state. Send a snapshot when you set up or reset state, and deltas for incremental changes.

For a related pattern that streams a tool's arguments into state as the model generates them, continue with Predictive State Updates below.

## Predictive State Updates

Predictive state updates let the UI react to a tool call *while its arguments are still being generated*, instead of waiting for the tool to finish. As the model streams the arguments for a tool, the server converts those partial arguments into state snapshots and sends them to the client. The client renders each snapshot immediately, giving the user an optimistic, live preview. For example, a document editor shows the text appearing in real time, then asks the user to confirm the change once the model is done.

> [!NOTE]
> This scenario maps the endpoint manually and uses the built-in `TypedResults.ServerSentEvents(...)`, which requires **.NET 10.0 or later**.

### How It Works

Unlike the [shared-state](#emit-a-state-snapshot-from-a-tool) scenario, where a tool *runs* and its result becomes a snapshot, the predictive scenario intercepts the tool **call** before it executes and streams the argument into state:

1. The agent declares a `write_document_local` tool. The model calls it with the full document text as its `document` argument.
2. The tool is **not** executed server-side. Instead, an `AGUIStreamOptions` `MapCall` mapping intercepts the call.
3. The mapping emits a series of `STATE_SNAPSHOT` events, each carrying a progressively longer prefix of the document, so the client sees the text stream in.
4. It then completes the tool call with a `TOOL_CALL_RESULT` event and injects a client-side `confirm_changes` tool call so the client can prompt the user to approve.
5. The client renders each snapshot and shows the confirm/reject prompt.

Because the mapping produces the tool's result itself, the document tool is declared but never invoked. The chat client is built **without** function invocation.

### Define the State Model

The state model describes the shape the client renders. Use `JsonPropertyName` so the property names match what the client expects:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed class DocumentState
{
    [JsonPropertyName("document")]
    public string Document { get; set; } = string.Empty;
}

[JsonSerializable(typeof(DocumentState))]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class DocumentSerializerContext : JsonSerializerContext;
```

### Declare the Document Tool

The tool signature is what the model fills in. It is declared so the model calls it, but its result is produced by the stream mapping, not by executing the method body:

```csharp
using System.ComponentModel;
using Microsoft.Extensions.AI;

[Description("Write a document in markdown format.")]
static string WriteDocument(
    [Description("The document content to write.")] string document) => "Document written successfully";

AITool writeDocument = AIFunctionFactory.Create(
    WriteDocument,
    name: "write_document_local",
    description: "Write a document. Use markdown formatting to format the document.");
```

### Configure the Predictive Stream Mapping

Register a `MapCall` mapping for the tool. When the model calls `write_document_local`, the mapping reads the streamed `document` argument, emits progressive `StateSnapshotEvent` snapshots, completes the tool call, and injects a `confirm_changes` client-side tool call:

```csharp
using System.Text.Json;
using AGUI.Abstractions;
using AGUI.Server;
using Microsoft.Extensions.AI;

static AGUIStreamOptions CreatePredictiveStreamOptions(JsonSerializerOptions jsonSerializerOptions)
{
    string? lastEmittedDocument = null;

    return new AGUIStreamOptions().MapCall("write_document_local", fcc =>
    {
        string? document = fcc.Arguments?.TryGetValue("document", out var value) == true
            ? value?.ToString()
            : null;

        if (document is null || document == lastEmittedDocument)
        {
            return [];
        }

        var events = new List<BaseEvent>();

        // Only stream the newly added portion if the document grew.
        int startIndex = lastEmittedDocument is not null &&
            document.StartsWith(lastEmittedDocument, StringComparison.Ordinal)
                ? lastEmittedDocument.Length
                : 0;

        const int chunkSize = 10;
        for (int i = startIndex; i < document.Length; i += chunkSize)
        {
            int length = Math.Min(chunkSize, document.Length - i);
            var snapshot = new DocumentState { Document = document[..(i + length)] };
            JsonElement snapshotJson = JsonSerializer.SerializeToElement(snapshot, jsonSerializerOptions);

            events.Add(new StateSnapshotEvent { Snapshot = snapshotJson });
        }

        // Complete the write_document_local call (its document is now reflected in state) so the
        // only tool call the client sees pending is confirm_changes.
        events.Add(new ToolCallResultEvent
        {
            MessageId = Guid.NewGuid().ToString("N"),
            ToolCallId = fcc.CallId,
            Content = "Document written.",
            Role = "tool",
        });

        // Inject a client-side confirm_changes tool call so the approval modal renders.
        string confirmCallId = Guid.NewGuid().ToString("N");
        string confirmMessageId = Guid.NewGuid().ToString("N");
        events.Add(new ToolCallStartEvent { ToolCallId = confirmCallId, ToolCallName = "confirm_changes", ParentMessageId = confirmMessageId });
        events.Add(new ToolCallArgsEvent { ToolCallId = confirmCallId, Delta = "{}" });
        events.Add(new ToolCallEndEvent { ToolCallId = confirmCallId });

        lastEmittedDocument = document;
        return events;
    });
}
```

> [!NOTE]
> Each snapshot contains the full document up to that point, so the client always renders a consistent view even if it misses an intermediate update.

### Map the Endpoint

Because the mapping produces the tool result itself, build the chat client **without** function invocation and stream through the AG-UI pipeline directly: adapt the incoming `RunAgentInput` with `ToChatRequestContext`, call `GetStreamingResponseAsync`, and convert the updates with `AsAGUIEventStreamAsync`.

```csharp
using AGUI.Abstractions;
using AGUI.Server;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

const string PredictiveSystemPrompt =
    """
    You are a document editor assistant. When asked to write or edit content:
    - Use the `write_document_local` tool with the full document text in Markdown format.
    - You MUST write the full document, even when changing only a few words.
    - When making edits, keep them minimal. Do not change every word.
    After writing the document, briefly summarize the changes you made in at most two sentences.
    """;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Add(DocumentSerializerContext.Default));
builder.Services.AddAGUIServer();

string endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"]
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
string deploymentName = builder.Configuration["AZURE_OPENAI_DEPLOYMENT_NAME"]
    ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT_NAME is not set.");

// No UseFunctionInvocation: the call is intercepted by the stream mapping, not executed.
IChatClient chatClient = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient();

WebApplication app = builder.Build();

JsonSerializerOptions jsonSerializerOptions = app.Services
    .GetRequiredService<IOptions<JsonOptions>>()
    .Value.SerializerOptions;

app.MapPost("/", (
    [FromBody] RunAgentInput input,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    AGUIStreamOptions streamOptions = CreatePredictiveStreamOptions(jsonSerializerOptions);

    ChatRequestContext ctx = input.ToChatRequestContext(jsonSerializerOptions, streamOptions);
    ctx.Messages.Insert(0, new ChatMessage(ChatRole.System, PredictiveSystemPrompt));
    (ctx.ChatOptions.Tools ??= []).Add(writeDocument);

    var updates = chatClient.GetStreamingResponseAsync(ctx.Messages, ctx.ChatOptions, cancellationToken);
    IAsyncEnumerable<BaseEvent> events = updates.AsAGUIEventStreamAsync(ctx, cancellationToken);

    return TypedResults.ServerSentEvents(events);
});

await app.RunAsync();
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

> [!NOTE]
> `confirm_changes` is a *client-side* tool. The stream mapping requests it, and the client renders the approval prompt. See [Human-in-the-Loop](human-in-the-loop.md) for the client-side tool pattern.

### Predictive Key Concepts

- **`AGUIStreamOptions.MapCall`**: Intercepts a tool *call* (before execution) and returns the AG-UI events to emit for it.
- **`FunctionCallContent.Arguments`**: The streamed tool arguments. Read `Arguments["document"]` to get the text as the model produces it.
- **`StateSnapshotEvent`**: Each snapshot holds the full document prefix so far, producing the optimistic streaming effect.
- **`ToChatRequestContext` / `AsAGUIEventStreamAsync`**: The AG-UI streaming pipeline that adapts a `RunAgentInput` to a chat request and converts the response updates back into AG-UI events.
- **`confirm_changes`**: A client-side tool call injected after the document is written, so the user can approve the result.

### Rendering on the Client

A UI toolkit such as [CopilotKit](https://copilotkit.ai/) subscribes to the state snapshots and re-renders the document on each one, then shows the confirm or reject prompt when the `confirm_changes` tool call arrives. You can see this scenario running in the [AG-UI Dojo](https://dojo.ag-ui.com/microsoft-agent-framework-dotnet).

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
from agent_framework import tool


@tool
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

from agent_framework import Agent
from agent_framework.openai import OpenAIChatCompletionClient
from agent_framework_ag_ui import (
    AgentFrameworkAgent,
    add_agent_framework_fastapi_endpoint,
)
from azure.identity import AzureCliCredential
from fastapi import FastAPI

# Create the chat agent with tools
agent = Agent(
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
    client=OpenAIChatCompletionClient(
        model=deployment_name,
        azure_endpoint=endpoint,
        api_version=os.getenv("AZURE_OPENAI_API_VERSION"),
        credential=AzureCliCredential(),
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

from agent_framework import Agent, Message, Role
from agent_framework_ag_ui import AGUIChatClient


async def main():
    """Example client with state tracking."""
    server_url = os.environ.get("AGUI_SERVER_URL", "http://127.0.0.1:8888/")
    print(f"Connecting to AG-UI server at: {server_url}\n")

    # Create AG-UI chat client
    chat_client = AGUIChatClient(endpoint=server_url)

    # Wrap with Agent for convenient API
    agent = Agent(
        name="ClientAgent",
        client=chat_client,
        instructions="You are a helpful assistant.",
    )

    # Get a thread for conversation continuity
    thread = agent.create_session()

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
            async for update in agent.run(message, session=thread, stream=True):
                # Handle text content
                if update.text:
                    print(update.text, end="", flush=True)

                # Handle state updates surfaced through AG-UI events.
                for content in update.contents:
                    if content.type == "data" and getattr(content, "media_type", None) == "application/json":
                        print("\n[JSON state payload received]")

            print(f"\n\nCurrent state: {json.dumps(state, indent=2)}")
            print()

    except KeyboardInterrupt:
        print("\n\nExiting...")


if __name__ == "__main__":
    # Install dependencies: pip install agent-framework-ag-ui --pre
    asyncio.run(main())
```

### Key Benefits

The `AGUIChatClient` provides:

- **Simplified Connection**: Automatic handling of HTTP/SSE communication
- **Thread Management**: Built-in thread ID tracking for conversation continuity
- **Agent Integration**: Works seamlessly with `Agent` for familiar API
- **State Handling**: Automatic parsing of state events from the server
- **Parity with .NET**: Consistent experience across languages

> [!TIP]
> Use `AGUIChatClient` with `Agent` to get the full benefit of the agent framework's features like conversation history, tool execution, and middleware support.

## Confirming predicted state

Set `require_confirmation=True` on `AgentFrameworkAgent` when predicted state changes should wait for client confirmation before being applied:

```python
recipe_agent = AgentFrameworkAgent(
    agent=agent,
    state_schema={"recipe": {"type": "object", "description": "The current recipe"}},
    predict_state_config={"recipe": {"tool": "update_recipe", "tool_argument": "recipe"}},
    require_confirmation=True,
)
```

Customize confirmation copy in your AG-UI client UI when rendering the confirmation event.

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
)
```

When enabled:

1. State updates stream as the agent generates tool arguments (predictive updates via `STATE_DELTA` events)
2. Agent pauses before executing the tool with a `tool_call` interrupt in `RUN_FINISHED.outcome.interrupts`
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


@tool
def generate_task_steps(steps: list[TaskStep]) -> str:
    """Generate task steps for a given task."""
    return f"Generated {len(steps)} steps."


@tool
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
@tool
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
@tool
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
agent = Agent(
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

### Customize confirmation UI

Customize approval and state-confirmation messages in your AG-UI client when rendering confirmation events from the server.

## Next Steps

You've now learned all the core AG-UI features! Next you can:

- Explore the [Agent Framework documentation](../../../../overview/index.md)
- Build a complete application combining all AG-UI features
- Deploy your AG-UI service to production

## Additional Resources

- [AG-UI Overview](index.md)
- [Getting Started](getting-started.md)
- [Backend Tool Rendering](backend-tool-rendering.md)
<!-- - [Human-in-the-Loop](human-in-the-loop.md) -->

::: zone-end

::: zone pivot="programming-language-go"

Go AG-UI state management can be implemented with middleware that emits structured `message.DataContent` updates alongside normal text updates.

```go
stateSnapshotMiddleware := agent.MiddlewareFunc(func(next agent.RunFunc, ctx context.Context, messages []*message.Message, opts ...agent.Option) iter.Seq2[*agent.ResponseUpdate, error] {
    return func(yield func(*agent.ResponseUpdate, error) bool) {
        for update, err := range next(ctx, messages, opts...) {
            if err != nil {
                yield(nil, err)
                return
            }
            if update != nil {
                // Inspect update contents and yield DataContent snapshots as needed.
            }
            if !yield(update, nil) {
                return
            }
        }
    }
})

a := foundryprovider.NewAgent(endpoint, token, foundryprovider.ModelDeployment(model), foundryprovider.AgentConfig{
    Config: agent.Config{
        Middlewares: []agent.Middleware{stateSnapshotMiddleware},
    },
})
```

> [!TIP]
> See the [AG-UI state management sample](https://github.com/microsoft/agent-framework-go/blob/main/examples/02-agents/agui/step05_state_management/server/main.go) for a complete runnable example.

::: zone-end
