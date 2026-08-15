---
title: Declarative Workflows - Overview
description: Learn how to define workflows using YAML configuration files instead of programmatic code in Microsoft Agent Framework.
zone_pivot_groups: programming-languages
author: moonbox3
ms.topic: tutorial
ms.author: evmattso
ms.date: 06/26/2026
ms.service: agent-framework
---

<!--
  Language parity table – keep in sync when adding/removing sections.

  Sections (presence in each language zone):

  | Section                        | C# | Python | Go | Notes                                          |
  |--------------------------------|:--:|:------:|:--:|------------------------------------------------|
  | Basic YAML Structure           | ✅ |   ✅   | ❌ | No Go declarative workflow package             |
  | Prerequisites                  | ✅ |   ✅   | ❌ |                                                |
  | Your First Declarative Workflow| ✅ |   ✅   | ❌ |                                                |
  | Core Concepts                  | ✅ |   ✅   | ❌ |                                                |
  | Variable Namespaces            | ✅ |   ✅   | ❌ | Python adds `Workflow.Inputs`                  |
  | Expression Language            | ✅ |   ✅   | ❌ |                                                |
  | Configuration Options          | ✅ |   ✅   | ❌ |                                                |
  | Agent Provider / Registry      | ✅ |   ✅   | ❌ |                                                |
  | Workflow Execution             | ✅ |   ✅   | ❌ |                                                |
  | Resuming from Checkpoints      | ✅ |   ✅   | ❌ |                                                |
  | Actions Reference              | ✅ |   ✅   | ❌ |                                                |
  | Advanced Patterns              | ✅ |   ✅   | ❌ |                                                |
  | Next Steps                     | ✅ |   ✅   | ❌ |                                                |

  Action-kind parity (verified against source at C:\code\agent-framework):
    - C#: dotnet/src/Microsoft.Agents.AI.Workflows.Declarative/Interpreter/WorkflowActionVisitor.cs
    - Python: python/packages/declarative/agent_framework_declarative/_workflows/_executors_*.py

  | Action                              | C# | Python | Notes                                            |
  |-------------------------------------|:--:|:------:|--------------------------------------------------|
  | SetVariable                         | ✅ |   ✅   |                                                  |
  | SetValue                            | ❌ |   ✅   | Python alias for SetVariable using `path:`       |
  | SetMultipleVariables                | ✅ |   ✅   |                                                  |
  | SetTextVariable                     | ✅ |   ✅   |                                                  |
  | ResetVariable                       | ✅ |   ✅   |                                                  |
  | ClearAllVariables                   | ✅ |   ✅   |                                                  |
  | ParseValue                          | ✅ |   ✅   |                                                  |
  | EditTable                           | ✅ |   ✅   |                                                  |
  | EditTableV2                         | ✅ |   ✅   |                                                  |
  | If                                  | ✅ |   ✅   |                                                  |
  | ConditionGroup                      | ✅ |   ✅   |                                                  |
  | Foreach                             | ✅ |   ✅   |                                                  |
  | BreakLoop                           | ✅ |   ✅   |                                                  |
  | ContinueLoop                        | ✅ |   ✅   |                                                  |
  | GotoAction                          | ✅ |   ✅   |                                                  |
  | SendActivity                        | ✅ |   ✅   |                                                  |
  | InvokeAzureAgent                    | ✅ |   ✅   |                                                  |
  | InvokeFunctionTool                  | ✅ |   ✅   |                                                  |
  | InvokeMcpTool                       | ✅ |   ✅   |                                                  |
  | HttpRequestAction                   | ✅ |   ✅   |                                                  |
  | Question                            | ✅ |   ✅   |                                                  |
  | RequestExternalInput                | ✅ |   ✅   |                                                  |
  | EndWorkflow / EndDialog             | ✅ |   ✅   | `EndDialog` is the Python alias                  |
  | EndConversation                     | ✅ |   ✅   |                                                  |
  | CreateConversation                  | ✅ |   ✅   |                                                  |
  | CancelDialog / CancelAllDialogs     | ✅ |   ✅   |                                                  |
  | AddConversationMessage              | ✅ |   ❌   | C#-only                                          |
  | CopyConversationMessages            | ✅ |   ❌   | C#-only                                          |
  | RetrieveConversationMessage[s]      | ✅ |   ❌   | C#-only                                          |
-->

# Declarative Workflows - Overview

Declarative workflows allow you to define workflow logic using YAML configuration files instead of writing programmatic code. This approach makes workflows easier to read, modify, and share across teams.

## Overview

With declarative workflows, you describe *what* your workflow should do rather than *how* to implement it. The framework handles the underlying execution, converting your YAML definitions into executable workflow graphs.

**Key benefits:**

- **Readable format**: YAML syntax is easy to understand, even for non-developers
- **Portable**: Workflow definitions can be shared, versioned, and modified without code changes
- **Rapid iteration**: Modify workflow behavior by editing configuration files
- **Consistent structure**: Predefined action types ensure workflows follow best practices

## When to Use Declarative vs. Programmatic Workflows

| Scenario | Recommended Approach |
|----------|---------------------|
| Standard orchestration patterns | Declarative |
| Workflows that change frequently | Declarative |
| Non-developers need to modify workflows | Declarative |
| Complex custom logic | Programmatic |
| Maximum flexibility and control | Programmatic |
| Integration with existing Python code | Programmatic |

## Basic YAML Structure

The YAML structure differs slightly between C# and Python implementations. See the language-specific sections below for details.

## Action Types

Declarative workflows support a wide range of action kinds covering variable management, control flow, agent and tool invocation, HTTP and MCP integration, human-in-the-loop, and conversation control. The complete language-specific reference appears in each zone below; for an at-a-glance availability matrix across both languages, see [Actions Quick Reference](#actions-quick-reference) at the bottom of this article.

::: zone pivot="programming-language-csharp"

### C# YAML Structure

C# declarative workflows use a trigger-based structure:

```yaml
#
# Workflow description as a comment
#
kind: Workflow
trigger:

  kind: OnConversationStart
  id: my_workflow
  actions:

    - kind: ActionType
      id: unique_action_id
      displayName: Human readable name
      # Action-specific properties
```

### Structure Elements

| Element | Required | Description |
|---------|----------|-------------|
| `kind` | Yes | Must be `Workflow` |
| `trigger.kind` | Yes | Trigger type (typically `OnConversationStart`) |
| `trigger.id` | Yes | Unique identifier for the workflow |
| `trigger.actions` | Yes | List of actions to execute |

::: zone-end

::: zone pivot="programming-language-python"

### Python YAML Structure

Python declarative workflows use a name-based structure with optional inputs:

```yaml
name: my-workflow
description: A brief description of what this workflow does

inputs:
  parameterName:
    type: string
    description: Description of the parameter

actions:
  - kind: ActionType
    id: unique_action_id
    displayName: Human readable name
    # Action-specific properties
```

### Structure Elements

| Element | Required | Description |
|---------|----------|-------------|
| `name` | Yes | Unique identifier for the workflow |
| `description` | No | Human-readable description |
| `inputs` | No | Input parameters the workflow accepts |
| `actions` | Yes | List of actions to execute |

::: zone-end

::: zone pivot="programming-language-csharp"

## Prerequisites

Before you begin, ensure you have:

- .NET 8.0 or later
- A [Microsoft Foundry](https://ai.azure.com/) project with at least one deployed agent
- The following NuGet packages installed:

```bash
dotnet add package Microsoft.Agents.AI.Workflows.Declarative --prerelease
dotnet add package Microsoft.Agents.AI.Workflows.Declarative.AzureAI --prerelease
```
- If you intend to add MCP tool invocation action to your workflow, also install the following NuGet package:

```bash
dotnet add package Microsoft.Agents.AI.Workflows.Declarative.Mcp --prerelease
```

- Basic familiarity with YAML syntax
- Understanding of [workflow concepts](../concepts/workflows/index.md)

## Your First Declarative Workflow

Let's create a simple workflow that greets a user based on their input.

### Step 1: Create the YAML File

Create a file named `greeting-workflow.yaml`:

```yaml
#
# This workflow demonstrates a simple greeting based on user input.
# The user's message is captured via System.LastMessage.
#
# Example input:
# Alice
#
kind: Workflow
trigger:

  kind: OnConversationStart
  id: greeting_workflow
  actions:

    # Capture the user's input from the last message
    - kind: SetVariable
      id: capture_name
      displayName: Capture user name
      variable: Local.userName
      value: =System.LastMessage.Text

    # Set a greeting prefix
    - kind: SetVariable
      id: set_greeting
      displayName: Set greeting prefix
      variable: Local.greeting
      value: Hello

    # Build the full message using an expression
    - kind: SetVariable
      id: build_message
      displayName: Build greeting message
      variable: Local.message
      value: =Concat(Local.greeting, ", ", Local.userName, "!")

    # Send the greeting to the user
    - kind: SendActivity
      id: send_greeting
      displayName: Send greeting to user
      activity: =Local.message
```

### Step 2: Configure the Agent Provider

Create a C# console application to execute the workflow. First, configure the agent provider that connects to Foundry:

```csharp
using Azure.Identity;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Declarative;
using Microsoft.Extensions.Configuration;

// Load configuration (endpoint should be set in user secrets or environment variables)
IConfiguration configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

string foundryEndpoint = configuration["FOUNDRY_PROJECT_ENDPOINT"]
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT not configured");

// Create the agent provider that connects to Foundry
// WARNING: DefaultAzureCredential is convenient for development but requires
// careful consideration in production environments.
AzureAgentProvider agentProvider = new(
    new Uri(foundryEndpoint),
    new DefaultAzureCredential());
```

### Step 3: Build and Run the Workflow

```csharp
// Define workflow options with the agent provider
DeclarativeWorkflowOptions options = new(agentProvider)
{
    Configuration = configuration,
    // LoggerFactory = loggerFactory, // Optional: Enable logging
    // ConversationId = conversationId, // Optional: Continue existing conversation
};

// Build the workflow from the YAML file
string workflowPath = Path.Combine(AppContext.BaseDirectory, "greeting-workflow.yaml");
Workflow workflow = DeclarativeWorkflowBuilder.Build<string>(workflowPath, options);

Console.WriteLine($"Loaded workflow from: {workflowPath}");
Console.WriteLine(new string('-', 40));

// Create a checkpoint manager (in-memory for this example)
CheckpointManager checkpointManager = CheckpointManager.CreateInMemory();

// Execute the workflow with input
string input = "Alice";
StreamingRun run = await InProcessExecution.RunStreamingAsync(
    workflow,
    input,
    checkpointManager);

// Process workflow events
await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync())
{
    switch (workflowEvent)
    {
        case MessageActivityEvent activityEvent:
            Console.WriteLine($"Activity: {activityEvent.Message}");
            break;
        case AgentResponseEvent responseEvent:
            Console.WriteLine($"Response: {responseEvent.Response.Text}");
            break;
        case WorkflowErrorEvent errorEvent:
            Console.WriteLine($"Error: {errorEvent.Data}");
            break;
    }
}

Console.WriteLine("Workflow completed!");
```

### Expected Output

```
Loaded workflow from: C:\path\to\greeting-workflow.yaml
----------------------------------------
Activity: Hello, Alice!
Workflow completed!
```

## Core Concepts

### Variable Namespaces

Declarative workflows in C# use namespaced variables to organize state:

| Namespace | Description | Example |
|-----------|-------------|---------|
| `Local.*` | Variables local to the workflow | `Local.message` |
| `System.*` | System-provided values | `System.ConversationId`, `System.LastMessage` |

> [!NOTE]
> C# declarative workflows do not use `Workflow.Inputs` or `Workflow.Outputs` namespaces. Input is received via `System.LastMessage` and output is sent via `SendActivity` actions.

#### System Variables

| Variable | Description |
|----------|-------------|
| `System.ConversationId` | Current conversation identifier |
| `System.LastMessage` | The most recent user message |
| `System.LastMessage.Text` | Text content of the last message |

### Expression Language

Values prefixed with `=` are evaluated as expressions using the PowerFx expression language:

```yaml
# Literal value (no evaluation)
value: Hello

# Expression (evaluated at runtime)
value: =Concat("Hello, ", Local.userName)

# Access last message text
value: =System.LastMessage.Text
```

Common functions include:
- `Concat(str1, str2, ...)` - Concatenate strings
- `If(condition, trueValue, falseValue)` - Conditional expression
- `IsBlank(value)` - Check if value is empty
- `Upper(text)` / `Lower(text)` - Case conversion
- `Find(searchText, withinText)` - Find text within string
- `MessageText(message)` - Extract text from a message object
- `UserMessage(text)` - Create a user message from text
- `AgentMessage(text)` - Create an agent message from text

### Configuration Options

The `DeclarativeWorkflowOptions` class provides configuration for workflow execution:

```csharp
DeclarativeWorkflowOptions options = new(agentProvider)
{
    // Application configuration for variable substitution
    Configuration = configuration,

    // Continue an existing conversation (optional)
    ConversationId = "existing-conversation-id",

    // Enable logging (optional)
    LoggerFactory = loggerFactory,

    // MCP tool handler for InvokeMcpTool actions (optional)
    McpToolHandler = mcpToolHandler,

    // HTTP request handler for HttpRequestAction actions (optional)
    HttpRequestHandler = new DefaultHttpRequestHandler(),

    // PowerFx expression limits (optional)
    MaximumCallDepth = 50,
    MaximumExpressionLength = 10000,

    // Telemetry configuration (optional)
    ConfigureTelemetry = opts => { /* configure telemetry */ },
    TelemetryActivitySource = activitySource,
};
```

### Agent Provider Setup

The `AzureAgentProvider` connects your workflow to Foundry agents:

```csharp
using Azure.Identity;
using Microsoft.Agents.AI.Workflows.Declarative;

// Create the agent provider with Azure credentials
AzureAgentProvider agentProvider = new(
    new Uri("https://your-project.api.azureml.ms"),
    new DefaultAzureCredential())
{
    // Optional: Define functions that agents can automatically invoke
    Functions = [
        AIFunctionFactory.Create(myPlugin.GetData),
        AIFunctionFactory.Create(myPlugin.ProcessItem),
    ],

    // Optional: Allow concurrent function invocation
    AllowConcurrentInvocation = true,

    // Optional: Allow multiple tool calls per response
    AllowMultipleToolCalls = true,
};
```

### Workflow Execution

Use `InProcessExecution` to run workflows and handle events:

```csharp
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;

// Create checkpoint manager (choose in-memory or file-based)
CheckpointManager checkpointManager = CheckpointManager.CreateInMemory();
// Or persist to disk:
// var checkpointFolder = Directory.CreateDirectory("./checkpoints");
// var checkpointManager = CheckpointManager.CreateJson(
//     new FileSystemJsonCheckpointStore(checkpointFolder));

// Start workflow execution
StreamingRun run = await InProcessExecution.RunStreamingAsync(
    workflow,
    input,
    checkpointManager);

// Process events as they occur
await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync())
{
    switch (workflowEvent)
    {
        case MessageActivityEvent activity:
            Console.WriteLine($"Message: {activity.Message}");
            break;

        case AgentResponseUpdateEvent streamEvent:
            Console.Write(streamEvent.Update.Text); // Streaming text
            break;

        case AgentResponseEvent response:
            Console.WriteLine($"Agent: {response.Response.Text}");
            break;

        case RequestInfoEvent request:
            // Handle external input requests (human-in-the-loop)
            var userInput = await GetUserInputAsync(request);
            await run.SendResponseAsync(request.Request.CreateResponse(userInput));
            break;

        case SuperStepCompletedEvent checkpoint:
            // Checkpoint created - can resume from here if needed
            var checkpointInfo = checkpoint.CompletionInfo?.Checkpoint;
            break;

        case WorkflowErrorEvent error:
            Console.WriteLine($"Error: {error.Data}");
            break;
    }
}
```

### Resuming from Checkpoints

Workflows can be resumed from checkpoints for fault tolerance:

```csharp
// Save checkpoint info when workflow yields
CheckpointInfo? lastCheckpoint = null;

await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync())
{
    if (workflowEvent is SuperStepCompletedEvent checkpointEvent)
    {
        lastCheckpoint = checkpointEvent.CompletionInfo?.Checkpoint;
    }
}

// Later: Resume from the saved checkpoint
if (lastCheckpoint is not null)
{
    // Recreate the workflow (can be on a different machine)
    Workflow workflow = DeclarativeWorkflowBuilder.Build<string>(workflowPath, options);

    StreamingRun resumedRun = await InProcessExecution.ResumeStreamingAsync(
        workflow,
        lastCheckpoint,
        checkpointManager);

    // Continue processing events...
}
```

### AOT and Trim-Aggressive Checkpointing

When you publish with Native AOT (`dotnet publish -p:PublishAot=true`) or otherwise disable `System.Text.Json`'s reflection fallback (`<JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>`), the default `CheckpointManager.CreateJson(store)` call fails on checkpoint commit or rehydration.

The declarative-workflow package ships a source-generated `JsonSerializerOptions` instance, `DeclarativeWorkflowJsonOptions.Default`, that covers every declarative-package type flowing through the checkpoint pipeline. Pass it as the second argument to `CheckpointManager.CreateJson`:

```csharp
using Microsoft.Agents.AI.Workflows.Checkpointing;
using Microsoft.Agents.AI.Workflows.Declarative;

// AOT-safe: type info is resolved via the source-generated JsonSerializerContext,
// so no runtime reflection is required.
CheckpointManager checkpointManager = CheckpointManager.CreateJson(
    store,
    DeclarativeWorkflowJsonOptions.Default);
```

> [!NOTE]
> Passing `DeclarativeWorkflowJsonOptions.Default` is **safe to use in non-AOT environments** as well. It is a drop-in upgrade for `CheckpointManager.CreateJson(store)` — reflection-enabled apps see no behavior change. Adopt it unconditionally so the same code keeps working if you later publish with AOT or trimming.

`DeclarativeWorkflowJsonOptions` is marked `[Experimental("MAAI001")]`. Suppress the diagnostic at the call site or in your project file:

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);MAAI001</NoWarn>
</PropertyGroup>
```

#### Registering user-defined types

If your workflow input, custom `ActionExecutorResult.Result` payloads, or non-primitive approval-request arguments are user-defined types, clone `Default` and append your own source-generated resolver:

```csharp
// Compose: declarative-package types + your app's source-gen context.
JsonSerializerOptions options = new(DeclarativeWorkflowJsonOptions.Default);
options.TypeInfoResolverChain.Add(MyAppJsonContext.Default);
options.MakeReadOnly();

CheckpointManager checkpointManager = CheckpointManager.CreateJson(store, options);
```

Where `MyAppJsonContext` is a `JsonSerializerContext` you define for your app's types:

```csharp
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(MyWorkflowInput))]
[JsonSerializable(typeof(MyCustomResult))]
internal sealed partial class MyAppJsonContext : JsonSerializerContext;
```

> [!TIP]
> For an end-to-end runnable example — including the YAML workflow, an `AzureCliCredential`-backed agent, and an observable "drop the options to see the failure" mode — see the [`AotCheckpointing` sample](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/Declarative/AotCheckpointing) in `dotnet/samples/03-workflows/Declarative/AotCheckpointing`. The sample's `.csproj` sets `JsonSerializerIsReflectionEnabledByDefault=false` to reproduce the AOT failure mode without requiring a full AOT publish.

## Actions Reference

Actions are the building blocks of declarative workflows. Each action performs a specific operation, and actions are executed sequentially in the order they appear in the YAML file.

### Action Structure

All actions share common properties:

```yaml
- kind: ActionType      # Required: The type of action
  id: unique_id         # Optional: Unique identifier for referencing
  displayName: Name     # Optional: Human-readable name for logging
  # Action-specific properties...
```

### Variable Management Actions

#### SetVariable

Sets a variable to a specified value.

```yaml
- kind: SetVariable
  id: set_greeting
  displayName: Set greeting message
  variable: Local.greeting
  value: Hello World
```

With an expression:

```yaml
- kind: SetVariable
  variable: Local.fullName
  value: =Concat(Local.firstName, " ", Local.lastName)
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `variable` | Yes | Variable path (e.g., `Local.name`, `Workflow.Outputs.result`) |
| `value` | Yes | Value to set (literal or expression) |

#### SetMultipleVariables

Sets multiple variables in a single action.

```yaml
- kind: SetMultipleVariables
  id: initialize_vars
  displayName: Initialize variables
  variables:
    Local.counter: 0
    Local.status: pending
    Local.message: =Concat("Processing order ", Local.orderId)
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `variables` | Yes | Map of variable paths to values |

#### SetTextVariable

Sets a text variable to a specified string value.

```yaml
- kind: SetTextVariable
  id: set_text
  displayName: Set text content
  variable: Local.description
  value: This is a text description
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `variable` | Yes | Variable path for the text value |
| `value` | Yes | Text value to set |

#### ResetVariable

Clears a variable's value.

```yaml
- kind: ResetVariable
  id: clear_counter
  variable: Local.counter
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `variable` | Yes | Variable path to reset |

#### ClearAllVariables

Resets all variables in the current context.

```yaml
- kind: ClearAllVariables
  id: clear_all
  displayName: Clear all workflow variables
```

#### ParseValue

Extracts or converts data into a usable format.

```yaml
- kind: ParseValue
  id: parse_json
  displayName: Parse JSON response
  source: =Local.rawResponse
  variable: Local.parsedData
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `source` | Yes | Expression returning the value to parse |
| `variable` | Yes | Variable path to store the parsed result |

#### EditTableV2

Modifies data in a structured table format.

```yaml
- kind: EditTableV2
  id: update_table
  displayName: Update configuration table
  table: Local.configTable
  operation: update
  row:
    key: =Local.settingName
    value: =Local.settingValue
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `table` | Yes | Variable path to the table |
| `operation` | Yes | Operation type (add, update, delete) |
| `row` | Yes | Row data for the operation |

### Control Flow Actions

#### If

Executes actions conditionally based on a condition.

```yaml
- kind: If
  id: check_age
  displayName: Check user age
  condition: =Local.age >= 18
  then:
    - kind: SendActivity
      activity:
        text: "Welcome, adult user!"
  else:
    - kind: SendActivity
      activity:
        text: "Welcome, young user!"
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `condition` | Yes | Expression that evaluates to true/false |
| `then` | Yes | Actions to execute if condition is true |
| `else` | No | Actions to execute if condition is false |

#### ConditionGroup

Evaluates multiple conditions like a switch/case statement.

```yaml
- kind: ConditionGroup
  id: route_by_category
  displayName: Route based on category
  conditions:
    - condition: =Local.category = "electronics"
      id: electronics_branch
      actions:
        - kind: SetVariable
          variable: Local.department
          value: Electronics Team
    - condition: =Local.category = "clothing"
      id: clothing_branch
      actions:
        - kind: SetVariable
          variable: Local.department
          value: Clothing Team
  elseActions:
    - kind: SetVariable
      variable: Local.department
      value: General Support
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `conditions` | Yes | List of condition/actions pairs (first match wins) |
| `elseActions` | No | Actions if no condition matches |

#### Foreach

Iterates over a collection.

```yaml
- kind: Foreach
  id: process_items
  displayName: Process each item
  source: =Local.items
  itemName: item
  indexName: index
  actions:
    - kind: SendActivity
      activity:
        text: =Concat("Processing item ", index, ": ", item)
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `source` | Yes | Expression returning a collection |
| `itemName` | No | Variable name for current item (default: `item`) |
| `indexName` | No | Variable name for current index (default: `index`) |
| `actions` | Yes | Actions to execute for each item |

#### BreakLoop

Exits the current loop immediately.

```yaml
- kind: Foreach
  source: =Local.items
  actions:
    - kind: If
      condition: =item = "stop"
      then:
        - kind: BreakLoop
    - kind: SendActivity
      activity:
        text: =item
```

#### ContinueLoop

Skips to the next iteration of the loop.

```yaml
- kind: Foreach
  source: =Local.numbers
  actions:
    - kind: If
      condition: =item < 0
      then:
        - kind: ContinueLoop
    - kind: SendActivity
      activity:
        text: =Concat("Positive number: ", item)
```

#### GotoAction

Jumps to a specific action by ID.

```yaml
- kind: SetVariable
  id: start_label
  variable: Local.attempts
  value: =Local.attempts + 1

- kind: SendActivity
  activity:
    text: =Concat("Attempt ", Local.attempts)

- kind: If
  condition: =And(Local.attempts < 3, Not(Local.success))
  then:
    - kind: GotoAction
      actionId: start_label
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `actionId` | Yes | ID of the action to jump to |

### Output Actions

#### SendActivity

Sends a message to the user.

```yaml
- kind: SendActivity
  id: send_welcome
  displayName: Send welcome message
  activity:
    text: "Welcome to our service!"
```

With an expression:

```yaml
- kind: SendActivity
  activity:
    text: =Concat("Hello, ", Local.userName, "! How can I help you today?")
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `activity` | Yes | The activity to send |
| `activity.text` | Yes | Message text (literal or expression) |

### Agent Invocation Actions

#### InvokeAzureAgent

Invokes a Foundry agent.

Basic invocation:

```yaml
- kind: InvokeAzureAgent
  id: call_assistant
  displayName: Call assistant agent
  agent:
    name: AssistantAgent
  conversationId: =System.ConversationId
```

With input and output configuration:

```yaml
- kind: InvokeAzureAgent
  id: call_analyst
  displayName: Call analyst agent
  agent:
    name: AnalystAgent
  conversationId: =System.ConversationId
  input:
    messages: =Local.userMessage
    arguments:
      topic: =Local.topic
  output:
    responseObject: Local.AnalystResult
    messages: Local.AnalystMessages
    autoSend: true
```

With external loop (continues until condition is met):

```yaml
- kind: InvokeAzureAgent
  id: support_agent
  agent:
    name: SupportAgent
  input:
    externalLoop:
      when: =Not(Local.IsResolved)
  output:
    responseObject: Local.SupportResult
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `agent.name` | Yes | Name of the registered agent |
| `conversationId` | No | Conversation context identifier |
| `input.messages` | No | Messages to send to the agent |
| `input.arguments` | No | Additional arguments for the agent |
| `input.externalLoop.when` | No | Condition to continue agent loop |
| `output.responseObject` | No | Path to store agent response |
| `output.messages` | No | Path to store conversation messages |
| `output.autoSend` | No | Automatically send response to user |

### Tool and HTTP Actions

#### InvokeFunctionTool

Invokes a function tool directly from the workflow without going through an AI agent.

```yaml
- kind: InvokeFunctionTool
  id: invoke_get_data
  displayName: Get data from function
  functionName: GetUserData
  conversationId: =System.ConversationId
  requireApproval: true
  arguments:
    userId: =Local.userId
  output:
    autoSend: true
    result: Local.UserData
    messages: Local.FunctionMessages
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `functionName` | Yes | Name of the function to invoke |
| `conversationId` | No | Conversation context identifier |
| `requireApproval` | No | Whether to require user approval before execution |
| `arguments` | No | Arguments to pass to the function |
| `output.result` | No | Path to store function result |
| `output.messages` | No | Path to store function messages |
| `output.autoSend` | No | Automatically send result to user |

**C# Setup for InvokeFunctionTool:**

Functions must be registered with the `WorkflowRunner` or handled via external input:

```csharp
// Define functions that can be invoked
AIFunction[] functions = [
    AIFunctionFactory.Create(myPlugin.GetUserData),
    AIFunctionFactory.Create(myPlugin.ProcessOrder),
];

// Create workflow runner with functions
WorkflowRunner runner = new(functions) { UseJsonCheckpoints = true };
await runner.ExecuteAsync(workflowFactory.CreateWorkflow, input);
```

#### InvokeMcpTool

Invokes a tool on an MCP (Model Context Protocol) server.

```yaml
- kind: InvokeMcpTool
  id: invoke_docs_search
  displayName: Search documentation
  serverUrl: https://learn.microsoft.com/api/mcp
  serverLabel: microsoft_docs
  toolName: microsoft_docs_search
  conversationId: =System.ConversationId
  requireApproval: false
  headers:
    X-Custom-Header: custom-value
  arguments:
    query: =Local.SearchQuery
  output:
    autoSend: true
    result: Local.SearchResults
```


With connection name for hosted scenarios:

```yaml
- kind: InvokeMcpTool
  id: invoke_hosted_mcp
  serverUrl: https://mcp.ai.azure.com
  toolName: my_tool
  # Connection name is used in hosted scenarios to connect to a ProjectConnectionId in Foundry.
  # Note: This feature is not fully supported yet.
  connection:
    name: my-foundry-connection
  output:
    result: Local.ToolResult
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `serverUrl` | Yes | URL of the MCP server |
| `serverLabel` | No | Human-readable label for the server |
| `toolName` | Yes | Name of the tool to invoke |
| `conversationId` | No | Conversation context identifier |
| `requireApproval` | No | Whether to require user approval |
| `arguments` | No | Arguments to pass to the tool |
| `headers` | No | Custom HTTP headers for the request |
| `connection.name` | No | Named connection for hosted scenarios (connects to ProjectConnectionId in Foundry; not fully supported yet) |
| `output.result` | No | Path to store tool result |
| `output.messages` | No | Path to store result messages |
| `output.autoSend` | No | Automatically send result to user |

**C# Setup for InvokeMcpTool:**

Configure the `McpToolHandler` in your workflow factory:

```csharp
using Azure.Core;
using Azure.Identity;
using Microsoft.Agents.AI.Workflows.Declarative;

// Create MCP tool handler with authentication callback
DefaultAzureCredential credential = new();
DefaultMcpToolHandler mcpToolHandler = new(
    httpClientProvider: async (serverUrl, cancellationToken) =>
    {
        if (serverUrl.StartsWith("https://mcp.ai.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            // Acquire token for Azure MCP server
            AccessToken token = await credential.GetTokenAsync(
                new TokenRequestContext(["https://mcp.ai.azure.com/.default"]),
                cancellationToken);

            HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
            return httpClient;
        }

        // Return null for servers that don't require authentication
        return null;
    });

// Configure workflow factory with MCP handler
WorkflowFactory workflowFactory = new("workflow.yaml", foundryEndpoint)
{
    McpToolHandler = mcpToolHandler
};
```

#### HttpRequestAction

Sends an HTTP request through the configured `IHttpRequestHandler`. Successful JSON responses are parsed before assignment; non-2xx responses fail the action.

```yaml
- kind: HttpRequestAction
  id: fetch_repo_info
  method: GET
  url: "https://api.github.com/repos/Microsoft/agent-framework"
  headers:
    Accept: application/vnd.github+json
    User-Agent: agent-framework
  queryParameters:
    per_page: 10
  response: Local.RepoInfo
  responseHeaders: Local.RepoHeaders
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `url` | Yes | Absolute request URL |
| `method` | No | HTTP method; defaults to `GET` |
| `headers` | No | Request headers |
| `queryParameters` | No | Query parameters appended to the URL |
| `body` | No | Request body; use `kind: json`, `raw`, or `none` |
| `requestTimeoutInMilliseconds` | No | Per-request timeout |
| `conversationId` | No | Adds a successful response body to the conversation |
| `response` | No | Path to store the parsed response body |
| `responseHeaders` | No | Path to store response headers |

**C# Setup for HttpRequestAction:**

Set `HttpRequestHandler` when building the workflow. Use a custom handler when you need retries, or URL allowlisting.

```csharp
DeclarativeWorkflowOptions options = new(agentProvider)
{
    HttpRequestHandler = new DefaultHttpRequestHandler(),
};

Workflow workflow = DeclarativeWorkflowBuilder.Build<string>("workflow.yaml", options);
```

### Human-in-the-Loop Actions

#### Question

Asks the user a question and stores the response.

```yaml
- kind: Question
  id: ask_name
  displayName: Ask for user name
  question:
    text: "What is your name?"
  variable: Local.userName
  default: "Guest"
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `question.text` | Yes | The question to ask |
| `variable` | Yes | Path to store the response |
| `default` | No | Default value if no response |

#### RequestExternalInput

Requests input from an external system or process.

```yaml
- kind: RequestExternalInput
  id: request_approval
  displayName: Request manager approval
  prompt:
    text: "Please provide approval for this request."
  variable: Local.approvalResult
  default: "pending"
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `prompt.text` | Yes | Description of required input |
| `variable` | Yes | Path to store the input |
| `default` | No | Default value |

### Workflow Control Actions

#### EndWorkflow

Terminates the workflow execution.

```yaml
- kind: EndWorkflow
  id: finish
  displayName: End workflow
```

#### EndConversation

Ends the current conversation.

```yaml
- kind: EndConversation
  id: end_chat
  displayName: End conversation
```

#### CreateConversation

Creates a new conversation context.

```yaml
- kind: CreateConversation
  id: create_new_conv
  displayName: Create new conversation
  conversationId: Local.NewConversationId
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `conversationId` | Yes | Path to store the new conversation ID |

### Conversation Actions (C# only)

#### AddConversationMessage

Adds a message to a conversation thread.

```yaml
- kind: AddConversationMessage
  id: add_system_message
  displayName: Add system context
  conversationId: =System.ConversationId
  message:
    role: system
    content: =Local.contextInfo
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `conversationId` | Yes | Target conversation identifier |
| `message` | Yes | Message to add |
| `message.role` | Yes | Message role (system, user, assistant) |
| `message.content` | Yes | Message content |

#### CopyConversationMessages

Copies messages from one conversation to another.

```yaml
- kind: CopyConversationMessages
  id: copy_context
  displayName: Copy conversation context
  sourceConversationId: =Local.SourceConversation
  targetConversationId: =System.ConversationId
  limit: 10
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `sourceConversationId` | Yes | Source conversation identifier |
| `targetConversationId` | Yes | Target conversation identifier |
| `limit` | No | Maximum number of messages to copy |

#### RetrieveConversationMessage

Retrieves a specific message from a conversation.

```yaml
- kind: RetrieveConversationMessage
  id: get_message
  displayName: Get specific message
  conversationId: =System.ConversationId
  messageId: =Local.targetMessageId
  variable: Local.retrievedMessage
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `conversationId` | Yes | Conversation identifier |
| `messageId` | Yes | Message identifier to retrieve |
| `variable` | Yes | Path to store the retrieved message |

#### RetrieveConversationMessages

Retrieves multiple messages from a conversation.

```yaml
- kind: RetrieveConversationMessages
  id: get_history
  displayName: Get conversation history
  conversationId: =System.ConversationId
  limit: 20
  newestFirst: true
  variable: Local.conversationHistory
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `conversationId` | Yes | Conversation identifier |
| `limit` | No | Maximum messages to retrieve (default: 20) |
| `newestFirst` | No | Return in descending order |
| `after` | No | Cursor for pagination |
| `before` | No | Cursor for pagination |
| `variable` | Yes | Path to store retrieved messages |

### Actions Quick Reference

| Action | Category | C# | Python | Description |
|--------|----------|-----|--------|-------------|
| `SetVariable` | Variable | ✅ | ✅ | Set a single variable |
| `SetMultipleVariables` | Variable | ✅ | ✅ | Set multiple variables |
| `SetTextVariable` | Variable | ✅ | ✅ | Set a text variable |
| `ResetVariable` | Variable | ✅ | ✅ | Clear a variable |
| `ClearAllVariables` | Variable | ✅ | ✅ | Clear all variables |
| `ParseValue` | Variable | ✅ | ✅ | Parse/transform data |
| `EditTableV2` | Variable | ✅ | ✅ | Modify table data |
| `If` | Control Flow | ✅ | ✅ | Conditional branching |
| `ConditionGroup` | Control Flow | ✅ | ✅ | Multi-branch switch |
| `Foreach` | Control Flow | ✅ | ✅ | Iterate over collection |
| `BreakLoop` | Control Flow | ✅ | ✅ | Exit current loop |
| `ContinueLoop` | Control Flow | ✅ | ✅ | Skip to next iteration |
| `GotoAction` | Control Flow | ✅ | ✅ | Jump to action by ID |
| `SendActivity` | Output | ✅ | ✅ | Send message to user |
| `InvokeAzureAgent` | Agent | ✅ | ✅ | Call Azure AI agent |
| `InvokeFunctionTool` | Tool | ✅ | ✅ | Invoke function directly |
| `InvokeMcpTool` | Tool | ✅ | ✅ | Invoke MCP server tool |
| `HttpRequestAction` | HTTP | ✅ | ✅ | Call HTTP endpoint |
| `Question` | Human-in-the-Loop | ✅ | ✅ | Ask user a question |
| `RequestExternalInput` | Human-in-the-Loop | ✅ | ✅ | Request external input |
| `EndWorkflow` | Workflow Control | ✅ | ✅ | Terminate workflow |
| `EndConversation` | Workflow Control | ✅ | ✅ | End conversation |
| `CreateConversation` | Workflow Control | ✅ | ✅ | Create new conversation |
| `AddConversationMessage` | Conversation | ✅ | ❌ | Add message to thread |
| `CopyConversationMessages` | Conversation | ✅ | ❌ | Copy messages |
| `RetrieveConversationMessage` | Conversation | ✅ | ❌ | Get single message |
| `RetrieveConversationMessages` | Conversation | ✅ | ❌ | Get multiple messages |

## Advanced Patterns

### Multi-Agent Orchestration

#### Sequential Agent Pipeline

Pass work through multiple agents in sequence.

```yaml
#
# Sequential agent pipeline for content creation
#
kind: Workflow
trigger:

  kind: OnConversationStart
  id: content_workflow
  actions:

    # First agent: Research
    - kind: InvokeAzureAgent
      id: invoke_researcher
      displayName: Research phase
      conversationId: =System.ConversationId
      agent:
        name: ResearcherAgent

    # Second agent: Write draft
    - kind: InvokeAzureAgent
      id: invoke_writer
      displayName: Writing phase
      conversationId: =System.ConversationId
      agent:
        name: WriterAgent

    # Third agent: Edit
    - kind: InvokeAzureAgent
      id: invoke_editor
      displayName: Editing phase
      conversationId: =System.ConversationId
      agent:
        name: EditorAgent
```

**C# Setup:**

```csharp
using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Azure.Identity;

// Ensure agents exist in Foundry
AIProjectClient aiProjectClient = new(foundryEndpoint, new DefaultAzureCredential());

await aiProjectClient.CreateAgentAsync(
    agentName: "ResearcherAgent",
    agentDefinition: new DeclarativeAgentDefinition(modelName)
    {
        Instructions = "You are a research specialist..."
    },
    agentDescription: "Research agent for content pipeline");

// Create and run workflow
WorkflowFactory workflowFactory = new("content-pipeline.yaml", foundryEndpoint);
WorkflowRunner runner = new();
await runner.ExecuteAsync(workflowFactory.CreateWorkflow, "Create content about AI");
```

#### Conditional Agent Routing

Route requests to different agents based on conditions.

```yaml
#
# Route to specialized support agents based on category
#
kind: Workflow
trigger:

  kind: OnConversationStart
  id: support_router
  actions:

    # Capture category from user input or set via another action
    - kind: SetVariable
      id: set_category
      variable: Local.category
      value: =System.LastMessage.Text

    - kind: ConditionGroup
      id: route_request
      displayName: Route to appropriate agent
      conditions:
        - condition: =Local.category = "billing"
          id: billing_route
          actions:
            - kind: InvokeAzureAgent
              id: billing_agent
              agent:
                name: BillingAgent
              conversationId: =System.ConversationId
        - condition: =Local.category = "technical"
          id: technical_route
          actions:
            - kind: InvokeAzureAgent
              id: technical_agent
              agent:
                name: TechnicalAgent
              conversationId: =System.ConversationId
      elseActions:
        - kind: InvokeAzureAgent
          id: general_agent
          agent:
            name: GeneralAgent
          conversationId: =System.ConversationId
```

### Tool Integration Patterns

#### Pre-fetching Data with InvokeFunctionTool

Fetch data before calling an agent:

```yaml
#
# Pre-fetch menu data before agent interaction
#
kind: Workflow
trigger:

  kind: OnConversationStart
  id: menu_workflow
  actions:
    # Pre-fetch today's specials
    - kind: InvokeFunctionTool
      id: get_specials
      functionName: GetSpecials
      requireApproval: true
      output:
        autoSend: true
        result: Local.Specials

    # Agent uses pre-fetched data
    - kind: InvokeAzureAgent
      id: menu_agent
      conversationId: =System.ConversationId
      agent:
        name: MenuAgent
      input:
        messages: =UserMessage("Describe today's specials: " & Local.Specials)
```

#### MCP Tool Integration

Call external server using MCP:

```yaml
#
# Search documentation using MCP
#
kind: Workflow
trigger:

  kind: OnConversationStart
  id: docs_search
  actions:

    - kind: SetVariable
      variable: Local.SearchQuery
      value: =System.LastMessage.Text

    # Search Microsoft Learn
    - kind: InvokeMcpTool
      id: search_docs
      serverUrl: https://learn.microsoft.com/api/mcp
      toolName: microsoft_docs_search
      conversationId: =System.ConversationId
      arguments:
        query: =Local.SearchQuery
      output:
        result: Local.SearchResults
        autoSend: true

    # Summarize results with agent
    - kind: InvokeAzureAgent
      id: summarize
      agent:
        name: SummaryAgent
      conversationId: =System.ConversationId
      input:
        messages: =UserMessage("Summarize these search results")
```

::: zone-end

::: zone pivot="programming-language-python"

## Prerequisites

Before you begin, ensure you have:

- Python 3.10 - 3.13 (Python 3.14 is not yet supported due to PowerFx compatibility)
- The Agent Framework declarative package installed:

```bash
pip install agent-framework-declarative --pre
```

This package pulls in the underlying `agent-framework-core` automatically.

- Basic familiarity with YAML syntax
- Understanding of [workflow concepts](../concepts/workflows/index.md)

## Your First Declarative Workflow

Let's create a simple workflow that greets a user by name.

### Step 1: Create the YAML File

Create a file named `greeting-workflow.yaml`:

```yaml
name: greeting-workflow
description: A simple workflow that greets the user

inputs:
  name:
    type: string
    description: The name of the person to greet

actions:
  # Set a greeting prefix
  - kind: SetVariable
    id: set_greeting
    displayName: Set greeting prefix
    variable: Local.greeting
    value: Hello

  # Build the full message using an expression
  - kind: SetVariable
    id: build_message
    displayName: Build greeting message
    variable: Local.message
    value: =Concat(Local.greeting, ", ", Workflow.Inputs.name, "!")

  # Send the greeting to the user
  - kind: SendActivity
    id: send_greeting
    displayName: Send greeting to user
    activity:
      text: =Local.message

  # Store the result in outputs
  - kind: SetVariable
    id: set_output
    displayName: Store result in outputs
    variable: Workflow.Outputs.greeting
    value: =Local.message
```

### Step 2: Load and Run the Workflow

Create a Python file to execute the workflow:

```python
import asyncio
from pathlib import Path

from agent_framework.declarative import WorkflowFactory


async def main() -> None:
    """Run the greeting workflow."""
    # Create a workflow factory
    factory = WorkflowFactory()

    # Load the workflow from YAML
    workflow_path = Path(__file__).parent / "greeting-workflow.yaml"
    workflow = factory.create_workflow_from_yaml_path(workflow_path)

    print(f"Loaded workflow: {workflow.name}")
    print("-" * 40)

    # Run with a name input
    result = await workflow.run({"name": "Alice"})
    for output in result.get_outputs():
        print(f"Output: {output}")
    for output in result.get_intermediate_outputs():
        print(f"Intermediate: {output}")


if __name__ == "__main__":
    asyncio.run(main())
```

### Expected Output

```
Loaded workflow: greeting-workflow
----------------------------------------
Output: Hello, Alice!
```

## Core Concepts

### Variable Namespaces

Declarative workflows use namespaced variables to organize state:

| Namespace | Description | Example |
|-----------|-------------|---------|
| `Local.*` | Variables local to the workflow | `Local.message` |
| `Workflow.Inputs.*` | Input parameters | `Workflow.Inputs.name` |
| `Workflow.Outputs.*` | Output values | `Workflow.Outputs.result` |
| `System.*` | System-provided values | `System.ConversationId` |

### Expression Language

Values prefixed with `=` are evaluated as expressions:

```yaml
# Literal value (no evaluation)
value: Hello

# Expression (evaluated at runtime)
value: =Concat("Hello, ", Workflow.Inputs.name)
```

Common functions include:
- `Concat(str1, str2, ...)` - Concatenate strings
- `If(condition, trueValue, falseValue)` - Conditional expression
- `IsBlank(value)` - Check if value is empty

### Action Types

Declarative workflows support various action types:

| Category | Actions |
|----------|---------|
| Variable Management | `SetVariable`, `SetMultipleVariables`, `ResetVariable` |
| Control Flow | `If`, `ConditionGroup`, `Foreach`, `BreakLoop`, `ContinueLoop`, `GotoAction` |
| Output | `SendActivity` |
| Agent Invocation | `InvokeAzureAgent` |
| Tool Invocation | `InvokeFunctionTool`, `InvokeMcpTool` |
| HTTP | `HttpRequestAction` |
| Human-in-the-Loop | `Question`, `RequestExternalInput` |
| Workflow Control | `EndWorkflow`, `EndConversation`, `CreateConversation` |

## Actions Reference

Actions are the building blocks of declarative workflows. Each action performs a specific operation, and actions are executed sequentially in the order they appear in the YAML file.

### Action Structure

All actions share common properties:

```yaml
- kind: ActionType      # Required: The type of action
  id: unique_id         # Optional: Unique identifier for referencing
  displayName: Name     # Optional: Human-readable name for logging
  # Action-specific properties...
```

### Variable Management Actions

#### SetVariable

Sets a variable to a specified value.

```yaml
- kind: SetVariable
  id: set_greeting
  displayName: Set greeting message
  variable: Local.greeting
  value: Hello World
```

With an expression:

```yaml
- kind: SetVariable
  variable: Local.fullName
  value: =Concat(Workflow.Inputs.firstName, " ", Workflow.Inputs.lastName)
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `variable` | Yes | Variable path (e.g., `Local.name`, `Workflow.Outputs.result`) |
| `value` | Yes | Value to set (literal or expression) |

> [!NOTE]
> Python also supports the `SetValue` action kind, which uses `path` instead of `variable` for the target property. Both `SetVariable` (with `variable`) and `SetValue` (with `path`) achieve the same result. For example:
>
> ```yaml
> - kind: SetValue
>   id: set_greeting
>   path: Local.greeting
>   value: Hello World
> ```

#### SetMultipleVariables

Sets multiple variables in a single action.

```yaml
- kind: SetMultipleVariables
  id: initialize_vars
  displayName: Initialize variables
  variables:
    Local.counter: 0
    Local.status: pending
    Local.message: =Concat("Processing order ", Workflow.Inputs.orderId)
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `variables` | Yes | Map of variable paths to values |

#### ResetVariable

Clears a variable's value.

```yaml
- kind: ResetVariable
  id: clear_counter
  variable: Local.counter
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `variable` | Yes | Variable path to reset |

### Control Flow Actions

#### If

Executes actions conditionally based on a condition.

```yaml
- kind: If
  id: check_age
  displayName: Check user age
  condition: =Workflow.Inputs.age >= 18
  then:
    - kind: SendActivity
      activity:
        text: "Welcome, adult user!"
  else:
    - kind: SendActivity
      activity:
        text: "Welcome, young user!"
```

Nested conditions:

```yaml
- kind: If
  condition: =Workflow.Inputs.role = "admin"
  then:
    - kind: SendActivity
      activity:
        text: "Admin access granted"
  else:
    - kind: If
      condition: =Workflow.Inputs.role = "user"
      then:
        - kind: SendActivity
          activity:
            text: "User access granted"
      else:
        - kind: SendActivity
          activity:
            text: "Access denied"
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `condition` | Yes | Expression that evaluates to true/false |
| `then` | Yes | Actions to execute if condition is true |
| `else` | No | Actions to execute if condition is false |

#### ConditionGroup

Evaluates multiple conditions like a switch/case statement.

```yaml
- kind: ConditionGroup
  id: route_by_category
  displayName: Route based on category
  conditions:
    - condition: =Workflow.Inputs.category = "electronics"
      id: electronics_branch
      actions:
        - kind: SetVariable
          variable: Local.department
          value: Electronics Team
    - condition: =Workflow.Inputs.category = "clothing"
      id: clothing_branch
      actions:
        - kind: SetVariable
          variable: Local.department
          value: Clothing Team
    - condition: =Workflow.Inputs.category = "food"
      id: food_branch
      actions:
        - kind: SetVariable
          variable: Local.department
          value: Food Team
  elseActions:
    - kind: SetVariable
      variable: Local.department
      value: General Support
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `conditions` | Yes | List of condition/actions pairs (first match wins) |
| `elseActions` | No | Actions if no condition matches |

#### Foreach

Iterates over a collection.

```yaml
- kind: Foreach
  id: process_items
  displayName: Process each item
  source: =Workflow.Inputs.items
  itemName: item
  indexName: index
  actions:
    - kind: SendActivity
      activity:
        text: =Concat("Processing item ", index, ": ", item)
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `source` | Yes | Expression returning a collection |
| `itemName` | No | Variable name for current item (default: `item`) |
| `indexName` | No | Variable name for current index (default: `index`) |
| `actions` | Yes | Actions to execute for each item |

#### BreakLoop

Exits the current loop immediately.

```yaml
- kind: Foreach
  source: =Workflow.Inputs.items
  actions:
    - kind: If
      condition: =item = "stop"
      then:
        - kind: BreakLoop
    - kind: SendActivity
      activity:
        text: =item
```

#### ContinueLoop

Skips to the next iteration of the loop.

```yaml
- kind: Foreach
  source: =Workflow.Inputs.numbers
  actions:
    - kind: If
      condition: =item < 0
      then:
        - kind: ContinueLoop
    - kind: SendActivity
      activity:
        text: =Concat("Positive number: ", item)
```

#### GotoAction

Jumps to a specific action by ID.

```yaml
- kind: SetVariable
  id: start_label
  variable: Local.attempts
  value: =Local.attempts + 1

- kind: SendActivity
  activity:
    text: =Concat("Attempt ", Local.attempts)

- kind: If
  condition: =And(Local.attempts < 3, Not(Local.success))
  then:
    - kind: GotoAction
      actionId: start_label
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `actionId` | Yes | ID of the action to jump to |

### Output Actions

#### SendActivity

Sends a message to the user.

```yaml
- kind: SendActivity
  id: send_welcome
  displayName: Send welcome message
  activity:
    text: "Welcome to our service!"
```

With an expression:

```yaml
- kind: SendActivity
  activity:
    text: =Concat("Hello, ", Workflow.Inputs.name, "! How can I help you today?")
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `activity` | Yes | The activity to send |
| `activity.text` | Yes | Message text (literal or expression) |

### Agent Invocation Actions

#### InvokeAzureAgent

Invokes an Azure AI agent.

Basic invocation:

```yaml
- kind: InvokeAzureAgent
  id: call_assistant
  displayName: Call assistant agent
  agent:
    name: AssistantAgent
  conversationId: =System.ConversationId
```

With input and output configuration:

```yaml
- kind: InvokeAzureAgent
  id: call_analyst
  displayName: Call analyst agent
  agent:
    name: AnalystAgent
  conversationId: =System.ConversationId
  input:
    messages: =Local.userMessage
    arguments:
      topic: =Workflow.Inputs.topic
  output:
    responseObject: Local.AnalystResult
    messages: Local.AnalystMessages
    autoSend: true
```

With external loop (continues until condition is met):

```yaml
- kind: InvokeAzureAgent
  id: support_agent
  agent:
    name: SupportAgent
  input:
    externalLoop:
      when: =Not(Local.IsResolved)
  output:
    responseObject: Local.SupportResult
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `agent.name` | Yes | Name of the registered agent |
| `conversationId` | No | Conversation context identifier |
| `input.messages` | No | Messages to send to the agent |
| `input.arguments` | No | Additional arguments for the agent |
| `input.externalLoop.when` | No | Condition to continue agent loop |
| `output.responseObject` | No | Path to store agent response |
| `output.messages` | No | Path to store conversation messages |
| `output.autoSend` | No | Automatically send response to user |

### Tool and HTTP Actions

#### InvokeFunctionTool

Invokes a registered Python function directly from the workflow without going through an AI agent.

```yaml
- kind: InvokeFunctionTool
  id: invoke_weather
  displayName: Get weather data
  functionName: get_weather
  arguments:
    location: =Local.location
    unit: =Local.unit
  output:
    result: Local.weatherInfo
    messages: Local.weatherToolCallItems
    autoSend: true
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `functionName` | Yes | Name of the registered function to invoke |
| `arguments` | No | Arguments to pass to the function |
| `output.result` | No | Path to store the function result |
| `output.messages` | No | Path to store function messages |
| `output.autoSend` | No | Automatically send result to user |

**Python setup for InvokeFunctionTool:**

Functions must be registered with the `WorkflowFactory` using `register_tool`:

```python
from agent_framework.declarative import WorkflowFactory

# Define your functions
def get_weather(location: str, unit: str = "F") -> dict:
    """Get weather information for a location."""
    # Your implementation here
    return {"location": location, "temp": 72, "unit": unit}

def format_message(template: str, data: dict) -> str:
    """Format a message template with data."""
    return template.format(**data)

# Register functions with the factory
factory = (
    WorkflowFactory()
    .register_tool("get_weather", get_weather)
    .register_tool("format_message", format_message)
)

# Load and run the workflow
workflow = factory.create_workflow_from_yaml_path("workflow.yaml")
result = await workflow.run({"location": "Seattle", "unit": "F"})
```

#### InvokeMcpTool

Invokes a tool on an MCP server through the configured `MCPToolHandler`.

```yaml
- kind: InvokeMcpTool
  id: search_docs
  serverUrl: https://learn.microsoft.com/api/mcp
  serverLabel: microsoft_docs
  toolName: microsoft_docs_search
  arguments:
    query: =Local.searchQuery
  output:
    result: Local.searchResults
    messages: Local.toolMessage
    autoSend: true
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `serverUrl` | Yes | MCP server URL |
| `toolName` | Yes | Tool name on the MCP server |
| `serverLabel` | No | Human-readable server label |
| `arguments` | No | Arguments passed to the tool |
| `headers` | No | Request headers; empty values are skipped |
| `connection.name` | No | Named connection for custom handlers |
| `conversationId` | No | Adds successful tool output to the conversation |
| `requireApproval` | No | Requests approval before invoking the tool |
| `output.result` | No | Path to store parsed tool output |
| `output.messages` | No | Path to store the tool message |
| `output.autoSend` | No | Emits tool output to the workflow result; defaults to `true` |

**Python setup for InvokeMcpTool:**

Pass an MCP tool handler to `WorkflowFactory`. Use a custom handler when you need authentication, managed connections, or URL allowlisting.

```python
from agent_framework.declarative import DefaultMCPToolHandler, WorkflowFactory

factory = WorkflowFactory(mcp_tool_handler=DefaultMCPToolHandler())
workflow = factory.create_workflow_from_yaml_path("workflow.yaml")
```

#### HttpRequestAction

Sends an HTTP request through the configured `HttpRequestHandler`. Successful JSON responses are parsed before assignment; non-2xx responses fail the action.

```yaml
- kind: HttpRequestAction
  id: fetch_repo_info
  method: GET
  url: =Concat("https://api.github.com/repos/", Local.repoName)
  headers:
    Accept: application/vnd.github+json
    User-Agent: agent-framework
  queryParameters:
    per_page: 10
  response: Local.repoInfo
  responseHeaders: Local.repoHeaders
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `url` | Yes | Absolute request URL |
| `method` | No | HTTP method; defaults to `GET` |
| `headers` | No | Request headers |
| `queryParameters` | No | Query parameters appended to the URL |
| `body` | No | Request body; use `kind: json`, `raw`, or `none` |
| `requestTimeoutInMilliseconds` | No | Per-request timeout |
| `connection.name` | No | Named connection for custom handlers |
| `conversationId` | No | Adds a successful response body to the conversation |
| `response` | No | Path to store the parsed response body |
| `responseHeaders` | No | Path to store response headers |

**Python setup for HttpRequestAction:**

Pass an HTTP request handler to `WorkflowFactory`. Use a custom handler when you need authentication, retries, or URL allowlisting.

```python
from agent_framework.declarative import DefaultHttpRequestHandler, WorkflowFactory

factory = WorkflowFactory(http_request_handler=DefaultHttpRequestHandler())
workflow = factory.create_workflow_from_yaml_path("workflow.yaml")
```

### Human-in-the-Loop Actions

#### Question

Asks the user a question and stores the response.

```yaml
- kind: Question
  id: ask_name
  displayName: Ask for user name
  question:
    text: "What is your name?"
  variable: Local.userName
  default: "Guest"
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `question.text` | Yes | The question to ask |
| `variable` | Yes | Path to store the response |
| `default` | No | Default value if no response |

#### RequestExternalInput

Requests input from an external system or process.

```yaml
- kind: RequestExternalInput
  id: request_approval
  displayName: Request manager approval
  prompt:
    text: "Please provide approval for this request."
  variable: Local.approvalResult
  default: "pending"
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `prompt.text` | Yes | Description of required input |
| `variable` | Yes | Path to store the input |
| `default` | No | Default value |

### Workflow Control Actions

#### EndWorkflow

Terminates the workflow execution.

```yaml
- kind: EndWorkflow
  id: finish
  displayName: End workflow
```

#### EndConversation

Ends the current conversation.

```yaml
- kind: EndConversation
  id: end_chat
  displayName: End conversation
```

#### CreateConversation

Creates a new conversation context.

```yaml
- kind: CreateConversation
  id: create_new_conv
  displayName: Create new conversation
  conversationId: Local.NewConversationId
```

**Properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `conversationId` | Yes | Path to store the new conversation ID |

### Actions Quick Reference

| Action | Category | Description |
|--------|----------|-------------|
| `SetVariable` | Variable | Set a single variable |
| `SetMultipleVariables` | Variable | Set multiple variables |
| `ResetVariable` | Variable | Clear a variable |
| `If` | Control Flow | Conditional branching |
| `ConditionGroup` | Control Flow | Multi-branch switch |
| `Foreach` | Control Flow | Iterate over collection |
| `BreakLoop` | Control Flow | Exit current loop |
| `ContinueLoop` | Control Flow | Skip to next iteration |
| `GotoAction` | Control Flow | Jump to action by ID |
| `SendActivity` | Output | Send message to user |
| `InvokeAzureAgent` | Agent | Call Azure AI agent |
| `InvokeFunctionTool` | Tool | Invoke registered function |
| `InvokeMcpTool` | Tool | Invoke MCP server tool |
| `HttpRequestAction` | HTTP | Call HTTP endpoint |
| `Question` | Human-in-the-Loop | Ask user a question |
| `RequestExternalInput` | Human-in-the-Loop | Request external input |
| `EndWorkflow` | Workflow Control | Terminate workflow |
| `EndConversation` | Workflow Control | End conversation |
| `CreateConversation` | Workflow Control | Create new conversation |

## Expression Syntax

Declarative workflows use a PowerFx-like expression language to manage state and compute dynamic values. Values prefixed with `=` are evaluated as expressions at runtime.

### Variable Namespace Details

| Namespace | Description | Access |
|-----------|-------------|--------|
| `Local.*` | Workflow-local variables | Read/Write |
| `Workflow.Inputs.*` | Input parameters passed to the workflow | Read-only |
| `Workflow.Outputs.*` | Values returned from the workflow | Read/Write |
| `System.*` | System-provided values | Read-only |
| `Agent.*` | Results from agent invocations | Read-only |

#### System Variables

| Variable | Description |
|----------|-------------|
| `System.ConversationId` | Current conversation identifier |
| `System.LastMessage` | The most recent message |
| `System.Timestamp` | Current timestamp |

#### Agent Variables

After invoking an agent, access response data through the output variable:

```yaml
actions:
  - kind: InvokeAzureAgent
    id: call_assistant
    agent:
      name: MyAgent
    output:
      responseObject: Local.AgentResult

  # Access agent response
  - kind: SendActivity
    activity:
      text: =Local.AgentResult.text
```

### Literal vs. Expression Values

```yaml
# Literal string (stored as-is)
value: Hello World

# Expression (evaluated at runtime)
value: =Concat("Hello ", Workflow.Inputs.name)

# Literal number
value: 42

# Expression returning a number
value: =Workflow.Inputs.quantity * 2
```

### String Operations

#### Concat

Concatenate multiple strings:

```yaml
value: =Concat("Hello, ", Workflow.Inputs.name, "!")
# Result: "Hello, Alice!" (if Workflow.Inputs.name is "Alice")

value: =Concat(Local.firstName, " ", Local.lastName)
# Result: "John Doe" (if firstName is "John" and lastName is "Doe")
```

#### IsBlank

Check if a value is empty or undefined:

```yaml
condition: =IsBlank(Workflow.Inputs.optionalParam)
# Returns true if the parameter is not provided

value: =If(IsBlank(Workflow.Inputs.name), "Guest", Workflow.Inputs.name)
# Returns "Guest" if name is blank, otherwise returns the name
```

### Conditional Expressions

#### If Function

Return different values based on a condition:

```yaml
value: =If(Workflow.Inputs.age < 18, "minor", "adult")

value: =If(Local.count > 0, "Items found", "No items")

# Nested conditions
value: =If(Workflow.Inputs.role = "admin", "Full access", If(Workflow.Inputs.role = "user", "Limited access", "No access"))
```

### Comparison Operators

| Operator | Description | Example |
|----------|-------------|---------|
| `=` | Equal to | `=Workflow.Inputs.status = "active"` |
| `<>` | Not equal to | `=Workflow.Inputs.status <> "deleted"` |
| `<` | Less than | `=Workflow.Inputs.age < 18` |
| `>` | Greater than | `=Workflow.Inputs.count > 0` |
| `<=` | Less than or equal | `=Workflow.Inputs.score <= 100` |
| `>=` | Greater than or equal | `=Workflow.Inputs.quantity >= 1` |

### Boolean Functions

```yaml
# Or - returns true if any condition is true
condition: =Or(Workflow.Inputs.role = "admin", Workflow.Inputs.role = "moderator")

# And - returns true if all conditions are true
condition: =And(Workflow.Inputs.age >= 18, Workflow.Inputs.hasConsent)

# Not - negates a condition
condition: =Not(IsBlank(Workflow.Inputs.email))
```

### Mathematical Operations

```yaml
# Addition
value: =Workflow.Inputs.price + Workflow.Inputs.tax

# Subtraction
value: =Workflow.Inputs.total - Workflow.Inputs.discount

# Multiplication
value: =Workflow.Inputs.quantity * Workflow.Inputs.unitPrice

# Division
value: =Workflow.Inputs.total / Workflow.Inputs.count
```

### Practical Expression Examples

#### User Categorization

```yaml
name: categorize-user
inputs:
  age:
    type: integer
    description: User's age

actions:
  - kind: SetVariable
    variable: Local.age
    value: =Workflow.Inputs.age

  - kind: SetVariable
    variable: Local.category
    value: =If(Local.age < 13, "child", If(Local.age < 20, "teenager", If(Local.age < 65, "adult", "senior")))

  - kind: SendActivity
    activity:
      text: =Concat("You are categorized as: ", Local.category)

  - kind: SetVariable
    variable: Workflow.Outputs.category
    value: =Local.category
```

#### Conditional Greeting

```yaml
name: smart-greeting
inputs:
  name:
    type: string
    description: User's name (optional)
  timeOfDay:
    type: string
    description: morning, afternoon, or evening

actions:
  # Set the greeting based on time of day
  - kind: SetVariable
    variable: Local.timeGreeting
    value: =If(Workflow.Inputs.timeOfDay = "morning", "Good morning", If(Workflow.Inputs.timeOfDay = "afternoon", "Good afternoon", "Good evening"))

  # Handle optional name
  - kind: SetVariable
    variable: Local.userName
    value: =If(IsBlank(Workflow.Inputs.name), "friend", Workflow.Inputs.name)

  # Build the full greeting
  - kind: SetVariable
    variable: Local.fullGreeting
    value: =Concat(Local.timeGreeting, ", ", Local.userName, "!")

  - kind: SendActivity
    activity:
      text: =Local.fullGreeting
```

#### Input Validation

```yaml
name: validate-order
inputs:
  quantity:
    type: integer
    description: Number of items to order
  email:
    type: string
    description: Customer email

actions:
  # Check if inputs are valid
  - kind: SetVariable
    variable: Local.isValidQuantity
    value: =And(Workflow.Inputs.quantity > 0, Workflow.Inputs.quantity <= 100)

  - kind: SetVariable
    variable: Local.hasEmail
    value: =Not(IsBlank(Workflow.Inputs.email))

  - kind: SetVariable
    variable: Local.isValid
    value: =And(Local.isValidQuantity, Local.hasEmail)

  - kind: If
    condition: =Local.isValid
    then:
      - kind: SendActivity
        activity:
          text: "Order validated successfully!"
    else:
      - kind: SendActivity
        activity:
          text: =If(Not(Local.isValidQuantity), "Invalid quantity (must be 1-100)", "Email is required")
```

## Advanced Patterns

As your workflows grow in complexity, you'll need patterns that handle multi-step processes, agent coordination, and interactive scenarios.

### Multi-Agent Orchestration

#### Sequential Agent Pipeline

Pass work through multiple agents in sequence, where each agent builds on the previous agent's output.

**Use case**: Content creation pipelines where different specialists handle research, writing, and editing.

```yaml
name: content-pipeline
description: Sequential agent pipeline for content creation

kind: Workflow
trigger:
  kind: OnConversationStart
  id: content_workflow
  actions:
    # First agent: Research and analyze
    - kind: InvokeAzureAgent
      id: invoke_researcher
      displayName: Research phase
      conversationId: =System.ConversationId
      agent:
        name: ResearcherAgent

    # Second agent: Write draft based on research
    - kind: InvokeAzureAgent
      id: invoke_writer
      displayName: Writing phase
      conversationId: =System.ConversationId
      agent:
        name: WriterAgent

    # Third agent: Edit and polish
    - kind: InvokeAzureAgent
      id: invoke_editor
      displayName: Editing phase
      conversationId: =System.ConversationId
      agent:
        name: EditorAgent
```

**Python setup**:

```python
from agent_framework.declarative import WorkflowFactory

# Create factory and register agents
factory = WorkflowFactory()
factory.register_agent("ResearcherAgent", researcher_agent)
factory.register_agent("WriterAgent", writer_agent)
factory.register_agent("EditorAgent", editor_agent)

# Load and run
workflow = factory.create_workflow_from_yaml_path("content-pipeline.yaml")
result = await workflow.run({"topic": "AI in healthcare"})
```

#### Conditional Agent Routing

Route requests to different agents based on the input or intermediate results.

**Use case**: Support systems that route to specialized agents based on issue type.

```yaml
name: support-router
description: Route to specialized support agents

inputs:
  category:
    type: string
    description: Support category (billing, technical, general)

actions:
  - kind: ConditionGroup
    id: route_request
    displayName: Route to appropriate agent
    conditions:
      - condition: =Workflow.Inputs.category = "billing"
        id: billing_route
        actions:
          - kind: InvokeAzureAgent
            id: billing_agent
            agent:
              name: BillingAgent
            conversationId: =System.ConversationId
      - condition: =Workflow.Inputs.category = "technical"
        id: technical_route
        actions:
          - kind: InvokeAzureAgent
            id: technical_agent
            agent:
              name: TechnicalAgent
            conversationId: =System.ConversationId
    elseActions:
      - kind: InvokeAzureAgent
        id: general_agent
        agent:
          name: GeneralAgent
        conversationId: =System.ConversationId
```

#### Agent with External Loop

Continue agent interaction until a condition is met, such as the issue being resolved.

**Use case**: Support conversations that continue until the user's problem is solved.

```yaml
name: support-conversation
description: Continue support until resolved

actions:
  - kind: SetVariable
    variable: Local.IsResolved
    value: false

  - kind: InvokeAzureAgent
    id: support_agent
    displayName: Support agent with external loop
    agent:
      name: SupportAgent
    conversationId: =System.ConversationId
    input:
      externalLoop:
        when: =Not(Local.IsResolved)
    output:
      responseObject: Local.SupportResult

  - kind: SendActivity
    activity:
      text: "Thank you for contacting support. Your issue has been resolved."
```

### Loop Control Patterns

#### Iterative Agent Conversation

Create back-and-forth conversations between agents with controlled iteration.

**Use case**: Student-teacher scenarios, debate simulations, or iterative refinement.

```yaml
name: student-teacher
description: Iterative learning conversation between student and teacher

kind: Workflow
trigger:
  kind: OnConversationStart
  id: learning_session
  actions:
    # Initialize turn counter
    - kind: SetVariable
      id: init_counter
      variable: Local.TurnCount
      value: 0

    - kind: SendActivity
      id: start_message
      activity:
        text: =Concat("Starting session for: ", Workflow.Inputs.problem)

    # Student attempts solution (loop entry point)
    - kind: SendActivity
      id: student_label
      activity:
        text: "\n[Student]:"

    - kind: InvokeAzureAgent
      id: student_attempt
      conversationId: =System.ConversationId
      agent:
        name: StudentAgent

    # Teacher reviews
    - kind: SendActivity
      id: teacher_label
      activity:
        text: "\n[Teacher]:"

    - kind: InvokeAzureAgent
      id: teacher_review
      conversationId: =System.ConversationId
      agent:
        name: TeacherAgent
      output:
        messages: Local.TeacherResponse

    # Increment counter
    - kind: SetVariable
      id: increment
      variable: Local.TurnCount
      value: =Local.TurnCount + 1

    # Check completion conditions
    - kind: ConditionGroup
      id: check_completion
      conditions:
        # Success: Teacher congratulated student
        - condition: =Not(IsBlank(Find("congratulations", Local.TeacherResponse)))
          id: success_check
          actions:
            - kind: SendActivity
              activity:
                text: "Session complete - student succeeded!"
            - kind: SetVariable
              variable: Workflow.Outputs.result
              value: success
        # Continue: Under turn limit
        - condition: =Local.TurnCount < 4
          id: continue_check
          actions:
            - kind: GotoAction
              actionId: student_label
      elseActions:
        # Timeout: Reached turn limit
        - kind: SendActivity
          activity:
            text: "Session ended - turn limit reached."
        - kind: SetVariable
          variable: Workflow.Outputs.result
          value: timeout
```

#### Counter-Based Loops

Implement traditional counting loops using variables and GotoAction.

```yaml
name: counter-loop
description: Process items with a counter

actions:
  - kind: SetVariable
    variable: Local.counter
    value: 0

  - kind: SetVariable
    variable: Local.maxIterations
    value: 5

  # Loop start
  - kind: SetVariable
    id: loop_start
    variable: Local.counter
    value: =Local.counter + 1

  - kind: SendActivity
    activity:
      text: =Concat("Processing iteration ", Local.counter)

  # Your processing logic here
  - kind: SetVariable
    variable: Local.result
    value: =Concat("Result from iteration ", Local.counter)

  # Check if should continue
  - kind: If
    condition: =Local.counter < Local.maxIterations
    then:
      - kind: GotoAction
        actionId: loop_start
    else:
      - kind: SendActivity
        activity:
          text: "Loop complete!"
```

#### Early Exit with BreakLoop

Use BreakLoop to exit iterations early when a condition is met.

```yaml
name: search-workflow
description: Search through items and stop when found

actions:
  - kind: SetVariable
    variable: Local.found
    value: false

  - kind: Foreach
    source: =Workflow.Inputs.items
    itemName: currentItem
    actions:
      # Check if this is the item we're looking for
      - kind: If
        condition: =currentItem.id = Workflow.Inputs.targetId
        then:
          - kind: SetVariable
            variable: Local.found
            value: true
          - kind: SetVariable
            variable: Local.result
            value: =currentItem
          - kind: BreakLoop

      - kind: SendActivity
        activity:
          text: =Concat("Checked item: ", currentItem.name)

  - kind: If
    condition: =Local.found
    then:
      - kind: SendActivity
        activity:
          text: =Concat("Found: ", Local.result.name)
    else:
      - kind: SendActivity
        activity:
          text: "Item not found"
```

### Human-in-the-Loop Patterns

#### Interactive Survey

Collect multiple pieces of information from the user.

```yaml
name: customer-survey
description: Interactive customer feedback survey

actions:
  - kind: SendActivity
    activity:
      text: "Welcome to our customer feedback survey!"

  # Collect name
  - kind: Question
    id: ask_name
    question:
      text: "What is your name?"
    variable: Local.userName
    default: "Anonymous"

  - kind: SendActivity
    activity:
      text: =Concat("Nice to meet you, ", Local.userName, "!")

  # Collect rating
  - kind: Question
    id: ask_rating
    question:
      text: "How would you rate our service? (1-5)"
    variable: Local.rating
    default: "3"

  # Respond based on rating
  - kind: If
    condition: =Local.rating >= 4
    then:
      - kind: SendActivity
        activity:
          text: "Thank you for the positive feedback!"
    else:
      - kind: Question
        id: ask_improvement
        question:
          text: "What could we improve?"
        variable: Local.feedback

  # Collect additional feedback
  - kind: RequestExternalInput
    id: additional_comments
    prompt:
      text: "Any additional comments? (optional)"
    variable: Local.comments
    default: ""

  # Summary
  - kind: SendActivity
    activity:
      text: =Concat("Thank you, ", Local.userName, "! Your feedback has been recorded.")

  - kind: SetVariable
    variable: Workflow.Outputs.survey
    value:
      name: =Local.userName
      rating: =Local.rating
      feedback: =Local.feedback
      comments: =Local.comments
```

#### Approval Workflow

Request approval before proceeding with an action.

```yaml
name: approval-workflow
description: Request approval before processing

inputs:
  requestType:
    type: string
    description: Type of request
  amount:
    type: number
    description: Request amount

actions:
  - kind: SendActivity
    activity:
      text: =Concat("Processing ", Workflow.Inputs.requestType, " request for $", Workflow.Inputs.amount)

  # Check if approval is needed
  - kind: If
    condition: =Workflow.Inputs.amount > 1000
    then:
      - kind: SendActivity
        activity:
          text: "This request requires manager approval."

      - kind: Question
        id: get_approval
        question:
          text: =Concat("Do you approve this ", Workflow.Inputs.requestType, " request for $", Workflow.Inputs.amount, "? (yes/no)")
        variable: Local.approved

      - kind: If
        condition: =Local.approved = "yes"
        then:
          - kind: SendActivity
            activity:
              text: "Request approved. Processing..."
          - kind: SetVariable
            variable: Workflow.Outputs.status
            value: approved
        else:
          - kind: SendActivity
            activity:
              text: "Request denied."
          - kind: SetVariable
            variable: Workflow.Outputs.status
            value: denied
    else:
      - kind: SendActivity
        activity:
          text: "Request auto-approved (under threshold)."
      - kind: SetVariable
        variable: Workflow.Outputs.status
        value: auto_approved
```

### Complex Orchestration

#### Support Ticket Workflow

A comprehensive example combining multiple patterns: agent routing, conditional logic, and conversation management.

```yaml
name: support-ticket-workflow
description: Complete support ticket handling with escalation

kind: Workflow
trigger:
  kind: OnConversationStart
  id: support_workflow
  actions:
    # Initial self-service agent
    - kind: InvokeAzureAgent
      id: self_service
      displayName: Self-service agent
      agent:
        name: SelfServiceAgent
      conversationId: =System.ConversationId
      input:
        externalLoop:
          when: =Not(Local.ServiceResult.IsResolved)
      output:
        responseObject: Local.ServiceResult

    # Check if resolved by self-service
    - kind: If
      condition: =Local.ServiceResult.IsResolved
      then:
        - kind: SendActivity
          activity:
            text: "Issue resolved through self-service."
        - kind: SetVariable
          variable: Workflow.Outputs.resolution
          value: self_service
        - kind: EndWorkflow
          id: end_resolved

    # Create support ticket
    - kind: SendActivity
      activity:
        text: "Creating support ticket..."

    - kind: SetVariable
      variable: Local.TicketId
      value: =Concat("TKT-", System.ConversationId)

    # Route to appropriate team
    - kind: ConditionGroup
      id: route_ticket
      conditions:
        - condition: =Local.ServiceResult.Category = "technical"
          id: technical_route
          actions:
            - kind: InvokeAzureAgent
              id: technical_support
              agent:
                name: TechnicalSupportAgent
              conversationId: =System.ConversationId
              output:
                responseObject: Local.TechResult
        - condition: =Local.ServiceResult.Category = "billing"
          id: billing_route
          actions:
            - kind: InvokeAzureAgent
              id: billing_support
              agent:
                name: BillingSupportAgent
              conversationId: =System.ConversationId
              output:
                responseObject: Local.BillingResult
      elseActions:
        # Escalate to human
        - kind: SendActivity
          activity:
            text: "Escalating to human support..."
        - kind: SetVariable
          variable: Workflow.Outputs.resolution
          value: escalated

    - kind: SendActivity
      activity:
        text: =Concat("Ticket ", Local.TicketId, " has been processed.")
```

### Best Practices

#### Naming Conventions

Use clear, descriptive names for actions and variables:

```yaml
# Good
- kind: SetVariable
  id: calculate_total_price
  variable: Local.orderTotal

# Avoid
- kind: SetVariable
  id: sv1
  variable: Local.x
```

#### Organizing Large Workflows

Break complex workflows into logical sections with comments:

```yaml
actions:
  # === INITIALIZATION ===
  - kind: SetVariable
    id: init_status
    variable: Local.status
    value: started

  # === DATA COLLECTION ===
  - kind: Question
    id: collect_name
    # ...

  # === PROCESSING ===
  - kind: InvokeAzureAgent
    id: process_request
    # ...

  # === OUTPUT ===
  - kind: SendActivity
    id: send_result
    # ...
```

#### Error Handling

Use conditional checks to handle potential issues:

```yaml
actions:
  - kind: SetVariable
    variable: Local.hasError
    value: false

  - kind: InvokeAzureAgent
    id: call_agent
    agent:
      name: ProcessingAgent
    output:
      responseObject: Local.AgentResult

  - kind: If
    condition: =IsBlank(Local.AgentResult)
    then:
      - kind: SetVariable
        variable: Local.hasError
        value: true
      - kind: SendActivity
        activity:
          text: "An error occurred during processing."
    else:
      - kind: SendActivity
        activity:
          text: =Local.AgentResult.message
```

#### Testing Strategies

1. **Start simple**: Test basic flows before adding complexity
2. **Use default values**: Provide sensible defaults for inputs
3. **Add logging**: Use SendActivity for debugging during development
4. **Test edge cases**: Verify behavior with missing or invalid inputs

```yaml
# Debug logging example
- kind: SendActivity
  id: debug_log
  activity:
    text: =Concat("[DEBUG] Current state: counter=", Local.counter, ", status=", Local.status)
```

::: zone-end

## Next Steps

::: zone pivot="programming-language-csharp"

- [C# Declarative Workflow Samples](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/Declarative) - Explore complete working examples including:
  - **StudentTeacher** - Multi-agent conversation with iterative learning
  - **InvokeMcpTool** - MCP server tool integration
  - **InvokeFunctionTool** - Direct function invocation from workflows
  - **FunctionTools** - Agent with function tools
  - **ToolApproval** - Human approval for tool execution
  - **CustomerSupport** - Complex support ticket workflow
  - **DeepResearch** - Research workflow with multiple agents

::: zone-end

::: zone pivot="programming-language-python"

- [Python Declarative Workflow Samples](https://github.com/microsoft/agent-framework/tree/main/python/samples/03-workflows/declarative) - Explore complete working examples

::: zone-end

::: zone pivot="programming-language-go"

> [!NOTE]
> Go support for this feature is coming soon. See the [Agent Framework Go repository](https://github.com/microsoft/agent-framework-go) for the latest status.

::: zone-end