---
title: Microsoft Agent Framework Workflows - Using Workflows as Agents
description: How to use workflows as Agents in Microsoft Agent Framework.
zone_pivot_groups: programming-languages
author: TaoChenOSU
ms.topic: tutorial
ms.author: taochen
ms.date: 09/12/2025
ms.service: agent-framework
---

# Microsoft Agent Framework Workflows - Using Workflows as Agents

This document provides an overview of how to use **Workflows as Agents** in Microsoft Agent Framework.

## Overview

Sometimes you've built a sophisticated workflow with multiple agents, custom executors, and complex logic - but you want to use it just like any other agent. That's exactly what workflow agents let you do. By wrapping your workflow as an `Agent`, you can interact with it through the same familiar API you'd use for a simple chat agent.

### Key Benefits

- **Unified Interface**: Interact with complex workflows using the same API as simple agents
- **API Compatibility**: Integrate workflows with existing systems that support the Agent interface
- **Composability**: Use workflow agents as building blocks in larger agent systems or other workflows
- **Thread Management**: Leverage agent threads for conversation state, checkpointing, and resumption
- **Streaming Support**: Get real-time updates as the workflow executes

### How It Works

When you convert a workflow to an agent:

1. The workflow is validated to ensure its start executor can accept chat messages
2. A thread is created to manage conversation state and checkpoints
3. Input messages are routed to the workflow's start executor
4. Workflow events are converted to agent response updates
5. External input requests (from `RequestInfoExecutor`) are surfaced as function calls

::: zone pivot="programming-language-csharp"

## Requirements

To use a workflow as an agent, the workflow's start executor must be able to handle `IEnumerable<ChatMessage>` as input. This is automatically satisfied when using `ChatClientAgent` or other agent-based executors.

## Create a Workflow Agent

Use the `AsAgent()` extension method to convert any compatible workflow into an agent:

```csharp
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

// First, build your workflow
var workflow = AgentWorkflowBuilder
    .CreateSequentialPipeline(researchAgent, writerAgent, reviewerAgent)
    .Build();

// Convert the workflow to an agent
AIAgent workflowAgent = workflow.AsAgent(
    id: "content-pipeline",
    name: "Content Pipeline Agent",
    description: "A multi-agent workflow that researches, writes, and reviews content"
);
```

### AsAgent Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | `string?` | Optional unique identifier for the agent. Auto-generated if not provided. |
| `name` | `string?` | Optional display name for the agent. |
| `description` | `string?` | Optional description of the agent's purpose. |
| `checkpointManager` | `CheckpointManager?` | Optional checkpoint manager for persistence across sessions. |
| `executionEnvironment` | `IWorkflowExecutionEnvironment?` | Optional execution environment. Defaults to `InProcessExecution.OffThread` or `InProcessExecution.Concurrent` based on workflow configuration. |

## Using Workflow Agents

### Creating a Thread

Each conversation with a workflow agent requires a thread to manage state:

```csharp
// Create a new thread for the conversation
AgentThread thread = await workflowAgent.GetNewThreadAsync();
```

### Non-Streaming Execution

For simple use cases where you want the complete response:

```csharp
var messages = new List<ChatMessage>
{
    new(ChatRole.User, "Write an article about renewable energy trends in 2025")
};

AgentResponse response = await workflowAgent.RunAsync(messages, thread);

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

await foreach (AgentResponseUpdate update in workflowAgent.RunStreamingAsync(messages, thread))
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
await foreach (AgentResponseUpdate update in workflowAgent.RunStreamingAsync(messages, thread))
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

## Thread Serialization and Resumption

Workflow agent threads can be serialized for persistence and resumed later:

```csharp
// Serialize the thread state
JsonElement serializedThread = thread.Serialize();

// Store serializedThread to your persistence layer...

// Later, resume the thread
AgentThread resumedThread = await workflowAgent.DeserializeThreadAsync(serializedThread);

// Continue the conversation
await foreach (var update in workflowAgent.RunStreamingAsync(newMessages, resumedThread))
{
    Console.Write(update.Text);
}
```

## Checkpointing with Workflow Agents

Enable checkpointing to persist workflow state across process restarts:

```csharp
// Create a checkpoint manager with your storage backend
var checkpointManager = new CheckpointManager(new FileCheckpointStorage("./checkpoints"));

// Create workflow agent with checkpointing enabled
AIAgent workflowAgent = workflow.AsAgent(
    id: "persistent-workflow",
    name: "Persistent Workflow Agent",
    checkpointManager: checkpointManager
);
```

::: zone-end

::: zone pivot="programming-language-python"

## Requirements

To use a workflow as an agent, the workflow's start executor must be able to handle `list[ChatMessage]` as input. This is automatically satisfied when using `ChatAgent` or `AgentExecutor`.

## Creating a Workflow Agent

Call `as_agent()` on any compatible workflow to convert it into an agent:

```python
from agent_framework import WorkflowBuilder, ChatAgent
from agent_framework.azure import AzureOpenAIChatClient
from azure.identity import AzureCliCredential

# Create your chat client and agents
chat_client = AzureOpenAIChatClient(credential=AzureCliCredential())

researcher = ChatAgent(
    name="Researcher",
    instructions="Research and gather information on the given topic.",
    chat_client=chat_client,
)

writer = ChatAgent(
    name="Writer", 
    instructions="Write clear, engaging content based on research.",
    chat_client=chat_client,
)

# Build your workflow
workflow = (
    WorkflowBuilder()
    .set_start_executor(researcher)
    .add_edge(researcher, writer)
    .build()
)

# Convert the workflow to an agent
workflow_agent = workflow.as_agent(name="Content Pipeline Agent")
```

### as_agent Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `name` | `str | None` | Optional display name for the agent. Auto-generated if not provided. |

## Using Workflow Agents

### Creating a Thread

Each conversation with a workflow agent requires a thread to manage state:

```python
# Create a new thread for the conversation
thread = workflow_agent.get_new_thread()
```

### Non-Streaming Execution

For simple use cases where you want the complete response:

```python
from agent_framework import ChatMessage, Role

messages = [ChatMessage(role=Role.USER, content="Write an article about AI trends")]

response = await workflow_agent.run(messages, thread=thread)

for message in response.messages:
    print(f"{message.author_name}: {message.text}")
```

### Streaming Execution

For real-time updates as the workflow executes:

```python
messages = [ChatMessage(role=Role.USER, content="Write an article about AI trends")]

async for update in workflow_agent.run_stream(messages, thread=thread):
    # Process streaming updates from each agent in the workflow
    if update.text:
        print(update.text, end="", flush=True)
```

## Handling External Input Requests

When a workflow contains executors that request external input (using `RequestInfoExecutor`), these requests are surfaced as function calls. The workflow agent tracks pending requests and expects responses before continuing:

```python
from agent_framework import (
    FunctionCallContent,
    FunctionApprovalRequestContent,
    FunctionApprovalResponseContent,
)

async for update in workflow_agent.run_stream(messages, thread=thread):
    for content in update.contents:
        if isinstance(content, FunctionApprovalRequestContent):
            # The workflow is requesting external input
            request_id = content.id
            function_call = content.function_call
            
            print(f"Workflow requests input: {function_call.name}")
            print(f"Request data: {function_call.arguments}")
            
            # Store the request_id to provide a response later

# Check for pending requests
if workflow_agent.pending_requests:
    print(f"Pending requests: {list(workflow_agent.pending_requests.keys())}")
```

### Providing Responses to Pending Requests

To continue workflow execution after an external input request:

```python
# Create a response for the pending request
response_content = FunctionApprovalResponseContent(
    id=request_id,
    function_call=function_call,
    approved=True,
)

response_message = ChatMessage(
    role=Role.USER,
    contents=[response_content],
)

# Continue the workflow with the response
async for update in workflow_agent.run_stream([response_message], thread=thread):
    if update.text:
        print(update.text, end="", flush=True)
```

## Complete Example

Here's a complete example demonstrating a workflow agent with streaming output:

```python
import asyncio
from agent_framework import (
    ChatAgent,
    ChatMessage,
    Role,
)
from agent_framework.azure import AzureOpenAIChatClient
from agent_framework._workflows import SequentialBuilder
from azure.identity import AzureCliCredential


async def main():
    # Set up the chat client
    chat_client = AzureOpenAIChatClient(credential=AzureCliCredential())
    
    # Create specialized agents
    researcher = ChatAgent(
        name="Researcher",
        instructions="Research the given topic and provide key facts.",
        chat_client=chat_client,
    )
    
    writer = ChatAgent(
        name="Writer",
        instructions="Write engaging content based on the research provided.",
        chat_client=chat_client,
    )
    
    reviewer = ChatAgent(
        name="Reviewer",
        instructions="Review the content and provide a final polished version.",
        chat_client=chat_client,
    )
    
    # Build a sequential workflow
    workflow = (
        SequentialBuilder()
        .add_agents([researcher, writer, reviewer])
        .build()
    )
    
    # Convert to a workflow agent
    workflow_agent = workflow.as_agent(name="Content Creation Pipeline")
    
    # Create a thread and run the workflow
    thread = workflow_agent.get_new_thread()
    messages = [ChatMessage(role=Role.USER, content="Write about quantum computing")]
    
    print("Starting workflow...")
    print("=" * 60)
    
    current_author = None
    async for update in workflow_agent.run_stream(messages, thread=thread):
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

When a workflow runs as an agent, workflow events are converted to agent responses. The type of response depends on which method you use:

- `run()`: Returns an `AgentResponse` containing the complete result after the workflow finishes
- `run_stream()`: Yields `AgentResponseUpdate` objects as the workflow executes, providing real-time updates

During execution, internal workflow events are mapped to agent responses as follows:

| Workflow Event | Agent Response |
|----------------|----------------|
| `AgentResponseUpdateEvent` | Passed through as `AgentResponseUpdate` (streaming) or aggregated into `AgentResponse` (non-streaming) |
| `RequestInfoEvent` | Converted to `FunctionCallContent` and `FunctionApprovalRequestContent` |
| Other events | Included in `raw_representation` for observability |

This conversion allows you to use the standard agent interface while still having access to detailed workflow information when needed.

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

- [Learn how to handle requests and responses](./requests-and-responses.md) in workflows
- [Learn how to manage state](./shared-states.md) in workflows
- [Learn how to create checkpoints and resume from them](./checkpoints.md)
- [Learn how to monitor workflows](./observability.md)
- [Learn about state isolation in workflows](./state-isolation.md)
- [Learn how to visualize workflows](./visualization.md)
