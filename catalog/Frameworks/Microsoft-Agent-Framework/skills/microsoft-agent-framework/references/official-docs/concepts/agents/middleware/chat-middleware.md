---
title: "Chat-Level Middleware"
description: "Learn how to implement chat-level middleware in Agent Framework."
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: reference
ms.author: edvan
ms.date: 07/01/2026
ms.service: agent-framework
---

# Chat-Level Middleware

Chat-level middleware allows you to intercept and modify calls to the underlying chat client implementation. This is useful for logging, modifying prompts before they reach the AI service, or transforming responses.

:::zone pivot="programming-language-csharp"

Chat client middleware intercepts calls going from the agent to the `IChatClient`. Here's how to define and apply it:

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

// IChatClient middleware that logs requests and responses
async Task<ChatResponse> LoggingChatMiddleware(
    IEnumerable<ChatMessage> messages,
    ChatOptions? options,
    IChatClient innerChatClient,
    CancellationToken cancellationToken)
{
    Console.WriteLine($"[ChatLog] Sending {messages.Count()} messages to model...");
    foreach (var msg in messages)
    {
        Console.WriteLine($"[ChatLog]   {msg.Role}: {msg.Text?.Substring(0, Math.Min(msg.Text.Length, 80))}");
    }

    var response = await innerChatClient.GetResponseAsync(messages, options, cancellationToken);

    Console.WriteLine($"[ChatLog] Received {response.Messages.Count} response messages.");
    return response;
}

// Register IChatClient middleware using the client factory
var agent = new AIProjectClient(
    new Uri("<your-foundry-project-endpoint>"),
    new DefaultAzureCredential())
        .AsAIAgent(
            model: "gpt-4o-mini",
            instructions: "You are a helpful assistant.",
            clientFactory: (chatClient) => chatClient
                .AsBuilder()
                    .Use(getResponseFunc: LoggingChatMiddleware, getStreamingResponseFunc: null)
                .Build());

Console.WriteLine(await agent.RunAsync("Hello, how are you?"));
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

> [!NOTE]
> For more information about `IChatClient` middleware, see [Custom IChatClient middleware](/dotnet/ai/microsoft-extensions-ai#custom-ichatclient-middleware).

:::zone-end

:::zone pivot="programming-language-python"

### Class-based chat middleware

```python
# Copyright (c) Microsoft. All rights reserved.

import asyncio
from collections.abc import Awaitable, Callable
from random import randint
from typing import Annotated

from agent_framework import (
    ChatContext,
    ChatMiddleware,
    ChatResponse,
    Message,
    MiddlewareTermination,
    chat_middleware,
    tool,
)
from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient
from azure.identity.aio import AzureCliCredential
from pydantic import Field

"""
Chat MiddlewareTypes Example

This sample demonstrates how to use chat middleware to observe and override
inputs sent to AI models. Chat middleware intercepts chat requests before they reach
the underlying AI service, allowing you to:

1. Observe and log input messages
2. Modify input messages before sending to AI
3. Override the entire response

The example covers:
- Class-based chat middleware inheriting from ChatMiddleware
- Function-based chat middleware with @chat_middleware decorator
- MiddlewareTypes registration at agent level (applies to all runs)
- MiddlewareTypes registration at run level (applies to specific run only)
"""


# NOTE: approval_mode="never_require" is for sample brevity. Use "always_require" in production; see samples/02-agents/tools/function_tool_with_approval.py and samples/02-agents/tools/function_tool_with_approval_and_sessions.py.
@tool(approval_mode="never_require")
def get_weather(
    location: Annotated[str, Field(description="The location to get the weather for.")],
) -> str:
    """Get the weather for a given location."""
    conditions = ["sunny", "cloudy", "rainy", "stormy"]
    return f"The weather in {location} is {conditions[randint(0, 3)]} with a high of {randint(10, 30)}°C."


class InputObserverMiddleware(ChatMiddleware):
    """Class-based middleware that observes and modifies input messages."""

    def __init__(self, replacement: str | None = None):
        """Initialize with a replacement for user messages."""
        self.replacement = replacement

    async def process(
        self,
        context: ChatContext,
        call_next: Callable[[], Awaitable[None]],
    ) -> None:
        """Observe and modify input messages before they are sent to AI."""
        print("[InputObserverMiddleware] Observing input messages:")

        for i, message in enumerate(context.messages):
            content = message.text if message.text else str(message.contents)
            print(f"  Message {i + 1} ({message.role}): {content}")

        print(f"[InputObserverMiddleware] Total messages: {len(context.messages)}")

        # Modify user messages by creating new messages with enhanced text
        modified_messages: list[Message] = []
        modified_count = 0

        for message in context.messages:
            if message.role == "user" and message.text:
                original_text = message.text
                updated_text = original_text

                if self.replacement:
                    updated_text = self.replacement
                    print(f"[InputObserverMiddleware] Updated: '{original_text}' -> '{updated_text}'")

                modified_message = Message(message.role, [updated_text])
                modified_messages.append(modified_message)
                modified_count += 1
            else:
                modified_messages.append(message)

        # Replace messages in context
        context.messages[:] = modified_messages

        # Continue to next middleware or AI execution
        await call_next()

        # Observe that processing is complete
        print("[InputObserverMiddleware] Processing completed")


@chat_middleware
async def security_and_override_middleware(
    context: ChatContext,
    call_next: Callable[[], Awaitable[None]],
) -> None:
    """Function-based middleware that implements security filtering and response override."""
    print("[SecurityMiddleware] Processing input...")

    # Security check - block sensitive information
    blocked_terms = ["password", "secret", "api_key", "token"]

    for message in context.messages:
        if message.text:
            message_lower = message.text.lower()
            for term in blocked_terms:
                if term in message_lower:
                    print(f"[SecurityMiddleware] BLOCKED: Found '{term}' in message")

                    # Override the response instead of calling AI
                    context.result = ChatResponse(
                        messages=[
                            Message(
                                role="assistant",
                                contents=[
                                    (
                                        "I cannot process requests containing sensitive information. "
                                        "Please rephrase your question without including passwords, secrets, or other "
                                        "sensitive data."
                                    )
                                ],
                            )
                        ]
                    )

                    # Raise MiddlewareTermination to stop execution after setting context.result
                    raise MiddlewareTermination

    # Continue to next middleware or AI execution
    await call_next()


async def class_based_chat_middleware() -> None:
    """Demonstrate class-based middleware at agent level."""
    print("\n" + "=" * 60)
    print("Class-based Chat MiddlewareTypes (Agent Level)")
    print("=" * 60)

    # For authentication, run `az login` command in terminal or replace AzureCliCredential with preferred
    # authentication option.
    async with (
        AzureCliCredential() as credential,
        Agent(
            client=FoundryChatClient(credential=credential),
            name="EnhancedChatAgent",
            instructions="You are a helpful AI assistant.",
            # Register class-based middleware at agent level (applies to all runs)
            middleware=[InputObserverMiddleware()],
            tools=get_weather,
        ) as agent,
    ):
        query = "What's the weather in Seattle?"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Final Response: {result.text if result.text else 'No response'}")


async def function_based_chat_middleware() -> None:
    """Demonstrate function-based middleware at agent level."""
    print("\n" + "=" * 60)
    print("Function-based Chat MiddlewareTypes (Agent Level)")
    print("=" * 60)

    async with (
        AzureCliCredential() as credential,
        Agent(
            client=FoundryChatClient(credential=credential),
            name="FunctionMiddlewareAgent",
            instructions="You are a helpful AI assistant.",
            # Register function-based middleware at agent level
            middleware=[security_and_override_middleware],
        ) as agent,
    ):
        # Scenario with normal query
        print("\n--- Scenario 1: Normal Query ---")
        query = "Hello, how are you?"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Final Response: {result.text if result.text else 'No response'}")

        # Scenario with security violation
        print("\n--- Scenario 2: Security Violation ---")
        query = "What is my password for this account?"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Final Response: {result.text if result.text else 'No response'}")


async def run_level_middleware() -> None:
    """Demonstrate middleware registration at run level."""
    print("\n" + "=" * 60)
    print("Run-level Chat MiddlewareTypes")
    print("=" * 60)

    async with (
        AzureCliCredential() as credential,
        Agent(
            client=FoundryChatClient(credential=credential),
            name="RunLevelAgent",
            instructions="You are a helpful AI assistant.",
            tools=get_weather,
            # No middleware at agent level
        ) as agent,
    ):
        # Scenario 1: Run without any middleware
        print("\n--- Scenario 1: No MiddlewareTypes ---")
        query = "What's the weather in Tokyo?"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Response: {result.text if result.text else 'No response'}")

        # Scenario 2: Run with specific middleware for this call only (both enhancement and security)
        print("\n--- Scenario 2: With Run-level MiddlewareTypes ---")
        print(f"User: {query}")
        result = await agent.run(
            query,
            middleware=[
                InputObserverMiddleware(replacement="What's the weather in Madrid?"),
                security_and_override_middleware,
            ],
        )
        print(f"Response: {result.text if result.text else 'No response'}")

        # Scenario 3: Security test with run-level middleware
        print("\n--- Scenario 3: Security Test with Run-level MiddlewareTypes ---")
        query = "Can you help me with my secret API key?"
        print(f"User: {query}")
        result = await agent.run(
            query,
            middleware=[security_and_override_middleware],
        )
        print(f"Response: {result.text if result.text else 'No response'}")


async def main() -> None:
    """Run all chat middleware examples."""
    print("Chat MiddlewareTypes Examples")
    print("========================")

    await class_based_chat_middleware()
    await function_based_chat_middleware()
    await run_level_middleware()


if __name__ == "__main__":
    asyncio.run(main())
```

### Decorator-based chat middleware

```python
# Copyright (c) Microsoft. All rights reserved.

import asyncio
from collections.abc import Awaitable, Callable
from random import randint
from typing import Annotated

from agent_framework import (
    ChatContext,
    ChatMiddleware,
    ChatResponse,
    Message,
    MiddlewareTermination,
    chat_middleware,
    tool,
)
from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient
from azure.identity.aio import AzureCliCredential
from pydantic import Field

"""
Chat MiddlewareTypes Example

This sample demonstrates how to use chat middleware to observe and override
inputs sent to AI models. Chat middleware intercepts chat requests before they reach
the underlying AI service, allowing you to:

1. Observe and log input messages
2. Modify input messages before sending to AI
3. Override the entire response

The example covers:
- Class-based chat middleware inheriting from ChatMiddleware
- Function-based chat middleware with @chat_middleware decorator
- MiddlewareTypes registration at agent level (applies to all runs)
- MiddlewareTypes registration at run level (applies to specific run only)
"""


# NOTE: approval_mode="never_require" is for sample brevity. Use "always_require" in production; see samples/02-agents/tools/function_tool_with_approval.py and samples/02-agents/tools/function_tool_with_approval_and_sessions.py.
@tool(approval_mode="never_require")
def get_weather(
    location: Annotated[str, Field(description="The location to get the weather for.")],
) -> str:
    """Get the weather for a given location."""
    conditions = ["sunny", "cloudy", "rainy", "stormy"]
    return f"The weather in {location} is {conditions[randint(0, 3)]} with a high of {randint(10, 30)}°C."


class InputObserverMiddleware(ChatMiddleware):
    """Class-based middleware that observes and modifies input messages."""

    def __init__(self, replacement: str | None = None):
        """Initialize with a replacement for user messages."""
        self.replacement = replacement

    async def process(
        self,
        context: ChatContext,
        call_next: Callable[[], Awaitable[None]],
    ) -> None:
        """Observe and modify input messages before they are sent to AI."""
        print("[InputObserverMiddleware] Observing input messages:")

        for i, message in enumerate(context.messages):
            content = message.text if message.text else str(message.contents)
            print(f"  Message {i + 1} ({message.role}): {content}")

        print(f"[InputObserverMiddleware] Total messages: {len(context.messages)}")

        # Modify user messages by creating new messages with enhanced text
        modified_messages: list[Message] = []
        modified_count = 0

        for message in context.messages:
            if message.role == "user" and message.text:
                original_text = message.text
                updated_text = original_text

                if self.replacement:
                    updated_text = self.replacement
                    print(f"[InputObserverMiddleware] Updated: '{original_text}' -> '{updated_text}'")

                modified_message = Message(message.role, [updated_text])
                modified_messages.append(modified_message)
                modified_count += 1
            else:
                modified_messages.append(message)

        # Replace messages in context
        context.messages[:] = modified_messages

        # Continue to next middleware or AI execution
        await call_next()

        # Observe that processing is complete
        print("[InputObserverMiddleware] Processing completed")


@chat_middleware
async def security_and_override_middleware(
    context: ChatContext,
    call_next: Callable[[], Awaitable[None]],
) -> None:
    """Function-based middleware that implements security filtering and response override."""
    print("[SecurityMiddleware] Processing input...")

    # Security check - block sensitive information
    blocked_terms = ["password", "secret", "api_key", "token"]

    for message in context.messages:
        if message.text:
            message_lower = message.text.lower()
            for term in blocked_terms:
                if term in message_lower:
                    print(f"[SecurityMiddleware] BLOCKED: Found '{term}' in message")

                    # Override the response instead of calling AI
                    context.result = ChatResponse(
                        messages=[
                            Message(
                                role="assistant",
                                contents=[
                                    (
                                        "I cannot process requests containing sensitive information. "
                                        "Please rephrase your question without including passwords, secrets, or other "
                                        "sensitive data."
                                    )
                                ],
                            )
                        ]
                    )

                    # Raise MiddlewareTermination to stop execution after setting context.result
                    raise MiddlewareTermination

    # Continue to next middleware or AI execution
    await call_next()


async def class_based_chat_middleware() -> None:
    """Demonstrate class-based middleware at agent level."""
    print("\n" + "=" * 60)
    print("Class-based Chat MiddlewareTypes (Agent Level)")
    print("=" * 60)

    # For authentication, run `az login` command in terminal or replace AzureCliCredential with preferred
    # authentication option.
    async with (
        AzureCliCredential() as credential,
        Agent(
            client=FoundryChatClient(credential=credential),
            name="EnhancedChatAgent",
            instructions="You are a helpful AI assistant.",
            # Register class-based middleware at agent level (applies to all runs)
            middleware=[InputObserverMiddleware()],
            tools=get_weather,
        ) as agent,
    ):
        query = "What's the weather in Seattle?"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Final Response: {result.text if result.text else 'No response'}")


async def function_based_chat_middleware() -> None:
    """Demonstrate function-based middleware at agent level."""
    print("\n" + "=" * 60)
    print("Function-based Chat MiddlewareTypes (Agent Level)")
    print("=" * 60)

    async with (
        AzureCliCredential() as credential,
        Agent(
            client=FoundryChatClient(credential=credential),
            name="FunctionMiddlewareAgent",
            instructions="You are a helpful AI assistant.",
            # Register function-based middleware at agent level
            middleware=[security_and_override_middleware],
        ) as agent,
    ):
        # Scenario with normal query
        print("\n--- Scenario 1: Normal Query ---")
        query = "Hello, how are you?"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Final Response: {result.text if result.text else 'No response'}")

        # Scenario with security violation
        print("\n--- Scenario 2: Security Violation ---")
        query = "What is my password for this account?"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Final Response: {result.text if result.text else 'No response'}")


async def run_level_middleware() -> None:
    """Demonstrate middleware registration at run level."""
    print("\n" + "=" * 60)
    print("Run-level Chat MiddlewareTypes")
    print("=" * 60)

    async with (
        AzureCliCredential() as credential,
        Agent(
            client=FoundryChatClient(credential=credential),
            name="RunLevelAgent",
            instructions="You are a helpful AI assistant.",
            tools=get_weather,
            # No middleware at agent level
        ) as agent,
    ):
        # Scenario 1: Run without any middleware
        print("\n--- Scenario 1: No MiddlewareTypes ---")
        query = "What's the weather in Tokyo?"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Response: {result.text if result.text else 'No response'}")

        # Scenario 2: Run with specific middleware for this call only (both enhancement and security)
        print("\n--- Scenario 2: With Run-level MiddlewareTypes ---")
        print(f"User: {query}")
        result = await agent.run(
            query,
            middleware=[
                InputObserverMiddleware(replacement="What's the weather in Madrid?"),
                security_and_override_middleware,
            ],
        )
        print(f"Response: {result.text if result.text else 'No response'}")

        # Scenario 3: Security test with run-level middleware
        print("\n--- Scenario 3: Security Test with Run-level MiddlewareTypes ---")
        query = "Can you help me with my secret API key?"
        print(f"User: {query}")
        result = await agent.run(
            query,
            middleware=[security_and_override_middleware],
        )
        print(f"Response: {result.text if result.text else 'No response'}")


async def main() -> None:
    """Run all chat middleware examples."""
    print("Chat MiddlewareTypes Examples")
    print("========================")

    await class_based_chat_middleware()
    await function_based_chat_middleware()
    await run_level_middleware()


if __name__ == "__main__":
    asyncio.run(main())
```

:::zone-end

:::zone pivot="programming-language-go"

Go chat middleware implements `agent.Middleware`. Use `agent.MiddlewareFunc` for function-based middleware that can inspect or modify messages and options before invoking the next layer.

```go
logging := agent.MiddlewareFunc(func(next agent.RunFunc, ctx context.Context, messages []*message.Message, options ...agent.Option) iter.Seq2[*agent.ResponseUpdate, error] {
    log.Printf("sending %d messages", len(messages))
    return next(ctx, messages, options...)
})

a := foundryprovider.NewAgent(endpoint, token, foundryprovider.ModelDeployment(model), foundryprovider.AgentConfig{
    Config: agent.Config{
        Middlewares: []agent.Middleware{logging},
    },
})
```

:::zone-end
## Next steps

> [!div class="nextstepaction"]
> [Agent vs Run Scope](./agent-vs-run-scope.md)
