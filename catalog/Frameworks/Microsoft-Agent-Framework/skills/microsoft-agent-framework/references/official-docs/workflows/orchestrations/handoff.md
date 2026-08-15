---
title: Microsoft Agent Framework Workflows Orchestrations - Handoff
description: In-depth look at Handoff Orchestrations in Microsoft Agent Framework Workflows.
author: TaoChenOSU
ms.topic: tutorial
ms.author: taochen
ms.date: 07/29/2026
ms.service: agent-framework
zone_pivot_groups: programming-languages
---

<!--
  Language parity table – keep in sync when adding/removing sections.

    | Section                              | C# | Python | Go | Notes                           |
    |--------------------------------------|:--:|:------:|:--:|--------------------------------|
    | Set Up the Client                    | ✅ |   ✅   | ❌ | No Go handoff builder          |
    | Define Specialized Agents            | ✅ |   ✅   | ❌ |                                |
    | Configure Handoff Rules              | ✅ |   ✅   | ❌ |                                |
    | Run Interactive Workflow             | ✅ |   ✅   | ❌ | Different patterns per language |
    | Autonomous Mode                      | ✅ |   ✅   | ❌ |                                |
    | Tool Approval (HITL)                 | ✅ |   ✅   | ❌ |                                |
    | Checkpointing                        | ❌ |   ✅   | ❌ |                                |
    | Sample Interaction                   | ✅ |   ✅   | ❌ |                                |
    | Context Synchronization              | ✅ |   ✅   | ❌ | Shared section                 |
    | Key Concepts                         | ✅ |   ✅   | ❌ |                                |
-->

# Microsoft Agent Framework Workflows Orchestrations - Handoff

Handoff orchestration allows agents to transfer control to one another based on the context or user request. Each agent can "handoff" the conversation to another agent with the appropriate expertise, ensuring that the right agent handles each part of the task. This is particularly useful in customer support, expert systems, or any scenario requiring dynamic delegation.

Internally, the handoff orchestration is implemented using a mesh topology where agents are connected directly without an orchestrator. Each agent can decide when to hand off the conversation based on predefined rules or the content of the messages.

<p align="center">
    <img src="../resources/images/orchestration-handoff.png" alt="Handoff Orchestration"/>
</p>

> [!NOTE]
> Handoff orchestration only supports `Agent` and the agents must support local tools execution.

## Differences Between Handoff and Agent-as-Tools

While agent-as-tools is commonly considered as a multi-agent pattern and it might look similar to handoff at first glance, there are fundamental differences between the two:

- **Control Flow**: In handoff orchestration, control is explicitly passed between agents based on defined rules. Each agent can decide to hand off the entire task to another agent. There is no central authority managing the workflow. In contrast, agent-as-tools involves a primary agent that delegates sub tasks to other agents and once the agent completes the sub task, control returns to the primary agent.
- **Task Ownership**: In handoff, the agent receiving the handoff takes full ownership of the task. In agent-as-tools, the primary agent retains overall responsibility for the task, while other agents are treated as tools to assist in specific subtasks.
- **Context Management**: In handoff orchestration, the conversation is handed off to another agent entirely. The receiving agent has full context of what has been done so far. In agent-as-tools, the primary agent manages the overall context and might provide only relevant information to the tool agents as needed.

## What You'll Learn

- How to create specialized agents for different domains
- How to configure handoff rules between agents
- How to build interactive workflows with dynamic agent routing
- How to handle multi-turn conversations with agent switching
- How to implement tool approval for sensitive operations (HITL)
- How to use checkpointing for durable handoff workflows

In handoff orchestration, agents can transfer control to one another based on context, allowing for dynamic routing and specialized expertise handling.

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

// 1) Set up the Azure OpenAI client
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

## Define Your Specialized Agents

Create domain-specific agents and a triage agent for routing:

```csharp
// 2) Create specialized agents
ChatClientAgent historyTutor = client.AsAIAgent(new ChatClientAgentOptions
{
    Id = "history-tutor",
    Name = "history_tutor",
    Description = "Specialist agent for historical questions",
    ChatOptions = new()
    {
        Instructions = "You provide assistance with historical queries. Explain important events and context clearly. Only respond about history."
    }
});

ChatClientAgent mathTutor = client.AsAIAgent(new ChatClientAgentOptions
{
    Id = "math-tutor",
    Name = "math_tutor",
    Description = "Specialist agent for math questions",
    ChatOptions = new()
    {
        Instructions = "You provide help with math problems. Explain your reasoning at each step and include examples. Only respond about math."
    }
});

ChatClientAgent triageAgent = client.AsAIAgent(new ChatClientAgentOptions
{
    Id = "triage-agent",
    Name = "triage_agent",
    Description = "Routes messages to the appropriate specialist agent",
    ChatOptions = new()
    {
        Instructions = "You determine which agent to use based on the user's homework question. ALWAYS handoff to another agent."
    }
});
```

> [!NOTE]
> If a Handoff workflow is checkpointed and later rebuilt, reuse the same unique `Id` (and, if set, the same `Name`) for every participating agent. Stable IDs are especially important when agents are scoped or reconstructed for each request because Handoff routing and checkpoint compatibility depend on the inner agent identities. For more information, see [Rehydrating from Checkpoints](../checkpoints.md#rehydrating-from-checkpoints).

## Configure Handoff Rules

Define which agents can hand off to which other agents:

```csharp
// 3) Build handoff workflow with routing rules
var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent)
    .WithHandoffs(triageAgent, [mathTutor, historyTutor]) // Triage can route to either specialist
    .WithHandoffs([mathTutor, historyTutor], triageAgent) // Both specialists can return to triage
    .Build();
```

## Run Interactive Handoff Workflow

Handle multi-turn conversations with dynamic agent switching:

```csharp
// 4) Process multi-turn conversations
List<ChatMessage> messages = new();

while (true)
{
    Console.Write("Q: ");
    string userInput = Console.ReadLine()!;
    messages.Add(new(ChatRole.User, userInput));

    // Execute workflow and process events
    await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, messages);
    await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

    string? lastExecutorId = null;
    List<ChatMessage> newMessages = new();
    await foreach (WorkflowEvent evt in run.WatchStreamAsync())
    {
        if (evt is AgentResponseUpdateEvent e)
        {
            if (e.ExecutorId != lastExecutorId)
            {
                lastExecutorId = e.ExecutorId;
                Console.WriteLine();
                Console.WriteLine(e.ExecutorId);
            }

            Console.Write(e.Update.Text);
        }
        else if (evt is WorkflowOutputEvent outputEvt)
        {
            newMessages = outputEvt.As<List<ChatMessage>>()!;
            break;
        }
    }

    // Add new messages to conversation history
    messages.AddRange(newMessages.Skip(messages.Count));
}
```

## Sample Interaction

```plaintext
Q: What is the derivative of x^2?
triage_agent: This is a math question. I'll hand this off to the math tutor.
math_tutor: The derivative of x^2 is 2x. Using the power rule, we bring down the exponent (2) and multiply it by the coefficient (1), then reduce the exponent by 1: d/dx(x^2) = 2x^(2-1) = 2x.

Q: Tell me about World War 2
triage_agent: This is a history question. I'll hand this off to the history tutor.
history_tutor: World War 2 was a global conflict from 1939 to 1945. It began when Germany invaded Poland and involved most of the world's nations. Key events included the Holocaust, Pearl Harbor attack, D-Day invasion, and ended with atomic bombs on Japan.

Q: Can you help me with calculus integration?
triage_agent: This is another math question. I'll route this to the math tutor.
math_tutor: I'd be happy to help with calculus integration! Integration is the reverse of differentiation. The basic power rule for integration is: ∫x^n dx = x^(n+1)/(n+1) + C, where C is the constant of integration.
```

## Autonomous Mode

By default, handoff orchestration is interactive: when an agent responds without handing off, the workflow returns control to you for the next user input. Enable **autonomous mode** to let an agent keep working without waiting for user input. When an agent does not hand off, the workflow feeds it a continuation prompt and invokes it again, until the agent hands off, a termination condition is met, or the per-agent turn limit is reached.

Enable it by calling `WithAutonomousMode()` on the handoff builder:

```csharp
var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent)
    .WithHandoffs(triageAgent, [mathTutor, historyTutor])
    .WithHandoffs([mathTutor, historyTutor], triageAgent)
    .WithAutonomousMode()
    .Build();
```

By default, each agent runs up to 50 autonomous turns, and each continuation uses the prompt `"User did not respond. Continue assisting autonomously."`. Override the turn limit and prompt as needed:

```csharp
var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent)
    .WithHandoffs(triageAgent, [mathTutor, historyTutor])
    .WithAutonomousMode(turnLimit: 10, continuationPrompt: "Continue assisting the user.")
    .Build();
```

Pass a list of agents to the `agents` parameter to enable autonomous mode for only a subset of participants. Agents not in the list always return control after a single response:

```csharp
var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent)
    .WithHandoffs(triageAgent, [mathTutor, historyTutor])
    .WithAutonomousMode(agents: [triageAgent]) // Only triageAgent runs autonomously
    .Build();
```

Combine autonomous mode with a termination condition to stop the loop when the conversation reaches a certain state:

```csharp
var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent)
    .WithHandoffs(triageAgent, [mathTutor, historyTutor])
    .WithAutonomousMode(turnLimit: 10)
    .WithTerminationCondition(conversation => conversation.Any(m => m.Text?.Contains("RESOLVED") == true))
    .Build();
```

## Advanced: Tool Approval in Handoff Workflows

Agents in a handoff workflow can use tools that require human approval before they run; useful for sensitive operations such as processing refunds, making purchases, or executing irreversible actions. Wrap the sensitive function with `ApprovalRequiredAIFunction`. When the agent tries to call it, the workflow pauses and emits a `RequestInfoEvent` containing a `ToolApprovalRequestContent`.

### Define Agents with Approval-Required Tools

```csharp
ChatClientAgent triageAgent = new(client,
    "You are frontline support. Route the customer to the right specialist.",
    "triage_agent",
    "Routes customers to specialists");

ChatClientAgent refundAgent = new(client,
    "You process refund requests.",
    "refund_agent",
    "Handles refund requests",
    [new ApprovalRequiredAIFunction(AIFunctionFactory.Create(ProcessRefund))]);
```

### Handle User Input and Tool Approval Requests

Two things can pause a handoff workflow: an agent finishing its turn and waiting for the next user message, and an approval-required tool call. Handle both in the same event loop; respond to a `RequestInfoEvent` approval with `SendResponseAsync`, and supply the next user message when the workflow returns control:

```csharp
var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent)
    .WithHandoffs(triageAgent, [refundAgent])
    .WithHandoffs([refundAgent], triageAgent)
    .Build();

List<ChatMessage> messages = [];

while (true)
{
    Console.Write("You: ");
    string userInput = Console.ReadLine()!;
    if (userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    messages.Add(new(ChatRole.User, userInput));

    await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, messages);
    await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

    List<ChatMessage> newMessages = [];
    await foreach (WorkflowEvent evt in run.WatchStreamAsync())
    {
        // An approval-required tool call pauses the workflow and emits a RequestInfoEvent.
        if (evt is RequestInfoEvent requestEvt &&
            requestEvt.Request.TryGetDataAs(out ToolApprovalRequestContent? approval))
        {
            var toolCall = (FunctionCallContent)approval.ToolCall;
            Console.Write($"Approve {toolCall.Name}? (y/n): ");
            bool approved = (Console.ReadLine() ?? "n").Trim().Equals("y", StringComparison.OrdinalIgnoreCase);
            await run.SendResponseAsync(requestEvt.Request.CreateResponse(approval.CreateResponse(approved)));
        }
        else if (evt is AgentResponseUpdateEvent update)
        {
            Console.Write(update.Update.Text);
        }
        else if (evt is WorkflowOutputEvent outputEvt)
        {
            newMessages = outputEvt.As<List<ChatMessage>>()!;
            break;
        }
    }

    // Control returns here after the agent responds without handing off. Merge the new
    // messages into the conversation and loop to collect the next user input.
    messages.AddRange(newMessages.Skip(messages.Count));
}
```

> [!NOTE]
> Tool approval works with `CreateHandoffBuilderWith()` out of the box; no extra builder configuration is needed. When an agent calls a tool wrapped with `ApprovalRequiredAIFunction`, the workflow automatically pauses and emits a `RequestInfoEvent`. The same `RequestInfoEvent` handling pattern is used across orchestrations; see the [`GroupChatToolApproval` sample](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/Agents/GroupChatToolApproval) for a complete runnable project.

::: zone-end

::: zone pivot="programming-language-python"

## Define a few tools for demonstration

```python
@tool
def process_refund(order_number: Annotated[str, "Order number to process refund for"]) -> str:
    """Simulated function to process a refund for a given order number."""
    return f"Refund processed successfully for order {order_number}."

@tool
def check_order_status(order_number: Annotated[str, "Order number to check status for"]) -> str:
    """Simulated function to check the status of a given order number."""
    return f"Order {order_number} is currently being processed and will ship in 2 business days."

@tool
def process_return(order_number: Annotated[str, "Order number to process return for"]) -> str:
    """Simulated function to process a return for a given order number."""
    return f"Return initiated successfully for order {order_number}. You will receive return instructions via email."
```

## Set Up the Chat Client

```python
import os

from agent_framework.foundry import FoundryChatClient
from azure.identity import AzureCliCredential

chat_client = FoundryChatClient(
    project_endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
    model=os.environ["FOUNDRY_MODEL"],
    credential=AzureCliCredential(),
)

```

## Define Your Specialized Agents

Create domain-specific agents with a coordinator for routing:

```python
# Create triage/coordinator agent
triage_agent = chat_client.as_agent(
    instructions=(
        "You are frontline support triage. Route customer issues to the appropriate specialist agents "
        "based on the problem described."
    ),
    description="Triage agent that handles general inquiries.",
    name="triage_agent",
)

# Refund specialist: Handles refund requests
refund_agent = chat_client.as_agent(
    instructions="You process refund requests.",
    description="Agent that handles refund requests.",
    name="refund_agent",
    # In a real application, an agent can have multiple tools; here we keep it simple
    tools=[process_refund],
)

# Order/shipping specialist: Resolves delivery issues
order_agent = chat_client.as_agent(
    instructions="You handle order and shipping inquiries.",
    description="Agent that handles order tracking and shipping issues.",
    name="order_agent",
    # In a real application, an agent can have multiple tools; here we keep it simple
    tools=[check_order_status],
)

# Return specialist: Handles return requests
return_agent = chat_client.as_agent(
    instructions="You manage product return requests.",
    description="Agent that handles return processing.",
    name="return_agent",
    # In a real application, an agent can have multiple tools; here we keep it simple
    tools=[process_return],
)
```

## Configure Handoff Rules

Build the handoff workflow using `HandoffBuilder`:

```python
from agent_framework.orchestrations import HandoffBuilder

# Build the handoff workflow
workflow = (
    HandoffBuilder(
        name="customer_support_handoff",
        participants=[triage_agent, refund_agent, order_agent, return_agent],
        termination_condition=lambda conversation: len(conversation) > 0 and "welcome" in conversation[-1].text.lower(),
    )
    .with_start_agent(triage_agent) # Triage receives initial user input
    .build()
)
```

By default, all agents can handoff to each other. For more advanced routing, you can configure handoffs:

```python
workflow = (
    HandoffBuilder(
        name="customer_support_handoff",
        participants=[triage_agent, refund_agent, order_agent, return_agent],
        termination_condition=lambda conversation: len(conversation) > 0 and "welcome" in conversation[-1].text.lower(),
    )
    .with_start_agent(triage_agent) # Triage receives initial user input
    # Triage cannot route directly to refund agent
    .add_handoff(triage_agent, [order_agent, return_agent])
    # Only the return agent can handoff to refund agent - users wanting refunds after returns
    .add_handoff(return_agent, [refund_agent])
    # All specialists can handoff back to triage for further routing
    .add_handoff(order_agent, [triage_agent])
    .add_handoff(return_agent, [triage_agent])
    .add_handoff(refund_agent, [triage_agent])
    .build()
)
```

> [!NOTE]
> Even with custom handoff rules, all agents are still connected in a mesh topology. This is because agents need to share context with each other to maintain conversation history (see [Context Synchronization](#context-synchronization) for more details). The handoff rules only govern which agents can take over the conversation next.

## Run Handoff Agent Interaction

Unlike other orchestrations, handoff is interactive because an agent may not decide to handoff after every turn. If an agent doesn't handoff, human input is required to continue the conversation. See [Autonomous Mode](#autonomous-mode) for bypassing this requirement. In other orchestrations, after an agent responds, the control either goes to the orchestrator or the next agent.

When an agent in a handoff workflow decides not to handoff (a handoff is triggered by a special tool call), the workflow emits a `WorkflowEvent` with `type="request_info"` and a `HandoffAgentUserRequest` payload containing the agent's most recent messages. The user must respond to this request to continue the workflow.

```python
from agent_framework import WorkflowEvent
from agent_framework.orchestrations import HandoffAgentUserRequest

# Start workflow with initial user message
events = [event async for event in workflow.run("I need help with my order", stream=True)]

# Process events and collect pending input requests
pending_requests = []
for event in events:
    if event.type == "request_info" and isinstance(event.data, HandoffAgentUserRequest):
        pending_requests.append(event)
        request_data = event.data
        print(f"Agent {event.executor_id} is awaiting your input")
        # The request contains the most recent messages generated by the
        # agent requesting input
        for msg in request_data.agent_response.messages[-3:]:
            print(f"{msg.author_name}: {msg.text}")

# Interactive loop: respond to requests
while pending_requests:
    user_input = input("You: ")

    # Send responses to all pending requests
    responses = {req.request_id: HandoffAgentUserRequest.create_response(user_input) for req in pending_requests}
    # You can also send a `HandoffAgentUserRequest.terminate()` to end the workflow early
    events = [event async for event in workflow.run(responses=responses, stream=True)]

    # Process new events
    pending_requests = []
    for event in events:
        # Check for new input requests
```

## Autonomous Mode

The Handoff orchestration is designed for interactive scenarios where human input is required when an agent decides not to handoff. However, as an **experimental feature**, you can enable "autonomous mode" to allow the workflow to continue without human intervention. In this mode, when an agent decides not to handoff, the workflow automatically sends a default response (e.g.`User did not respond. Continue assisting autonomously.`) to the agent, allowing it to continue the conversation.

> [!TIP]
> Why is Handoff orchestration inherently interactive? Unlike other orchestrations where there is only one path to follow after an agent responds (e.g. back to orchestrator or next agent), in a Handoff orchestration, the agent has the option to either handoff to another agent or continue assisting the user itself. And because handoffs are achieved through tool calls, if an agent does not call a handoff tool but generates a response instead, the workflow won't know what to do next but to delegate back to the user for further input. It is also not possible to force an agent to always handoff by requiring it to call the handoff tool because the agent won't be able to generate meaningful responses otherwise.

**Autonomous Mode** is enabled by calling `with_autonomous_mode()` on the `HandoffBuilder`. This configures the workflow to automatically respond to input requests with a default message, allowing the agent to continue without waiting for human input.

```python
workflow = (
    HandoffBuilder(
        name="autonomous_customer_support",
        participants=[triage_agent, refund_agent, order_agent, return_agent],
    )
    .with_start_agent(triage_agent)
    .with_autonomous_mode()
    .build()
)
```

You can also enable autonomous mode on only a subset of agents by passing a list of agent instances to `with_autonomous_mode()`.

```python
workflow = (
    HandoffBuilder(
        name="partially_autonomous_support",
        participants=[triage_agent, refund_agent, order_agent, return_agent],
    )
    .with_start_agent(triage_agent)
    .with_autonomous_mode(agents=[triage_agent])  # Only triage_agent runs autonomously
    .build()
)
```

You can customize the default response message.

```python
workflow = (
    HandoffBuilder(
        name="custom_autonomous_support",
        participants=[triage_agent, refund_agent, order_agent, return_agent],
    )
    .with_start_agent(triage_agent)
    .with_autonomous_mode(
        agents=[triage_agent],
        prompts={triage_agent.name: "Continue with your best judgment as the user is unavailable."},
    )
    .build()
)
```

You can customize the number of turns an agent can run autonomously before requiring human input. This can prevent the workflow from running indefinitely without user involvement.

```python
workflow = (
    HandoffBuilder(
        name="limited_autonomous_support",
        participants=[triage_agent, refund_agent, order_agent, return_agent],
    )
    .with_start_agent(triage_agent)
    .with_autonomous_mode(
        agents=[triage_agent],
        turn_limits={triage_agent.name: 3},  # Max 3 autonomous turns
    )
    .build()
)
```

## Advanced: Tool Approval in Handoff Workflows

Handoff workflows can include agents with tools that require human approval before execution. This is useful for sensitive operations like processing refunds, making purchases, or executing irreversible actions.

### Define Tools with Approval Required

```python
from typing import Annotated
from agent_framework import tool

@tool(approval_mode="always_require")
def process_refund(order_number: Annotated[str, "Order number to process refund for"]) -> str:
    """Simulated function to process a refund for a given order number."""
    return f"Refund processed successfully for order {order_number}."
```

### Create Agents with Approval-Required Tools

```python
import os
from agent_framework import Agent
from agent_framework.openai import OpenAIChatCompletionClient
from azure.identity import AzureCliCredential

chat_client = OpenAIChatCompletionClient(
    model=os.environ["AZURE_OPENAI_CHAT_COMPLETION_MODEL"],
    azure_endpoint=os.environ["AZURE_OPENAI_ENDPOINT"],
    api_version=os.getenv("AZURE_OPENAI_API_VERSION"),
    credential=AzureCliCredential(),
)

triage_agent = chat_client.as_agent(
    instructions=(
        "You are frontline support triage. Route customer issues to the appropriate specialist agents "
        "based on the problem described."
    ),
    description="Triage agent that handles general inquiries.",
    name="triage_agent",
)

refund_agent = chat_client.as_agent(
    instructions="You process refund requests.",
    description="Agent that handles refund requests.",
    name="refund_agent",
    tools=[process_refund],
)

order_agent = chat_client.as_agent(
    instructions="You handle order and shipping inquiries.",
    description="Agent that handles order tracking and shipping issues.",
    name="order_agent",
    tools=[check_order_status],
)
```

### Handle Both User Input and Tool Approval Requests

```python
from agent_framework import (
    Content,
    WorkflowEvent,
)
from agent_framework.orchestrations import HandoffBuilder, HandoffAgentUserRequest

workflow = (
    HandoffBuilder(
        name="support_with_approvals",
        participants=[triage_agent, refund_agent, order_agent],
    )
    .with_start_agent(triage_agent)
    .build()
)

pending_requests: list[WorkflowEvent] = []

# Start workflow
async for event in workflow.run("My order 12345 arrived damaged. I need a refund.", stream=True):
    if event.type == "request_info":
        pending_requests.append(event)

# Process pending requests - could be user input OR tool approval
while pending_requests:
    responses: dict[str, object] = {}

    for request in pending_requests:
        if isinstance(request.data, HandoffAgentUserRequest):
            # Agent needs user input
            print(f"Agent {request.executor_id} asks:")
            for msg in request.data.agent_response.messages[-2:]:
                print(f"  {msg.author_name}: {msg.text}")

            user_input = input("You: ")
            responses[request.request_id] = HandoffAgentUserRequest.create_response(user_input)

        elif isinstance(request.data, Content) and request.data.type == "function_approval_request":
            # Agent wants to call a tool that requires approval
            func_call = request.data.function_call
            args = func_call.parse_arguments() or {}

            print(f"\nTool approval requested: {func_call.name}")
            print(f"Arguments: {args}")

            approval = input("Approve? (y/n): ").strip().lower() == "y"
            responses[request.request_id] = request.data.to_function_approval_response(approved=approval)

    # Send all responses and collect new requests
    pending_requests = []
    async for event in workflow.run(responses=responses, stream=True):
        if event.type == "request_info":
            pending_requests.append(event)
        elif event.type == "output":
            print("\nWorkflow completed!")
```

### With Checkpointing for Durable Workflows

For long-running workflows where tool approvals may happen hours or days later, use checkpointing:

```python
from agent_framework import FileCheckpointStorage

storage = FileCheckpointStorage(storage_path="./checkpoints")

workflow = (
    HandoffBuilder(
        name="durable_support",
        participants=[triage_agent, refund_agent, order_agent],
        checkpoint_storage=storage,
    )
    .with_start_agent(triage_agent)
    .build()
)

# Initial run - workflow pauses when approval is needed
pending_requests = []
async for event in workflow.run("I need a refund for order 12345", stream=True):
    if event.type == "request_info":
        pending_requests.append(event)

# Process can exit here - checkpoint is saved automatically

# Later: Resume from checkpoint and provide approval
checkpoints = await storage.list_checkpoints(workflow_name="durable_support")
latest = sorted(checkpoints, key=lambda c: c.timestamp, reverse=True)[0]

# Step 1: Restore checkpoint to reload pending requests
restored_requests = []
async for event in workflow.run(checkpoint_id=latest.checkpoint_id, stream=True):
    if event.type == "request_info":
        restored_requests.append(event)

# Step 2: Send responses
responses = {}
for req in restored_requests:
    if isinstance(req.data, Content) and req.data.type == "function_approval_request":
        responses[req.request_id] = req.data.to_function_approval_response(approved=True)
    elif isinstance(req.data, HandoffAgentUserRequest):
        responses[req.request_id] = HandoffAgentUserRequest.create_response("Yes, please process the refund.")

async for event in workflow.run(responses=responses, stream=True):
    if event.type == "output":
        print("Refund workflow completed!")
```

## Sample Interaction

```plaintext
User: I need help with my order

triage_agent: I'd be happy to help you with your order. Could you please provide more details about the issue?

User: My order 1234 arrived damaged

triage_agent: I'm sorry to hear that your order arrived damaged. I will connect you with a specialist.

support_agent: I'm sorry about the damaged order. To assist you better, could you please:
- Describe the damage
- Would you prefer a replacement or refund?

User: I'd like a refund

triage_agent: I'll connect you with the refund specialist.

refund_agent: I'll process your refund for order 1234. Here's what will happen next:
1. Verification of the damaged items
2. Refund request submission
3. Return instructions if needed
4. Refund processing within 5-10 business days

Could you provide photos of the damage to expedite the process?
````

::: zone-end

## Context Synchronization

Agents in Agent Framework relies on agent sessions ([`AgentSession`](../../concepts/agents/conversations/session.md)) to manage context. In a Handoff orchestration, agents **do not** share the same session instance, participants are responsible for ensuring context consistency. To achieve this, participants are designed to broadcast their responses or user inputs received to all others in the workflow whenever they generate a response, making sure all participants have the latest context for their next turn.

<p align="center">
    <img src="../resources/images/orchestration-handoff.png" alt="Handoff Context Synchronization">
</p>

> [!NOTE]
> Tool related contents, including handoff tool calls, are not broadcasted to other agents. Only user and agent messages are synchronized across all participants.

> [!TIP]
> Agents do not share the same session instance because different [agent types](../../integrations/by-component/model-providers/index.md) may have different implementations of the `AgentSession` abstraction. Sharing the same session instance could lead to inconsistencies in how each agent processes and maintains context.

After broadcasting the response, the participant then checks whether it needs to handoff the conversation to another agent. If so, it sends a request to the selected agent to take over the conversation. Otherwise, it requests user input or continues autonomously based on the workflow configuration.

## Key Concepts

::: zone pivot="programming-language-csharp"

- **Dynamic Routing**: Agents can decide which agent should handle the next interaction based on context
- **AgentWorkflowBuilder.CreateHandoffBuilderWith()**: Defines the initial agent that starts the workflow
- **WithHandoff()** and **WithHandoffs()**: Configures handoff rules between specific agents
- **Context Preservation**: Full conversation history is maintained across all handoffs
- **Multi-turn Support**: Supports ongoing conversations with seamless agent switching
- **Specialized Expertise**: Each agent focuses on their domain while collaborating through handoffs
- **WithAutonomousMode()**: Lets agents continue without waiting for user input, up to a per-agent turn limit or until a termination condition is met
- **Tool Approval (HITL)**: Wrap sensitive tools with `ApprovalRequiredAIFunction`; the workflow pauses and emits a `RequestInfoEvent` with `ToolApprovalRequestContent`, which you answer via `SendResponseAsync`

::: zone-end

::: zone pivot="programming-language-python"

- **Dynamic Routing**: Agents can decide which agent should handle the next interaction based on context
- **HandoffBuilder**: Creates workflows with automatic handoff tool registration
- **with_start_agent()**: Defines which agent receives user input first
- **add_handoff()**: Configures specific handoff relationships between agents
- **Output**: By default, `output_from` is set to **all participants**, so every agent's response surfaces as an `"output"` (terminal) event (`AgentResponse` in non-streaming mode, `AgentResponseUpdate` in streaming mode). To designate specific agents as intermediate sources instead, pass `intermediate_output_from=[agent_a, agent_b]` to `HandoffBuilder` — this implicitly demotes those agents from the default output set so their responses become `"intermediate"` events. There is no overlap error; the demotion is silent and intentional.
- **Context Preservation**: Full conversation history is maintained across all handoffs
- **Request/Response Cycle**: Workflow requests user input, processes responses, and continues until termination condition is met
- **Tool Approval**: Use `@tool(approval_mode="always_require")` for sensitive operations that need human approval
- **Function Approval Handling**: When an agent calls a tool requiring approval, a `Content` object with type `"function_approval_request"` is emitted; use `to_function_approval_response(approved=...)` to respond
- **Checkpointing**: Pass `checkpoint_storage=` to `HandoffBuilder` for durable workflows that can pause and resume across process restarts
- **Specialized Expertise**: Each agent focuses on their domain while collaborating through handoffs

::: zone-end

::: zone pivot="programming-language-go"

> [!NOTE]
> Go support for this feature is coming soon. See the [Agent Framework Go repository](https://github.com/microsoft/agent-framework-go) for the latest status.

::: zone-end
## The Handoff Agent Executor

Unlike standard workflows where agents are wrapped in a general-purpose [Agent Executor](../../concepts/workflows/advanced/agent-executor.md), handoff orchestration uses a specialized `HandoffAgentExecutor`. This executor extends the base agent executor with handoff-specific capabilities:

- **Handoff tool injection** — automatically registers handoff tools on each agent based on the configured handoff rules, so the agent can invoke a tool to transfer control.
- **Handoff function detection** — inspects the agent's response for handoff tool calls and routes control to the target agent.
- **Tool call filtering** — filters out handoff-related function calls and tool results from the conversation history before forwarding to the next agent, preventing internal workflow mechanics from confusing the model.

## Next steps

> [!div class="nextstepaction"]
> [Group Chat Orchestration](group-chat.md)
