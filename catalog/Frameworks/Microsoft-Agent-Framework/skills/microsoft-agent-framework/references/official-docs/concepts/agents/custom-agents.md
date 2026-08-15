---
title: Custom Agents
description: Learn how to build custom agents with Microsoft Agent Framework.
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: tutorial
ms.author: westey
ms.date: 06/01/2026
ms.service: agent-framework
---

# Custom Agents

::: zone pivot="programming-language-csharp"

Microsoft Agent Framework supports building custom agents by inheriting from the `AIAgent` class and implementing the required methods.

This article shows how to build a simple custom agent that parrots back user input in upper case.
In most cases building your own agent will involve more complex logic and integration with an AI service.

## Getting Started

Add the required NuGet packages to your project.

```dotnetcli
dotnet add package Microsoft.Agents.AI.Abstractions --prerelease
```

## Create a Custom Agent

### The Agent Session

To create a custom agent you also need a session, which is used to keep track of the state
of a single conversation, including message history, and any other state the agent needs to maintain.

To make it easy to get started, you can inherit from various base classes that implement common session storage mechanisms.

1. `InMemoryAgentSession` - stores the chat history in memory and can be serialized to JSON.
1. `ServiceIdAgentSession` - doesn't store any chat history, but allows you to associate an ID with the session, under which the chat history can be stored externally.

For this example, you'll use the `InMemoryAgentSession` as the base class for the custom session.

```csharp
internal sealed class CustomAgentSession : InMemoryAgentSession
{
    internal CustomAgentSession() : base() { }
    internal CustomAgentSession(JsonElement serializedSessionState, JsonSerializerOptions? jsonSerializerOptions = null)
        : base(serializedSessionState, jsonSerializerOptions) { }
}
```

### The Agent class

Next, create the agent class itself by inheriting from the `AIAgent` class.

```csharp
internal sealed class UpperCaseParrotAgent : AIAgent
{
}
```

### Constructing sessions

Sessions are always created via two factory methods on the agent class.
This allows for the agent to control how sessions are created and deserialized.
Agents can therefore attach any additional state or behaviors needed to the session when constructed.

Two methods are required to be implemented:

```csharp
    protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default)
        => new(new CustomAgentSession());

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(JsonElement serializedState, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
        => new(new CustomAgentSession(serializedState, jsonSerializerOptions));
```

### Core agent logic

The core logic of the agent is to take any input messages, convert their text to upper case, and return them as response messages.

Add the following method to contain this logic.
The input messages are cloned, since various aspects of the input messages have to be modified to be valid response messages. For example, the role has to be changed to `Assistant`.

```csharp
    private static IEnumerable<ChatMessage> CloneAndToUpperCase(IEnumerable<ChatMessage> messages, string agentName) => messages.Select(x =>
        {
            var messageClone = x.Clone();
            messageClone.Role = ChatRole.Assistant;
            messageClone.MessageId = Guid.NewGuid().ToString();
            messageClone.AuthorName = agentName;
            messageClone.Contents = x.Contents.Select(c => c is TextContent tc ? new TextContent(tc.Text.ToUpperInvariant())
            {
                AdditionalProperties = tc.AdditionalProperties,
                Annotations = tc.Annotations,
                RawRepresentation = tc.RawRepresentation
            } : c).ToList();
            return messageClone;
        });
```

### Agent run methods

Finally, you need to implement the two core methods that are used to run the agent:
one for non-streaming and one for streaming.

For both methods, you need to ensure that a session is provided, and if not, create a new session.
Messages can be retrieved and passed to the `ChatHistoryProvider` on the session.
If you don't do this, the user won't be able to have a multi-turn conversation with the agent and each run will be a fresh interaction.

```csharp
    protected override async Task<AgentResponse> RunCoreAsync(IEnumerable<ChatMessage> messages, AgentSession? session = null, AgentRunOptions? options = null, CancellationToken cancellationToken = default)
    {
        session ??= await this.CreateSessionAsync(cancellationToken);

        // Get existing messages from the store
        var invokingContext = new ChatHistoryProvider.InvokingContext(messages);
        var storeMessages = await typedSession.ChatHistoryProvider.InvokingAsync(invokingContext, cancellationToken);

        List<ChatMessage> responseMessages = CloneAndToUpperCase(messages, this.DisplayName).ToList();

        // Notify the session of the input and output messages.
        var invokedContext = new ChatHistoryProvider.InvokedContext(messages, storeMessages)
        {
            ResponseMessages = responseMessages
        };
        await typedSession.ChatHistoryProvider.InvokedAsync(invokedContext, cancellationToken);

        return new AgentResponse
        {
            AgentId = this.Id,
            ResponseId = Guid.NewGuid().ToString(),
            Messages = responseMessages
        };
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(IEnumerable<ChatMessage> messages, AgentSession? session = null, AgentRunOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        session ??= await this.CreateSessionAsync(cancellationToken);

        // Get existing messages from the store
        var invokingContext = new ChatHistoryProvider.InvokingContext(messages);
        var storeMessages = await typedSession.ChatHistoryProvider.InvokingAsync(invokingContext, cancellationToken);

        List<ChatMessage> responseMessages = CloneAndToUpperCase(messages, this.DisplayName).ToList();

        // Notify the session of the input and output messages.
        var invokedContext = new ChatHistoryProvider.InvokedContext(messages, storeMessages)
        {
            ResponseMessages = responseMessages
        };
        await typedSession.ChatHistoryProvider.InvokedAsync(invokedContext, cancellationToken);

        foreach (var message in responseMessages)
        {
            yield return new AgentResponseUpdate
            {
                AgentId = this.Id,
                AuthorName = this.DisplayName,
                Role = ChatRole.Assistant,
                Contents = message.Contents,
                ResponseId = Guid.NewGuid().ToString(),
                MessageId = Guid.NewGuid().ToString()
            };
        }
    }
```

> [!TIP]
> See the [.NET samples](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples) for complete runnable examples.

## Tools

A custom `AIAgent` has whatever tool surface you decide to give it. If you wrap an existing `IChatClient` and pass `tools` through, you inherit that client's tool support — see, for example, the [OpenAI](../../integrations/by-component/model-providers/openai.md#tools), [Azure OpenAI](../../integrations/by-component/model-providers/azure-openai.md#tools), or [Microsoft Foundry](../../integrations/by-component/model-providers/microsoft-foundry.md#tools) provider pages for what the underlying clients support. If your custom agent does not call a chat client (for example, the echo agent above), there are no tools to invoke.

## Using the Agent

If the `AIAgent` methods are all implemented correctly, the agent would be a standard `AIAgent` and support standard agent operations.

For more information on how to run and interact with agents, see the [Agent getting started tutorials](../../get-started/your-first-agent.md).

::: zone-end
::: zone pivot="programming-language-python"

Microsoft Agent Framework supports building custom agents by inheriting from the `BaseAgent` class and implementing the required methods.

This document shows how to build a simple custom agent that echoes back user input with a prefix.
In most cases building your own agent will involve more complex logic and integration with an AI service.

## Getting Started

Add the required Python packages to your project.

```bash
pip install agent-framework-core
```

## Create a Custom Agent

### The Agent Protocol

The framework provides the `SupportsAgentRun` protocol that defines the interface all agents must implement. Custom agents can either implement this protocol directly or extend the `BaseAgent` class for convenience.

```python
from typing import Any, Literal, overload
from collections.abc import Awaitable, Sequence
from agent_framework import (
    AgentResponse,
    AgentResponseUpdate,
    AgentSession,
    Message,
    ResponseStream,
    SupportsAgentRun,
)

class MyCustomAgent(SupportsAgentRun):
    """A custom agent that implements the SupportsAgentRun directly."""

    @property
    def id(self) -> str:
        """Returns the ID of the agent."""
        ...

    @overload
    def run(
        self,
        messages: str | Message | Sequence[str | Message] | None = None,
        *,
        stream: Literal[False] = False,
        session: AgentSession | None = None,
        **kwargs: Any,
    ) -> Awaitable[AgentResponse]: ...

    @overload
    def run(
        self,
        messages: str | Message | Sequence[str | Message] | None = None,
        *,
        stream: Literal[True],
        session: AgentSession | None = None,
        **kwargs: Any,
    ) -> ResponseStream[AgentResponseUpdate, AgentResponse]: ...

    def run(
        self,
        messages: str | Message | Sequence[str | Message] | None = None,
        *,
        stream: bool = False,
        session: AgentSession | None = None,
        **kwargs: Any,
    ) -> Awaitable[AgentResponse] | ResponseStream[AgentResponseUpdate, AgentResponse]:
        """Execute the agent and return either an awaitable response or a ResponseStream."""
        ...
```

> [!TIP]
> Add `@overload` signatures to `run()` so IDEs and static type checkers infer the return type based on `stream` (`Awaitable[AgentResponse]` for `stream=False` and `ResponseStream[AgentResponseUpdate, AgentResponse]` for `stream=True`).

### Using BaseAgent

The recommended approach is to extend the `BaseAgent` class, which provides common functionality and simplifies implementation:

```python
import asyncio
from collections.abc import AsyncIterable, Awaitable, Sequence
from typing import Any, Literal, overload

from agent_framework import (
    AgentResponse,
    AgentResponseUpdate,
    AgentSession,
    BaseAgent,
    Content,
    Message,
    ResponseStream,
    normalize_messages,
)


class EchoAgent(BaseAgent):
    """A simple custom agent that echoes user messages with a prefix."""

    echo_prefix: str = "Echo: "

    def __init__(
        self,
        *,
        name: str | None = None,
        description: str | None = None,
        echo_prefix: str = "Echo: ",
        **kwargs: Any,
    ) -> None:
        super().__init__(
            name=name,
            description=description,
            echo_prefix=echo_prefix,
            **kwargs,
        )

    @overload
    def run(
        self,
        messages: str | Message | Sequence[str | Message] | None = None,
        *,
        stream: Literal[False] = False,
        session: AgentSession | None = None,
        **kwargs: Any,
    ) -> Awaitable[AgentResponse]: ...

    @overload
    def run(
        self,
        messages: str | Message | Sequence[str | Message] | None = None,
        *,
        stream: Literal[True],
        session: AgentSession | None = None,
        **kwargs: Any,
    ) -> ResponseStream[AgentResponseUpdate, AgentResponse]: ...

    def run(
        self,
        messages: str | Message | Sequence[str | Message] | None = None,
        *,
        stream: bool = False,
        session: AgentSession | None = None,
        **kwargs: Any,
    ) -> Awaitable[AgentResponse] | ResponseStream[AgentResponseUpdate, AgentResponse]:
        """Execute the agent.

        Args:
            messages: The message(s) to process.
            stream: If True, return a ResponseStream of updates.
            session: The conversation session (optional).

        Returns:
            When stream=False: An awaitable AgentResponse.
            When stream=True: A ResponseStream with AgentResponseUpdate items and final response support.
        """
        if stream:
            return ResponseStream(
                self._run_stream(messages=messages, session=session, **kwargs),
                finalizer=AgentResponse.from_updates,
            )
        return self._run(messages=messages, session=session, **kwargs)

    async def _run(
        self,
        messages: str | Message | Sequence[str | Message] | None = None,
        *,
        session: AgentSession | None = None,
        **kwargs: Any,
    ) -> AgentResponse:
        normalized_messages = normalize_messages(messages)

        if not normalized_messages:
            response_message = Message(
                role="assistant",
                contents=[Content.from_text("Hello! I'm a custom echo agent. Send me a message and I'll echo it back.")],
            )
        else:
            last_message = normalized_messages[-1]
            echo_text = f"{self.echo_prefix}{last_message.text}" if last_message.text else f"{self.echo_prefix}[Non-text message received]"
            response_message = Message(role="assistant", contents=[Content.from_text(echo_text)])

        if session is not None:
            stored = session.state.setdefault("memory", {}).setdefault("messages", [])
            stored.extend(normalized_messages)
            stored.append(response_message)

        return AgentResponse(messages=[response_message])

    async def _run_stream(
        self,
        messages: str | Message | Sequence[str | Message] | None = None,
        *,
        session: AgentSession | None = None,
        **kwargs: Any,
    ) -> AsyncIterable[AgentResponseUpdate]:
        normalized_messages = normalize_messages(messages)

        if not normalized_messages:
            response_text = "Hello! I'm a custom echo agent. Send me a message and I'll echo it back."
        else:
            last_message = normalized_messages[-1]
            response_text = f"{self.echo_prefix}{last_message.text}" if last_message.text else f"{self.echo_prefix}[Non-text message received]"

        words = response_text.split()
        for i, word in enumerate(words):
            chunk_text = f" {word}" if i > 0 else word
            yield AgentResponseUpdate(
                contents=[Content.from_text(chunk_text)],
                role="assistant",
            )
            await asyncio.sleep(0.1)

        if session is not None:
            complete_response = Message(role="assistant", contents=[Content.from_text(response_text)])
            stored = session.state.setdefault("memory", {}).setdefault("messages", [])
            stored.extend(normalized_messages)
            stored.append(complete_response)
```

## Tools

A custom `BaseAgent` has whatever tool surface you decide to give it. If you wrap an existing chat client and pass `tools` through, you inherit that client's tool support — see, for example, the [OpenAI](../../integrations/by-component/model-providers/openai.md#tools), [Microsoft Foundry](../../integrations/by-component/model-providers/microsoft-foundry.md#tools), or [Anthropic](../../integrations/by-component/model-providers/anthropic.md#tools) provider pages for what the underlying clients support. If your custom agent does not call a chat client (for example, the echo agent above), there are no tools to invoke.

## Using the Agent

If agent methods are all implemented correctly, the agent supports standard operations, including streaming via `ResponseStream`:

```python
stream = echo_agent.run("Stream this response", stream=True, session=echo_agent.create_session())
async for update in stream:
    print(update.text or "", end="", flush=True)
final_response = await stream.get_final_response()
```

For more information on how to run and interact with agents, see the [Agent getting started tutorials](../../get-started/your-first-agent.md).

::: zone-end

::: zone pivot="programming-language-go"
## Custom providers

You can create a custom provider by implementing `agent.ProviderConfig` and passing it to `agent.New`:

```go
import (
    "context"
    "iter"

    "github.com/microsoft/agent-framework-go/agent"
    "github.com/microsoft/agent-framework-go/message"
)

a := agent.New(agent.ProviderConfig{
    ProviderName: "my-custom-provider",
    Run: func(ctx context.Context, messages []*message.Message,
        options ...agent.Option) iter.Seq2[*agent.ResponseUpdate, error] {
        // Your custom LLM logic here
        return func(yield func(*agent.ResponseUpdate, error) bool) {
            yield(&agent.ResponseUpdate{
                Role: message.RoleAssistant,
                Contents: []message.Content{
                    &message.TextContent{Text: "Hello from custom provider!"},
                },
            }, nil)
        }
    },
}, agent.Config{
    Name: "CustomAgent",
})
```

### ProviderConfig fields

| Field | Purpose |
|---|---|
| `CreateSession` | Create a new session for the provider |
| `Run` | Execute a request and stream response updates |
| `Middlewares` | Add provider-scoped middleware that runs after history and context providers |
| `Format` | Generate a response format descriptor for structured output |
| `Unmarshal` | Decode structured output into a target type |

::: zone-end
## Next steps

> [!div class="nextstepaction"]
> [Running Agents](running-agents.md)
