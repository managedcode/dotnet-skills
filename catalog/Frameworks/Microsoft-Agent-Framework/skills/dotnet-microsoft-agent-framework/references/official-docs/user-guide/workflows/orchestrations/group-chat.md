---
title: Microsoft Agent Framework Workflows Orchestrations - Group Chat
description: In-depth look at Group Chat Orchestrations in Microsoft Agent Framework Workflows.
zone_pivot_groups: programming-languages
author: moonbox3
ms.topic: tutorial
ms.author: evmattso
ms.date: 11/12/2025
ms.service: agent-framework
---

# Microsoft Agent Framework Workflows Orchestrations - Group Chat

Group chat orchestration models a collaborative conversation among multiple agents, coordinated by an orchestrator that determines speaker selection and conversation flow. This pattern is ideal for scenarios requiring iterative refinement, collaborative problem-solving, or multi-perspective analysis.

Internally, the group chat orchestration assembles agents in a star topology, with an orchestrator in the middle. The orchestrator can implement various strategies for selecting which agent speaks next, such as round-robin, prompt-based selection, or custom logic based on conversation context, making it a flexible and powerful pattern for multi-agent collaboration.

<p align="center">
    <img src="../resources/images/orchestration-groupchat.png" alt="Group Chat Orchestration"/>
</p>

## Differences Between Group Chat and Other Patterns

Group chat orchestration has distinct characteristics compared to other multi-agent patterns:

- **Centralized Coordination**: Unlike handoff patterns where agents directly transfer control, group chat uses an orchestrator to coordinate who speaks next
- **Iterative Refinement**: Agents can review and build upon each other's responses in multiple rounds
- **Flexible Speaker Selection**: The orchestrator can use various strategies (round-robin, prompt-based, custom logic) to select speakers
- **Shared Context**: All agents see the full conversation history, enabling collaborative refinement

## What You'll Learn

- How to create specialized agents for group collaboration
- How to configure speaker selection strategies
- How to build workflows with iterative agent refinement
- How to customize conversation flow with custom orchestrators

::: zone pivot="programming-language-csharp"

## Set Up the Azure OpenAI Client

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;

// Set up the Azure OpenAI client
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ??
    throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-4o-mini";
var client = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient();
```

## Define Your Agents

Create specialized agents for different roles in the group conversation:

```csharp
// Create a copywriter agent
ChatClientAgent writer = new(client,
    "You are a creative copywriter. Generate catchy slogans and marketing copy. Be concise and impactful.",
    "CopyWriter",
    "A creative copywriter agent");

// Create a reviewer agent
ChatClientAgent reviewer = new(client,
    "You are a marketing reviewer. Evaluate slogans for clarity, impact, and brand alignment. " +
    "Provide constructive feedback or approval.",
    "Reviewer",
    "A marketing review agent");
```

## Configure Group Chat with Round-Robin Orchestrator

Build the group chat workflow using `AgentWorkflowBuilder`:

```csharp
// Build group chat with round-robin speaker selection
// The manager factory receives the list of agents and returns a configured manager
var workflow = AgentWorkflowBuilder
    .CreateGroupChatBuilderWith(agents =>
        new RoundRobinGroupChatManager(agents)
        {
            MaximumIterationCount = 5  // Maximum number of turns
        })
    .AddParticipants(writer, reviewer)
    .Build();
```

## Run the Group Chat Workflow

Execute the workflow and observe the iterative conversation:

```csharp
// Start the group chat
var messages = new List<ChatMessage> {
    new(ChatRole.User, "Create a slogan for an eco-friendly electric vehicle.")
};

StreamingRun run = await InProcessExecution.StreamAsync(workflow, messages);
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

await foreach (WorkflowEvent evt in run.WatchStreamAsync().ConfigureAwait(false))
{
    if (evt is AgentResponseUpdateEvent update)
    {
        // Process streaming agent responses
        AgentResponse response = update.AsResponse();
        foreach (ChatMessage message in response.Messages)
        {
            Console.WriteLine($"[{update.ExecutorId}]: {message.Text}");
        }
    }
    else if (evt is WorkflowOutputEvent output)
    {
        // Workflow completed
        var conversationHistory = output.As<List<ChatMessage>>();
        Console.WriteLine("\n=== Final Conversation ===");
        foreach (var message in conversationHistory)
        {
            Console.WriteLine($"{message.AuthorName}: {message.Text}");
        }
        break;
    }
}
```

## Sample Interaction

```plaintext
[CopyWriter]: "Green Dreams, Zero Emissions" - Drive the future with style and sustainability.

[Reviewer]: The slogan is good, but "Green Dreams" might be a bit abstract. Consider something
more direct like "Pure Power, Zero Impact" to emphasize both performance and environmental benefit.

[CopyWriter]: "Pure Power, Zero Impact" - Experience electric excellence without compromise.

[Reviewer]: Excellent! This slogan is clear, impactful, and directly communicates the key benefits.
The tagline reinforces the message perfectly. Approved for use.

[CopyWriter]: Thank you! The final slogan is: "Pure Power, Zero Impact" - Experience electric
excellence without compromise.
```

::: zone-end

::: zone pivot="programming-language-python"

## Set Up the Chat Client

```python
from agent_framework.azure import AzureOpenAIChatClient
from azure.identity import AzureCliCredential

# Initialize the Azure OpenAI chat client
chat_client = AzureOpenAIChatClient(credential=AzureCliCredential())
```

## Define Your Agents

Create specialized agents with distinct roles:

```python
from agent_framework import ChatAgent

# Create a researcher agent
researcher = ChatAgent(
    name="Researcher",
    description="Collects relevant background information.",
    instructions="Gather concise facts that help answer the question. Be brief and factual.",
    chat_client=chat_client,
)

# Create a writer agent
writer = ChatAgent(
    name="Writer",
    description="Synthesizes polished answers using gathered information.",
    instructions="Compose clear, structured answers using any notes provided. Be comprehensive.",
    chat_client=chat_client,
)
```

## Configure Group Chat with Simple Selector

Build a group chat with custom speaker selection logic:

```python
from agent_framework import GroupChatBuilder, GroupChatState

def round_robin_selector(state: GroupChatState) -> str:
    """A round-robin selector function that picks the next speaker based on the current round index."""

    participant_names = list(state.participants.keys())
    return participant_names[state.current_round % len(participant_names)]


# Build the group chat workflow
workflow = (
    GroupChatBuilder()
    .with_select_speaker_func(round_robin_selector)
    .participants([researcher, writer])
    # Terminate after 4 turns (researcher → writer → researcher → writer)
    .with_termination_condition(lambda conversation: len(conversation) >= 4)
    .build()
)
```

## Configure Group Chat with Agent-Based Orchestrator

Alternatively, use an agent-based orchestrator for intelligent speaker selection. The orchestrator is a full `ChatAgent` with access to tools, context, and observability:

```python
# Create orchestrator agent for speaker selection
orchestrator_agent = ChatAgent(
    name="Orchestrator",
    description="Coordinates multi-agent collaboration by selecting speakers",
    instructions="""
You coordinate a team conversation to solve the user's task.

Guidelines:
- Start with Researcher to gather information
- Then have Writer synthesize the final answer
- Only finish after both have contributed meaningfully
""",
    chat_client=chat_client,
)

# Build group chat with agent-based orchestrator
workflow = (
    GroupChatBuilder()
    .with_agent_orchestrator(orchestrator_agent)
    # Set a hard termination condition: stop after 4 assistant messages
    # The agent orchestrator will intelligently decide when to end before this limit but just in case
    .with_termination_condition(lambda messages: sum(1 for msg in messages if msg.role == Role.ASSISTANT) >= 4)
    .participants([researcher, writer])
    .build()
)
```

## Run the Group Chat Workflow

Execute the workflow and process events:

```python
from typing import cast
from agent_framework import AgentResponseUpdateEvent, Role, WorkflowOutputEvent

task = "What are the key benefits of async/await in Python?"

print(f"Task: {task}\n")
print("=" * 80)

final_conversation: list[ChatMessage] = []
last_executor_id: str | None = None

# Run the workflow
async for event in workflow.run_stream(task):
    if isinstance(event, AgentResponseUpdateEvent):
        # Print streaming agent updates
        eid = event.executor_id
        if eid != last_executor_id:
            if last_executor_id is not None:
                print()
            print(f"[{eid}]:", end=" ", flush=True)
            last_executor_id = eid
        print(event.data, end="", flush=True)
    elif isinstance(event, WorkflowOutputEvent):
        # Workflow completed - data is a list of ChatMessage
        final_conversation = cast(list[ChatMessage], event.data)

if final_conversation:
    print("\n\n" + "=" * 80)
    print("Final Conversation:")
    for msg in final_conversation:
        author = getattr(msg, "author_name", "Unknown")
        text = getattr(msg, "text", str(msg))
        print(f"\n[{author}]\n{text}")
        print("-" * 80)

print("\nWorkflow completed.")
```

## Sample Interaction

```plaintext
Task: What are the key benefits of async/await in Python?

================================================================================

[Researcher]: Async/await in Python provides non-blocking I/O operations, enabling
concurrent execution without threading overhead. Key benefits include improved
performance for I/O-bound tasks, better resource utilization, and simplified
concurrent code structure using native coroutines.

[Writer]: The key benefits of async/await in Python are:

1. **Non-blocking Operations**: Allows I/O operations to run concurrently without
   blocking the main thread, significantly improving performance for network
   requests, file I/O, and database queries.

2. **Resource Efficiency**: Avoids the overhead of thread creation and context
   switching, making it more memory-efficient than traditional threading.

3. **Simplified Concurrency**: Provides a clean, synchronous-looking syntax for
   asynchronous code, making concurrent programs easier to write and maintain.

4. **Scalability**: Enables handling thousands of concurrent connections with
   minimal resource consumption, ideal for high-performance web servers and APIs.

--------------------------------------------------------------------------------

Workflow completed.
```

::: zone-end

## Key Concepts

::: zone pivot="programming-language-csharp"

- **Centralized Manager**: Group chat uses a manager to coordinate speaker selection and flow
- **AgentWorkflowBuilder.CreateGroupChatBuilderWith()**: Creates workflows with a manager factory function
- **RoundRobinGroupChatManager**: Built-in manager that alternates speakers in round-robin fashion
- **MaximumIterationCount**: Controls the maximum number of agent turns before termination
- **Custom Managers**: Extend `RoundRobinGroupChatManager` or implement custom logic
- **Iterative Refinement**: Agents review and improve each other's contributions
- **Shared Context**: All participants see the full conversation history

::: zone-end

::: zone pivot="programming-language-python"

- **Flexible Orchestrator Strategies**: Choose between simple selectors, agent-based orchestrators, or custom logic
- **GroupChatBuilder**: Creates workflows with configurable speaker selection
- **with_select_speaker_func()**: Define custom Python functions for speaker selection
- **with_agent_orchestrator()**: Use an agent-based orchestrator for intelligent speaker coordination
- **GroupChatState**: Provides conversation state for selection decisions
- **Iterative Collaboration**: Agents build upon each other's contributions
- **Event Streaming**: Process `AgentResponseUpdateEvent` and `WorkflowOutputEvent` in real-time
- **list[ChatMessage] Output**: All orchestrations return a list of chat messages

::: zone-end

## Advanced: Custom Speaker Selection

::: zone pivot="programming-language-csharp"

You can implement custom manager logic by creating a custom group chat manager:

```csharp
public class ApprovalBasedManager : RoundRobinGroupChatManager
{
    private readonly string _approverName;

    public ApprovalBasedManager(IReadOnlyList<AIAgent> agents, string approverName)
        : base(agents)
    {
        _approverName = approverName;
    }

    // Override to add custom termination logic
    protected override ValueTask<bool> ShouldTerminateAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken = default)
    {
        var last = history.LastOrDefault();
        bool shouldTerminate = last?.AuthorName == _approverName &&
            last.Text?.Contains("approve", StringComparison.OrdinalIgnoreCase) == true;

        return ValueTask.FromResult(shouldTerminate);
    }
}

// Use custom manager in workflow
var workflow = AgentWorkflowBuilder
    .CreateGroupChatBuilderWith(agents =>
        new ApprovalBasedManager(agents, "Reviewer")
        {
            MaximumIterationCount = 10
        })
    .AddParticipants(writer, reviewer)
    .Build();
```

::: zone-end

::: zone pivot="programming-language-python"

You can implement sophisticated selection logic based on conversation state:

```python
def smart_selector(state: GroupChatState) -> str:
    """Select speakers based on conversation content and context."""
    conversation = state.conversation

    last_message = conversation[-1] if conversation else None

    # If no messages yet, start with Researcher
    if not last_message:
        return "Researcher"

    # Check last message content
    last_text = last_message.text.lower()

    # If researcher finished gathering info, switch to writer
    if "I have finished" in last_text and last_message.author_name == "Researcher":
        return "Writer"

    # Else continue with researcher until it indicates completion
    return "Researcher"

workflow = (
    GroupChatBuilder()
    .with_select_speaker_func(smart_selector, orchestrator_name="SmartOrchestrator")
    .participants([researcher, writer])
    .build()
)
```

::: zone-end

## Context Synchronization

As mentioned at the beginning of this guide, all agents in a group chat see the full conversation history.

Agents in Agent Framework relies on agent threads ([`AgentThread`](../../agents/multi-turn-conversation.md)) to manage context. In a group chat orchestration, agents **do not** share the same thread instance, but the orchestrator ensures that each agent's thread is synchronized with the complete conversation history before each turn. To achieve this, after each agent's turn, the orchestrator broadcasts the response to all other agents, making sure all participants have the latest context for their next turn.

<p align="center">
    <img src="../resources/images/orchestration-groupchat-synchronization.gif" alt="Group Chat Context Synchronization">
</p>

> [!TIP]
> Agents do not share the same thread instance because different [agent types](../../agents/agent-types/index.md) may have different implementations of the `AgentThread` abstraction. Sharing the same thread instance could lead to inconsistencies in how each agent processes and maintains context.

After broadcasting the response, the orchestrator then decide the next speaker and sends a request to the selected agent, which now has the full conversation history to generate its response.

## When to Use Group Chat

Group chat orchestration is ideal for:

- **Iterative Refinement**: Multiple rounds of review and improvement
- **Collaborative Problem-Solving**: Agents with complementary expertise working together
- **Content Creation**: Writer-reviewer workflows for document creation
- **Multi-Perspective Analysis**: Getting diverse viewpoints on the same input
- **Quality Assurance**: Automated review and approval processes

**Consider alternatives when:**

- You need strict sequential processing (use Sequential orchestration)
- Agents should work completely independently (use Concurrent orchestration)
- Direct agent-to-agent handoffs are needed (use Handoff orchestration)
- Complex dynamic planning is required (use Magentic orchestration)

## Next steps

> [!div class="nextstepaction"]
> [Magentic Orchestration](./magentic.md)
