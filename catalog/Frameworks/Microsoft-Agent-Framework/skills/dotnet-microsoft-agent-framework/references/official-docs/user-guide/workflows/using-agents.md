---
title: Microsoft Agent Framework Workflows - Working with Agents
description: In-depth look at Working with Agents in Microsoft Agent Framework Workflows.
zone_pivot_groups: programming-languages
author: TaoChenOSU
ms.topic: tutorial
ms.author: taochen
ms.date: 09/12/2025
ms.service: agent-framework
---

# Microsoft Agent Framework Workflows - Working with Agents

This page provides an overview of how to use **Agents** within Microsoft Agent Framework Workflows.

## Overview

To add intelligence to your workflows, you can leverage AI agents as part of your workflow execution. AI agents can be easily integrated into workflows, allowing you to create complex, intelligent solutions that were previously difficult to achieve.

::: zone pivot="programming-language-csharp"

## Add an Agent Directly to a Workflow

You can add agents to your workflow via edges:

```csharp
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;

// Create the agents first
AIAgent agentA = new ChatClientAgent(chatClient, instructions);
AIAgent agentB = new ChatClientAgent(chatClient, instructions);

// Build a workflow with the agents
WorkflowBuilder builder = new(agentA);
builder.AddEdge(agentA, agentB);
Workflow<ChatMessage> workflow = builder.Build<ChatMessage>();
```

### Running the Workflow

Inside the workflow created above, the agents are actually wrapped inside an executor that handles the communication of the agent with other parts of the workflow. The executor can handle three message types:

- `ChatMessage`: A single chat message.
- `List<ChatMessage>`: A list of chat messages.
- `TurnToken`: A turn token that signals the start of a new turn.

The executor doesn't trigger the agent to respond until it receives a `TurnToken`. Any messages received before the `TurnToken` are buffered and sent to the agent when the `TurnToken` is received.

```csharp
StreamingRun run = await InProcessExecution.StreamAsync(workflow, new ChatMessage(ChatRole.User, "Hello World!"));
// Must send the turn token to trigger the agents. The agents are wrapped as executors.
// When they receive messages, they will cache the messages and only start processing
// when they receive a TurnToken. The turn token will be passed from one agent to the next.
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
await foreach (WorkflowEvent evt in run.WatchStreamAsync().ConfigureAwait(false))
{
    // The agents will run in streaming mode and an AgentResponseUpdateEvent
    // will be emitted as new chunks are generated.
    if (evt is AgentResponseUpdateEvent agentRunUpdate)
    {
        Console.WriteLine($"{agentRunUpdate.ExecutorId}: {agentRunUpdate.Data}");
    }
}
```

::: zone-end

::: zone pivot="programming-language-python"

## Using the Built-in Agent Executor

You can add agents to your workflow via edges:

```python
from agent_framework import WorkflowBuilder
from agent_framework.azure import AzureChatClient
from azure.identity import AzureCliCredential

# Create the agents first
chat_client = AzureChatClient(credential=AzureCliCredential())
writer_agent: ChatAgent = chat_client.as_agent(
    instructions=(
        "You are an excellent content writer. You create new content and edit contents based on the feedback."
    ),
    name="writer_agent",
)
reviewer_agent = chat_client.as_agent(
    instructions=(
        "You are an excellent content reviewer."
        "Provide actionable feedback to the writer about the provided content."
        "Provide the feedback in the most concise manner possible."
    ),
    name="reviewer_agent",
)

# Build a workflow with the agents
builder = WorkflowBuilder()
builder.set_start_executor(writer_agent)
builder.add_edge(writer_agent, reviewer_agent)
workflow = builder.build()
```

### Running the Workflow

Inside the workflow created above, the agents are actually wrapped inside an executor that handles the communication of the agent with other parts of the workflow. The executor can handle three message types:

- `str`: A single chat message in string format
- `ChatMessage`: A single chat message
- `List<ChatMessage>`: A list of chat messages

Whenever the executor receives a message of one of these types, it will trigger the agent to respond, and the response type will be an `AgentExecutorResponse` object. This class contains useful information about the agent's response, including:

- `executor_id`: The ID of the executor that produced this response
- `agent_run_response`: The full response from the agent
- `full_conversation`: The full conversation history up to this point

Two possible event type related to the agents' responses can be emitted when running the workflow:

- `AgentResponseUpdateEvent` containing chunks of the agent's response as they are generated in streaming mode.
- `AgentRunEvent` containing the full response from the agent in non-streaming mode.

> By default, agents are wrapped in executors that run in streaming mode. You can customize this behavior by creating a custom executor. See the next section for more details.

```python
last_executor_id = None
async for event in workflow.run_streaming("Write a short blog post about AI agents."):
    if isinstance(event, AgentResponseUpdateEvent):
        if event.executor_id != last_executor_id:
            if last_executor_id is not None:
                print()
            print(f"{event.executor_id}:", end=" ", flush=True)
            last_executor_id = event.executor_id
        print(event.data, end="", flush=True)
```

::: zone-end

## Using a Custom Agent Executor

Sometimes you might want to customize how AI agents are integrated into a workflow. You can achieve this by creating a custom executor. This allows you to control:

- The invocation of the agent: streaming or non-streaming
- The message types the agent will handle, including custom message types
- The life cycle of the agent, including initialization and cleanup
- The usage of agent threads and other resources
- Additional events emitted during the agent's execution, including custom events
- Integration with other workflow features, such as shared states and requests/responses

::: zone pivot="programming-language-csharp"

```csharp
internal sealed class CustomAgentExecutor : Executor<CustomInput, CustomOutput>("CustomAgentExecutor")
{
    private readonly AIAgent _agent;

    /// <summary>
    /// Creates a new instance of the <see cref="CustomAgentExecutor"/> class.
    /// </summary>
    /// <param name="agent">The AI agent used for custom processing</param>
    public CustomAgentExecutor(AIAgent agent) : base("CustomAgentExecutor")
    {
        this._agent = agent;
    }

    public async ValueTask<CustomOutput> HandleAsync(CustomInput message, IWorkflowContext context)
    {
        // Retrieve any shared states if needed
        var sharedState = await context.ReadStateAsync<SharedStateType>("sharedStateId", scopeName: "SharedStateScope");

        // Render the input for the agent
        var agentInput = RenderInput(message, sharedState);

        // Invoke the agent
        // Assume the agent is configured with structured outputs with type `CustomOutput`
        var response = await this._agent.RunAsync(agentInput);
        var customOutput = JsonSerializer.Deserialize<CustomOutput>(response.Text);

        return customOutput;
    }
}
```

::: zone-end

::: zone pivot="programming-language-python"

```python
from agent_framework import (
    ChatAgent,
    ChatMessage,
    Executor,
    WorkflowContext,
    handler
)

class Writer(Executor):

    agent: ChatAgent

    def __init__(self, chat_client: AzureChatClient, id: str = "writer"):
        # Create a domain specific agent using your configured AzureChatClient.
        agent = chat_client.as_agent(
            instructions=(
                "You are an excellent content writer. You create new content and edit contents based on the feedback."
            ),
        )
        # Associate the agent with this executor node. The base Executor stores it on self.agent.
        super().__init__(agent=agent, id=id)

    @handler
    async def handle(self, message: ChatMessage, ctx: WorkflowContext[list[ChatMessage]]) -> None:
        """Handles a single chat message and forwards the accumulated messages to the next executor in the workflow."""
        # Invoke the agent with the incoming message and get the response
        messages: list[ChatMessage] = [message]
        response = await self.agent.run(messages)
        # Accumulate messages and send them to the next executor in the workflow.
        messages.extend(response.messages)
        await ctx.send_message(messages)
```

::: zone-end

## Next Steps

- [Learn how to use workflows as agents](./as-agents.md).
- [Learn how to handle requests and responses](./requests-and-responses.md) in workflows.
- [Learn how to manage state](./shared-states.md) in workflows.
- [Learn how to create checkpoints and resume from them](./checkpoints.md).
- [Learn how to monitor workflows](./observability.md).
- [Learn about state isolation in workflows](./state-isolation.md).
- [Learn how to visualize workflows](./visualization.md).
