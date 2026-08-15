---
title: Agent Middleware
description: Learn how to create middleware with Agent Framework
zone_pivot_groups: programming-languages
author: dmytrostruk
ms.topic: reference
ms.author: dmytrostruk
ms.date: 07/01/2026
ms.service: agent-framework
---

# Agent Middleware

Middleware in Agent Framework provides a powerful way to intercept, modify, and enhance agent interactions at various stages of execution. You can use middleware to implement cross-cutting concerns such as logging, security validation, error handling, and result transformation without modifying your core agent or function logic.

::: zone pivot="programming-language-csharp"

Agent Framework can be customized using three different types of middleware:

1. Agent Run middleware: Allows interception of all agent runs, so that input and output can be inspected and/or modified as needed.
1. Function calling middleware: Allows interception of all function calls executed by the agent, so that input and output can be inspected and modified as needed.
1. <xref:Microsoft.Extensions.AI.IChatClient> middleware: Allows interception of calls to an `IChatClient` implementation, where an agent is using `IChatClient` for inference calls, for example, when using `ChatClientAgent`.

All the types of middleware are implemented via a function callback, and when multiple middleware instances of the same type are registered, they form a chain,
where each middleware instance is expected to call the next in the chain, via a provided `next` `Func`.

Agent run and function calling middleware types can be registered on an agent, by using the agent builder with an existing agent object.

```csharp
var middlewareEnabledAgent = originalAgent
    .AsBuilder()
        .Use(runFunc: CustomAgentRunMiddleware, runStreamingFunc: CustomAgentRunStreamingMiddleware)
        .Use(CustomFunctionCallingMiddleware)
    .Build();
```

> [!IMPORTANT]
> Ideally both `runFunc` and `runStreamingFunc` should be provided. When providing just the non-streaming middleware, the agent will use it for both streaming and non-streaming invocations. Streaming will only run in non-streaming mode to suffice the middleware expectations.

> [!NOTE]
> There's an additional overload, `Use(sharedFunc: ...)`, that allows you to provide the same middleware for non-streaming and streaming without blocking the streaming. However, the shared middleware won't be able to intercept or override the output. This overload should be used for scenarios where you only need to inspect or modify the input before it reaches the agent.

`IChatClient` middleware can be registered on an `IChatClient` before it is used with a `ChatClientAgent`, by using the chat client builder pattern.

```csharp
var chatClient = new AIProjectClient(
    new Uri("<your-foundry-project-endpoint>"),
    new DefaultAzureCredential())
        .GetProjectOpenAIClient()
        .GetProjectResponsesClient()
        .AsIChatClient(deploymentName);

var middlewareEnabledChatClient = chatClient
    .AsBuilder()
        .Use(getResponseFunc: CustomChatClientMiddleware, getStreamingResponseFunc: null)
    .Build();

var agent = new ChatClientAgent(middlewareEnabledChatClient, instructions: "You are a helpful assistant.");
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

`IChatClient` middleware can also be registered using a factory method when constructing
 an agent via one of the helper methods on SDK clients.

```csharp
var agent = new AIProjectClient(
    new Uri("<your-foundry-project-endpoint>"),
    new DefaultAzureCredential())
        .AsAIAgent(
            model: deploymentName,
            instructions: "You are a helpful assistant.",
            clientFactory: (chatClient) => chatClient
                .AsBuilder()
                    .Use(getResponseFunc: CustomChatClientMiddleware, getStreamingResponseFunc: null)
                .Build());
```

## Agent Run Middleware

Here is an example of agent run middleware, that can inspect and/or modify the input and output from the agent run.

```csharp
async Task<AgentResponse> CustomAgentRunMiddleware(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
{
    Console.WriteLine(messages.Count());
    var response = await innerAgent.RunAsync(messages, session, options, cancellationToken).ConfigureAwait(false);
    Console.WriteLine(response.Messages.Count);
    return response;
}
```

## Agent Run Streaming Middleware

Here is an example of agent run streaming middleware, that can inspect and/or modify the input and output from the agent streaming run.

```csharp
    async IAsyncEnumerable<AgentResponseUpdate> CustomAgentRunStreamingMiddleware(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    Console.WriteLine(messages.Count());
    List<AgentResponseUpdate> updates = [];
    await foreach (var update in innerAgent.RunStreamingAsync(messages, session, options, cancellationToken))
    {
        updates.Add(update);
        yield return update;
    }

    Console.WriteLine(updates.ToAgentResponse().Messages.Count);
}
```

## Function calling middleware

> [!NOTE]
> Function calling middleware is currently only supported with an `AIAgent` that uses <xref:Microsoft.Extensions.AI.FunctionInvokingChatClient>, for example, `ChatClientAgent`.

Here is an example of function calling middleware, that can inspect and/or modify the function being called, and the result from the function call.

```csharp
async ValueTask<object?> CustomFunctionCallingMiddleware(
    AIAgent agent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
{
    Console.WriteLine($"Function Name: {context!.Function.Name}");
    var result = await next(context, cancellationToken);
    Console.WriteLine($"Function Call Result: {result}");

    return result;
}
```

It is possible to terminate the function call loop with function calling middleware by setting the provided `FunctionInvocationContext.Terminate` to true.
This will prevent the function calling loop from issuing a request to the inference service containing the function call results after function invocation.
If there were more than one function available for invocation during this iteration, it might also prevent any remaining functions from being executed.

> [!WARNING]
> Terminating the function call loop might result in your chat history being left in an inconsistent state, for example, containing function call content with no function result content.
> This might result in the chat history being unusable for further runs.

## IChatClient middleware

Here is an example of chat client middleware, that can inspect and/or modify the input and output for the request to the inference service that the chat client provides.

```csharp
async Task<ChatResponse> CustomChatClientMiddleware(
    IEnumerable<ChatMessage> messages,
    ChatOptions? options,
    IChatClient innerChatClient,
    CancellationToken cancellationToken)
{
    Console.WriteLine(messages.Count());
    var response = await innerChatClient.GetResponseAsync(messages, options, cancellationToken);
    Console.WriteLine(response.Messages.Count);

    return response;
}
```

> [!TIP]
> See the [.NET samples](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples) for complete runnable examples.

> [!NOTE]
> For more information about `IChatClient` middleware, see [Custom IChatClient middleware](/dotnet/ai/microsoft-extensions-ai#custom-ichatclient-middleware).

::: zone-end
::: zone pivot="programming-language-python"

Agent Framework can be customized using three different types of middleware:

1. **Agent middleware**: Intercepts agent run execution, allowing you to inspect and modify inputs, outputs, and control flow.
2. **Function middleware**: Intercepts function (tool) calls made during agent execution, enabling input validation, result transformation, and execution control.
3. **Chat middleware**: Intercepts the underlying chat requests sent to AI models, providing access to the raw messages, options, and responses.

All types support both function-based and class-based implementations. When multiple middleware of the same type are registered, they form a chain where each calls the `call_next` callback to continue processing. `call_next` does not take the context as an argument; middleware mutates the shared context object directly and then awaits `call_next()`.

> [!NOTE]
> Middleware order with mixed registration scopes:
> - Agent-level middleware wraps run-level middleware.
> - For agent middleware `[A1, A2]` and run middleware `[R1, R2]`, execution order is:
>   `A1 -> A2 -> R1 -> R2 -> Agent -> R2 -> R1 -> A2 -> A1`.
> - Function/chat middleware follows the same wrapping principle at tool/chat-call time.

## Agent Middleware

Agent middleware intercepts and modifies agent run execution. It uses the `AgentContext` which contains:

- `agent`: The agent being invoked
- `messages`: List of chat messages in the conversation
- `session`: The current agent session, if any
- `options`: Agent run options for this invocation
- `stream`: Boolean indicating if the response is streaming
- `metadata`: Dictionary for storing additional data between middleware
- `result`: The agent's response (can be modified)
- `kwargs`: Legacy runtime keyword arguments passed to the agent run method
- `client_kwargs`: Client-specific runtime values for downstream chat clients
- `function_invocation_kwargs`: Runtime values that will be forwarded to tools

The `call_next` callback continues the middleware chain or executes the agent if it's the last middleware.

### Function-based

```python
async def inject_tool_runtime_defaults(
    context: AgentContext,
    call_next: Callable[[], Awaitable[None]],
) -> None:
    """Agent middleware that sets tool-only runtime defaults."""
    print("[Agent] Starting execution")
    context.function_invocation_kwargs.setdefault("tenant", "contoso")
    context.function_invocation_kwargs.setdefault("request_source", "agent-middleware")

    await call_next()

    print("[Agent] Execution completed")
```

### Class-based

Class-based agent middleware uses a `process` method that has the same signature and behavior as function-based middleware.

```python
from agent_framework import AgentMiddleware, AgentContext

class LoggingAgentMiddleware(AgentMiddleware):
    """Agent middleware that logs execution."""

    async def process(
        self,
        context: AgentContext,
        call_next: Callable[[], Awaitable[None]],
    ) -> None:
        print("[Agent Class] Starting execution")
        await call_next()
        print("[Agent Class] Execution completed")
```

## Function Middleware

Function middleware intercepts function calls within agents. It uses the `FunctionInvocationContext` which contains:

- `function`: The function being invoked
- `arguments`: The validated arguments for the function
- `session`: The current agent session, if any
- `metadata`: Dictionary for storing additional data between middleware
- `result`: The function's return value (can be modified)
- `kwargs`: Runtime keyword arguments that will be forwarded to the tool invocation

The `call_next` callback continues to the next middleware or executes the actual function.

### Function-based

```python
async def inject_function_kwargs(
    context: FunctionInvocationContext,
    call_next: Callable[[], Awaitable[None]],
) -> None:
    """Function middleware that enriches tool runtime values."""
    context.kwargs.setdefault("tenant", "contoso")
    context.kwargs.setdefault("request_source", "function-middleware")

    await call_next()
```

### Class-based

```python
from agent_framework import FunctionMiddleware, FunctionInvocationContext

class LoggingFunctionMiddleware(FunctionMiddleware):
    """Function middleware that logs function execution."""

    async def process(
        self,
        context: FunctionInvocationContext,
        call_next: Callable[[], Awaitable[None]],
    ) -> None:
        print(f"[Function Class] Calling {context.function.name}")
        await call_next()
        print(f"[Function Class] {context.function.name} completed")
```

## Chat Middleware

Chat middleware intercepts chat requests sent to AI models. It uses the `ChatContext` which contains:

- `client`: The chat client being invoked
- `messages`: List of messages being sent to the AI service
- `options`: The options for the chat request
- `stream`: Boolean indicating if this is a streaming invocation
- `metadata`: Dictionary for storing additional data between middleware
- `result`: The chat response from the AI (can be modified)
- `kwargs`: Additional keyword arguments passed to the chat client
- `function_invocation_kwargs`: Tool-only runtime values that will be forwarded by the chat layer

The `call_next` callback continues to the next middleware or sends the request to the AI service.

> [!NOTE]
> Chat middleware runs inside the function invocation loop. This means it executes for **each model call**, including calls that send tool results back to the model during a multi-turn tool calling sequence.

### Function-based

```python
async def logging_chat_middleware(
    context: ChatContext,
    call_next: Callable[[], Awaitable[None]],
) -> None:
    """Chat middleware that logs AI interactions."""
    # Pre-processing: Log before AI call
    print(f"[Chat] Sending {len(context.messages)} messages to AI")

    # Continue to next middleware or AI service
    await call_next()

    # Post-processing: Log after AI response
    print("[Chat] AI response received")
```

### Class-based

```python
from agent_framework import ChatMiddleware, ChatContext

class LoggingChatMiddleware(ChatMiddleware):
    """Chat middleware that logs AI interactions."""

    async def process(
        self,
        context: ChatContext,
        call_next: Callable[[], Awaitable[None]],
    ) -> None:
        print(f"[Chat Class] Sending {len(context.messages)} messages to AI")
        await call_next()
        print("[Chat Class] AI response received")
```

## Middleware Decorators

Decorators provide explicit middleware type declaration without requiring type annotations. They're helpful when you don't use type annotations or want to prevent type mismatches:

```python
from agent_framework import agent_middleware, function_middleware, chat_middleware

@agent_middleware
async def simple_agent_middleware(context, call_next):
    print("Before agent execution")
    await call_next()
    print("After agent execution")

@function_middleware
async def simple_function_middleware(context, call_next):
    print(f"Calling function: {context.function.name}")
    await call_next()
    print("Function call completed")

@chat_middleware
async def simple_chat_middleware(context, call_next):
    print(f"Processing {len(context.messages)} chat messages")
    await call_next()
    print("Chat processing completed")
```

## Middleware Registration

Middleware can be registered at two levels with different scopes and behaviors.

### Agent-Level vs Run-Level Middleware

```python
from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient
from azure.identity.aio import AzureCliCredential

# Agent-level middleware: Applied to ALL runs of the agent
async with Agent(
    client=FoundryChatClient(credential=credential),
    name="WeatherAgent",
    instructions="You are a helpful weather assistant.",
    tools=get_weather,
    middleware=[
        SecurityAgentMiddleware(),  # Applies to all runs
        TimingFunctionMiddleware(),  # Applies to all runs
    ],
) as agent:

    # This run uses agent-level middleware only
    result1 = await agent.run("What's the weather in Seattle?")

    # This run uses agent-level + run-level middleware
    result2 = await agent.run(
        "What's the weather in Portland?",
        middleware=[  # Run-level middleware (this run only)
            logging_chat_middleware,
        ]
    )

    # This run uses agent-level middleware only (no run-level)
    result3 = await agent.run("What's the weather in Vancouver?")
```

**Key Differences:**
- **Agent-level**: Persistent across all runs, configured once when creating the agent
- **Run-level**: Applied only to specific runs, allows per-request customization
- **Execution Order**: Agent middleware (outermost) → Run middleware (innermost) → Agent execution

## Middleware Termination

Middleware can terminate execution early by setting `context.result` and raising `MiddlewareTermination`. This is useful for security checks, rate limiting, or validation failures.

```python
from agent_framework import AgentContext, AgentResponse, Message, MiddlewareTermination

async def blocking_middleware(
    context: AgentContext,
    call_next: Callable[[], Awaitable[None]],
) -> None:
    """Middleware that blocks execution based on conditions."""
    # Check for blocked content
    last_message = context.messages[-1] if context.messages else None
    if last_message and last_message.text:
        if "blocked" in last_message.text.lower():
            print("Request blocked by middleware")
            context.result = AgentResponse(
                messages=[Message(role="assistant", contents=["This request was blocked by middleware."])]
            )
            raise MiddlewareTermination(result=context.result)

    # If no issues, continue normally
    await call_next()
```

**What termination means:**
- Set `context.result` before raising `MiddlewareTermination` if you want to return a custom response
- Raising `MiddlewareTermination` stops the remainder of the middleware chain and skips the normal execution path
- This pattern works for agent, function, and chat middleware

## Middleware Result Override

Middleware can override results in both non-streaming and streaming scenarios, allowing you to modify or completely replace agent responses.

The result type in `context.result` depends on whether the agent invocation is streaming or non-streaming:

- **Non-streaming**: `context.result` contains an `AgentResponse` with the complete response
- **Streaming**: `context.result` contains an async generator that yields `AgentResponseUpdate` chunks

You can use `context.stream` to differentiate between these scenarios and handle result overrides appropriately.

```python
async def weather_override_middleware(
    context: AgentContext,
    call_next: Callable[[], Awaitable[None]]
) -> None:
    """Middleware that overrides weather results for both streaming and non-streaming."""

    # Execute the original agent logic
    await call_next()

    # Override results if present
    if context.result is not None:
        custom_message_parts = [
            "Weather Override: ",
            "Perfect weather everywhere today! ",
            "22°C with gentle breezes. ",
            "Great day for outdoor activities!"
        ]

        if context.stream:
            # Streaming override
            async def override_stream() -> AsyncIterable[AgentResponseUpdate]:
                for chunk in custom_message_parts:
                    yield AgentResponseUpdate(contents=[Content.from_text(text=chunk)])

            context.result = override_stream()
        else:
            # Non-streaming override
            custom_message = "".join(custom_message_parts)
            context.result = AgentResponse(
                messages=[Message(role="assistant", contents=[custom_message])]
            )
```

This middleware approach allows you to implement sophisticated response transformation, content filtering, result enhancement, and streaming customization while keeping your agent logic clean and focused.

### Complete middleware examples

#### Class-based middleware

```python
# Copyright (c) Microsoft. All rights reserved.

import asyncio
import time
from collections.abc import Awaitable, Callable
from random import randint
from typing import Annotated

from agent_framework import (
    AgentContext,
    AgentMiddleware,
    AgentResponse,
    FunctionInvocationContext,
    FunctionMiddleware,
    Message,
    tool,
)
from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient
from azure.identity.aio import AzureCliCredential
from pydantic import Field

"""
Class-based MiddlewareTypes Example

This sample demonstrates how to implement middleware using class-based approach by inheriting
from AgentMiddleware and FunctionMiddleware base classes. The example includes:

- SecurityAgentMiddleware: Checks for security violations in user queries and blocks requests
  containing sensitive information like passwords or secrets
- LoggingFunctionMiddleware: Logs function execution details including timing and parameters

This approach is useful when you need stateful middleware or complex logic that benefits
from object-oriented design patterns.
"""


# NOTE: approval_mode="never_require" is for sample brevity. Use "always_require" in production; see samples/02-agents/tools/function_tool_with_approval.py and samples/02-agents/tools/function_tool_with_approval_and_sessions.py.
@tool(approval_mode="never_require")
def get_weather(
    location: Annotated[str, Field(description="The location to get the weather for.")],
) -> str:
    """Get the weather for a given location."""
    conditions = ["sunny", "cloudy", "rainy", "stormy"]
    return f"The weather in {location} is {conditions[randint(0, 3)]} with a high of {randint(10, 30)}°C."


class SecurityAgentMiddleware(AgentMiddleware):
    """Agent middleware that checks for security violations."""

    async def process(
        self,
        context: AgentContext,
        call_next: Callable[[], Awaitable[None]],
    ) -> None:
        # Check for potential security violations in the query
        # Look at the last user message
        last_message = context.messages[-1] if context.messages else None
        if last_message and last_message.text:
            query = last_message.text
            if "password" in query.lower() or "secret" in query.lower():
                print("[SecurityAgentMiddleware] Security Warning: Detected sensitive information, blocking request.")
                # Override the result with warning message
                context.result = AgentResponse(
                    messages=[Message("assistant", ["Detected sensitive information, the request is blocked."])]
                )
                # Simply don't call call_next() to prevent execution
                return

        print("[SecurityAgentMiddleware] Security check passed.")
        await call_next()


class LoggingFunctionMiddleware(FunctionMiddleware):
    """Function middleware that logs function calls."""

    async def process(
        self,
        context: FunctionInvocationContext,
        call_next: Callable[[], Awaitable[None]],
    ) -> None:
        function_name = context.function.name
        print(f"[LoggingFunctionMiddleware] About to call function: {function_name}.")

        start_time = time.time()

        await call_next()

        end_time = time.time()
        duration = end_time - start_time

        print(f"[LoggingFunctionMiddleware] Function {function_name} completed in {duration:.5f}s.")


async def main() -> None:
    """Example demonstrating class-based middleware."""
    print("=== Class-based MiddlewareTypes Example ===")

    # For authentication, run `az login` command in terminal or replace AzureCliCredential with preferred
    # authentication option.
    async with (
        AzureCliCredential() as credential,
        Agent(
            client=FoundryChatClient(credential=credential),
            name="WeatherAgent",
            instructions="You are a helpful weather assistant.",
            tools=get_weather,
            middleware=[SecurityAgentMiddleware(), LoggingFunctionMiddleware()],
        ) as agent,
    ):
        # Test with normal query
        print("\n--- Normal Query ---")
        query = "What's the weather like in Seattle?"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Agent: {result.text}\n")

        # Test with security-related query
        print("--- Security Test ---")
        query = "What's the password for the weather service?"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Agent: {result.text}\n")


if __name__ == "__main__":
    asyncio.run(main())
```

#### Function-based middleware

```python
# Copyright (c) Microsoft. All rights reserved.

import asyncio
import time
from collections.abc import Awaitable, Callable
from random import randint
from typing import Annotated

from agent_framework import (
    AgentContext,
    AgentMiddleware,
    AgentResponse,
    FunctionInvocationContext,
    FunctionMiddleware,
    Message,
    tool,
)
from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient
from azure.identity.aio import AzureCliCredential
from pydantic import Field

"""
Class-based MiddlewareTypes Example

This sample demonstrates how to implement middleware using class-based approach by inheriting
from AgentMiddleware and FunctionMiddleware base classes. The example includes:

- SecurityAgentMiddleware: Checks for security violations in user queries and blocks requests
  containing sensitive information like passwords or secrets
- LoggingFunctionMiddleware: Logs function execution details including timing and parameters

This approach is useful when you need stateful middleware or complex logic that benefits
from object-oriented design patterns.
"""


# NOTE: approval_mode="never_require" is for sample brevity. Use "always_require" in production; see samples/02-agents/tools/function_tool_with_approval.py and samples/02-agents/tools/function_tool_with_approval_and_sessions.py.
@tool(approval_mode="never_require")
def get_weather(
    location: Annotated[str, Field(description="The location to get the weather for.")],
) -> str:
    """Get the weather for a given location."""
    conditions = ["sunny", "cloudy", "rainy", "stormy"]
    return f"The weather in {location} is {conditions[randint(0, 3)]} with a high of {randint(10, 30)}°C."


class SecurityAgentMiddleware(AgentMiddleware):
    """Agent middleware that checks for security violations."""

    async def process(
        self,
        context: AgentContext,
        call_next: Callable[[], Awaitable[None]],
    ) -> None:
        # Check for potential security violations in the query
        # Look at the last user message
        last_message = context.messages[-1] if context.messages else None
        if last_message and last_message.text:
            query = last_message.text
            if "password" in query.lower() or "secret" in query.lower():
                print("[SecurityAgentMiddleware] Security Warning: Detected sensitive information, blocking request.")
                # Override the result with warning message
                context.result = AgentResponse(
                    messages=[Message("assistant", ["Detected sensitive information, the request is blocked."])]
                )
                # Simply don't call call_next() to prevent execution
                return

        print("[SecurityAgentMiddleware] Security check passed.")
        await call_next()


class LoggingFunctionMiddleware(FunctionMiddleware):
    """Function middleware that logs function calls."""

    async def process(
        self,
        context: FunctionInvocationContext,
        call_next: Callable[[], Awaitable[None]],
    ) -> None:
        function_name = context.function.name
        print(f"[LoggingFunctionMiddleware] About to call function: {function_name}.")

        start_time = time.time()

        await call_next()

        end_time = time.time()
        duration = end_time - start_time

        print(f"[LoggingFunctionMiddleware] Function {function_name} completed in {duration:.5f}s.")


async def main() -> None:
    """Example demonstrating class-based middleware."""
    print("=== Class-based MiddlewareTypes Example ===")

    # For authentication, run `az login` command in terminal or replace AzureCliCredential with preferred
    # authentication option.
    async with (
        AzureCliCredential() as credential,
        Agent(
            client=FoundryChatClient(credential=credential),
            name="WeatherAgent",
            instructions="You are a helpful weather assistant.",
            tools=get_weather,
            middleware=[SecurityAgentMiddleware(), LoggingFunctionMiddleware()],
        ) as agent,
    ):
        # Test with normal query
        print("\n--- Normal Query ---")
        query = "What's the weather like in Seattle?"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Agent: {result.text}\n")

        # Test with security-related query
        print("--- Security Test ---")
        query = "What's the password for the weather service?"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Agent: {result.text}\n")


if __name__ == "__main__":
    asyncio.run(main())
```

#### Decorator-based middleware

```python
# Copyright (c) Microsoft. All rights reserved.

import asyncio
import time
from collections.abc import Awaitable, Callable
from random import randint
from typing import Annotated

from agent_framework import (
    AgentContext,
    AgentMiddleware,
    AgentResponse,
    FunctionInvocationContext,
    FunctionMiddleware,
    Message,
    tool,
)
from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient
from azure.identity.aio import AzureCliCredential
from pydantic import Field

"""
Class-based MiddlewareTypes Example

This sample demonstrates how to implement middleware using class-based approach by inheriting
from AgentMiddleware and FunctionMiddleware base classes. The example includes:

- SecurityAgentMiddleware: Checks for security violations in user queries and blocks requests
  containing sensitive information like passwords or secrets
- LoggingFunctionMiddleware: Logs function execution details including timing and parameters

This approach is useful when you need stateful middleware or complex logic that benefits
from object-oriented design patterns.
"""


# NOTE: approval_mode="never_require" is for sample brevity. Use "always_require" in production; see samples/02-agents/tools/function_tool_with_approval.py and samples/02-agents/tools/function_tool_with_approval_and_sessions.py.
@tool(approval_mode="never_require")
def get_weather(
    location: Annotated[str, Field(description="The location to get the weather for.")],
) -> str:
    """Get the weather for a given location."""
    conditions = ["sunny", "cloudy", "rainy", "stormy"]
    return f"The weather in {location} is {conditions[randint(0, 3)]} with a high of {randint(10, 30)}°C."


class SecurityAgentMiddleware(AgentMiddleware):
    """Agent middleware that checks for security violations."""

    async def process(
        self,
        context: AgentContext,
        call_next: Callable[[], Awaitable[None]],
    ) -> None:
        # Check for potential security violations in the query
        # Look at the last user message
        last_message = context.messages[-1] if context.messages else None
        if last_message and last_message.text:
            query = last_message.text
            if "password" in query.lower() or "secret" in query.lower():
                print("[SecurityAgentMiddleware] Security Warning: Detected sensitive information, blocking request.")
                # Override the result with warning message
                context.result = AgentResponse(
                    messages=[Message("assistant", ["Detected sensitive information, the request is blocked."])]
                )
                # Simply don't call call_next() to prevent execution
                return

        print("[SecurityAgentMiddleware] Security check passed.")
        await call_next()


class LoggingFunctionMiddleware(FunctionMiddleware):
    """Function middleware that logs function calls."""

    async def process(
        self,
        context: FunctionInvocationContext,
        call_next: Callable[[], Awaitable[None]],
    ) -> None:
        function_name = context.function.name
        print(f"[LoggingFunctionMiddleware] About to call function: {function_name}.")

        start_time = time.time()

        await call_next()

        end_time = time.time()
        duration = end_time - start_time

        print(f"[LoggingFunctionMiddleware] Function {function_name} completed in {duration:.5f}s.")


async def main() -> None:
    """Example demonstrating class-based middleware."""
    print("=== Class-based MiddlewareTypes Example ===")

    # For authentication, run `az login` command in terminal or replace AzureCliCredential with preferred
    # authentication option.
    async with (
        AzureCliCredential() as credential,
        Agent(
            client=FoundryChatClient(credential=credential),
            name="WeatherAgent",
            instructions="You are a helpful weather assistant.",
            tools=get_weather,
            middleware=[SecurityAgentMiddleware(), LoggingFunctionMiddleware()],
        ) as agent,
    ):
        # Test with normal query
        print("\n--- Normal Query ---")
        query = "What's the weather like in Seattle?"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Agent: {result.text}\n")

        # Test with security-related query
        print("--- Security Test ---")
        query = "What's the password for the weather service?"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Agent: {result.text}\n")


if __name__ == "__main__":
    asyncio.run(main())
```

::: zone-end

::: zone pivot="programming-language-go"
## Middleware overview

Middleware in Go intercepts and modifies agent behavior at the run level. All middleware implements the `agent.Middleware` interface.

### Built-in and framework-provided middleware

| Component | Registration | Layer | Purpose |
|---|---|---|---|
| Auto-call | `agent/harness/toolautocall` | Provider middleware | Automatically invokes function tools |
| Structured output | `agent.WithStructuredOutput` | Provider middleware | Handles structured output parsing |
| OpenTelemetry | `provider/otelprovider` | Agent middleware | Traces agent invocations |
| Run logger | `agent.Config.Logger` | Agent middleware | Logs agent interactions |

Context providers are adjacent lifecycle components rather than `agent.Middleware` implementations. They run after custom agent middleware has entered the run and before provider middleware calls the model.

### Registering middleware

```go
import otelprovider "github.com/microsoft/agent-framework-go/provider/otelprovider"

a := foundryprovider.NewAgent(endpoint, token, foundryprovider.ModelDeployment(model), foundryprovider.AgentConfig{
    Config: agent.Config{
        Middlewares: []agent.Middleware{
            otelprovider.NewMiddleware(otelprovider.MiddlewareConfig{}),
            myCustomMiddleware,
        },
    },
})
```

Middleware registered in `agent.Config.Middlewares` is applied in the order declared; the first middleware wraps the outermost custom layer. That custom layer wraps history providers, context providers, and provider middleware.

### Creating middleware

Use `agent.MiddlewareFunc` when a full struct type is unnecessary:

```go
addGuidance := agent.MiddlewareFunc(
    func(next agent.RunFunc, ctx context.Context, messages []*message.Message, options ...agent.Option) iter.Seq2[*agent.ResponseUpdate, error] {
        guided := append([]*message.Message{message.NewText("Keep the response concise and avoid exposing secrets.")}, messages...)
        return next(ctx, guided, options...)
    },
)

a := foundryprovider.NewAgent(endpoint, token, foundryprovider.ModelDeployment(model), foundryprovider.AgentConfig{
    Config: agent.Config{
        Middlewares: []agent.Middleware{addGuidance},
    },
})
```

::: zone-end
## Next steps

> [!div class="nextstepaction"]
> [Defining Middleware](./defining-middleware.md)
