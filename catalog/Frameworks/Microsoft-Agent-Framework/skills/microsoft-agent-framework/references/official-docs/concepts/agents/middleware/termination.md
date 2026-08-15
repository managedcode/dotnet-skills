---
title: "Termination & Guardrails"
description: "Learn how to implement termination conditions and guardrails with middleware."
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: reference
ms.author: edvan
ms.date: 05/27/2026
ms.service: agent-framework
---

# Termination & Guardrails

Middleware can be used to implement guardrails that control when an agent should stop processing, enforce content policies, or limit conversation length.

:::zone pivot="programming-language-csharp"

In C#, you can implement guardrails using agent run middleware or function calling middleware. Here's an example of a guardrail middleware:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

// Guardrail middleware that checks input and can return early without calling the agent
async Task<AgentResponse> GuardrailMiddleware(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
{
    // Pre-execution check: block requests containing sensitive words
    var lastMessage = messages.LastOrDefault()?.Text?.ToLower() ?? "";
    string[] blockedWords = ["password", "secret", "credentials"];

    foreach (var word in blockedWords)
    {
        if (lastMessage.Contains(word))
        {
            Console.WriteLine($"[Guardrail] Blocked request containing '{word}'.");
            return new AgentResponse([new ChatMessage(ChatRole.Assistant,
                $"Sorry, I cannot process requests containing '{word}'.")]);
        }
    }

    // Input passed validation — proceed with agent execution
    var response = await innerAgent.RunAsync(messages, session, options, cancellationToken);

    // Post-execution check: validate the output
    var responseText = response.Messages.LastOrDefault()?.Text ?? "";
    if (responseText.Length > 5000)
    {
        Console.WriteLine("[Guardrail] Response too long, truncating.");
        return new AgentResponse([new ChatMessage(ChatRole.Assistant,
            responseText.Substring(0, 5000) + "... [truncated]")]);
    }

    return response;
}

AIAgent agent = new AIProjectClient(
    new Uri("<your-foundry-project-endpoint>"),
    new DefaultAzureCredential())
        .AsAIAgent(
            model: "gpt-4o-mini",
            instructions: "You are a helpful assistant.");

var guardedAgent = agent
    .AsBuilder()
        .Use(runFunc: GuardrailMiddleware, runStreamingFunc: null)
    .Build();

// Normal request — passes guardrail
Console.WriteLine(await guardedAgent.RunAsync("What's the weather in Seattle?"));

// Blocked request — guardrail returns early without calling agent
Console.WriteLine(await guardedAgent.RunAsync("What is my password?"));
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

:::zone-end

:::zone pivot="programming-language-python"

In Python, middleware stops execution by setting `context.result` when needed and raising `MiddlewareTermination`, or by short-circuiting the chain without calling `call_next()`.

> [!NOTE]
> History providers normally persist once after the full `agent.run()`. If your run can make multiple model calls (for example through tool loops) and you want local history to match service-managed conversation behavior when termination happens after a tool call, create the agent with `require_per_service_call_history_persistence=True`.

### Pre-termination middleware

Middleware that terminates before agent execution — useful for blocking disallowed content:

```python
# Copyright (c) Microsoft. All rights reserved.

import asyncio
from collections.abc import Awaitable, Callable
from random import randint
from typing import Annotated

from agent_framework import (
    AgentContext,
    AgentMiddleware,
    AgentResponse,
    Message,
    MiddlewareTermination,
    tool,
)
from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient
from azure.identity.aio import AzureCliCredential

"""
MiddlewareTypes Termination Example

This sample demonstrates how middleware can terminate execution using the `MiddlewareTermination` exception.
The example includes:

- PreTerminationMiddleware: Terminates execution before calling call_next() to prevent agent processing
- PostTerminationMiddleware: Allows processing to complete but terminates further execution

This is useful for implementing security checks, rate limiting, or early exit conditions.
"""


# NOTE: approval_mode="never_require" is for sample brevity. Use "always_require" in production; see samples/02-agents/tools/function_tool_with_approval.py and samples/02-agents/tools/function_tool_with_approval_and_sessions.py.
@tool(approval_mode="never_require")
def get_weather(
    location: Annotated[str, "The location to get the weather for."],
) -> str:
    """Get the weather for a given location."""
    conditions = ["sunny", "cloudy", "rainy", "stormy"]
    return f"The weather in {location} is {conditions[randint(0, 3)]} with a high of {randint(10, 30)}°C."


class PreTerminationMiddleware(AgentMiddleware):
    """MiddlewareTypes that terminates execution before calling the agent."""

    def __init__(self, blocked_words: list[str]):
        self.blocked_words = [word.lower() for word in blocked_words]

    async def process(
        self,
        context: AgentContext,
        call_next: Callable[[], Awaitable[None]],
    ) -> None:
        # Check if the user message contains any blocked words
        last_message = context.messages[-1] if context.messages else None
        if last_message and last_message.text:
            query = last_message.text.lower()
            for blocked_word in self.blocked_words:
                if blocked_word in query:
                    print(f"[PreTerminationMiddleware] Blocked word '{blocked_word}' detected. Terminating request.")

                    # Set a custom response
                    context.result = AgentResponse(
                        messages=[
                            Message(
                                role="assistant",
                                contents=[
                                    (
                                        f"Sorry, I cannot process requests containing '{blocked_word}'. "
                                        "Please rephrase your question."
                                    )
                                ],
                            )
                        ]
                    )

                    # Terminate to prevent further processing
                    raise MiddlewareTermination(result=context.result)

        await call_next()


class PostTerminationMiddleware(AgentMiddleware):
    """MiddlewareTypes that allows processing but terminates after reaching max responses across multiple runs."""

    def __init__(self, max_responses: int = 1):
        self.max_responses = max_responses
        self.response_count = 0

    async def process(
        self,
        context: AgentContext,
        call_next: Callable[[], Awaitable[None]],
    ) -> None:
        print(f"[PostTerminationMiddleware] Processing request (response count: {self.response_count})")

        # Check if we should terminate before processing
        if self.response_count >= self.max_responses:
            print(
                f"[PostTerminationMiddleware] Maximum responses ({self.max_responses}) reached. "
                "Terminating further processing."
            )
            raise MiddlewareTermination

        # Allow the agent to process normally
        await call_next()

        # Increment response count after processing
        self.response_count += 1


async def pre_termination_middleware() -> None:
    """Demonstrate pre-termination middleware that blocks requests with certain words."""
    print("\n--- Example 1: Pre-termination MiddlewareTypes ---")
    async with (
        AzureCliCredential() as credential,
        Agent(
            client=FoundryChatClient(credential=credential),
            name="WeatherAgent",
            instructions="You are a helpful weather assistant.",
            tools=get_weather,
            middleware=[PreTerminationMiddleware(blocked_words=["bad", "inappropriate"])],
        ) as agent,
    ):
        # Test with normal query
        print("\n1. Normal query:")
        query = "What's the weather like in Seattle?"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Agent: {result.text}")

        # Test with blocked word
        print("\n2. Query with blocked word:")
        query = "What's the bad weather in New York?"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Agent: {result.text}")


async def post_termination_middleware() -> None:
    """Demonstrate post-termination middleware that limits responses across multiple runs."""
    print("\n--- Example 2: Post-termination MiddlewareTypes ---")
    async with (
        AzureCliCredential() as credential,
        Agent(
            client=FoundryChatClient(credential=credential),
            name="WeatherAgent",
            instructions="You are a helpful weather assistant.",
            tools=get_weather,
            middleware=[PostTerminationMiddleware(max_responses=1)],
        ) as agent,
    ):
        # First run (should work)
        print("\n1. First run:")
        query = "What's the weather in Paris?"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Agent: {result.text}")

        # Second run (should be terminated by middleware)
        print("\n2. Second run (should be terminated):")
        query = "What about the weather in London?"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Agent: {result.text if result and result.text else 'No response (terminated)'}")

        # Third run (should also be terminated)
        print("\n3. Third run (should also be terminated):")
        query = "And New York?"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Agent: {result.text if result and result.text else 'No response (terminated)'}")


async def main() -> None:
    """Example demonstrating middleware termination functionality."""
    print("=== MiddlewareTypes Termination Example ===")
    await pre_termination_middleware()
    await post_termination_middleware()


if __name__ == "__main__":
    asyncio.run(main())
```

### Post-termination middleware

Middleware that terminates after agent execution — useful for validating responses:

```python
# Copyright (c) Microsoft. All rights reserved.

import asyncio
from collections.abc import Awaitable, Callable
from random import randint
from typing import Annotated

from agent_framework import (
    AgentContext,
    AgentMiddleware,
    AgentResponse,
    Message,
    MiddlewareTermination,
    tool,
)
from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient
from azure.identity.aio import AzureCliCredential
from pydantic import Field

"""
MiddlewareTypes Termination Example

This sample demonstrates how middleware can terminate execution using the `MiddlewareTermination` exception.
The example includes:

- PreTerminationMiddleware: Terminates execution before calling call_next() to prevent agent processing
- PostTerminationMiddleware: Allows processing to complete but terminates further execution

This is useful for implementing security checks, rate limiting, or early exit conditions.
"""


# NOTE: approval_mode="never_require" is for sample brevity. Use "always_require" in production; see samples/02-agents/tools/function_tool_with_approval.py and samples/02-agents/tools/function_tool_with_approval_and_sessions.py.
@tool(approval_mode="never_require")
def get_weather(
    location: Annotated[str, Field(description="The location to get the weather for.")],
) -> str:
    """Get the weather for a given location."""
    conditions = ["sunny", "cloudy", "rainy", "stormy"]
    return f"The weather in {location} is {conditions[randint(0, 3)]} with a high of {randint(10, 30)}°C."


class PreTerminationMiddleware(AgentMiddleware):
    """MiddlewareTypes that terminates execution before calling the agent."""

    def __init__(self, blocked_words: list[str]):
        self.blocked_words = [word.lower() for word in blocked_words]

    async def process(
        self,
        context: AgentContext,
        call_next: Callable[[], Awaitable[None]],
    ) -> None:
        # Check if the user message contains any blocked words
        last_message = context.messages[-1] if context.messages else None
        if last_message and last_message.text:
            query = last_message.text.lower()
            for blocked_word in self.blocked_words:
                if blocked_word in query:
                    print(f"[PreTerminationMiddleware] Blocked word '{blocked_word}' detected. Terminating request.")

                    # Set a custom response
                    context.result = AgentResponse(
                        messages=[
                            Message(
                                role="assistant",
                                contents=[
                                    (
                                        f"Sorry, I cannot process requests containing '{blocked_word}'. "
                                        "Please rephrase your question."
                                    )
                                ],
                            )
                        ]
                    )

                    # Terminate to prevent further processing
                    raise MiddlewareTermination(result=context.result)

        await call_next()


class PostTerminationMiddleware(AgentMiddleware):
    """MiddlewareTypes that allows processing but terminates after reaching max responses across multiple runs."""

    def __init__(self, max_responses: int = 1):
        self.max_responses = max_responses
        self.response_count = 0

    async def process(
        self,
        context: AgentContext,
        call_next: Callable[[], Awaitable[None]],
    ) -> None:
        print(f"[PostTerminationMiddleware] Processing request (response count: {self.response_count})")

        # Check if we should terminate before processing
        if self.response_count >= self.max_responses:
            print(
                f"[PostTerminationMiddleware] Maximum responses ({self.max_responses}) reached. "
                "Terminating further processing."
            )
            raise MiddlewareTermination

        # Allow the agent to process normally
        await call_next()

        # Increment response count after processing
        self.response_count += 1


async def pre_termination_middleware() -> None:
    """Demonstrate pre-termination middleware that blocks requests with certain words."""
    print("\n--- Example 1: Pre-termination MiddlewareTypes ---")
    async with (
        AzureCliCredential() as credential,
        Agent(
            client=FoundryChatClient(credential=credential),
            name="WeatherAgent",
            instructions="You are a helpful weather assistant.",
            tools=get_weather,
            middleware=[PreTerminationMiddleware(blocked_words=["bad", "inappropriate"])],
        ) as agent,
    ):
        # Test with normal query
        print("\n1. Normal query:")
        query = "What's the weather like in Seattle?"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Agent: {result.text}")

        # Test with blocked word
        print("\n2. Query with blocked word:")
        query = "What's the bad weather in New York?"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Agent: {result.text}")


async def post_termination_middleware() -> None:
    """Demonstrate post-termination middleware that limits responses across multiple runs."""
    print("\n--- Example 2: Post-termination MiddlewareTypes ---")
    async with (
        AzureCliCredential() as credential,
        Agent(
            client=FoundryChatClient(credential=credential),
            name="WeatherAgent",
            instructions="You are a helpful weather assistant.",
            tools=get_weather,
            middleware=[PostTerminationMiddleware(max_responses=1)],
        ) as agent,
    ):
        # First run (should work)
        print("\n1. First run:")
        query = "What's the weather in Paris?"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Agent: {result.text}")

        # Second run (should be terminated by middleware)
        print("\n2. Second run (should be terminated):")
        query = "What about the weather in London?"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Agent: {result.text if result and result.text else 'No response (terminated)'}")

        # Third run (should also be terminated)
        print("\n3. Third run (should also be terminated):")
        query = "And New York?"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Agent: {result.text if result and result.text else 'No response (terminated)'}")


async def main() -> None:
    """Example demonstrating middleware termination functionality."""
    print("=== MiddlewareTypes Termination Example ===")
    await pre_termination_middleware()
    await post_termination_middleware()


if __name__ == "__main__":
    asyncio.run(main())
```

:::zone-end

:::zone pivot="programming-language-go"

Go middleware can terminate a run before provider invocation by returning without calling `next`, or it can stop forwarding updates after a condition is met.

```go
guardrail := agent.MiddlewareFunc(func(next agent.RunFunc, ctx context.Context, messages []*message.Message, options ...agent.Option) iter.Seq2[*agent.ResponseUpdate, error] {
    return func(yield func(*agent.ResponseUpdate, error) bool) {
        if violatesPolicy(messages) {
            yield(&agent.ResponseUpdate{
                Contents: message.Contents{&message.TextContent{Text: "I can't help with that request."}},
            }, nil)
            return
        }

        for update, err := range next(ctx, messages, options...) {
            if !yield(update, err) {
                return
            }
        }
    }
})
```

:::zone-end
## Next steps

> [!div class="nextstepaction"]
> [Result Overrides](./result-overrides.md)
