---
title: Microsoft Agent Framework Workflows Orchestrations - Group Chat
description: In-depth look at Group Chat Orchestrations in Microsoft Agent Framework Workflows.
zone_pivot_groups: programming-languages
author: moonbox3
ms.topic: tutorial
ms.author: evmattso
ms.date: 07/01/2026
ms.service: agent-framework
---

<!--
  Language parity table – keep in sync when adding/removing sections.

    | Section                                        | C# | Python | Go | Notes           |
    |------------------------------------------------|:--:|:------:|:--:|-----------------|
    | Set Up the Client                              | ✅ |   ✅   | ✅ |                 |
    | Define Your Agents                             | ✅ |   ✅   | ✅ |                 |
    | Configure Group Chat (Round-Robin / Selector)  | ✅ |   ✅   | ✅ |                 |
    | Configure Group Chat (Agent-Based Orchestrator)| ❌ |   ✅   | ❌ | Python-specific |
    | Run the Workflow                               | ✅ |   ✅   | ✅ |                 |
    | Sample Interaction                             | ✅ |   ✅   | ✅ |                 |
    | Key Concepts                                   | ✅ |   ✅   | ✅ |                 |
    | Advanced: Custom Speaker Selection             | ✅ |   ✅   | ✅ |                 |
    | Intermediate Outputs                           | ❌ |   ✅   | ✅ | Python/Go-specific |
    | Context Synchronization                        | ✅ |   ✅   | ✅ | Shared section  |
    | When to Use Group Chat                         | ✅ |   ✅   | ✅ | Shared section  |
-->

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
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;

// Set up the Azure OpenAI client
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ??
    throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-4o-mini";
var client = new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential())
    .GetProjectOpenAIClient()
    .GetProjectResponsesClient()
    .AsIChatClient(deploymentName);
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

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

await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, messages);
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
import os

from agent_framework.foundry import FoundryChatClient
from azure.identity import AzureCliCredential

# Initialize the Azure OpenAI client
client = FoundryChatClient(
    project_endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
    model=os.environ["FOUNDRY_MODEL"],
    credential=AzureCliCredential(),
)
```

## Define Your Agents

Create specialized agents with distinct roles:

```python
from agent_framework import Agent

# Create a researcher agent
researcher = Agent(
    client=client,
    name="Researcher",
    description="Collects relevant background information.",
    instructions="Gather concise facts that help answer the question. Be brief and factual.",
)

# Create a writer agent
writer = Agent(
    client=client,
    name="Writer",
    description="Synthesizes polished answers using gathered information.",
    instructions="Compose clear, structured answers using any notes provided. Be comprehensive.",
)
```

## Configure Group Chat with Simple Selector

Build a group chat with custom speaker selection logic:

```python
from agent_framework.orchestrations import GroupChatBuilder, GroupChatState

def round_robin_selector(state: GroupChatState) -> str:
    """A round-robin selector function that picks the next speaker based on the current round index."""

    participant_names = list(state.participants.keys())
    return participant_names[state.current_round % len(participant_names)]


# Build the group chat workflow
workflow = GroupChatBuilder(
    participants=[researcher, writer],
    termination_condition=lambda conversation: len(conversation) >= 4,
    intermediate_output_from=[researcher, writer],
    selection_func=round_robin_selector,
).build()
```

## Configure Group Chat with Agent-Based Orchestrator

Alternatively, use an agent-based orchestrator for intelligent speaker selection. The orchestrator is a full `Agent` with access to tools, context, and observability:

```python
# Create orchestrator agent for speaker selection
orchestrator_agent = Agent(
    name="Orchestrator",
    description="Coordinates multi-agent collaboration by selecting speakers",
    instructions="""
You coordinate a team conversation to solve the user's task.

Guidelines:
- Start with Researcher to gather information
- Then have Writer synthesize the final answer
- Only finish after both have contributed meaningfully
""",
    client=client,
)

# Build group chat with agent-based orchestrator
workflow = GroupChatBuilder(
    participants=[researcher, writer],
    # Set a hard termination condition: stop after 4 assistant messages
    # The agent orchestrator will intelligently decide when to end before this limit but just in case
    termination_condition=lambda messages: sum(1 for msg in messages if msg.role == "assistant") >= 4,
    orchestrator_agent=orchestrator_agent,
    intermediate_output_from=[researcher, writer],
).build()
```

## Run the Group Chat Workflow

Execute the workflow and process streaming participant updates. The non-streaming terminal output is an `AgentResponse`; streaming terminal output is emitted as `AgentResponseUpdate` chunks.

```python
from agent_framework import AgentResponseUpdate, Message

task = "What are the key benefits of async/await in Python?"

print(f"Task: {task}\n")
print("=" * 80)

last_author: str | None = None
# Run the workflow with streaming enabled
stream = workflow.run(task, stream=True)
async for event in stream:
    if event.type in ("intermediate", "output") and isinstance(event.data, AgentResponseUpdate):
        # Print streaming agent updates
        author = event.data.author_name
        if author != last_author:
            if last_author is not None:
                print()
            print(f"[{author}]:", end=" ", flush=True)
            last_author = author
        print(event.data.text, end="", flush=True)
result = await stream.get_final_response()
if outputs := result.get_outputs():
    print("\n\n" + "=" * 80)
    print("Final Response:")
    print(outputs[-1])

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

::: zone pivot="programming-language-go"

## Set Up Foundry Configuration

```go
endpoint := os.Getenv("FOUNDRY_PROJECT_ENDPOINT")
model := cmp.Or(os.Getenv("FOUNDRY_MODEL"), "gpt-4o-mini")

token, err := azidentity.NewDefaultAzureCredential(nil)
if err != nil {
    return err
}
```

> [!WARNING]
> `azidentity.NewDefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential, such as `azidentity.NewManagedIdentityCredential`, to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

## Define Your Agents

Create specialized agents with distinct roles in the conversation:

```go
copywriter := foundryprovider.NewAgent(
    endpoint,
    token,
    foundryprovider.ModelDeployment(model),
    foundryprovider.AgentConfig{
        Instructions: "You are a creative copywriter. Generate catchy slogans and marketing copy. Be concise and impactful.",
        Config:      agent.Config{Name: "CopyWriter"},
    },
)

reviewer := foundryprovider.NewAgent(
    endpoint,
    token,
    foundryprovider.ModelDeployment(model),
    foundryprovider.AgentConfig{
        Instructions: "You are a marketing reviewer. Evaluate slogans for clarity, impact, and brand alignment. Provide constructive feedback or approval.",
        Config:      agent.Config{Name: "Reviewer"},
    },
)
```

## Configure Group Chat with Round-Robin Manager

Build the group chat workflow with `agentworkflow.NewGroupChatWorkflowBuilder`. The builder takes a manager factory and the participating agents. `NewRoundRobinGroupChatManager` selects each agent in turn and stops after the configured maximum number of participant turns.

```go
managerFactory := func(agents []*agent.Agent) *agentworkflow.GroupChatManager {
    return agentworkflow.NewRoundRobinGroupChatManager(
        agents,
        agentworkflow.RoundRobinGroupChatOptions{MaximumIterationCount: 5},
    )
}

wf, err := agentworkflow.NewGroupChatWorkflowBuilder(managerFactory, copywriter, reviewer).
    WithName("Marketing Review Group Chat").
    WithDescription("A copywriter and reviewer collaborate on marketing copy.").
    Build()
if err != nil {
    return err
}
```

## Run the Group Chat Workflow

Run the workflow with a user message and a turn token. When event emission is enabled, participant updates arrive as intermediate output events and the final transcript arrives as a terminal output event.

```go
run, err := inproc.Default.RunStreaming(ctx, wf, []*message.Message{
    message.NewText("Create a slogan for an eco-friendly electric vehicle."),
})
if err != nil {
    return err
}
defer run.Close(ctx)

emitEvents := true
if err := run.SendMessage(ctx, workflow.TurnToken{EmitEvents: &emitEvents}); err != nil {
    return err
}

lastExecutorID := ""
for evt, err := range run.WatchStream(ctx) {
    if err != nil {
        return err
    }

    switch e := evt.(type) {
    case workflow.OutputEvent:
        switch value := e.Output.(type) {
        case *agent.ResponseUpdate:
            if e.ExecutorID != lastExecutorID {
                lastExecutorID = e.ExecutorID
                fmt.Printf("\n[%s]: ", e.ExecutorID)
            }
            fmt.Print(value.String())
        case []*message.Message:
            fmt.Println("\n\n=== Final Conversation ===")
            for _, msg := range value {
                author := msg.AuthorName
                if author == "" {
                    author = string(msg.Role)
                }
                fmt.Printf("%s: %s\n", author, msg.String())
            }
        }
    case workflow.ErrorEvent:
        return e.Error
    case workflow.ExecutorFailedEvent:
        return fmt.Errorf("executor %q failed: %w", e.ExecutorID, e.Error)
    }
}
```

## Sample Interaction

```plaintext
[CopyWriter]: "Pure Power, Zero Impact" - Experience electric performance without compromise.

[Reviewer]: This is clear and memorable. It communicates performance and sustainability directly.
Approved.

[CopyWriter]: The final slogan is: "Pure Power, Zero Impact" - Experience electric performance
without compromise.

=== Final Conversation ===
user: Create a slogan for an eco-friendly electric vehicle.
CopyWriter: "Pure Power, Zero Impact" - Experience electric performance without compromise.
Reviewer: This is clear and memorable. It communicates performance and sustainability directly. Approved.
CopyWriter: The final slogan is: "Pure Power, Zero Impact" - Experience electric performance without compromise.
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

- **Flexible Orchestrator Strategies**: Choose between simple selectors, agent-based orchestrators, or custom logic via constructor parameters (`selection_func`, `orchestrator_agent`, or `orchestrator`).
- **GroupChatBuilder**: Creates workflows with configurable speaker selection
- **GroupChatState**: Provides conversation state for selection decisions
- **Iterative Collaboration**: Agents build upon each other's contributions
- **AgentResponse Output**: The terminal output is an `AgentResponse` containing the orchestrator's completion message
- **Event Streaming**: Process `AgentResponseUpdate` events in real-time via `workflow.run(task, stream=True)`
- **Intermediate Outputs**: Pass `intermediate_output_from=[participant, ...]` to surface each listed participant's output as `"intermediate"` events, in addition to the orchestrator's terminal `"output"` event

::: zone-end

::: zone pivot="programming-language-go"

- **GroupChatWorkflowBuilder**: Creates a star-topology workflow with a group chat host in the center and hosted agents as participants
- **GroupChatManager**: Selects the next participant, can update broadcast history, and can terminate the conversation
- **NewRoundRobinGroupChatManager**: Built-in manager that alternates participants in round-robin order
- **RoundRobinGroupChatOptions**: Configures the maximum number of participant turns and an optional termination function
- **Output Events**: By default, participant outputs are intermediate events and the group chat host yields the terminal transcript
- **Custom Managers**: Implement `SelectNextAgent` and optional lifecycle callbacks for custom speaker selection or checkpointed state

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
    if "i have finished" in last_text and last_message.author_name == "Researcher":
        return "Writer"

    # Else continue with researcher until it indicates completion
    return "Researcher"

workflow = GroupChatBuilder(
    participants=[researcher, writer],
    selection_func=smart_selector,
).build()
```

> [!IMPORTANT]
> When using a custom implementation of `BaseGroupChatOrchestrator` for advanced scenarios, all properties must be set, including `participant_registry`, `max_rounds`, and `termination_condition`. `max_rounds` and `termination_condition` set in the builder will be ignored.

## Intermediate Outputs

By default, only the orchestrator's final output surfaces as a workflow `"output"` (terminal) event. Pass `intermediate_output_from` with the participants you want to designate as intermediate sources to also surface their individual outputs as `"intermediate"` events:

```python
workflow = GroupChatBuilder(
    participants=[researcher, writer],
    termination_condition=lambda conversation: len(conversation) >= 4,
    selection_func=round_robin_selector,
    intermediate_output_from=[researcher, writer],
).build()
```

::: zone-end

::: zone pivot="programming-language-go"

Implement custom speaker selection by returning a `GroupChatManager` from the builder's manager factory:

```go
type approvalManager struct {
    agents []*agent.Agent
}

func newApprovalManager(agents []*agent.Agent) *agentworkflow.GroupChatManager {
    manager := &approvalManager{agents: agents}
    return &agentworkflow.GroupChatManager{
        SelectNextAgent: manager.selectNextAgent,
        ShouldTerminate: manager.shouldTerminate,
    }
}

func (m *approvalManager) selectNextAgent(_ context.Context, history []*message.Message) (*agent.Agent, error) {
    last := lastAssistantMessage(history)
    if last == nil || last.AuthorName == "Reviewer" {
        return m.agentByName("CopyWriter")
    }
    return m.agentByName("Reviewer")
}

func (m *approvalManager) shouldTerminate(_ context.Context, history []*message.Message, iterationCount int) (bool, error) {
    if iterationCount >= 10 {
        return true, nil
    }
    last := lastAssistantMessage(history)
    return last != nil &&
        last.AuthorName == "Reviewer" &&
        strings.Contains(strings.ToLower(last.String()), "approve"), nil
}

func (m *approvalManager) agentByName(name string) (*agent.Agent, error) {
    for _, currentAgent := range m.agents {
        if currentAgent.Name() == name {
            return currentAgent, nil
        }
    }
    return nil, fmt.Errorf("agent %q is not part of the group chat", name)
}

func lastAssistantMessage(history []*message.Message) *message.Message {
    for i := len(history) - 1; i >= 0; i-- {
        if history[i].Role == message.RoleAssistant {
            return history[i]
        }
    }
    return nil
}

wf, err := agentworkflow.NewGroupChatWorkflowBuilder(newApprovalManager, copywriter, reviewer).
    WithName("Approval Group Chat").
    Build()
```

`GroupChatManager` also supports `UpdateHistory`, `Reset`, `OnCheckpoint`, and `OnCheckpointRestored` callbacks for advanced managers that filter broadcast messages or persist manager-owned state.

## Intermediate Outputs

By default, `GroupChatWorkflowBuilder` emits participant outputs as intermediate workflow outputs and emits the accumulated conversation transcript as the terminal output. Use `OutputEvent.IsIntermediate()` to distinguish participant updates from the final transcript:

```go
if output, ok := evt.(workflow.OutputEvent); ok {
    if output.IsIntermediate() {
        fmt.Printf("intermediate from %s: %v\n", output.ExecutorID, output.Output)
        return nil
    }

    fmt.Printf("terminal output: %v\n", output.Output)
}
```

Calling `WithOutputFrom` or `WithIntermediateOutputFrom` on the group chat builder switches to explicit output designation. Use these methods when you want selected participant outputs instead of the default final transcript plus all participant intermediate outputs.

::: zone-end
## Context Synchronization

As mentioned at the beginning of this guide, all agents in a group chat see the full conversation history.

Agents in Agent Framework rely on agent sessions ([`AgentSession`](../../concepts/agents/conversations/session.md)) to manage context. In a group chat orchestration, agents **do not** share the same session instance, but the orchestrator ensures that each agent's session is synchronized with the complete conversation history before each turn. To achieve this, after each agent's turn, the orchestrator broadcasts the response to all other agents, making sure all participants have the latest context for their next turn.

<p align="center">
    <img src="../resources/images/orchestration-groupchat.png" alt="Group Chat Context Synchronization">
</p>

> [!TIP]
> Agents do not share the same session instance because different [agent types](../../integrations/by-component/model-providers/index.md) may have different implementations of the `AgentSession` abstraction. Sharing the same session instance could lead to inconsistencies in how each agent processes and maintains context.

After broadcasting the response, the orchestrator decides the next speaker and sends a request to the selected agent, which now has the full conversation history to generate its response.

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
