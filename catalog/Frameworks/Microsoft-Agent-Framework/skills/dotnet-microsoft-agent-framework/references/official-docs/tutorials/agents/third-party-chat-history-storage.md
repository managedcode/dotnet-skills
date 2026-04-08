---
title: Storing Chat History in 3rd Party Storage
description: How to store agent chat history in external storage using a custom ChatMessageStore.
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: tutorial
ms.author: westey
ms.date: 09/25/2025
ms.service: agent-framework
---

# Storing Chat History in 3rd Party Storage

::: zone pivot="programming-language-csharp"

This tutorial shows how to store agent chat history in external storage by implementing a custom `ChatMessageStore` and using it with a `ChatClientAgent`.

By default, when using `ChatClientAgent`, chat history is stored either in memory in the `AgentThread` object or the underlying inference service, if the service supports it.

Where services do not require chat history to be stored in the service, it is possible to provide a custom store for persisting chat history instead of relying on the default in-memory behavior.

## Prerequisites

For prerequisites, see the [Create and run a simple agent](./run-agent.md) step in this tutorial.

## Install NuGet packages

To use Microsoft Agent Framework with Azure OpenAI, you need to install the following NuGet packages:

```dotnetcli
dotnet add package Azure.AI.OpenAI --prerelease
dotnet add package Azure.Identity
dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
```

In addition, you'll use the in-memory vector store to store chat messages.

```dotnetcli
dotnet add package Microsoft.SemanticKernel.Connectors.InMemory --prerelease
```

## Create a custom ChatMessage Store

To create a custom `ChatMessageStore`, you need to implement the abstract `ChatMessageStore` class and provide implementations for the required methods.

### Message storage and retrieval methods

The most important methods to implement are:

- `InvokingAsync` - called at the start of agent invocation to retrieve messages from the store that should be provided as context.
- `InvokedAsync` - called at the end of agent invocation to add new messages to the store.

`InvokingAsync` should return the messages in ascending chronological order (oldest first). All messages returned by it will be used by the `ChatClientAgent` when making calls to the underlying <xref:Microsoft.Extensions.AI.IChatClient>. It's therefore important that this method considers the limits of the underlying model, and only returns as many messages as can be handled by the model.

Any chat history reduction logic, such as summarization or trimming, should be done before returning messages from `InvokingAsync`.

### Serialization

`ChatMessageStore` instances are created and attached to an `AgentThread` when the thread is created, and when a thread is resumed from a serialized state.

While the actual messages making up the chat history are stored externally, the `ChatMessageStore` instance might need to store keys or other state to identify the chat history in the external store.

To allow persisting threads, you need to implement the `Serialize` method of the `ChatMessageStore` class. This method should return a `JsonElement` containing the state needed to restore the store later. When deserializing, the agent framework will pass this serialized state to the ChatMessageStoreFactory, allowing you to use it to recreate the store.

### Sample ChatMessageStore implementation

The following sample implementation stores chat messages in a vector store.

`InvokedAsync` upserts messages into the vector store, using a unique key for each message. It stores both the request messages and response messages from the invocation context.

`InvokingAsync` retrieves the messages for the current thread from the vector store, orders them by timestamp, and returns them in ascending chronological order (oldest first).

When the first invocation occurs, the store generates a unique key for the thread, which is then used to identify the chat history in the vector store for subsequent calls.

The unique key is stored in the `ThreadDbKey` property, which is serialized using the `Serialize` method and deserialized via the constructor that takes a `JsonElement`.
This key will therefore be persisted as part of the `AgentThread` state, allowing the thread to be resumed later and continue using the same chat history.

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;

internal sealed class VectorChatMessageStore : ChatMessageStore
{
    private readonly VectorStore _vectorStore;

    public VectorChatMessageStore(
        VectorStore vectorStore,
        JsonElement serializedStoreState,
        JsonSerializerOptions? jsonSerializerOptions = null)
    {
        this._vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        if (serializedStoreState.ValueKind is JsonValueKind.String)
        {
            this.ThreadDbKey = serializedStoreState.Deserialize<string>();
        }
    }

    public string? ThreadDbKey { get; private set; }

    public override async ValueTask<IEnumerable<ChatMessage>> InvokingAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        if (this.ThreadDbKey is null)
        {
            // No thread key yet, so no messages to retrieve
            return [];
        }

        var collection = this._vectorStore.GetCollection<string, ChatHistoryItem>("ChatHistory");
        await collection.EnsureCollectionExistsAsync(cancellationToken);
        var records = collection
            .GetAsync(
                x => x.ThreadId == this.ThreadDbKey, 
                10,
                new() { OrderBy = x => x.Descending(y => y.Timestamp) },
                cancellationToken);

        List<ChatMessage> messages = [];
        await foreach (var record in records)
        {
            messages.Add(JsonSerializer.Deserialize<ChatMessage>(record.SerializedMessage!)!);
        }
        
        // Reverse to return in ascending chronological order (oldest first)
        messages.Reverse();
        return messages;
    }

    public override async ValueTask InvokedAsync(
        InvokedContext context,
        CancellationToken cancellationToken = default)
    {
        // Don't store messages if the request failed.
        if (context.InvokeException is not null)
        {
            return;
        }

        this.ThreadDbKey ??= Guid.NewGuid().ToString("N");
        
        var collection = this._vectorStore.GetCollection<string, ChatHistoryItem>("ChatHistory");
        await collection.EnsureCollectionExistsAsync(cancellationToken);
        
        // Store request messages, response messages, and optionally AIContextProvider messages
        var allNewMessages = context.RequestMessages
            .Concat(context.AIContextProviderMessages ?? [])
            .Concat(context.ResponseMessages ?? []);
        
        await collection.UpsertAsync(allNewMessages.Select(x => new ChatHistoryItem()
        {
            Key = this.ThreadDbKey + x.MessageId,
            Timestamp = DateTimeOffset.UtcNow,
            ThreadId = this.ThreadDbKey,
            SerializedMessage = JsonSerializer.Serialize(x),
            MessageText = x.Text
        }), cancellationToken);
    }

    public override JsonElement Serialize(JsonSerializerOptions? jsonSerializerOptions = null) =>
        // We have to serialize the thread id, so that on deserialization you can retrieve the messages using the same thread id.
        JsonSerializer.SerializeToElement(this.ThreadDbKey);

    private sealed class ChatHistoryItem
    {
        [VectorStoreKey]
        public string? Key { get; set; }
        [VectorStoreData]
        public string? ThreadId { get; set; }
        [VectorStoreData]
        public DateTimeOffset? Timestamp { get; set; }
        [VectorStoreData]
        public string? SerializedMessage { get; set; }
        [VectorStoreData]
        public string? MessageText { get; set; }
    }
}
```

## Using the custom ChatMessageStore with a ChatClientAgent

To use the custom `ChatMessageStore`, you need to provide a `ChatMessageStoreFactory` when creating the agent. This factory allows the agent to create a new instance of the desired `ChatMessageStore` for each thread.

When creating a `ChatClientAgent` it is possible to provide a `ChatClientAgentOptions` object that allows providing the `ChatMessageStoreFactory` in addition to all other agent options.

The factory is an async function that receives a context object and a cancellation token, and returns a `ValueTask<ChatMessageStore>`.

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;

// Create a vector store to store the chat messages in.
VectorStore vectorStore = new InMemoryVectorStore();

AIAgent agent = new AzureOpenAIClient(
    new Uri("https://<myresource>.openai.azure.com"),
    new AzureCliCredential())
     .GetChatClient("gpt-4o-mini")
     .AsAIAgent(new ChatClientAgentOptions
     {
         Name = "Joker",
         ChatOptions = new() { Instructions = "You are good at telling jokes." },
         ChatMessageStoreFactory = (ctx, ct) => new ValueTask<ChatMessageStore>(
             // Create a new chat message store for this agent that stores the messages in a vector store.
             // Each thread must get its own copy of the VectorChatMessageStore, since the store
             // also contains the id that the thread is stored under.
             new VectorChatMessageStore(
                vectorStore,
                ctx.SerializedState,
                ctx.JsonSerializerOptions))
     });

// Start a new thread for the agent conversation.
AgentThread thread = await agent.GetNewThreadAsync();

// Run the agent with the thread
var response = await agent.RunAsync("Tell me a joke about a pirate.", thread);

// The thread state can be serialized for storage
JsonElement serializedThread = thread.Serialize();

// Later, deserialize the thread to resume the conversation
AgentThread resumedThread = await agent.DeserializeThreadAsync(serializedThread);
```

::: zone-end
::: zone pivot="programming-language-python"

This tutorial shows how to store agent chat history in external storage by implementing a custom `ChatMessageStore` and using it with a `ChatAgent`.

By default, when using `ChatAgent`, chat history is stored either in memory in the `AgentThread` object or the underlying inference service, if the service supports it.

Where services do not require or are not capable of the chat history to be stored in the service, it is possible to provide a custom store for persisting chat history instead of relying on the default in-memory behavior.

## Prerequisites

For prerequisites, see the [Create and run a simple agent](./run-agent.md) step in this tutorial.

## Create a custom ChatMessage Store

To create a custom `ChatMessageStore`, you need to implement the `ChatMessageStore` protocol and provide implementations for the required methods.

### Message storage and retrieval methods

The most important methods to implement are:

- `add_messages` - called to add new messages to the store.
- `list_messages` - called to retrieve the messages from the store.

`list_messages` should return the messages in ascending chronological order. All messages returned by it will be used by the `ChatAgent` when making calls to the underlying chat client. It's therefore important that this method considers the limits of the underlying model, and only returns as many messages as can be handled by the model.

Any chat history reduction logic, such as summarization or trimming, should be done before returning messages from `list_messages`.

### Serialization

`ChatMessageStore` instances are created and attached to an `AgentThread` when the thread is created, and when a thread is resumed from a serialized state.

While the actual messages making up the chat history are stored externally, the `ChatMessageStore` instance might need to store keys or other state to identify the chat history in the external store.

To allow persisting threads, you need to implement the `serialize_state` and `deserialize_state` methods of the `ChatMessageStore` protocol. These methods allow the store's state to be persisted and restored when resuming a thread.

### Sample ChatMessageStore implementation

The following sample implementation stores chat messages in Redis using the Redis Lists data structure.

In `add_messages`, it stores messages in Redis using RPUSH to append them to the end of the list in chronological order.

`list_messages` retrieves the messages for the current thread from Redis using LRANGE, and returns them in ascending chronological order.

When the first message is received, the store generates a unique key for the thread, which is then used to identify the chat history in Redis for subsequent calls.

The unique key and other configuration are stored and can be serialized and deserialized using the `serialize_state` and `deserialize_state` methods.
This state will therefore be persisted as part of the `AgentThread` state, allowing the thread to be resumed later and continue using the same chat history.

```python
from collections.abc import Sequence
from typing import Any
from uuid import uuid4
from pydantic import BaseModel
import json
import redis.asyncio as redis
from agent_framework import ChatMessage


class RedisStoreState(BaseModel):
    """State model for serializing and deserializing Redis chat message store data."""

    thread_id: str
    redis_url: str | None = None
    key_prefix: str = "chat_messages"
    max_messages: int | None = None


class RedisChatMessageStore:
    """Redis-backed implementation of ChatMessageStore using Redis Lists."""

    def __init__(
        self,
        redis_url: str | None = None,
        thread_id: str | None = None,
        key_prefix: str = "chat_messages",
        max_messages: int | None = None,
    ) -> None:
        """Initialize the Redis chat message store.

        Args:
            redis_url: Redis connection URL (for example, "redis://localhost:6379").
            thread_id: Unique identifier for this conversation thread.
                      If not provided, a UUID will be auto-generated.
            key_prefix: Prefix for Redis keys to namespace different applications.
            max_messages: Maximum number of messages to retain in Redis.
                         When exceeded, oldest messages are automatically trimmed.
        """
        if redis_url is None:
            raise ValueError("redis_url is required for Redis connection")

        self.redis_url = redis_url
        self.thread_id = thread_id or f"thread_{uuid4()}"
        self.key_prefix = key_prefix
        self.max_messages = max_messages

        # Initialize Redis client
        self._redis_client = redis.from_url(redis_url, decode_responses=True)

    @property
    def redis_key(self) -> str:
        """Get the Redis key for this thread's messages."""
        return f"{self.key_prefix}:{self.thread_id}"

    async def add_messages(self, messages: Sequence[ChatMessage]) -> None:
        """Add messages to the Redis store.

        Args:
            messages: Sequence of ChatMessage objects to add to the store.
        """
        if not messages:
            return

        # Serialize messages and add to Redis list
        serialized_messages = [self._serialize_message(msg) for msg in messages]
        await self._redis_client.rpush(self.redis_key, *serialized_messages)

        # Apply message limit if configured
        if self.max_messages is not None:
            current_count = await self._redis_client.llen(self.redis_key)
            if current_count > self.max_messages:
                # Keep only the most recent max_messages using LTRIM
                await self._redis_client.ltrim(self.redis_key, -self.max_messages, -1)

    async def list_messages(self) -> list[ChatMessage]:
        """Get all messages from the store in chronological order.

        Returns:
            List of ChatMessage objects in chronological order (oldest first).
        """
        # Retrieve all messages from Redis list (oldest to newest)
        redis_messages = await self._redis_client.lrange(self.redis_key, 0, -1)

        messages = []
        for serialized_message in redis_messages:
            message = self._deserialize_message(serialized_message)
            messages.append(message)

        return messages

    async def serialize_state(self, **kwargs: Any) -> Any:
        """Serialize the current store state for persistence.

        Returns:
            Dictionary containing serialized store configuration.
        """
        state = RedisStoreState(
            thread_id=self.thread_id,
            redis_url=self.redis_url,
            key_prefix=self.key_prefix,
            max_messages=self.max_messages,
        )
        return state.model_dump(**kwargs)

    async def deserialize_state(self, serialized_store_state: Any, **kwargs: Any) -> None:
        """Deserialize state data into this store instance.

        Args:
            serialized_store_state: Previously serialized state data.
            **kwargs: Additional arguments for deserialization.
        """
        if serialized_store_state:
            state = RedisStoreState.model_validate(serialized_store_state, **kwargs)
            self.thread_id = state.thread_id
            self.key_prefix = state.key_prefix
            self.max_messages = state.max_messages

            # Recreate Redis client if the URL changed
            if state.redis_url and state.redis_url != self.redis_url:
                self.redis_url = state.redis_url
                self._redis_client = redis.from_url(self.redis_url, decode_responses=True)

    def _serialize_message(self, message: ChatMessage) -> str:
        """Serialize a ChatMessage to JSON string."""
        message_dict = message.model_dump()
        return json.dumps(message_dict, separators=(",", ":"))

    def _deserialize_message(self, serialized_message: str) -> ChatMessage:
        """Deserialize a JSON string to ChatMessage."""
        message_dict = json.loads(serialized_message)
        return ChatMessage.model_validate(message_dict)

    async def clear(self) -> None:
        """Remove all messages from the store."""
        await self._redis_client.delete(self.redis_key)

    async def aclose(self) -> None:
        """Close the Redis connection."""
        await self._redis_client.aclose()
```

## Using the custom ChatMessageStore with a ChatAgent

To use the custom `ChatMessageStore`, you need to provide a `chat_message_store_factory` when creating the agent. This factory allows the agent to create a new instance of the desired `ChatMessageStore` for each thread.

When creating a `ChatAgent`, you can provide the `chat_message_store_factory` parameter in addition to all other agent options.

```python
from azure.identity import AzureCliCredential
from agent_framework import ChatAgent
from agent_framework.openai import AzureOpenAIChatClient

# Create the chat agent with custom message store factory
agent = ChatAgent(
    chat_client=AzureOpenAIChatClient(
        endpoint="https://<myresource>.openai.azure.com",
        credential=AzureCliCredential(),
        ai_model_id="gpt-4o-mini"
    ),
    name="Joker",
    instructions="You are good at telling jokes.",
    chat_message_store_factory=lambda: RedisChatMessageStore(
        redis_url="redis://localhost:6379"
    )
)

# Use the agent with persistent chat history
thread = agent.get_new_thread()
response = await agent.run("Tell me a joke about pirates", thread=thread)
print(response.text)
```

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Adding Memory to an Agent](memory.md)
