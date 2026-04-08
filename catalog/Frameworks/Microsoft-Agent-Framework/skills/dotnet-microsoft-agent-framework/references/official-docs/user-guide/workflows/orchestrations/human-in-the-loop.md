---
title: Microsoft Agent Framework Workflows Orchestrations - HITL
description: In-depth look at Human-in-the-Loop in Orchestrations in Microsoft Agent Framework Workflows.
zone_pivot_groups: programming-languages
author: TaoChenOSU
ms.topic: tutorial
ms.author: taochen
ms.date: 01/11/2026
ms.service: agent-framework
---

# Microsoft Agent Framework Workflows Orchestrations - Human-in-the-Loop

Although fully autonomous agents sound powerful, practical applications often require human intervention for critical decisions, approvals, or feedback before proceeding.

All Microsoft Agent Framework orchestrations support Human-in-the-Loop (HITL) capabilities, allowing the workflow to pause and request input from a human user at designated points. This ensures the following:

1. Sensitive actions are reviewed and approved by humans, enhancing safety and reliability.
2. A feedback loop exists where humans can guide agent behavior, improving outcomes.

> [!IMPORTANT]
> The Handoff orchestration is specifically designed for complex multi-agent scenarios requiring extensive human interaction. Thus, its HITL features are designed differently from other orchestrations. See the [Handoff Orchestration](./handoff.md) documentation for details.

> [!IMPORTANT]
> For group-chat-based orchestrations (Group Chat and Magentic), the orchestrator can also request human feedback and approvals as needed, depending on the implementation of the orchestrator.

## How Human-in-the-Loop Works

> [!TIP]
> The HITL functionality is built on top of the existing request/response mechanism in Microsoft Agent Framework workflows. If you're unfamiliar with this mechanism, please refer to the [Request and Response](../requests-and-responses.md) documentation first.

::: zone pivot="programming-language-csharp"

Coming soon...

::: zone-end

::: zone pivot="programming-language-python"

When HITL is enabled in an orchestration, via the `with_request_info()` method on the corresponding builder (e.g., `SequentialBuilder`), a subworkflow is created to facilitate human interaction for the agent participants.

Take the Sequential orchestration as an example. Without HITL, the agent participants are directly plugged into a sequential pipeline:

<p align="center">
    <img src="../resources/images/orchestration-sequential.png" alt="Sequential Orchestration">
</p>

With HITL enabled, the agent participants are plugged into a subworkflow that handles human requests and responses in a loop:

<p align="center">
    <img src="../resources/images/orchestration-sequential-hitl.png" alt="Sequential Orchestration with HITL">
</p>

When an agent produces an output, the output doesn't go directly to the next agent or the orchestrator. Instead, it is sent to the `AgentRequestInfoExecutor` in the subworkflow, which sends the output as a request and waits for a response of type `AgentRequestInfoResponse`.

To proceed, the system (typically a human user) must provide a response to the request. This response can be one of the following:

1. **Feedback**: The human user can provide feedback on the agent's output, which is then sent back to the agent for further refinement. Can be created via `AgentRequestInfoResponse.from_messages()` or `AgentRequestInfoResponse.from_strings()`.
2. **Approval**: If the agent's output meets the human user's expectations, the user can approve it to allow the subworkflow to output the agent's response and the parent workflow to continue. Can be created via `AgentRequestInfoResponse.approve()`.

> [!TIP]
> The same process applies to [Concurrent](concurrent.md), [Group Chat](group-chat.md), and [Magnetic](magentic.md) orchestrations.

::: zone-end

## Only enable HITL for a subset of agents

::: zone pivot="programming-language-csharp"

Coming soon...

::: zone-end

::: zone pivot="programming-language-python"

You can choose to enable HITL for only a subset of agents in the orchestration by specifying the agent IDs when calling `with_request_info()`. For example, in a sequential orchestration with three agents, you can enable HITL only for the second agent:

```python
builder = (
    SequentialBuilder()
    .participants([agent1, agent2, agent3])
    .with_request_info(agents=[agent2])
)
```

::: zone-end

## Function Approval with HITL

When your agents use functions that require human approval (e.g., functions decorated with `@ai_function(approval_mode="always_require")`), the HITL mechanism seamlessly integrates function approval requests into the workflow.

> [!TIP]
> See the [Function Approval](../../../tutorials/agents/function-tools-approvals.md) documentation for more details on function approval.

When an agent attempts to call such a function, a `FunctionApprovalRequestContent` request is generated and sent to the human user for approval. The workflow pauses if no other path is available and waits for the user's decision. The user can then approve or reject the function call, and the response is sent back to the agent to proceed accordingly.

## Next steps

Head over to our samples in the [Microsoft Agent Framework GitHub repository](https://github.com/microsoft/agent-framework/tree/main/python/samples/getting_started/workflows/human-in-the-loop) to see HITL in action.
