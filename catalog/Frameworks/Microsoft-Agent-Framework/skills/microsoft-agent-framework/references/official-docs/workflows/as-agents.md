---
title: Microsoft Agent Framework Workflows - Using Workflows as Agents
description: How to use workflows as Agents in Microsoft Agent Framework.
zone_pivot_groups: programming-languages
author: TaoChenOSU
ms.topic: tutorial
ms.author: taochen
ms.date: 07/29/2026
ms.service: agent-framework
---

<!--
  Language parity table – keep in sync when adding/removing sections.

    | Section                            | C# | Python | Go | Notes                                     |
    |------------------------------------|:--:|:------:|:--:|-------------------------------------------|
    | Requirements                       | ✅ |   ✅   | ✅ |                                           |
    | Create a Workflow Agent             | ✅ |   ✅   | ✅ |                                           |
    | AsAIAgent / as_agent Parameters     | ✅ |   ✅   | ✅ | Go uses agentworkflow.AgentConfig           |
    | Creating a Session                  | ✅ |   ✅   | ✅ |                                           |
    | Non-Streaming Execution             | ✅ |   ✅   | ✅ |                                           |
    | Streaming Execution                 | ✅ |   ✅   | ✅ |                                           |
    | Handling External Input Requests    | ✅ |   ✅   | ✅ |                                           |
    | Providing Responses to Requests     | ❌ |   ✅   | ✅ | Python/Go use content helpers             |
    | Session Serialization and Resumption | ✅ |   ❌   | ✅ |                                           |
    | Complete Example                    | ❌ |   ✅   | ✅ | Go includes complete sample               |
    | Event Conversion                    | ❌ |   ✅   | ❌ | Python-specific                           |
-->

# Microsoft Agent Framework Workflows - Using Workflows as Agents

This document provides an overview of how to use **Workflows as Agents** in Microsoft Agent Framework.

## Overview

Sometimes you've built a sophisticated workflow with multiple agents, custom executors, and complex logic - but you want to use it just like any other agent. That's exactly what workflow agents let you do. By wrapping your workflow as an `Agent`, you can interact with it through the same familiar API you'd use for a simple chat agent.

### Key Benefits

- **Unified Interface**: Interact with complex workflows using the same API as simple agents
- **API Compatibility**: Integrate workflows with existing systems that support the Agent interface
- **Composability**: Use workflow agents as building blocks in larger agent systems or other workflows
- **Session Management**: Leverage agent sessions for conversation state and resumption
- **Streaming Support**: Get real-time updates as the workflow executes

### How It Works

When you convert a workflow to an agent:

1. The workflow is validated to ensure its start executor can accept the required input types
2. A session is created to manage conversation state
3. Input messages are routed to the workflow's start executor
4. Workflow events are converted to agent response updates
5. External input requests (from `RequestInfoExecutor`) are surfaced as function calls

::: zone pivot="programming-language-csharp"

## Requirements

To use a workflow as an agent, the workflow's start executor must be able to handle `IEnumerable<ChatMessage>` as input. This is automatically satisfied when using agent-based executors created with `AsAIAgent`.

## Create a Workflow Agent

Use the `AsAIAgent()` extension method to convert any compatible workflow into an agent:

```csharp
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

// Create agents
AIAgent researchAgent = chatClient.AsAIAgent("You are a researcher. Research and gather information on the given topic.");
AIAgent writerAgent = chatClient.AsAIAgent("You are a writer. Write clear, engaging content based on research.");
AIAgent reviewerAgent = chatClient.AsAIAgent("You are a reviewer. Review the content and provide a final polished version.");

// Build a sequential workflow
var workflow = new WorkflowBuilder(researchAgent)
    .AddEdge(researchAgent, writerAgent)
    .AddEdge(writerAgent, reviewerAgent)
    .Build();

// Convert the workflow to an agent
AIAgent workflowAgent = workflow.AsAIAgent(
    id: "content-pipeline",
    name: "Content Pipeline Agent",
    description: "A multi-agent workflow that researches, writes, and reviews content"
);
```

### AsAIAgent Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | `string?` | Optional unique identifier for the agent. Auto-generated if not provided. |
| `name` | `string?` | Optional display name for the agent. |
| `description` | `string?` | Optional description of the agent's purpose. |
| `executionEnvironment` | `IWorkflowExecutionEnvironment?` | Optional execution environment. Defaults to `InProcessExecution.OffThread` or `InProcessExecution.Concurrent` based on workflow configuration. |
| `includeExceptionDetails` | `bool` | If `true`, includes exception messages in error content. Defaults to `false`. |
| `includeWorkflowOutputsInResponse` | `bool` | If `true`, transforms outgoing workflow outputs into content in agent responses. Defaults to `false`. |

## Using Workflow Agents

### Creating a Session

Each conversation with a workflow agent requires a session to manage state:

```csharp
// Create a new session for the conversation
AgentSession session = await workflowAgent.CreateSessionAsync();
```

### Non-Streaming Execution

For simple use cases where you want the complete response:

```csharp
var messages = new List<ChatMessage>
{
    new(ChatRole.User, "Write an article about renewable energy trends in 2025")
};

AgentResponse response = await workflowAgent.RunAsync(messages, session);

foreach (ChatMessage message in response.Messages)
{
    Console.WriteLine($"{message.AuthorName}: {message.Text}");
}
```

### Streaming Execution

For real-time updates as the workflow executes:

```csharp
var messages = new List<ChatMessage>
{
    new(ChatRole.User, "Write an article about renewable energy trends in 2025")
};

await foreach (AgentResponseUpdate update in workflowAgent.RunStreamingAsync(messages, session))
{
    // Process streaming updates from each agent in the workflow
    if (!string.IsNullOrEmpty(update.Text))
    {
        Console.Write(update.Text);
    }
}
```

## Handling External Input Requests

When a workflow contains executors that request external input (using `RequestInfoExecutor`), these requests are surfaced as function calls in the agent response:

```csharp
await foreach (AgentResponseUpdate update in workflowAgent.RunStreamingAsync(messages, session))
{
    // Check for function call requests
    foreach (AIContent content in update.Contents)
    {
        if (content is FunctionCallContent functionCall)
        {
            // Handle the external input request
            Console.WriteLine($"Workflow requests input: {functionCall.Name}");
            Console.WriteLine($"Request data: {functionCall.Arguments}");

            // Provide the response in the next message
        }
    }
}
```

## Session Serialization and Resumption

Workflow agent sessions can be serialized for persistence and resumed later:

```csharp
// Serialize the session state
JsonElement serializedSession = await workflowAgent.SerializeSessionAsync(session);

// Store serializedSession to your persistence layer...

// Later, resume the session
AgentSession resumedSession = await workflowAgent.DeserializeSessionAsync(serializedSession);

// Continue the conversation
await foreach (var update in workflowAgent.RunStreamingAsync(newMessages, resumedSession))
{
    Console.Write(update.Text);
}
```

> [!IMPORTANT]
> A serialized workflow-agent session contains the inner workflow checkpoint. If your application reconstructs the workflow before deserializing or running the session, every inner agent must be recreated with the same `ChatClientAgentOptions.Id` (and, if a `Name` is set, the same `Name`).
>
> The `id` passed to `workflow.AsAIAgent(...)` identifies only the outer workflow agent. It does not stabilize the executor identities of agents inside the workflow. For configuration guidance, see [Rehydrating from Checkpoints](./checkpoints.md#rehydrating-from-checkpoints).

::: zone-end

::: zone pivot="programming-language-python"

## Requirements

To use a workflow as an agent, the workflow's start executor must be able to handle message input. This is automatically satisfied when using `Agent` or agent-based executors.

## Create a Workflow Agent

Call `as_agent()` on any compatible workflow to convert it into an agent:

```python
from agent_framework.foundry import FoundryChatClient
from agent_framework.orchestrations import SequentialBuilder
from azure.identity import AzureCliCredential

# Create your chat client and agents
client = FoundryChatClient(
    project_endpoint="<your-endpoint>",
    model="<your-deployment>",
    credential=AzureCliCredential(),
)

researcher = client.as_agent(
    name="Researcher",
    instructions="Research and gather information on the given topic.",
)

writer = client.as_agent(
    name="Writer",
    instructions="Write clear, engaging content based on research.",
)

# Build a sequential workflow
workflow = SequentialBuilder(participants=[researcher, writer]).build()

# Convert the workflow to an agent
workflow_agent = workflow.as_agent(name="Content Pipeline Agent")
```

### as_agent Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `name` | `str | None` | Optional display name for the agent. Auto-generated if not provided. |

## Using Workflow Agents

### Creating a Session

You can optionally create a session to manage conversation state across multiple turns:

```python
# Create a new session for the conversation
session = await workflow_agent.create_session()
```

> [!NOTE]
> Sessions are optional. If you don't pass a `session` to `run()`, the agent handles state internally.
> If `workflow.as_agent()` is created without `context_providers`, the framework adds an `InMemoryHistoryProvider()` by default so multi-turn history works out of the box.
> If you pass `context_providers` explicitly, that list is used as-is.

### Non-Streaming Execution

For simple use cases where you want the complete response:

```python
# You can pass a plain string as input
response = await workflow_agent.run("Write an article about AI trends")

for message in response.messages:
    print(f"{message.author_name}: {message.text}")
```

### Streaming Execution

For real-time updates as the workflow executes:

```python
async for update in workflow_agent.run(
    "Write an article about AI trends",
    stream=True,
):
    if update.text:
        print(update.text, end="", flush=True)
```

## Handling External Input Requests

When a workflow contains executors that request external input (using `request_info`), these requests are surfaced as function calls in the agent response. The function call uses the name `WorkflowAgent.REQUEST_INFO_FUNCTION_NAME`:

```python
from agent_framework import Content, Message, WorkflowAgent

response = await workflow_agent.run("Process my request")

# Look for function calls in the response
human_review_function_call = None
for message in response.messages:
    for content in message.contents:
        if content.name == WorkflowAgent.REQUEST_INFO_FUNCTION_NAME:
            human_review_function_call = content
```

### Providing Responses to Pending Requests

To continue workflow execution after an external input request, create a function result and send it back:

```python
if human_review_function_call:
    # Parse the request arguments
    request = WorkflowAgent.RequestInfoFunctionArgs.from_json(
        human_review_function_call.arguments
    )

    # Create a response (your custom response type)
    result_data = MyResponseType(approved=True, feedback="Looks good")

    # Create the function call result
    function_result = Content.from_function_result(
        call_id=human_review_function_call.call_id,
        result=result_data,
    )

    # Send the response back to continue the workflow
    response = await workflow_agent.run(Message("tool", [function_result]))
```

## Complete Example

Here's a complete example demonstrating a workflow agent with streaming output:

```python
import asyncio
import os

from agent_framework.foundry import FoundryChatClient
from agent_framework.orchestrations import SequentialBuilder
from azure.identity import AzureCliCredential


async def main():
    # Set up the chat client
    client = FoundryChatClient(
        project_endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
        model=os.environ["FOUNDRY_MODEL"],
        credential=AzureCliCredential(),
    )

    # Create specialized agents
    researcher = client.as_agent(
        name="Researcher",
        instructions="Research the given topic and provide key facts.",
    )

    writer = client.as_agent(
        name="Writer",
        instructions="Write engaging content based on the research provided.",
    )

    reviewer = client.as_agent(
        name="Reviewer",
        instructions="Review the content and provide a final polished version.",
    )

    # Build a sequential workflow
    workflow = SequentialBuilder(participants=[researcher, writer, reviewer]).build()

    # Convert to a workflow agent
    workflow_agent = workflow.as_agent(name="Content Creation Pipeline")

    # Run the workflow
    print("Starting workflow...")
    print("=" * 60)

    current_author = None
    async for update in workflow_agent.run(
        "Write about quantum computing",
        stream=True,
    ):
        # Show when different agents are responding
        if update.author_name and update.author_name != current_author:
            if current_author:
                print("\n" + "-" * 40)
            print(f"\n[{update.author_name}]:")
            current_author = update.author_name

        if update.text:
            print(update.text, end="", flush=True)

    print("\n" + "=" * 60)
    print("Workflow completed!")


if __name__ == "__main__":
    asyncio.run(main())
```

## Understanding Event Conversion

When a workflow runs as an agent, workflow events are converted to agent responses. The type of response depends on how you call `run()`:

- `run()`: Returns an `AgentResponse` containing the complete result after the workflow finishes
- `run(..., stream=True)`: Returns an async iterable of `AgentResponseUpdate` objects as the workflow executes, providing real-time updates

`as_agent()` forwards both `"output"` (terminal) and `"intermediate"` events to the caller. The set of forwarded event types is `AGENT_FORWARDED_EVENT_TYPES = {"output", "intermediate"}`. All other workflow-internal events are dropped.

During execution, internal workflow events are mapped to agent responses as follows:

| Workflow Event | Agent Response |
|----------------|----------------|
| `event.type == "output"` | Terminal answer — passed through as `AgentResponseUpdate` (streaming) or aggregated into `AgentResponse` (non-streaming). `response.text` returns only these terminal outputs. |
| `event.type == "intermediate"` | Observational progress — rendered as `text_reasoning` content in `AgentResponseUpdate`. Not included in `response.text`. |
| `event.type == "request_info"` | Converted to function call content using `WorkflowAgent.REQUEST_INFO_FUNCTION_NAME` |
| Other events | Ignored (workflow-internal only) |

This conversion allows you to use the standard agent interface while still having access to detailed workflow information when needed. The `.text` property on both `AgentResponse` and `AgentResponseUpdate` returns only the terminal (`"output"`) answer; inspect `text_reasoning` content items to access intermediate progress.

::: zone-end

::: zone pivot="programming-language-go"

Go wraps workflows as agents with `workflow/agentworkflow`. This lets callers use the normal agent run APIs while the provider executes the workflow behind the scenes.

## Requirements

The workflow's start executor must accept `[]*message.Message`. Hosted agent executors and executors configured with `messageworkflow.Configure` satisfy this requirement.

## Create a Workflow Agent

Use `agentworkflow.New` to wrap any compatible workflow as an agent:

```go
wfAgent, err := agentworkflow.New(wf, agentworkflow.AgentConfig{
    IncludeOutputsInResponse: true,
    Config: agent.Config{
        Name: "WorkflowAgent",
    },
})
if err != nil {
    return err
}
```

### agentworkflow.AgentConfig Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `Config` | `agent.Config` | Embedded agent configuration, including name, description, middleware, tools, and run options. |
| `Environment` | `*inproc.ExecutionEnvironment` | Optional execution environment. Defaults to `inproc.OffThread`, or to `inproc.Concurrent` when the workflow allows concurrent execution. |
| `IncludeErrorDetails` | `bool` | If `true`, includes detailed workflow error messages in agent responses. Defaults to `false`. |
| `IncludeOutputsInResponse` | `bool` | If `true`, transforms outgoing workflow message outputs into content in agent responses. Defaults to `false`. |

## Using Workflow Agents

### Creating a Session

Create an agent session when you want workflow state to persist across turns:

```go
session, err := wfAgent.CreateSession(ctx)
if err != nil {
    return err
}
```

### Non-Streaming Execution

Use `RunText` or `Run` and collect the response for non-streaming execution:

```go
response, err := wfAgent.RunText(ctx, "Analyze this", agent.WithSession(session)).Collect()
if err != nil {
    return err
}
fmt.Println(response.String())
```

### Streaming Execution

For real-time updates as the workflow executes:

```go
for update, err := range wfAgent.RunText(ctx, "Analyze this", agent.WithSession(session), agent.Stream(true)) {
    if err != nil {
        return err
    }
    fmt.Print(update.String())
}
```

## Handling External Input Requests

External requests from the workflow are surfaced as function call content in the agent response. Inspect response messages for request content and send the matching response in a later run.

```go
var requestCall *message.FunctionCallContent
for content := range response.Contents() {
    if call, ok := content.(*message.FunctionCallContent); ok {
        requestCall = call
        break
    }
}
```

### Providing Responses to Pending Requests

To continue workflow execution, return the matching response content to the workflow agent:

```go
result := &message.FunctionResultContent{
    CallID: requestCall.CallID,
    Result: "approved",
}

response, err = wfAgent.Run(
    ctx,
    []*message.Message{{
        Role:     message.RoleTool,
        Contents: []message.Content{result},
    }},
    agent.WithSession(session),
).Collect()
if err != nil {
    return err
}
```

## Session Serialization and Resumption

Workflow agent sessions can be serialized for persistence and resumed later:

```go
// Serialize the session state.
serializedSession, err := json.Marshal(session)
if err != nil {
    return err
}

// Store serializedSession to your persistence layer...

// Later, resume the session.
var resumedSession agent.Session
if err := json.Unmarshal(serializedSession, &resumedSession); err != nil {
    return err
}

for update, err := range wfAgent.RunText(ctx, "Continue the article", agent.WithSession(&resumedSession), agent.Stream(true)) {
    if err != nil {
        return err
    }
    fmt.Print(update.String())
}
```

## Complete Example

The following example builds a content pipeline workflow, wraps it as an agent, and streams responses through the normal agent API:

```go
package main

import (
    "cmp"
    "context"
    "fmt"
    "log"
    "os"

    "github.com/microsoft/agent-framework-go/agent"
    "github.com/microsoft/agent-framework-go/provider/foundryprovider"
    "github.com/microsoft/agent-framework-go/workflow/agentworkflow"

    "github.com/Azure/azure-sdk-for-go/sdk/azidentity"
)

func main() {
    ctx := context.Background()
    endpoint := os.Getenv("FOUNDRY_PROJECT_ENDPOINT")
    model := cmp.Or(os.Getenv("FOUNDRY_MODEL"), "gpt-4o-mini")

    credential, err := azidentity.NewDefaultAzureCredential(nil)
    if err != nil {
        log.Fatal(err)
    }

    researcher := foundryprovider.NewAgent(endpoint, credential, foundryprovider.ModelDeployment(model), foundryprovider.AgentConfig{
        Instructions: "Research and gather information on the given topic.",
        Config:      agent.Config{Name: "Researcher"},
    })
    writer := foundryprovider.NewAgent(endpoint, credential, foundryprovider.ModelDeployment(model), foundryprovider.AgentConfig{
        Instructions: "Write clear, engaging content based on research.",
        Config:      agent.Config{Name: "Writer"},
    })
    reviewer := foundryprovider.NewAgent(endpoint, credential, foundryprovider.ModelDeployment(model), foundryprovider.AgentConfig{
        Instructions: "Review the content and provide a final polished version.",
        Config:      agent.Config{Name: "Reviewer"},
    })

    wf, err := agentworkflow.NewSequentialWorkflowBuilder(researcher, writer, reviewer).
        WithName("content-pipeline").
        Build()
    if err != nil {
        log.Fatal(err)
    }

    wfAgent, err := agentworkflow.New(wf, agentworkflow.AgentConfig{
        IncludeOutputsInResponse: true,
        Config: agent.Config{
            Name: "Content Pipeline Agent",
        },
    })
    if err != nil {
        log.Fatal(err)
    }

    session, err := wfAgent.CreateSession(ctx)
    if err != nil {
        log.Fatal(err)
    }

    for update, err := range wfAgent.RunText(ctx, "Write about quantum computing", agent.WithSession(session), agent.Stream(true)) {
        if err != nil {
            log.Fatal(err)
        }
        if text := update.String(); text != "" {
            fmt.Print(text)
        }
    }
}
```

> [!WARNING]
> `azidentity.NewDefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential, such as `azidentity.NewManagedIdentityCredential`, to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

> [!TIP]
> See the [workflow as an agent sample](https://github.com/microsoft/agent-framework-go/blob/main/examples/03-workflows/agents/workflow_as_an_agent/main.go) for a complete runnable example.

::: zone-end
## Use Cases

### 1. Complex Agent Pipelines

Wrap a multi-agent workflow as a single agent for use in applications:

```
User Request --> [Workflow Agent] --> Final Response
                      |
                      +-- Researcher Agent
                      +-- Writer Agent
                      +-- Reviewer Agent
```

### 2. Agent Composition

Use workflow agents as components in larger systems:

- A workflow agent can be used as a tool by another agent
- Multiple workflow agents can be orchestrated together
- Workflow agents can be nested within other workflows

### 3. API Integration

Expose complex workflows through APIs that expect the standard Agent interface, enabling:

- Chat interfaces that use sophisticated backend workflows
- Integration with existing agent-based systems
- Gradual migration from simple agents to complex workflows

## Next Steps

- [Learn how to handle requests and responses](../concepts/workflows/state.md) in workflows
- [Learn how to manage state](../concepts/workflows/state.md) in workflows
- [Learn how to create checkpoints and resume from them](./checkpoints.md)
- [Learn how to monitor workflows](./observability.md)
- [Learn about state isolation in workflows](../concepts/workflows/state.md)
- [Learn how to visualize workflows](./visualization.md)
