---
title: Custom Agents
description: Learn how to build custom agents with Microsoft Agent Framework.
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: tutorial
ms.author: westey
ms.date: 09/25/2025
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

### The Agent Thread

To create a custom agent you also need a thread, which is used to keep track of the state
of a single conversation, including message history, and any other state the agent needs to maintain.

To make it easy to get started, you can inherit from various base classes that implement common thread storage mechanisms.

1. `InMemoryAgentThread` - stores the chat history in memory and can be serialized to JSON.
1. `ServiceIdAgentThread` - doesn't store any chat history, but allows you to associate an ID with the thread, under which the chat history can be stored externally.

For this example, you'll use the `InMemoryAgentThread` as the base class for the custom thread.

```csharp
internal sealed class CustomAgentThread : InMemoryAgentThread
{
    internal CustomAgentThread() : base() { }
    internal CustomAgentThread(JsonElement serializedThreadState, JsonSerializerOptions? jsonSerializerOptions = null)
        : base(serializedThreadState, jsonSerializerOptions) { }
}
```

### The Agent class

Next, create the agent class itself by inheriting from the `AIAgent` class.

```csharp
internal sealed class UpperCaseParrotAgent : AIAgent
{
}
```

### Constructing threads

Threads are always created via two factory methods on the agent class.
This allows for the agent to control how threads are created and deserialized.
Agents can therefore attach any additional state or behaviors needed to the thread when constructed.

Two methods are required to be implemented:

```csharp
    public override Task<AgentThread> GetNewThreadAsync(CancellationToken cancellationToken = default) 
        => Task.FromResult<AgentThread>(new CustomAgentThread());

    public override Task<AgentThread> DeserializeThreadAsync(JsonElement serializedThread, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
        => Task.FromResult<AgentThread>(new CustomAgentThread(serializedThread, jsonSerializerOptions));
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

For both methods, you need to ensure that a thread is provided, and if not, create a new thread.
The thread can then be updated with the new messages by calling `NotifyThreadOfNewMessagesAsync`.
If you don't do this, the user won't be able to have a multi-turn conversation with the agent and each run will be a fresh interaction.

```csharp
    public override async Task<AgentResponse> RunAsync(IEnumerable<ChatMessage> messages, AgentThread? thread = null, AgentRunOptions? options = null, CancellationToken cancellationToken = default)
    {
        thread ??= await this.GetNewThreadAsync(cancellationToken);
        List<ChatMessage> responseMessages = CloneAndToUpperCase(messages, this.DisplayName).ToList();
        await NotifyThreadOfNewMessagesAsync(thread, messages.Concat(responseMessages), cancellationToken);
        return new AgentResponse
        {
            AgentId = this.Id,
            ResponseId = Guid.NewGuid().ToString(),
            Messages = responseMessages
        };
    }

    public override async IAsyncEnumerable<AgentResponseUpdate> RunStreamingAsync(IEnumerable<ChatMessage> messages, AgentThread? thread = null, AgentRunOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        thread ??= await this.GetNewThreadAsync(cancellationToken);
        List<ChatMessage> responseMessages = CloneAndToUpperCase(messages, this.DisplayName).ToList();
        await NotifyThreadOfNewMessagesAsync(thread, messages.Concat(responseMessages), cancellationToken);
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

## Using the Agent

If the `AIAgent` methods are all implemented correctly, the agent would be a standard `AIAgent` and support standard agent operations.

For more information on how to run and interact with agents, see the [Agent getting started tutorials](../../../tutorials/overview.md).

::: zone-end
::: zone pivot="programming-language-python"

Microsoft Agent Framework supports building custom agents by inheriting from the `BaseAgent` class and implementing the required methods.

This document shows how to build a simple custom agent that echoes back user input with a prefix.
In most cases building your own agent will involve more complex logic and integration with an AI service.

## Getting Started

Add the required Python packages to your project.

```bash
pip install agent-framework-core --pre
```

## Create a Custom Agent

### The Agent Protocol

The framework provides the `AgentProtocol` protocol that defines the interface all agents must implement. Custom agents can either implement this protocol directly or extend the `BaseAgent` class for convenience.

```python
from agent_framework import AgentProtocol, AgentResponse, AgentResponseUpdate, AgentThread, ChatMessage
from collections.abc import AsyncIterable
from typing import Any

class MyCustomAgent(AgentProtocol):
    """A custom agent that implements the AgentProtocol directly."""

    @property
    def id(self) -> str:
        """Returns the ID of the agent."""
        ...

    async def run(
        self,
        messages: str | ChatMessage | list[str] | list[ChatMessage] | None = None,
        *,
        thread: AgentThread | None = None,
        **kwargs: Any,
    ) -> AgentResponse:
        """Execute the agent and return a complete response."""
        ...

    def run_stream(
        self,
        messages: str | ChatMessage | list[str] | list[ChatMessage] | None = None,
        *,
        thread: AgentThread | None = None,
        **kwargs: Any,
    ) -> AsyncIterable[AgentResponseUpdate]:
        """Execute the agent and yield streaming response updates."""
        ...
```

### Using BaseAgent

The recommended approach is to extend the `BaseAgent` class, which provides common functionality and simplifies implementation:

```python
from agent_framework import (
    BaseAgent,
    AgentResponse,
    AgentResponseUpdate,
    AgentThread,
    ChatMessage,
    Role,
    TextContent,
)
from collections.abc import AsyncIterable
from typing import Any


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
        """Initialize the EchoAgent.

        Args:
            name: The name of the agent.
            description: The description of the agent.
            echo_prefix: The prefix to add to echoed messages.
            **kwargs: Additional keyword arguments passed to BaseAgent.
        """
        super().__init__(
            name=name,
            description=description,
            echo_prefix=echo_prefix,
            **kwargs,
        )

    async def run(
        self,
        messages: str | ChatMessage | list[str] | list[ChatMessage] | None = None,
        *,
        thread: AgentThread | None = None,
        **kwargs: Any,
    ) -> AgentResponse:
        """Execute the agent and return a complete response.

        Args:
            messages: The message(s) to process.
            thread: The conversation thread (optional).
            **kwargs: Additional keyword arguments.

        Returns:
            An AgentResponse containing the agent's reply.
        """
        # Normalize input messages to a list
        normalized_messages = self._normalize_messages(messages)

        if not normalized_messages:
            response_message = ChatMessage(
                role=Role.ASSISTANT,
                contents=[TextContent(text="Hello! I'm a custom echo agent. Send me a message and I'll echo it back.")],
            )
        else:
            # For simplicity, echo the last user message
            last_message = normalized_messages[-1]
            if last_message.text:
                echo_text = f"{self.echo_prefix}{last_message.text}"
            else:
                echo_text = f"{self.echo_prefix}[Non-text message received]"

            response_message = ChatMessage(role=Role.ASSISTANT, contents=[TextContent(text=echo_text)])

        # Notify the thread of new messages if provided
        if thread is not None:
            await self._notify_thread_of_new_messages(thread, normalized_messages, response_message)

        return AgentResponse(messages=[response_message])

    async def run_stream(
        self,
        messages: str | ChatMessage | list[str] | list[ChatMessage] | None = None,
        *,
        thread: AgentThread | None = None,
        **kwargs: Any,
    ) -> AsyncIterable[AgentResponseUpdate]:
        """Execute the agent and yield streaming response updates.

        Args:
            messages: The message(s) to process.
            thread: The conversation thread (optional).
            **kwargs: Additional keyword arguments.

        Yields:
            AgentResponseUpdate objects containing chunks of the response.
        """
        # Normalize input messages to a list
        normalized_messages = self._normalize_messages(messages)

        if not normalized_messages:
            response_text = "Hello! I'm a custom echo agent. Send me a message and I'll echo it back."
        else:
            # For simplicity, echo the last user message
            last_message = normalized_messages[-1]
            if last_message.text:
                response_text = f"{self.echo_prefix}{last_message.text}"
            else:
                response_text = f"{self.echo_prefix}[Non-text message received]"

        # Simulate streaming by yielding the response word by word
        words = response_text.split()
        for i, word in enumerate(words):
            # Add space before word except for the first one
            chunk_text = f" {word}" if i > 0 else word

            yield AgentResponseUpdate(
                contents=[TextContent(text=chunk_text)],
                role=Role.ASSISTANT,
            )

            # Small delay to simulate streaming
            await asyncio.sleep(0.1)

        # Notify the thread of the complete response if provided
        if thread is not None:
            complete_response = ChatMessage(role=Role.ASSISTANT, contents=[TextContent(text=response_text)])
            await self._notify_thread_of_new_messages(thread, normalized_messages, complete_response)
```

## Using the Agent

If agent methods are all implemented correctly, the agent would support all standard agent operations.

For more information on how to run and interact with agents, see the [Agent getting started tutorials](../../../tutorials/overview.md).

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Running Agents](../running-agents.md)
