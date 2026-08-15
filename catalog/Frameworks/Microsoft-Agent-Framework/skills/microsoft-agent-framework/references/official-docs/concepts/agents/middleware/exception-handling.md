---
title: "Exception Handling"
description: "Learn how to handle exceptions in middleware."
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: reference
ms.author: edvan
ms.date: 05/27/2026
ms.service: agent-framework
---

# Exception Handling

Middleware provides a natural place to implement error handling, retry logic, and graceful degradation for agent interactions.

:::zone pivot="programming-language-csharp"

In C#, you can wrap agent execution in try-catch blocks within middleware to handle exceptions:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

// Middleware that catches exceptions and provides graceful fallback responses
async Task<AgentResponse> ExceptionHandlingMiddleware(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
{
    try
    {
        Console.WriteLine("[ExceptionHandler] Executing agent run...");
        return await innerAgent.RunAsync(messages, session, options, cancellationToken);
    }
    catch (TimeoutException ex)
    {
        Console.WriteLine($"[ExceptionHandler] Caught timeout: {ex.Message}");
        return new AgentResponse([new ChatMessage(ChatRole.Assistant,
            "Sorry, the request timed out. Please try again later.")]);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ExceptionHandler] Caught error: {ex.Message}");
        return new AgentResponse([new ChatMessage(ChatRole.Assistant,
            "An error occurred while processing your request.")]);
    }
}

AIAgent agent = new AIProjectClient(
    new Uri("<your-foundry-project-endpoint>"),
    new DefaultAzureCredential())
        .AsAIAgent(
            model: "gpt-4o-mini",
            instructions: "You are a helpful assistant.");

var safeAgent = agent
    .AsBuilder()
        .Use(runFunc: ExceptionHandlingMiddleware, runStreamingFunc: null)
    .Build();

Console.WriteLine(await safeAgent.RunAsync("Get user statistics"));
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

:::zone-end

:::zone pivot="programming-language-python"

### Exception handling middleware

This example demonstrates how to catch and handle exceptions within middleware:

```python
# Copyright (c) Microsoft. All rights reserved.

import asyncio
from collections.abc import Awaitable, Callable
from typing import Annotated

from agent_framework import FunctionInvocationContext, tool
from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient
from azure.identity.aio import AzureCliCredential
from pydantic import Field

"""
Exception Handling with MiddlewareTypes

This sample demonstrates how to use middleware for centralized exception handling in function calls.
The example shows:

- How to catch exceptions thrown by functions and provide graceful error responses
- Overriding function results when errors occur to provide user-friendly messages
- Using middleware to implement retry logic, fallback mechanisms, or error reporting

The middleware catches TimeoutError from an unstable data service and replaces it with
a helpful message for the user, preventing raw exceptions from reaching the end user.
"""


# NOTE: approval_mode="never_require" is for sample brevity. Use "always_require" in production; see samples/02-agents/tools/function_tool_with_approval.py and samples/02-agents/tools/function_tool_with_approval_and_sessions.py.
@tool(approval_mode="never_require")
def unstable_data_service(
    query: Annotated[str, Field(description="The data query to execute.")],
) -> str:
    """A simulated data service that sometimes throws exceptions."""
    # Simulate failure
    raise TimeoutError("Data service request timed out")


async def exception_handling_middleware(
    context: FunctionInvocationContext, call_next: Callable[[], Awaitable[None]]
) -> None:
    function_name = context.function.name

    try:
        print(f"[ExceptionHandlingMiddleware] Executing function: {function_name}")
        await call_next()
        print(f"[ExceptionHandlingMiddleware] Function {function_name} completed successfully.")
    except TimeoutError as e:
        print(f"[ExceptionHandlingMiddleware] Caught TimeoutError: {e}")
        # Override function result to provide custom message in response.
        context.result = (
            "Request Timeout: The data service is taking longer than expected to respond. "
            "Respond with message - 'Sorry for the inconvenience, please try again later.'"
        )


async def main() -> None:
    """Example demonstrating exception handling with middleware."""
    print("=== Exception Handling MiddlewareTypes Example ===")

    # For authentication, run `az login` command in terminal or replace AzureCliCredential with preferred
    # authentication option.
    async with (
        AzureCliCredential() as credential,
        Agent(
            client=FoundryChatClient(credential=credential),
            name="DataAgent",
            instructions="You are a helpful data assistant. Use the data service tool to fetch information for users.",
            tools=unstable_data_service,
            middleware=[exception_handling_middleware],
        ) as agent,
    ):
        query = "Get user statistics"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Agent: {result}")


if __name__ == "__main__":
    asyncio.run(main())
```

### Example: Unstable tool

Here's a tool that may raise exceptions, which the middleware above can handle:

```python
# Copyright (c) Microsoft. All rights reserved.

import asyncio
from collections.abc import Awaitable, Callable
from typing import Annotated

from agent_framework import FunctionInvocationContext, tool
from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient
from azure.identity.aio import AzureCliCredential
from pydantic import Field

"""
Exception Handling with MiddlewareTypes

This sample demonstrates how to use middleware for centralized exception handling in function calls.
The example shows:

- How to catch exceptions thrown by functions and provide graceful error responses
- Overriding function results when errors occur to provide user-friendly messages
- Using middleware to implement retry logic, fallback mechanisms, or error reporting

The middleware catches TimeoutError from an unstable data service and replaces it with
a helpful message for the user, preventing raw exceptions from reaching the end user.
"""


# NOTE: approval_mode="never_require" is for sample brevity. Use "always_require" in production; see samples/02-agents/tools/function_tool_with_approval.py and samples/02-agents/tools/function_tool_with_approval_and_sessions.py.
@tool(approval_mode="never_require")
def unstable_data_service(
    query: Annotated[str, Field(description="The data query to execute.")],
) -> str:
    """A simulated data service that sometimes throws exceptions."""
    # Simulate failure
    raise TimeoutError("Data service request timed out")


async def exception_handling_middleware(
    context: FunctionInvocationContext, call_next: Callable[[], Awaitable[None]]
) -> None:
    function_name = context.function.name

    try:
        print(f"[ExceptionHandlingMiddleware] Executing function: {function_name}")
        await call_next()
        print(f"[ExceptionHandlingMiddleware] Function {function_name} completed successfully.")
    except TimeoutError as e:
        print(f"[ExceptionHandlingMiddleware] Caught TimeoutError: {e}")
        # Override function result to provide custom message in response.
        context.result = (
            "Request Timeout: The data service is taking longer than expected to respond. "
            "Respond with message - 'Sorry for the inconvenience, please try again later.'"
        )


async def main() -> None:
    """Example demonstrating exception handling with middleware."""
    print("=== Exception Handling MiddlewareTypes Example ===")

    # For authentication, run `az login` command in terminal or replace AzureCliCredential with preferred
    # authentication option.
    async with (
        AzureCliCredential() as credential,
        Agent(
            client=FoundryChatClient(credential=credential),
            name="DataAgent",
            instructions="You are a helpful data assistant. Use the data service tool to fetch information for users.",
            tools=unstable_data_service,
            middleware=[exception_handling_middleware],
        ) as agent,
    ):
        query = "Get user statistics"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Agent: {result}")


if __name__ == "__main__":
    asyncio.run(main())
```

:::zone-end

:::zone pivot="programming-language-go"

Go middleware receives the response stream from `next`, so it can handle provider or downstream middleware errors as they are yielded.

```go
fallback := agent.MiddlewareFunc(func(next agent.RunFunc, ctx context.Context, messages []*message.Message, options ...agent.Option) iter.Seq2[*agent.ResponseUpdate, error] {
    return func(yield func(*agent.ResponseUpdate, error) bool) {
        for update, err := range next(ctx, messages, options...) {
            if err != nil {
                yield(&agent.ResponseUpdate{
                    Contents: message.Contents{&message.TextContent{Text: "Sorry, I couldn't complete that request."}},
                }, nil)
                return
            }
            if !yield(update, nil) {
                return
            }
        }
    }
})
```

:::zone-end
## Next steps

> [!div class="nextstepaction"]
> [Shared State](./shared-state.md)
