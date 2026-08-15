---
title: Human-in-the-Loop with AG-UI
description: Learn how to implement approval workflows for tool execution using AG-UI protocol
zone_pivot_groups: programming-languages
author: moonbox3
ms.topic: tutorial
ms.author: evmattso
ms.date: 07/10/2026
ms.service: agent-framework
---

# Human-in-the-Loop with AG-UI

::: zone pivot="programming-language-csharp"

This tutorial demonstrates how to implement human-in-the-loop approval workflows with AG-UI in .NET. The .NET implementation uses Microsoft.Extensions.AI's `ApprovalRequiredAIFunction` and translates approval requests into AG-UI "client tool calls" that the client handles and responds to.

## Overview

The C# AG-UI approval pattern works as follows:

1. **Server**: Wraps a function with `ApprovalRequiredAIFunction` to mark it as requiring approval, and maps the agent with `MapAGUIServer`.
2. **Interrupt**: When the model calls the tool, the run ends with an interrupt instead of executing it. The AG-UI client surfaces this as a `ToolApprovalRequestContent`.
3. **Client**: Presents the request to the user, then creates a decision with `CreateResponse(approved)` and sends it back.
4. **Resume**: `AGUIChatClient` transports the decision over the AG-UI resume mechanism. The agent continues and runs (or skips) the tool.

## Prerequisites

- Azure OpenAI resource with a deployed model
- Environment variables:
  - `AZURE_OPENAI_ENDPOINT`
  - `AZURE_OPENAI_DEPLOYMENT_NAME`
- Understanding of [Backend Tool Rendering](backend-tool-rendering.md)

## Server Implementation

To require human approval before a tool runs, wrap the tool's `AIFunction` in `ApprovalRequiredAIFunction` and map the agent with `MapAGUIServer`. The AG-UI hosting layer raises an approval interrupt automatically when the model calls the tool.

```csharp
using System.ComponentModel;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddAGUIServer();

WebApplication app = builder.Build();

string endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"]
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
string deploymentName = builder.Configuration["AZURE_OPENAI_DEPLOYMENT_NAME"]
    ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT_NAME is not set.");

// A tool that must be approved before it runs.
[Description("Send an email to a recipient.")]
static string SendEmail(
    [Description("The email address to send to.")] string to,
    [Description("The subject line.")] string subject,
    [Description("The email body.")] string body)
    => $"Email sent to {to} with subject '{subject}'.";

AITool sendEmail = new ApprovalRequiredAIFunction(AIFunctionFactory.Create(SendEmail, name: "send_email"));

AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
    .GetChatClient(deploymentName)
    .AsAIAgent(
        name: "AGUIAssistant",
        instructions: "You are a helpful assistant. Use the send_email tool when asked to send email.",
        tools: [sendEmail]);

app.MapAGUIServer("/", agent);

await app.RunAsync();
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

When the model calls `SendEmail`, the run ends with an **interrupt** outcome instead of executing the
tool. The AG-UI client surfaces this as a `ToolApprovalRequestContent` that carries the tool call and a
response schema of `{ "approved": boolean }`. The tool runs only after the client sends an approval.

## Client Implementation

A client handles the interrupt by reading the `ToolApprovalRequestContent`, creating a decision with
`CreateResponse(approved)`, and sending it back on the next turn. You don't hand-encode the AG-UI
resume message. `AGUIChatClient` converts the decision into the AG-UI resume for you, and reusing the
same `AgentSession` resumes the run.

```csharp
using AGUI.Client;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

string serverUrl = Environment.GetEnvironmentVariable("AGUI_SERVER_URL") ?? "http://localhost:8888";
using HttpClient httpClient = new() { BaseAddress = new Uri(serverUrl) };

AGUIChatClient chatClient = new(new AGUIChatClientOptions(httpClient, "/"));
AIAgent agent = chatClient.AsAIAgent();
AgentSession session = await agent.CreateSessionAsync();

List<ChatMessage> messages = [new(ChatRole.User, "Email alice@example.com to say the report is ready.")];

// First turn: run until the agent requests approval.
ToolApprovalRequestContent? approvalRequest = null;
await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(messages, session))
{
    foreach (AIContent content in update.Contents)
    {
        if (content is ToolApprovalRequestContent request)
        {
            approvalRequest = request;
            var call = request.ToolCall as FunctionCallContent;
            Console.WriteLine($"Approval requested for '{call?.Name}'.");
        }
        else if (content is TextContent text)
        {
            Console.Write(text.Text);
        }
    }
}

// Second turn: send the decision. The agent resumes and runs (or skips) the tool.
if (approvalRequest is not null)
{
    ToolApprovalResponseContent decision = approvalRequest.CreateResponse(approved: true);
    List<ChatMessage> resume = [new(ChatRole.User, [decision])];

    // Reusing the same session resumes the run. No thread/run id plumbing is needed.
    await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(resume, session))
    {
        foreach (AIContent content in update.Contents)
        {
            if (content is TextContent text)
            {
                Console.Write(text.Text);
            }
        }
    }
}
```

To reject instead, call `approvalRequest.CreateResponse(approved: false)`; the agent continues without running the tool.

## Approval modes

Marking a tool for approval, and deciding *when* a call needs it, is a general Agent Framework
capability, not something AG-UI defines. It works the same for an agent exposed over AG-UI:

- **Always require / never require** approval by wrapping (or not wrapping) the function in
  `ApprovalRequiredAIFunction`.
- **Selective approval**: wrap only the sensitive tools so the rest run unattended.
- **Conditional approval**: auto-approve some calls to an approval-required tool based on their
  **arguments** using `AIAgentBuilder.UseToolApproval` with `AutoApprovalRules`.

For the APIs, examples, and guidance, see [Using function tools with human-in-the-loop approvals](../../../../agents/tools/tool-approval.md).

What AG-UI adds is the transport. A call that needs a human ends the run with a `RUN_FINISHED`
**interrupt** that carries the tool call and a `{ "approved": boolean }` response schema, which the
AG-UI client approves and resumes. The [Server](#server-implementation) and [Client](#client-implementation)
implementations above show this end to end. Calls that run directly, or that a conditional rule
auto-approves, stream their `TOOL_CALL_RESULT` normally and never interrupt.

## Next steps

> [!div class="nextstepaction"]
> [MCP Apps Compatibility](./mcp-apps.md)

::: zone-end

::: zone pivot="programming-language-python"

This tutorial shows you how to implement human-in-the-loop workflows with AG-UI, where users must approve tool executions before they are performed. This is essential for sensitive operations like financial transactions, data modifications, or actions that have significant consequences.

## Prerequisites

Before you begin, ensure you have completed the [Backend Tool Rendering](backend-tool-rendering.md) tutorial and understand:

- How to create function tools
- How AG-UI streams tool events
- Basic server and client setup

## What is Human-in-the-Loop?

Human-in-the-Loop (HITL) is a pattern where the agent requests user approval before executing certain operations. With AG-UI:

- The agent generates tool calls as usual
- Instead of executing immediately, the server sends approval requests to the client
- The client displays the request and prompts the user
- The user approves or rejects the action
- The server receives the response and proceeds accordingly

### Benefits

- **Safety**: Prevent unintended actions from being executed
- **Transparency**: Users see exactly what the agent wants to do
- **Control**: Users have final say over sensitive operations
- **Compliance**: Meet regulatory requirements for human oversight

## Marking Tools for Approval

To require approval for a tool, use the `approval_mode` parameter in the `@tool` decorator:

```python
from agent_framework import tool
from typing import Annotated
from pydantic import Field


@tool(approval_mode="always_require")
def send_email(
    to: Annotated[str, Field(description="Email recipient address")],
    subject: Annotated[str, Field(description="Email subject line")],
    body: Annotated[str, Field(description="Email body content")],
) -> str:
    """Send an email to the specified recipient."""
    # Send email logic here
    return f"Email sent to {to} with subject '{subject}'"


@tool(approval_mode="always_require")
def delete_file(
    filepath: Annotated[str, Field(description="Path to the file to delete")],
) -> str:
    """Delete a file from the filesystem."""
    # Delete file logic here
    return f"File {filepath} has been deleted"
```

### Approval Modes

- **`always_require`**: Always request approval before execution
- **`never_require`**: Never request approval (default behavior)
- **`conditional`**: Request approval based on certain conditions (custom logic)

## Creating a Server with Human-in-the-Loop

Here's a complete server implementation with approval-required tools:

```python
"""AG-UI server with human-in-the-loop."""

import os
from typing import Annotated

from agent_framework import Agent, tool
from agent_framework.openai import OpenAIChatCompletionClient
from agent_framework_ag_ui import AgentFrameworkAgent, add_agent_framework_fastapi_endpoint
from azure.identity import AzureCliCredential
from fastapi import FastAPI
from pydantic import Field


# Tools that require approval
@tool(approval_mode="always_require")
def transfer_money(
    from_account: Annotated[str, Field(description="Source account number")],
    to_account: Annotated[str, Field(description="Destination account number")],
    amount: Annotated[float, Field(description="Amount to transfer")],
    currency: Annotated[str, Field(description="Currency code")] = "USD",
) -> str:
    """Transfer money between accounts."""
    return f"Transferred {amount} {currency} from {from_account} to {to_account}"


@tool(approval_mode="always_require")
def cancel_subscription(
    subscription_id: Annotated[str, Field(description="Subscription identifier")],
) -> str:
    """Cancel a subscription."""
    return f"Subscription {subscription_id} has been cancelled"


# Regular tools (no approval required)
@tool
def check_balance(
    account: Annotated[str, Field(description="Account number")],
) -> str:
    """Check account balance."""
    # Simulated balance check
    return f"Account {account} balance: $5,432.10 USD"


# Read required configuration
endpoint = os.environ.get("AZURE_OPENAI_ENDPOINT")
deployment_name = os.environ.get("AZURE_OPENAI_CHAT_COMPLETION_MODEL")

if not endpoint:
    raise ValueError("AZURE_OPENAI_ENDPOINT environment variable is required")
if not deployment_name:
    raise ValueError("AZURE_OPENAI_CHAT_COMPLETION_MODEL environment variable is required")

chat_client = OpenAIChatCompletionClient(
    model=deployment_name,
    azure_endpoint=endpoint,
    api_version=os.getenv("AZURE_OPENAI_API_VERSION"),
    credential=AzureCliCredential(),
)

# Create agent with tools
agent = Agent(
    name="BankingAssistant",
    instructions="You are a banking assistant. Help users with their banking needs. Always confirm details before performing transfers.",
    client=chat_client,
    tools=[transfer_money, cancel_subscription, check_balance],
)

# Wrap agent to enable human-in-the-loop
wrapped_agent = AgentFrameworkAgent(
    agent=agent,
    require_confirmation=True,  # Enable human-in-the-loop
)

# Create FastAPI app
app = FastAPI(title="AG-UI Banking Assistant")
add_agent_framework_fastapi_endpoint(app, wrapped_agent, "/")

if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="127.0.0.1", port=8888)
```

### Key Concepts

- **`AgentFrameworkAgent` wrapper**: Enables AG-UI protocol features like human-in-the-loop
- **`require_confirmation=True`**: Activates approval workflow for marked tools
- **Tool-level control**: Only tools marked with `approval_mode="always_require"` will request approval

## Understanding Approval Interrupts

When a tool requires approval, the run finishes with a canonical AG-UI interrupt.

### Approval Interrupt

```json
{
  "type": "RUN_FINISHED",
  "threadId": "thread-1",
  "runId": "run-1",
  "outcome": {
    "type": "interrupt",
    "interrupts": [
      {
        "id": "approval-1",
        "reason": "tool_call",
        "message": "Approve tool call transfer_money?",
        "toolCallId": "call-1",
        "responseSchema": {
          "type": "object",
          "properties": {
            "accepted": { "type": "boolean" },
            "arguments": { "type": "object" }
          },
          "required": ["accepted"]
        },
        "metadata": {
          "agent_framework": {
            "type": "function_approval_request",
            "function_call": {
              "call_id": "call-1",
              "name": "transfer_money",
              "arguments": {
                "from_account": "1234567890",
                "to_account": "0987654321",
                "amount": 500.00,
                "currency": "USD"
              }
            }
          }
        }
      }
    ]
  }
}
```

Tool approval interrupts use `reason: "tool_call"` and include a `toolCallId`. The final `ChatResponseUpdate`
from `AGUIChatClient` preserves the `outcome` and `interrupts` values in `additional_properties`.
`Interrupt` and `ResumeEntry` are protocol types from `ag_ui.core`, not Agent Framework-specific models.

### Resume Format

Resume the same thread with a canonical `resume` array. Use `accepted: false` to reject the operation while allowing
the agent to continue. Use `status: "cancelled"` without a payload to cancel the interrupted run.

```json
{
  "threadId": "thread-1",
  "messages": [],
  "resume": [
    {
      "interruptId": "approval-1",
      "status": "resolved",
      "payload": {
        "accepted": true
      }
    }
  ]
}
```

## Client with Approval Support

Here's a client using `AGUIChatClient` that handles approval requests:

```python
"""AG-UI client with human-in-the-loop support."""

import asyncio
import os

from agent_framework import Agent
from agent_framework_ag_ui import AGUIChatClient


def display_approval_request(update) -> None:
    """Display approval request details to the user."""
    print("\n\033[93m" + "=" * 60 + "\033[0m")
    print("\033[93mAPPROVAL REQUIRED\033[0m")
    print("\033[93m" + "=" * 60 + "\033[0m")

    # Display tool call details from update contents
    for i, content in enumerate(update.contents, 1):
        if content.type == "function_approval_request":
            function_call = content.function_call
            print(f"\nAction {i}:")
            print(f"  Tool: \033[95m{function_call.name}\033[0m")
            print(f"  Arguments:")
            for key, value in (function_call.arguments or {}).items():
                print(f"    {key}: {value}")

    print("\n\033[93m" + "=" * 60 + "\033[0m")


async def main():
    """Main client loop with approval handling."""
    server_url = os.environ.get("AGUI_SERVER_URL", "http://127.0.0.1:8888/")
    print(f"Connecting to AG-UI server at: {server_url}\n")

    # Create AG-UI chat client
    chat_client = AGUIChatClient(endpoint=server_url)

    # Create agent with the chat client
    agent = Agent(
        name="ClientAgent",
        client=chat_client,
        instructions="You are a helpful assistant.",
    )

    # Get a thread for conversation continuity
    thread = agent.create_session()

    try:
        while True:
            message = input("\nUser (:q or quit to exit): ")
            if not message.strip():
                continue

            if message.lower() in (":q", "quit"):
                break

            print("\nAssistant: ", end="", flush=True)
            pending_interrupts = []

            async for update in agent.run(message, session=thread, stream=True):
                # Check if this update carries an approval request.
                if any(content.type == "function_approval_request" for content in update.contents):
                    display_approval_request(update)

                if update.text:
                    print(f"\033[96m{update.text}\033[0m", end="", flush=True)

                properties = update.additional_properties or {}
                outcome = properties.get("outcome")
                if isinstance(outcome, dict) and outcome.get("type") == "interrupt":
                    pending_interrupts = outcome.get("interrupts", [])

            if pending_interrupts:
                resume_entries = []
                for interrupt in pending_interrupts:
                    prompt = interrupt.get("message", "Approve this action?")
                    user_choice = input(f"\n{prompt} (yes/no): ").strip().lower()
                    resume_entries.append({
                        "interruptId": interrupt["id"],
                        "status": "resolved",
                        "payload": {"accepted": user_choice in ("yes", "y")},
                    })

                print("\nAssistant: ", end="", flush=True)
                async for update in agent.run(
                    [],
                    session=thread,
                    stream=True,
                    options={
                        "available_interrupts": pending_interrupts,
                        "resume": resume_entries,
                    },
                ):
                    if update.text:
                        print(f"\033[96m{update.text}\033[0m", end="", flush=True)

            print()

    except KeyboardInterrupt:
        print("\n\nExiting...")
    except Exception as e:
        print(f"\n\033[91mError: {e}\033[0m")


if __name__ == "__main__":
    asyncio.run(main())
```

## Example Interaction

With the server and client running:

```
User (:q or quit to exit): Transfer $500 from account 1234567890 to account 0987654321

[Run Started]
============================================================
APPROVAL REQUIRED
============================================================

Action 1:
  Tool: transfer_money
  Arguments:
    from_account: 1234567890
    to_account: 0987654321
    amount: 500.0
    currency: USD

============================================================

Approve this action? (yes/no): yes

[Sending approval response: True]

[Tool Result: Transferred 500.0 USD from 1234567890 to 0987654321]
The transfer of $500 from account 1234567890 to account 0987654321 has been completed successfully.
[Run Finished]
```

If the user rejects:

```
Approve this action? (yes/no): no

[Sending approval response: False]

I understand. The transfer has been cancelled and no money was moved.
[Run Finished]
```

## Custom Confirmation Messages

Customize approval and confirmation messages in your AG-UI client UI when rendering approval interrupts from the
server. The Python `AgentFrameworkAgent` exposes approval requests and interrupt metadata; it doesn't take a
server-side confirmation strategy object.

## Best Practices

### Clear Tool Descriptions

Provide detailed descriptions so users understand what they're approving:

```python
@tool(approval_mode="always_require")
def delete_database(
    database_name: Annotated[str, Field(description="Name of the database to permanently delete")],
) -> str:
    """
    Permanently delete a database and all its contents.

    WARNING: This action cannot be undone. All data in the database will be lost.
    Use with extreme caution.
    """
    # Implementation
    pass
```

### Granular Approval

Request approval for individual sensitive actions rather than batching:

```python
# Good: Individual approval per transfer
@tool(approval_mode="always_require")
def transfer_money(...): pass

# Avoid: Batching multiple sensitive operations
# Users should approve each operation separately
```

### Informative Arguments

Use descriptive parameter names and provide context:

```python
@tool(approval_mode="always_require")
def purchase_item(
    item_name: Annotated[str, Field(description="Name of the item to purchase")],
    quantity: Annotated[int, Field(description="Number of items to purchase")],
    price_per_item: Annotated[float, Field(description="Price per item in USD")],
    total_cost: Annotated[float, Field(description="Total cost including tax and shipping")],
) -> str:
    """Purchase items from the store."""
    pass
```

### Timeout Handling

Set appropriate timeouts for approval requests:

```python
# Client side
async with httpx.AsyncClient(timeout=120.0) as client:  # 2 minutes for user to respond
    # Handle approval
    pass
```

## Selective Approval

You can mix tools that require approval with those that don't:

```python
# No approval needed for read-only operations
@tool
def get_account_balance(...): pass

@tool
def list_transactions(...): pass

# Approval required for write operations
@tool(approval_mode="always_require")
def transfer_funds(...): pass

@tool(approval_mode="always_require")
def close_account(...): pass
```

## Batched Approvals and Cancellation

One model response can contain both approval-required tools and tools that do not require approval. Resolving the
visible interrupt also completes the other tool calls from that batch according to their approval decisions. For
example, a `never_require` sibling executes and its `TOOL_CALL_RESULT` is streamed in the resumed run even when the
approval-required sibling is rejected.

Cancelling with `status: "cancelled"` aborts the approval resume and clears queued approval state for the thread.
Later requests cannot resurface or execute stale tool calls from the cancelled batch.

## Next steps

> [!div class="nextstepaction"]
> [MCP Apps Compatibility](./mcp-apps.md)

## Additional Resources

- [AG-UI Overview](index.md)
- [Backend Tool Rendering](backend-tool-rendering.md)
- [Function Tools with Approvals](../../../../agents/tools/tool-approval.md)

::: zone-end

::: zone pivot="programming-language-go"

Go supports AG-UI human-in-the-loop flows with approval-required tools. Wrap a function tool with `tool.ApprovalRequiredFunc`, then host the agent through `aguiprovider`.

```go
approveExpense := functool.MustNew(functool.Config{
    Name:        "approve_expense_report",
    Description: "Approve the expense report.",
}, func(ctx context.Context, expenseReportID string) (string, error) {
    return fmt.Sprintf("Expense report %s approved", expenseReportID), nil
})

a := foundryprovider.NewAgent(endpoint, token, foundryprovider.ModelDeployment(model), foundryprovider.AgentConfig{
    Config: agent.Config{
        Tools: []tool.Tool{tool.ApprovalRequiredFunc(approveExpense)},
    },
})
```

> [!TIP]
> See the [AG-UI human-in-the-loop sample](https://github.com/microsoft/agent-framework-go/blob/main/examples/02-agents/agui/step04_human_in_loop/server/main.go) for a complete runnable example.

::: zone-end
