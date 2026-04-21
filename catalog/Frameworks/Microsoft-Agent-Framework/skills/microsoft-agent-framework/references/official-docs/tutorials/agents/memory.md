---
title: Adding Memory to an Agent
description: How to add memory to an agent using an AIContextProvider.
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: tutorial
ms.author: westey
ms.date: 09/25/2025
ms.service: agent-framework
---

# Adding Memory to an Agent

::: zone pivot="programming-language-csharp"
This tutorial shows how to add memory to an agent by implementing an `AIContextProvider` and attaching it to the agent.

> [!IMPORTANT]
> Not all agent types support `AIContextProvider`. This step uses a `ChatClientAgent`, which does support `AIContextProvider`.

## Prerequisites

For prerequisites and installing NuGet packages, see the [Create and run a simple agent](./run-agent.md) step in this tutorial.

## Create an AIContextProvider

`AIContextProvider` is an abstract class that you can inherit from, and which can be associated with the `AgentThread` for a `ChatClientAgent`.
It allows you to:

1. Run custom logic before and after the agent invokes the underlying inference service.
1. Provide additional context to the agent before it invokes the underlying inference service.
1. Inspect all messages provided to and produced by the agent.

### Pre and post invocation events

The `AIContextProvider` class has two methods that you can override to run custom logic before and after the agent invokes the underlying inference service:

- `InvokingAsync` - called before the agent invokes the underlying inference service. You can provide additional context to the agent by returning an `AIContext` object. This context will be merged with the agent's existing context before invoking the underlying service. It is possible to provide instructions, tools, and messages to add to the request.
- `InvokedAsync` - called after the agent has received a response from the underlying inference service. You can inspect the request and response messages, and update the state of the context provider.

### Serialization

`AIContextProvider` instances are created and attached to an `AgentThread` when the thread is created, and when a thread is resumed from a serialized state.

The `AIContextProvider` instance might have its own state that needs to be persisted between invocations of the agent. For example, a memory component that remembers information about the user might have memories as part of its state.

To allow persisting threads, you need to implement the `SerializeAsync` method of the `AIContextProvider` class. You also need to provide a constructor that takes a `JsonElement` parameter, which can be used to deserialize the state when resuming a thread.

### Sample AIContextProvider implementation

The following example of a custom memory component remembers a user's name and age and provides it to the agent before each invocation.

First, create a model class to hold the memories.

```csharp
internal sealed class UserInfo
{
    public string? UserName { get; set; }
    public int? UserAge { get; set; }
}
```

Then you can implement the `AIContextProvider` to manage the memories.
The `UserInfoMemory` class below contains the following behavior:

1. It uses an `IChatClient` to look for the user's name and age in user messages when new messages are added to the thread at the end of each run.
1. It provides any current memories to the agent before each invocation.
1. If no memories are available, it instructs the agent to ask the user for the missing information, and not to answer any questions until the information is provided.
1. It also implements serialization to allow persisting the memories as part of the thread state.

```csharp
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

internal sealed class UserInfoMemory : AIContextProvider
{
    private readonly IChatClient _chatClient;
    public UserInfoMemory(IChatClient chatClient, UserInfo? userInfo = null)
    {
        this._chatClient = chatClient;
        this.UserInfo = userInfo ?? new UserInfo();
    }

    public UserInfoMemory(IChatClient chatClient, JsonElement serializedState, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        this._chatClient = chatClient;
        this.UserInfo = serializedState.ValueKind == JsonValueKind.Object ?
            serializedState.Deserialize<UserInfo>(jsonSerializerOptions)! :
            new UserInfo();
    }

    public UserInfo UserInfo { get; set; }

    public override async ValueTask InvokedAsync(
        InvokedContext context,
        CancellationToken cancellationToken = default)
    {
        if ((this.UserInfo.UserName is null || this.UserInfo.UserAge is null) && context.RequestMessages.Any(x => x.Role == ChatRole.User))
        {
            var result = await this._chatClient.GetResponseAsync<UserInfo>(
                context.RequestMessages,
                new ChatOptions()
                {
                    Instructions = "Extract the user's name and age from the message if present. If not present return nulls."
                },
                cancellationToken: cancellationToken);
            this.UserInfo.UserName ??= result.Result.UserName;
            this.UserInfo.UserAge ??= result.Result.UserAge;
        }
    }

    public override ValueTask<AIContext> InvokingAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        StringBuilder instructions = new();
        instructions
            .AppendLine(
                this.UserInfo.UserName is null ?
                    "Ask the user for their name and politely decline to answer any questions until they provide it." :
                    $"The user's name is {this.UserInfo.UserName}.")
            .AppendLine(
                this.UserInfo.UserAge is null ?
                    "Ask the user for their age and politely decline to answer any questions until they provide it." :
                    $"The user's age is {this.UserInfo.UserAge}.");
        return new ValueTask<AIContext>(new AIContext
        {
            Instructions = instructions.ToString()
        });
    }

    public override JsonElement Serialize(JsonSerializerOptions? jsonSerializerOptions = null)
    {
        return JsonSerializer.SerializeToElement(this.UserInfo, jsonSerializerOptions);
    }
}
```

## Using the AIContextProvider with an agent

To use the custom `AIContextProvider`, you need to provide an `AIContextProviderFactory` when creating the agent. This factory allows the agent to create a new instance of the desired `AIContextProvider` for each thread.

When creating a `ChatClientAgent` it is possible to provide a `ChatClientAgentOptions` object that allows providing the `AIContextProviderFactory` in addition to all other agent options.

The factory is an async function that receives a context object and a cancellation token.

```csharp
using System;
using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;
using OpenAI;

ChatClient chatClient = new AzureOpenAIClient(
    new Uri("https://<myresource>.openai.azure.com"),
    new AzureCliCredential())
    .GetChatClient("gpt-4o-mini");

AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions()
{
    ChatOptions = new() { Instructions = "You are a friendly assistant. Always address the user by their name." },
    AIContextProviderFactory = (ctx, ct) => new ValueTask<AIContextProvider>(
        new UserInfoMemory(
            chatClient.AsIChatClient(),
            ctx.SerializedState,
            ctx.JsonSerializerOptions))
});
```

When creating a new thread, the `AIContextProvider` will be created by `GetNewThreadAsync`
and attached to the thread. Once memories are extracted it is therefore possible to access the memory component via the thread's `GetService` method and inspect the memories.

```csharp
// Create a new thread for the conversation.
AgentThread thread = await agent.GetNewThreadAsync();

Console.WriteLine(await agent.RunAsync("Hello, what is the square root of 9?", thread));
Console.WriteLine(await agent.RunAsync("My name is Ruaidhrí", thread));
Console.WriteLine(await agent.RunAsync("I am 20 years old", thread));

// Access the memory component via the thread's GetService method.
var userInfo = thread.GetService<UserInfoMemory>()?.UserInfo;
Console.WriteLine($"MEMORY - User Name: {userInfo?.UserName}");
Console.WriteLine($"MEMORY - User Age: {userInfo?.UserAge}");
```

::: zone-end
::: zone pivot="programming-language-python"

This tutorial shows how to add memory to an agent by implementing a `ContextProvider` and attaching it to the agent.

> [!IMPORTANT]
> Not all agent types support `ContextProvider`. This step uses a `ChatAgent`, which does support `ContextProvider`.

## Prerequisites

For prerequisites and installing packages, see the [Create and run a simple agent](./run-agent.md) step in this tutorial.

## Create a ContextProvider

`ContextProvider` is an abstract class that you can inherit from, and which can be associated with an `AgentThread` for a `ChatAgent`.
It allows you to:

1. Run custom logic before and after the agent invokes the underlying inference service.
1. Provide additional context to the agent before it invokes the underlying inference service.
1. Inspect all messages provided to and produced by the agent.

### Pre and post invocation events

The `ContextProvider` class has two methods that you can override to run custom logic before and after the agent invokes the underlying inference service:

- `invoking` - called before the agent invokes the underlying inference service. You can provide additional context to the agent by returning a `Context` object. This context will be merged with the agent's existing context before invoking the underlying service. It is possible to provide instructions, tools, and messages to add to the request.
- `invoked` - called after the agent has received a response from the underlying inference service. You can inspect the request and response messages, and update the state of the context provider.

### Serialization

`ContextProvider` instances are created and attached to an `AgentThread` when the thread is created, and when a thread is resumed from a serialized state.

The `ContextProvider` instance might have its own state that needs to be persisted between invocations of the agent. For example, a memory component that remembers information about the user might have memories as part of its state.

To allow persisting threads, you need to implement serialization for the `ContextProvider` class. You also need to provide a constructor that can restore state from serialized data when resuming a thread.

### Sample ContextProvider implementation

The following example of a custom memory component remembers a user's name and age and provides it to the agent before each invocation.

First, create a model class to hold the memories.

```python
from pydantic import BaseModel

class UserInfo(BaseModel):
    name: str | None = None
    age: int | None = None
```

Then you can implement the `ContextProvider` to manage the memories.
The `UserInfoMemory` class below contains the following behavior:

1. It uses a chat client to look for the user's name and age in user messages when new messages are added to the thread at the end of each run.
1. It provides any current memories to the agent before each invocation.
1. If no memories are available, it instructs the agent to ask the user for the missing information, and not to answer any questions until the information is provided.
1. It also implements serialization to allow persisting the memories as part of the thread state.

```python

from collections.abc import MutableSequence, Sequence
from typing import Any

from agent_framework import ContextProvider, Context, ChatAgent, ChatClientProtocol, ChatMessage, ChatOptions


class UserInfoMemory(ContextProvider):
    def __init__(self, chat_client: ChatClientProtocol, user_info: UserInfo | None = None, **kwargs: Any):
        """Create the memory.

        If you pass in kwargs, they will be attempted to be used to create a UserInfo object.
        """
        self._chat_client = chat_client
        if user_info:
            self.user_info = user_info
        elif kwargs:
            self.user_info = UserInfo.model_validate(kwargs)
        else:
            self.user_info = UserInfo()

    async def invoked(
        self,
        request_messages: ChatMessage | Sequence[ChatMessage],
        response_messages: ChatMessage | Sequence[ChatMessage] | None = None,
        invoke_exception: Exception | None = None,
        **kwargs: Any,
    ) -> None:
        """Extract user information from messages after each agent call."""
        # Ensure request_messages is a list
        messages_list = [request_messages] if isinstance(request_messages, ChatMessage) else list(request_messages)
        
        # Check if we need to extract user info from user messages
        user_messages = [msg for msg in messages_list if msg.role.value == "user"]

        if (self.user_info.name is None or self.user_info.age is None) and user_messages:
            try:
                # Use the chat client to extract structured information
                result = await self._chat_client.get_response(
                    messages=messages_list,
                    chat_options=ChatOptions(
                        instructions=(
                            "Extract the user's name and age from the message if present. "
                            "If not present return nulls."
                        ),
                        response_format=UserInfo,
                    ),
                )

                # Update user info with extracted data
                if result.value and isinstance(result.value, UserInfo):
                    if self.user_info.name is None and result.value.name:
                        self.user_info.name = result.value.name
                    if self.user_info.age is None and result.value.age:
                        self.user_info.age = result.value.age

            except Exception:
                pass  # Failed to extract, continue without updating

    async def invoking(self, messages: ChatMessage | MutableSequence[ChatMessage], **kwargs: Any) -> Context:
        """Provide user information context before each agent call."""
        instructions: list[str] = []

        if self.user_info.name is None:
            instructions.append(
                "Ask the user for their name and politely decline to answer any questions until they provide it."
            )
        else:
            instructions.append(f"The user's name is {self.user_info.name}.")

        if self.user_info.age is None:
            instructions.append(
                "Ask the user for their age and politely decline to answer any questions until they provide it."
            )
        else:
            instructions.append(f"The user's age is {self.user_info.age}.")

        # Return context with additional instructions
        return Context(instructions=" ".join(instructions))

    def serialize(self) -> str:
        """Serialize the user info for thread persistence."""
        return self.user_info.model_dump_json()
```

## Using the ContextProvider with an agent

To use the custom `ContextProvider`, you need to provide the instantiated `ContextProvider` when creating the agent.

When creating a `ChatAgent` you can provide the `context_providers` parameter to attach the memory component to the agent.

```python
import asyncio
from agent_framework import ChatAgent
from agent_framework.azure import AzureAIAgentClient
from azure.identity.aio import AzureCliCredential

async def main():
    async with AzureCliCredential() as credential:
        chat_client = AzureAIAgentClient(credential=credential)

        # Create the memory provider
        memory_provider = UserInfoMemory(chat_client)

        # Create the agent with memory
        async with ChatAgent(
            chat_client=chat_client,
            instructions="You are a friendly assistant. Always address the user by their name.",
            context_providers=memory_provider,
        ) as agent:
            # Create a new thread for the conversation
            thread = agent.get_new_thread()

            print(await agent.run("Hello, what is the square root of 9?", thread=thread))
            print(await agent.run("My name is Ruaidhrí", thread=thread))
            print(await agent.run("I am 20 years old", thread=thread))

            # Access the memory component via the thread's context_providers attribute and inspect the memories
            if thread.context_provider:
                user_info_memory = thread.context_provider.providers[0]
                if isinstance(user_info_memory, UserInfoMemory):
                    print()
                    print(f"MEMORY - User Name: {user_info_memory.user_info.name}")
                    print(f"MEMORY - User Age: {user_info_memory.user_info.age}")


if __name__ == "__main__":
    asyncio.run(main())
```

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Create a simple workflow](../workflows/simple-sequential-workflow.md)
