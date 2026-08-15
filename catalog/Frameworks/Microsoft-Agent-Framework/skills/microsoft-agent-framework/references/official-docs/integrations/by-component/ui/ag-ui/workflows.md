---
title: Workflows with AG-UI
description: Learn how to expose Agent Framework workflows through AG-UI with step tracking, interrupt/resume, and custom events
zone_pivot_groups: programming-languages
author: moonbox3
ms.topic: tutorial
ms.author: evmattso
ms.date: 07/10/2026
ms.service: agent-framework
---

# Workflows with AG-UI

::: zone pivot="programming-language-csharp"

[Workflows](../../../../concepts/workflows/index.md) orchestrate multiple agents in a defined execution graph. In .NET
you expose a workflow over AG-UI exactly the way you expose any agent: convert it to an `AIAgent` with
`AsAIAgent()` and map it with `MapAGUIServer`. There is no workflow-specific server API to learn.

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Agents.AI.Workflows;
using OpenAI.Chat;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddAGUIServer();

string endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"]
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
string deploymentName = builder.Configuration["AZURE_OPENAI_DEPLOYMENT_NAME"]
    ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT_NAME is not set.");

ChatClient chatClient = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
    .GetChatClient(deploymentName);

AIAgent researcher = chatClient.AsAIAgent(
    name: "researcher", instructions: "Research the user's topic and write a short, factual brief.");
AIAgent reporter = chatClient.AsAIAgent(
    name: "reporter", instructions: "Summarize the researcher's brief into a single clear paragraph.");

// A workflow-as-agent is just an AIAgent. Map it like any other agent.
AIAgent workflowAgent = AgentWorkflowBuilder.BuildSequential(researcher, reporter).AsAIAgent();

WebApplication app = builder.Build();
app.MapAGUIServer("/", workflowAgent);

await app.RunAsync();
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

The client side is unchanged. Connect with `AGUIChatClient` as in [Getting Started](getting-started.md).
Each agent's output streams as normal AG-UI text and tool-call events, and the `AuthorName` on each update
identifies which agent in the workflow produced it.

> [!NOTE]
> The .NET integration streams a workflow's **agent output** (text and tool calls) over AG-UI, but not the
> workflow-specific AG-UI events shown in the Python version of this article: step tracking
> (`STEP_STARTED` / `STEP_FINISHED`), activity snapshots, and workflow-level interrupts. Those events are
> still evolving for .NET (tracked by [agent-framework#2494](https://github.com/microsoft/agent-framework/issues/2494)).

## Next steps

- [State Management](state-management.md)
- [Human-in-the-Loop](human-in-the-loop.md)
- [Workflows](../../../../concepts/workflows/index.md)

::: zone-end

::: zone pivot="programming-language-python"

This tutorial shows you how to expose Agent Framework workflows through an AG-UI endpoint. Workflows orchestrate multiple agents and tools in a defined execution graph, and the AG-UI integration streams rich workflow events — step tracking, activity snapshots, interrupts, and custom events — to web clients in real time.

## Prerequisites

Before you begin, ensure you have:

- Python 3.10 or later
- `agent-framework-ag-ui` and `agent-framework-foundry` installed
- Familiarity with the [Getting Started](getting-started.md) tutorial
- Basic understanding of Agent Framework [workflow concepts](../../../../concepts/workflows/index.md)

## When to Use Workflows with AG-UI

Use a workflow instead of a single agent when you need:

- **Multi-agent orchestration**: Route tasks between specialized agents (for example, triage → refund → order)
- **Structured execution steps**: Track progress through defined stages with `STEP_STARTED` / `STEP_FINISHED` events
- **Interrupt / resume flows**: Pause execution to collect human input or approvals, then resume
- **Custom event streaming**: Emit domain-specific events (`request_info`, `status`, `workflow_output`) to the client

## Wrapping a Workflow with AgentFrameworkWorkflow

`AgentFrameworkWorkflow` is a lightweight wrapper that adapts a native `Workflow` to the AG-UI protocol. You can provide either a pre-built workflow instance or a factory that creates a new workflow per thread.

### Direct instance

Use a direct instance when a single workflow object can safely serve all requests (for example, stateless pipelines):

```python
from agent_framework import Workflow
from agent_framework.ag_ui import AgentFrameworkWorkflow

workflow = build_my_workflow()  # returns a Workflow

ag_ui_workflow = AgentFrameworkWorkflow(
    workflow=workflow,
    name="my-workflow",
    description="Single-instance workflow.",
)
```

### Thread-scoped factory

Use `workflow_factory` when each conversation thread needs its own workflow state. The factory receives the `thread_id` and returns a fresh `Workflow`:

```python
from agent_framework.ag_ui import AgentFrameworkWorkflow

ag_ui_workflow = AgentFrameworkWorkflow(
    workflow_factory=lambda thread_id: build_my_workflow(),
    name="my-workflow",
    description="Thread-scoped workflow.",
)
```

> [!IMPORTANT]
> You must pass **either** `workflow` **or** `workflow_factory`, not both. The wrapper raises a `ValueError` if both are provided.

## Registering the Endpoint

Register the workflow with `add_agent_framework_fastapi_endpoint` the same way you would register a single agent:

```python
from fastapi import FastAPI
from agent_framework.ag_ui import (
    AgentFrameworkWorkflow,
    add_agent_framework_fastapi_endpoint,
)

app = FastAPI(title="Workflow AG-UI Server")

ag_ui_workflow = AgentFrameworkWorkflow(
    workflow_factory=lambda thread_id: build_my_workflow(),
    name="handoff-demo",
    description="Multi-agent handoff workflow.",
)

add_agent_framework_fastapi_endpoint(
    app=app,
    agent=ag_ui_workflow,
    path="/workflow",
)
```

You can also pass a bare `Workflow` directly — the endpoint auto-wraps it in `AgentFrameworkWorkflow`:

```python
add_agent_framework_fastapi_endpoint(app, my_workflow, "/workflow")
```

## AG-UI Events Emitted by Workflows

Workflow runs emit a richer set of AG-UI events compared to single-agent runs:

| Event | When emitted | Description |
|---|---|---|
| `RUN_STARTED` | Run begins | Marks the start of workflow execution |
| `STEP_STARTED` | An executor or superstep begins | `step_name` identifies the agent or step (for example, `"triage_agent"`) |
| `TEXT_MESSAGE_*` | Agent produces text | Standard streaming text events |
| `TOOL_CALL_*` | Agent invokes a tool | Standard tool call events |
| `STEP_FINISHED` | An executor or superstep completes | Closes the step for UI progress tracking |
| `CUSTOM` (`status`) | Workflow state changes | Contains `{"state": "<value>"}` in the event value |
| `CUSTOM` (`request_info`) | Workflow requests human input | Contains the request payload for the client to render a prompt |
| `CUSTOM` (`workflow_output`) | Workflow produces output | Emitted for both `"output"` (terminal) and `"intermediate"` workflow events. Terminal outputs carry the final answer; intermediate outputs surface as `text_reasoning` content when the workflow runs behind `as_agent()`. |
| `RUN_FINISHED` | Run completes | Includes `outcome.type == "interrupt"` and `outcome.interrupts` when the workflow is waiting for input |

Clients can use `STEP_STARTED` / `STEP_FINISHED` events to render progress indicators showing which agent is currently active.

## Interrupt and Resume

Workflows can pause execution to collect human input or tool approvals. The AG-UI integration handles this through the interrupt/resume protocol.

### How interrupts work

1. During execution, the workflow raises a pending request (for example, a `HandoffAgentUserRequest` asking for more details, or a tool with `approval_mode="always_require"`).
2. The AG-UI bridge emits a `CUSTOM` event with `name="request_info"` containing the request data.
3. The run finishes with a `RUN_FINISHED` event whose `outcome.interrupts` field contains the pending requests:

    ```json
    {
      "type": "RUN_FINISHED",
      "threadId": "abc123",
      "runId": "run_xyz",
      "outcome": {
        "type": "interrupt",
        "interrupts": [
          {
            "id": "request-id-1",
            "reason": "input_required",
            "message": "Provide the requested information.",
            "responseSchema": { "type": "string" },
            "metadata": {
              "agent_framework": {
                "request_type": "HandoffAgentUserRequest"
              }
            }
          }
        ]
      }
    }
    ```

4. The client renders UI for the user to respond (a text input, an approval button, etc.).

### How resume works

The client sends a new request with a canonical `resume` array. Each entry identifies the interrupt and supplies the
user's response:

```json
{
  "threadId": "abc123",
  "messages": [],
  "resume": [
    {
      "interruptId": "request-id-1",
      "status": "resolved",
      "payload": "User's response text or approval decision"
    }
  ]
}
```

The server converts the resume payload into workflow responses and continues execution from where it paused. To
cancel the interrupted run instead, set `status` to `"cancelled"` and omit `payload`.

## Complete Example: Multi-Agent Handoff Workflow

This example shows a customer-support workflow with three agents that hand off work to each other, use tools requiring approval, and request human input when needed.

### Define the agents and tools

```python
"""AG-UI workflow server with multi-agent handoff."""

import os

from agent_framework import Agent, Message, Workflow, tool
from agent_framework.ag_ui import (
    AgentFrameworkWorkflow,
    add_agent_framework_fastapi_endpoint,
)
from agent_framework.foundry import FoundryChatClient
from agent_framework.orchestrations import HandoffBuilder
from azure.identity import AzureCliCredential
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware


@tool(approval_mode="always_require")
def submit_refund(refund_description: str, amount: str, order_id: str) -> str:
    """Capture a refund request for manual review before processing."""
    return f"Refund recorded for order {order_id} (amount: {amount}): {refund_description}"


@tool(approval_mode="always_require")
def submit_replacement(order_id: str, shipping_preference: str, replacement_note: str) -> str:
    """Capture a replacement request for manual review before processing."""
    return f"Replacement recorded for order {order_id} (shipping: {shipping_preference}): {replacement_note}"


@tool(approval_mode="never_require")
def lookup_order_details(order_id: str) -> dict[str, str]:
    """Return order details for a given order ID."""
    return {
        "order_id": order_id,
        "item_name": "Wireless Headphones",
        "amount": "$129.99",
        "status": "delivered",
    }
```

### Build the workflow

```python
def create_handoff_workflow() -> Workflow:
    """Build a handoff workflow with triage, refund, and order agents."""
    client = FoundryChatClient(
        project_endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
        model=os.environ["FOUNDRY_MODEL"],
        credential=AzureCliCredential(),
    )

    triage = Agent(id="triage_agent", name="triage_agent", instructions="...", client=client)
    refund = Agent(id="refund_agent", name="refund_agent", instructions="...", client=client,
                   tools=[lookup_order_details, submit_refund])
    order = Agent(id="order_agent", name="order_agent", instructions="...", client=client,
                  tools=[lookup_order_details, submit_replacement])

    def termination_condition(conversation: list[Message]) -> bool:
        for msg in reversed(conversation):
            if msg.role == "assistant" and (msg.text or "").strip().lower().endswith("case complete."):
                return True
        return False

    builder = HandoffBuilder(
        name="support_workflow",
        participants=[triage, refund, order],
        termination_condition=termination_condition,
    )
    builder.add_handoff(triage, [refund], description="Route refund requests.")
    builder.add_handoff(triage, [order], description="Route replacement requests.")
    builder.add_handoff(refund, [order], description="Route to order after refund.")
    builder.add_handoff(order, [triage], description="Route back after completion.")

    return builder.with_start_agent(triage).build()
```

### Create the FastAPI app

```python
app = FastAPI(title="Workflow AG-UI Demo")
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

ag_ui_workflow = AgentFrameworkWorkflow(
    workflow_factory=lambda _thread_id: create_handoff_workflow(),
    name="support_workflow",
    description="Customer support handoff workflow.",
)

add_agent_framework_fastapi_endpoint(
    app=app,
    agent=ag_ui_workflow,
    path="/support",
)

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="127.0.0.1", port=8888)
```

### Event sequence

A typical multi-turn interaction produces events like:

```
RUN_STARTED           threadId=abc123
STEP_STARTED          stepName=triage_agent
TEXT_MESSAGE_START     role=assistant
TEXT_MESSAGE_CONTENT   delta="I'll look into your refund..."
TEXT_MESSAGE_END
STEP_FINISHED         stepName=triage_agent
STEP_STARTED          stepName=refund_agent
TOOL_CALL_START       toolCallName=lookup_order_details
TOOL_CALL_ARGS        delta='{"order_id":"12345"}'
TOOL_CALL_END
TOOL_CALL_START       toolCallName=submit_refund
TOOL_CALL_ARGS        delta='{"order_id":"12345","amount":"$129.99",...}'
TOOL_CALL_END
RUN_FINISHED          outcome={type: "interrupt", interrupts: [{id: "...", reason: "tool_call"}]}
```

The client can then display an approval dialog and resume with the user's decision.

## Receiving Forwarded Props

AG-UI clients (such as CopilotKit) can include a `forwarded_props` (or `forwardedProps`) field in the input payload. The AG-UI integration automatically passes these props to the workflow's `run` method via the `function_invocation_kwargs` keyword argument:

```python
class MyWorkflow(Workflow):
    async def run(
        self,
        *,
        message=None,
        responses=None,
        stream: bool = False,
        function_invocation_kwargs: dict | None = None,
    ):
        forwarded_props = (function_invocation_kwargs or {}).get("forwarded_props", {})
        # Use forwarded_props for custom routing, feature flags, etc.
        ...
```

Key details:

- Both `forwarded_props` and `forwardedProps` are accepted in the input payload; internally they are normalized to `forwarded_props`.
- If `workflow.run()` does not accept `function_invocation_kwargs` (or `**kwargs`), the props are silently dropped — existing workflows are unaffected.
- Forwarded props are also stored in session metadata but are filtered from LLM-bound metadata, so they do not leak into chat client requests.

## Next steps

> [!div class="nextstepaction"]
> [Human-in-the-Loop](./human-in-the-loop.md)

## Additional Resources

- [AG-UI Overview](index.md)
- [Getting Started](getting-started.md)
- [Workflows](../../../../concepts/workflows/index.md)
- [Agent Framework GitHub Repository](https://github.com/microsoft/agent-framework)

::: zone-end


::: zone pivot="programming-language-go"

Go can expose workflows to AG-UI by wrapping a `workflow.Workflow` as an agent with `workflow/agentworkflow`, then hosting that agent with `provider/aguiprovider`.

```go
workflowAgent, err := agentworkflow.New(wf, agentworkflow.AgentConfig{
    IncludeOutputsInResponse: true,
    Config: agent.Config{
        Name: "WorkflowAgent",
    },
})
if err != nil {
    panic(err)
}

mux := http.NewServeMux()
mux.Handle("/", aguiprovider.NewJSONHTTPHandler(workflowAgent, aguiprovider.HandlerConfig{}))
```

> [!TIP]
> See the [workflow as an agent sample](https://github.com/microsoft/agent-framework-go/blob/main/examples/03-workflows/agents/workflow_as_an_agent/main.go) and the [AG-UI server sample](https://github.com/microsoft/agent-framework-go/blob/main/examples/02-agents/agui/step01_getting_started/server/main.go) for complete runnable examples.

::: zone-end