---
title: Microsoft Agent Framework Workflows Orchestrations - Magentic
description: In-depth look at Magentic Orchestrations in Microsoft Agent Framework Workflows.
zone_pivot_groups: programming-languages
author: TaoChenOSU
ms.topic: tutorial
ms.author: taochen
ms.date: 09/12/2025
ms.service: agent-framework
---

# Microsoft Agent Framework Workflows Orchestrations - Magentic

Magentic orchestration is designed based on the [Magentic-One](https://microsoft.github.io/autogen/stable/user-guide/agentchat-user-guide/magentic-one.html) system invented by AutoGen. It is a flexible, general-purpose multi-agent pattern designed for complex, open-ended tasks that require dynamic collaboration. In this pattern, a dedicated Magentic manager coordinates a team of specialized agents, selecting which agent should act next based on the evolving context, task progress, and agent capabilities.

The Magentic manager maintains a shared context, tracks progress, and adapts the workflow in real time. This enables the system to break down complex problems, delegate subtasks, and iteratively refine solutions through agent collaboration. The orchestration is especially well-suited for scenarios where the solution path is not known in advance and might require multiple rounds of reasoning, research, and computation.

<p align="center">
    <img src="../resources/images/orchestration-magentic.png" alt="Magentic Orchestration">
</p>

> [!TIP]
> The Magentic orchestration has the same archetecture as the [Group Chat orchestration](./group-chat.md) pattern, with a very powerful manager that uses planning to coordinate agent collaboration. If your scenario requires simpler coordination without complex planning, consider using the Group Chat pattern instead.

> [!NOTE]
> In the [Magentic-One](https://microsoft.github.io/autogen/stable/user-guide/agentchat-user-guide/magentic-one.html) paper, 4 highly specialized agents are designed to solve a very specific set of tasks. In the Magentic orchestration in Agent Framework, you can define your own specialized agents to suit your specific application needs. However, it is untested how well the Magentic orchestration will perform outside of the original Magentic-One design.

## What You'll Learn

- How to set up a Magentic manager to coordinate multiple specialized agents
- How to handle streaming events with `AgentRunUpdateEvent`
- How to implement human-in-the-loop plan review
- How to track agent collaboration and progress through complex tasks

## Define Your Specialized Agents

::: zone pivot="programming-language-csharp"

Coming soon...

::: zone-end

::: zone pivot="programming-language-python"

In Magentic orchestration, you define specialized agents that the manager can dynamically select based on task requirements:

```python
from agent_framework import ChatAgent, HostedCodeInterpreterTool
from agent_framework.openai import OpenAIChatClient, OpenAIResponsesClient

researcher_agent = ChatAgent(
    name="ResearcherAgent",
    description="Specialist in research and information gathering",
    instructions=(
        "You are a Researcher. You find information without additional computation or quantitative analysis."
    ),
    # This agent requires the gpt-4o-search-preview model to perform web searches
    chat_client=OpenAIChatClient(model_id="gpt-4o-search-preview"),
)

coder_agent = ChatAgent(
    name="CoderAgent",
    description="A helpful assistant that writes and executes code to process and analyze data.",
    instructions="You solve questions using code. Please provide detailed analysis and computation process.",
    chat_client=OpenAIResponsesClient(),
    tools=HostedCodeInterpreterTool(),
)

# Create a manager agent for orchestration
manager_agent = ChatAgent(
    name="MagenticManager",
    description="Orchestrator that coordinates the research and coding workflow",
    instructions="You coordinate a team to complete complex tasks efficiently.",
    chat_client=OpenAIChatClient(),
)
```

## Build the Magentic Workflow

Use `MagenticBuilder` to configure the workflow with a standard manager:

```python
from agent_framework import MagenticBuilder

workflow = (
    MagenticBuilder()
    .participants([researcher_agent, coder_agent])
    .with_standard_manager(
        agent=manager_agent,
        max_round_count=10,
        max_stall_count=3,
        max_reset_count=2,
    )
    .build()
)
```

> [!TIP]
> A standard manager is implemented based on the Magentic-One design, with fixed prompts taken from the original paper. You can customize the manager's behavior by passing in your own prompts to `with_standard_manager()`. To further customize the manager, you can also implement your own manager by sub classing the `MagenticManagerBase` class.

## Run the Workflow with Event Streaming

Execute a complex task and handle events for streaming output and orchestration updates:

```python
import json
import asyncio
from typing import cast

from agent_framework import (
    AgentRunUpdateEvent,
    ChatMessage,
    MagenticOrchestratorEvent,
    MagenticProgressLedger,
    WorkflowOutputEvent,
)

task = (
    "I am preparing a report on the energy efficiency of different machine learning model architectures. "
    "Compare the estimated training and inference energy consumption of ResNet-50, BERT-base, and GPT-2 "
    "on standard datasets (for example, ImageNet for ResNet, GLUE for BERT, WebText for GPT-2). "
    "Then, estimate the CO2 emissions associated with each, assuming training on an Azure Standard_NC6s_v3 "
    "VM for 24 hours. Provide tables for clarity, and recommend the most energy-efficient model "
    "per task type (image classification, text classification, and text generation)."
)

# Keep track of the last executor to format output nicely in streaming mode
last_message_id: str | None = None
output_event: WorkflowOutputEvent | None = None
async for event in workflow.run_stream(task):
    if isinstance(event, AgentRunUpdateEvent):
        message_id = event.data.message_id
        if message_id != last_message_id:
            if last_message_id is not None:
                print("\n")
            print(f"- {event.executor_id}:", end=" ", flush=True)
            last_message_id = message_id
        print(event.data, end="", flush=True)

    elif isinstance(event, MagenticOrchestratorEvent):
        print(f"\n[Magentic Orchestrator Event] Type: {event.event_type.name}")
        if isinstance(event.data, MagenticProgressLedger):
            print(f"Please review progress ledger:\n{json.dumps(event.data.to_dict(), indent=2)}")
        else:
            print(f"Unknown data type in MagenticOrchestratorEvent: {type(event.data)}")

        # Block to allow user to read the plan/progress before continuing
        # Note: this is for demonstration only and is not the recommended way to handle human interaction.
        # Please refer to `with_plan_review` for proper human interaction during planning phases.
        await asyncio.get_event_loop().run_in_executor(None, input, "Press Enter to continue...")

    elif isinstance(event, WorkflowOutputEvent):
        output_event = event

# The output of the Magentic workflow is a list of ChatMessages with only one final message
# generated by the orchestrator.
output_messages = cast(list[ChatMessage], output_event.data)
output = output_messages[-1].text
print(output)
```

## Advanced: Human-in-the-Loop Plan Review

Enable human-in-the-loop (HITL) to allow users to review and approve the manager's proposed plan before execution. This is useful for ensuring that the plan aligns with user expectations and requirements.

There are two options for plan review:

1. **Revise**: The user can provide feedback to revise the plan, which will trigger the manage to replan based on the feedback.
2. **Approve**: The user can approve the plan as-is, allowing the workflow to proceed.

Enaable plan review simply by adding `.with_plan_review()` when building the Magentic workflow:

```python
from agent_framework import (
    AgentRunUpdateEvent,
    ChatAgent,
    ChatMessage,
    MagenticBuilder,
    MagenticPlanReviewRequest,
    RequestInfoEvent,
    WorkflowOutputEvent,
)

workflow = (
    MagenticBuilder()
    .participants([researcher_agent, analyst_agent])
    .with_standard_manager(
        agent=manager_agent,
        max_round_count=10,
        max_stall_count=1,
        max_reset_count=2,
    )
    .with_plan_review()  # Request human input for plan review
    .build()
)
```

Plan review requests are emitted as `RequestInfoEvent` with `MagenticPlanReviewRequest` data. You can handle these requests in the event stream:

> [!TIP]
> Learn more about requests and responses in the [Requests and Responses](../requests-and-responses.md) guide.

```python
pending_request: RequestInfoEvent | None = None
pending_responses: dict[str, MagenticPlanReviewResponse] | None = None
output_event: WorkflowOutputEvent | None = None

while not output_event:
    if pending_responses is not None:
        stream = workflow.send_responses_streaming(pending_responses)
    else:
        stream = workflow.run_stream(task)

    last_message_id: str | None = None
    async for event in stream:
        if isinstance(event, AgentRunUpdateEvent):
            message_id = event.data.message_id
            if message_id != last_message_id:
                if last_message_id is not None:
                    print("\n")
                print(f"- {event.executor_id}:", end=" ", flush=True)
                last_message_id = message_id
            print(event.data, end="", flush=True)

        elif isinstance(event, RequestInfoEvent) and event.request_type is MagenticPlanReviewRequest:
            pending_request = event

        elif isinstance(event, WorkflowOutputEvent):
            output_event = event

    pending_responses = None

    # Handle plan review request if any
    if pending_request is not None:
        event_data = cast(MagenticPlanReviewRequest, pending_request.data)

        print("\n\n[Magentic Plan Review Request]")
        if event_data.current_progress is not None:
            print("Current Progress Ledger:")
            print(json.dumps(event_data.current_progress.to_dict(), indent=2))
            print()
        print(f"Proposed Plan:\n{event_data.plan.text}\n")
        print("Please provide your feedback (press Enter to approve):")

        reply = await asyncio.get_event_loop().run_in_executor(None, input, "> ")
        if reply.strip() == "":
            print("Plan approved.\n")
            pending_responses = {pending_request.request_id: event_data.approve()}
        else:
            print("Plan revised by human.\n")
            pending_responses = {pending_request.request_id: event_data.revise(reply)}
        pending_request = None
```

## Key Concepts

- **Dynamic Coordination**: The Magentic manager dynamically selects which agent should act next based on the evolving context
- **Iterative Refinement**: The system can break down complex problems and iteratively refine solutions through multiple rounds
- **Progress Tracking**: Built-in mechanisms to detect stalls and reset the plan if needed
- **Flexible Collaboration**: Agents can be called multiple times in any order as determined by the manager
- **Human Oversight**: Optional human-in-the-loop mechanisms for plan review

## Workflow Execution Flow

The Magentic orchestration follows this execution pattern:

1. **Planning Phase**: The manager analyzes the task and creates an initial plan
2. **Optional Plan Review**: If enabled, humans can review and approve/modify the plan
3. **Agent Selection**: The manager selects the most appropriate agent for each subtask
4. **Execution**: The selected agent executes their portion of the task
5. **Progress Assessment**: The manager evaluates progress and updates the plan
6. **Stall Detection**: If progress stalls, auto-replan with an optional human review process
7. **Iteration**: Steps 3-6 repeat until the task is complete or limits are reached
8. **Final Synthesis**: The manager synthesizes all agent outputs into a final result

## Complete Example

See complete samples in the [Agent Framework Samples repository](https://github.com/microsoft/agent-framework/tree/main/python/samples/getting_started/workflows/orchestration).

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Handoff Orchestration](./handoff.md)
