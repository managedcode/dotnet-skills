---
title: "Shared State"
description: "Learn how to share state across middleware components."
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: reference
ms.author: edvan
ms.date: 05/27/2026
ms.service: agent-framework
---

# Shared State

Shared state allows middleware components to communicate and share data during the processing of an agent request. This is useful for passing information between middleware in the chain, such as timing data, request IDs, or accumulated metrics.

:::zone pivot="programming-language-csharp"

In C#, middleware can use a shared `AgentRunOptions` or custom context objects to pass state between middleware components. You can also use the `Use(sharedFunc: ...)` overload for input-only inspection middleware.

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

// Shared state container that middleware instances can reference
var sharedState = new Dictionary<string, object> { ["callCount"] = 0 };

// Middleware that increments a shared call counter
async Task<AgentResponse> CounterMiddleware(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
{
    var count = (int)sharedState["callCount"] + 1;
    sharedState["callCount"] = count;
    Console.WriteLine($"[Counter] Call #{count}");

    return await innerAgent.RunAsync(messages, session, options, cancellationToken);
}

// Middleware that reads shared state to enrich output
async Task<AgentResponse> EnrichMiddleware(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
{
    var response = await innerAgent.RunAsync(messages, session, options, cancellationToken);
    var count = (int)sharedState["callCount"];
    Console.WriteLine($"[Enrich] Total calls so far: {count}");
    return response;
}

AIAgent agent = new AIProjectClient(
    new Uri("<your-foundry-project-endpoint>"),
    new DefaultAzureCredential())
        .AsAIAgent(
            model: "gpt-4o-mini",
            instructions: "You are a helpful assistant.");

var agentWithState = agent
    .AsBuilder()
        .Use(runFunc: CounterMiddleware, runStreamingFunc: null)
        .Use(runFunc: EnrichMiddleware, runStreamingFunc: null)
    .Build();

Console.WriteLine(await agentWithState.RunAsync("What's the weather in New York?"));
Console.WriteLine(await agentWithState.RunAsync("What time is it in London?"));
Console.WriteLine($"Total calls: {sharedState["callCount"]}");
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

:::zone-end

:::zone pivot="programming-language-python"

### Middleware container with shared state

The following example shows how to use a middleware container to share state across middleware components:

```python
# Copyright (c) Microsoft. All rights reserved.

import asyncio
from collections.abc import Awaitable, Callable
from random import randint
from typing import Annotated

from agent_framework import (
    FunctionInvocationContext,
    tool,
)
from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient
from azure.identity.aio import AzureCliCredential
from pydantic import Field

"""
Shared State Function-based MiddlewareTypes Example

This sample demonstrates how to implement function-based middleware within a class to share state.
The example includes:

- A MiddlewareContainer class with two simple function middleware methods
- First middleware: Counts function calls and stores the count in shared state
- Second middleware: Uses the shared count to add call numbers to function results

This approach shows how middleware can work together by sharing state within the same class instance.
"""


# NOTE: approval_mode="never_require" is for sample brevity. Use "always_require" in production; see samples/02-agents/tools/function_tool_with_approval.py and samples/02-agents/tools/function_tool_with_approval_and_sessions.py.
@tool(approval_mode="never_require")
def get_weather(
    location: Annotated[str, Field(description="The location to get the weather for.")],
) -> str:
    """Get the weather for a given location."""
    conditions = ["sunny", "cloudy", "rainy", "stormy"]
    return f"The weather in {location} is {conditions[randint(0, 3)]} with a high of {randint(10, 30)}°C."


@tool(approval_mode="never_require")
def get_time(
    timezone: Annotated[str, Field(description="The timezone to get the time for.")] = "UTC",
) -> str:
    """Get the current time for a given timezone."""
    import datetime

    return f"The current time in {timezone} is {datetime.datetime.now().strftime('%H:%M:%S')}"


class MiddlewareContainer:
    """Container class that holds middleware functions with shared state."""

    def __init__(self) -> None:
        # Simple shared state: count function calls
        self.call_count: int = 0

    async def call_counter_middleware(
        self,
        context: FunctionInvocationContext,
        call_next: Callable[[], Awaitable[None]],
    ) -> None:
        """First middleware: increments call count in shared state."""
        # Increment the shared call count
        self.call_count += 1

        print(f"[CallCounter] This is function call #{self.call_count}")

        # Call the next middleware/function
        await call_next()

    async def result_enhancer_middleware(
        self,
        context: FunctionInvocationContext,
        call_next: Callable[[], Awaitable[None]],
    ) -> None:
        """Second middleware: uses shared call count to enhance function results."""
        print(f"[ResultEnhancer] Current total calls so far: {self.call_count}")

        # Call the next middleware/function
        await call_next()

        # After function execution, enhance the result using shared state
        if context.result:
            enhanced_result = f"[Call #{self.call_count}] {context.result}"
            context.result = enhanced_result
            print("[ResultEnhancer] Enhanced result with call number")


async def main() -> None:
    """Example demonstrating shared state function-based middleware."""
    print("=== Shared State Function-based MiddlewareTypes Example ===")

    # Create middleware container with shared state
    middleware_container = MiddlewareContainer()

    # For authentication, run `az login` command in terminal or replace AzureCliCredential with preferred
    # authentication option.
    async with (
        AzureCliCredential() as credential,
        Agent(
            client=FoundryChatClient(credential=credential),
            name="UtilityAgent",
            instructions="You are a helpful assistant that can provide weather information and current time.",
            tools=[get_weather, get_time],
            # Pass both middleware functions from the same container instance
            # Order matters: counter runs first to increment count,
            # then result enhancer uses the updated count
            middleware=[
                middleware_container.call_counter_middleware,
                middleware_container.result_enhancer_middleware,
            ],
        ) as agent,
    ):
        # Test multiple requests to see shared state in action
        queries = [
            "What's the weather like in New York?",
            "What time is it in London?",
            "What's the weather in Tokyo?",
        ]

        for i, query in enumerate(queries, 1):
            print(f"\n--- Query {i} ---")
            print(f"User: {query}")
            result = await agent.run(query)
            print(f"Agent: {result.text if result.text else 'No response'}")

        # Display final statistics
        print("\n=== Final Statistics ===")
        print(f"Total function calls made: {middleware_container.call_count}")


if __name__ == "__main__":
    asyncio.run(main())
```

:::zone-end

:::zone pivot="programming-language-go"

Use `agent.Session` for state that should follow a conversation across runs. Middleware can read the session from options with `agent.GetOption`.

```go
const countKey = "run_count"

counter := agent.MiddlewareFunc(func(next agent.RunFunc, ctx context.Context, messages []*message.Message, options ...agent.Option) iter.Seq2[*agent.ResponseUpdate, error] {
    session, _ := agent.GetOption(options, agent.WithSession)
    var count int
    _, _ = session.Get(countKey, &count)
    session.Set(countKey, count+1)
    return next(ctx, messages, options...)
})
```

:::zone-end
## Next steps

> [!div class="nextstepaction"]
> [Runtime Context](./runtime-context.md)
