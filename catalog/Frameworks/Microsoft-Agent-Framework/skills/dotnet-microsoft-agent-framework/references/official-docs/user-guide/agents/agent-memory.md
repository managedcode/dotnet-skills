---
title: Agent Chat History and Memory
description: Learn how to use chat history and memory with Agent Framework
zone_pivot_groups: programming-languages
author: markwallace
ms.topic: reference
ms.author: markwallace
ms.date: 09/24/2025
ms.service: agent-framework
---

# Agent Chat History and Memory

Agent chat history and memory are crucial capabilities that allow agents to maintain context across conversations, remember user preferences, and provide personalized experiences. The Agent Framework provides multiple features to suit different use cases, from simple in-memory chat message storage to persistent databases and specialized memory services.

::: zone pivot="programming-language-csharp"

## Chat History

Various chat history storage options are supported by Agent Framework. The available options vary by agent type and the underlying service(s) used to build the agent.

The two main supported scenarios are:

- **In-memory storage**: Agent is built on a service that doesn't support in-service storage of chat history (for example, OpenAI Chat Completion). By default, Agent Framework stores the full chat history in-memory in the `AgentThread` object, but developers can provide a custom `ChatMessageStore` implementation to store chat history in a third-party store if required.
- **In-service storage**: Agent is built on a service that requires in-service storage of chat history (for example, Azure AI Foundry Persistent Agents). Agent Framework stores the ID of the remote chat history in the `AgentThread` object, and no other chat history storage options are supported.

### In-memory chat history storage

When using a service that doesn't support in-service storage of chat history, Agent Framework defaults to storing chat history in-memory in the `AgentThread` object. In this case, the full chat history that's stored in the thread object, plus any new messages, will be provided to the underlying service on each agent run. This design allows for a natural conversational experience with the agent. The caller only provides the new user message, and the agent only returns new answers. But the agent has access to the full conversation history and will use it when generating its response.

When using OpenAI Chat Completion as the underlying service for agents, the following code results in the thread object containing the chat history from the agent run.

```csharp
AIAgent agent = new OpenAIClient("<your_api_key>")
     .GetChatClient(modelName)
     .AsAIAgent(JokerInstructions, JokerName);
AgentThread thread = await agent.GetNewThreadAsync();
Console.WriteLine(await agent.RunAsync("Tell me a joke about a pirate.", thread));
```

Where messages are stored in memory, it's possible to retrieve the list of messages from the thread and manipulate the messages directly if required.

```csharp
IList<ChatMessage>? messages = thread.GetService<IList<ChatMessage>>();
```

> [!NOTE]
> Retrieving messages from the `AgentThread` object in this way only works if in-memory storage is being used.

#### Chat history reduction with in-memory storage

The built-in `InMemoryChatMessageStore` that's used by default when the underlying service does not support in-service storage,
can be configured with a reducer to manage the size of the chat history.
This is useful to avoid exceeding the context size limits of the underlying service.

The `InMemoryChatMessageStore` can take an optional `Microsoft.Extensions.AI.IChatReducer` implementation to reduce the size of the chat history.
It also allows you to configure the event during which the reducer is invoked, either after a message is added to the chat history
or before the chat history is returned for the next invocation.

To configure the `InMemoryChatMessageStore` with a reducer, you can provide a factory to construct a new `InMemoryChatMessageStore`
for each new `AgentThread` and pass it a reducer of your choice. The `InMemoryChatMessageStore` can also be passed an optional trigger event
which can be set to either `InMemoryChatMessageStore.ChatReducerTriggerEvent.AfterMessageAdded` or `InMemoryChatMessageStore.ChatReducerTriggerEvent.BeforeMessagesRetrieval`.

The factory is an async function that receives a context object and a cancellation token.

```csharp
AIAgent agent = new OpenAIClient("<your_api_key>")
    .GetChatClient(modelName)
    .AsAIAgent(new ChatClientAgentOptions
    {
        Name = JokerName,
        ChatOptions = new() { Instructions = JokerInstructions },
        ChatMessageStoreFactory = (ctx, ct) => new ValueTask<ChatMessageStore>(
            new InMemoryChatMessageStore(
                new MessageCountingChatReducer(2),
                ctx.SerializedState,
                ctx.JsonSerializerOptions,
                InMemoryChatMessageStore.ChatReducerTriggerEvent.AfterMessageAdded))
    });
```

> [!NOTE]
> This feature is only supported when using the `InMemoryChatMessageStore`. When a service has in-service chat history storage, it is up to the service itself to manage the size of the chat history. Similarly, when using 3rd party storage (see below), it is up to the 3rd party storage solution to manage the chat history size. If you provide a `ChatMessageStoreFactory` for a message store but you use a service with built-in chat history storage, the factory will not be used.

### Inference service chat history storage

When using a service that requires in-service storage of chat history, Agent Framework stores the ID of the remote chat history in the `AgentThread` object.

For example, when using OpenAI Responses with store=true as the underlying service for agents, the following code will result in the thread object containing the last response ID returned by the service.

```csharp
AIAgent agent = new OpenAIClient("<your_api_key>")
     .GetOpenAIResponseClient(modelName)
     .AsAIAgent(JokerInstructions, JokerName);
AgentThread thread = await agent.GetNewThreadAsync();
Console.WriteLine(await agent.RunAsync("Tell me a joke about a pirate.", thread));
```

> [!NOTE]
> Some services, for example, OpenAI Responses support either in-service storage of chat history (store=true), or providing the full chat history on each invocation (store=false).
> Therefore, depending on the mode that the service is used in, Agent Framework will either default to storing the full chat history in memory, or storing an ID reference to the service stored chat history.

### Third-party chat history storage

When using a service that does not support in-service storage of chat history, Agent Framework allows developers to replace the default in-memory storage of chat history with third-party chat history storage. The developer is required to provide a subclass of the base abstract `ChatMessageStore` class.

The `ChatMessageStore` class defines the interface for storing and retrieving chat messages. Developers must implement the `InvokedAsync` and `InvokingAsync` methods to add messages to the remote store as they are generated, and retrieve messages from the remote store before invoking the underlying service.

The agent will use all messages returned by `InvokingAsync` when processing a user query. It is up to the implementer of `ChatMessageStore` to ensure that the size of the chat history does not exceed the context window of the underlying service.

When implementing a custom `ChatMessageStore` which stores chat history in a remote store, the chat history for that thread should be stored under a key that is unique to that thread. The `ChatMessageStore` implementation should generate this key and keep it in its state. `ChatMessageStore` has a `Serialize` method that can be overridden to serialize its state when the thread is serialized. The `ChatMessageStore` should also provide a constructor that takes a <xref:System.Text.Json.JsonElement> as input to support deserialization of its state.

To supply a custom `ChatMessageStore` to a `ChatClientAgent`, you can use the `ChatMessageStoreFactory` option when creating the agent.
Here is an example showing how to pass the custom implementation of `ChatMessageStore` to a `ChatClientAgent` that is based on Azure OpenAI Chat Completion.

The factory is an async function that receives a context object and a cancellation token.

```csharp
AIAgent agent = new AzureOpenAIClient(
    new Uri(endpoint),
    new AzureCliCredential())
    .GetChatClient(deploymentName)
    .AsAIAgent(new ChatClientAgentOptions
    {
        Name = JokerName,
        ChatOptions = new() { Instructions = JokerInstructions },
        ChatMessageStoreFactory = (ctx, ct) => new ValueTask<ChatMessageStore>(
            // Create a new chat message store for this agent that stores the messages in a custom store.
            // Each thread must get its own copy of the CustomMessageStore, since the store
            // also contains the ID that the thread is stored under.
            new CustomMessageStore(
                vectorStore,
                ctx.SerializedState,
                ctx.JsonSerializerOptions))
    });
```

> [!TIP]
> For a detailed example on how to create a custom message store, see the [Storing Chat History in 3rd Party Storage](../../tutorials/agents/third-party-chat-history-storage.md) tutorial.

## Long term memory

The Agent Framework allows developers to provide custom components that can extract memories or provide memories to an agent.

To implement such a memory component, the developer needs to subclass the `AIContextProvider` abstract base class. This class has two core methods, `InvokingAsync` and `InvokedAsync`. When overridden, `InvokedAsync` allows developers to inspect all messages provided by users or generated by the agent. `InvokingAsync` allows developers to inject additional context for a specific agent run. System instructions, additional messages and additional functions can be provided.

> [!TIP]
> For a detailed example on how to create a custom memory component, see the [Adding Memory to an Agent](../../tutorials/agents/memory.md) tutorial.

## AgentThread Serialization

It is important to be able to persist an `AgentThread` object between agent invocations. This allows for situations where a user might ask a question of the agent, and take a long time to ask follow up questions. This allows the `AgentThread` state to survive service or app restarts.

Even if the chat history is stored in a remote store, the `AgentThread` object still contains an ID referencing the remote chat history. Losing the `AgentThread` state will therefore result in also losing the ID of the remote chat history.

The `AgentThread` as well as any objects attached to it, all therefore provide the `Serialize` method to serialize their state. The `AIAgent` also provides a `DeserializeThreadAsync` method that re-creates a thread from the serialized state. The `DeserializeThreadAsync` method re-creates the thread with the `ChatMessageStore` and `AIContextProvider` configured on the agent.

```csharp
// Serialize the thread state to a JsonElement, so it can be stored for later use.
JsonElement serializedThreadState = thread.Serialize();

// Re-create the thread from the JsonElement.
AgentThread resumedThread = await agent.DeserializeThreadAsync(serializedThreadState);
```

> [!NOTE]
> `AgentThread` objects may contain more than just chat history, e.g. context providers may also store state in the thread object. Therefore, it is important to always serialize, store and deserialize the entire `AgentThread` object to ensure that all state is preserved.
> [!IMPORTANT]
> Always treat `AgentThread` objects as opaque objects, unless you are very sure of the internals. The contents may vary not just by agent type, but also by service type and configuration.
> [!WARNING]
> Deserializing a thread with a different agent than that which originally created it, or with an agent that has a different configuration than the original agent, might result in errors or unexpected behavior.

::: zone-end
::: zone pivot="programming-language-python"

## Memory Types

The Agent Framework supports several types of memory to accommodate different use cases, including managing chat history as part of short term memory and providing extension points for extracting, storing and injecting long term memories into agents.

### In-Memory Storage (Default)

The simplest form of memory where conversation history is stored in memory during the application runtime. This is the default behavior and requires no additional configuration.

```python
from agent_framework import ChatAgent
from agent_framework.openai import OpenAIChatClient

# Default behavior - uses in-memory storage
agent = ChatAgent(
    chat_client=OpenAIChatClient(),
    instructions="You are a helpful assistant."
)

# Conversation history is maintained in memory for this thread
thread = agent.get_new_thread()

response = await agent.run("Hello, my name is Alice", thread=thread)
```

### Persistent Message Stores
For applications that need to persist conversation history across sessions, the framework provides `ChatMessageStore` implementations:

#### Built-in ChatMessageStore
The default in-memory implementation that can be serialized:

```python
from agent_framework import ChatMessageStore

# Create a custom message store
def create_message_store():
    return ChatMessageStore()

agent = ChatAgent(
    chat_client=OpenAIChatClient(),
    instructions="You are a helpful assistant.",
    chat_message_store_factory=create_message_store
)
```

#### Redis Message Store
For production applications requiring persistent storage:

```python
from agent_framework.redis import RedisChatMessageStore

def create_redis_store():
    return RedisChatMessageStore(
        redis_url="redis://localhost:6379",
        thread_id="user_session_123",
        max_messages=100  # Keep last 100 messages
    )

agent = ChatAgent(
    chat_client=OpenAIChatClient(),
    instructions="You are a helpful assistant.",
    chat_message_store_factory=create_redis_store
)
```

#### Custom Message Store
You can implement your own storage backend by implementing the `ChatMessageStoreProtocol`:

```python
from agent_framework import ChatMessage, ChatMessageStoreProtocol
from typing import Any
from collections.abc import Sequence

class DatabaseMessageStore(ChatMessageStoreProtocol):
    def __init__(self, connection_string: str):
        self.connection_string = connection_string
        self._messages: list[ChatMessage] = []

    async def add_messages(self, messages: Sequence[ChatMessage]) -> None:
        """Add messages to database."""
        # Implement database insertion logic
        self._messages.extend(messages)

    async def list_messages(self) -> list[ChatMessage]:
        """Retrieve messages from database."""
        # Implement database query logic
        return self._messages

    async def serialize(self, **kwargs: Any) -> Any:
        """Serialize store state for persistence."""
        return {"connection_string": self.connection_string}

    async def update_from_state(self, serialized_store_state: Any, **kwargs: Any) -> None:
        """Update store from serialized state."""
        if serialized_store_state:
            self.connection_string = serialized_store_state["connection_string"]
```

> [!TIP]
> For a detailed example on how to create a custom message store, see the [Storing Chat History in 3rd Party Storage](../../tutorials/agents/third-party-chat-history-storage.md) tutorial.

### Context Providers (Dynamic Memory)
Context providers enable sophisticated memory patterns by injecting relevant context before each agent invocation:

#### Basic Context Provider
```python
from agent_framework import ContextProvider, Context, ChatMessage
from collections.abc import MutableSequence
from typing import Any

class UserPreferencesMemory(ContextProvider):
    def __init__(self):
        self.preferences = {}

    async def invoking(self, messages: ChatMessage | MutableSequence[ChatMessage], **kwargs: Any) -> Context:
        """Provide user preferences before each invocation."""
        if self.preferences:
            preferences_text = ", ".join([f"{k}: {v}" for k, v in self.preferences.items()])
            instructions = f"User preferences: {preferences_text}"
            return Context(instructions=instructions)
        return Context()

    async def invoked(
        self,
        request_messages: ChatMessage | Sequence[ChatMessage],
        response_messages: ChatMessage | Sequence[ChatMessage] | None = None,
        invoke_exception: Exception | None = None,
        **kwargs: Any,
    ) -> None:
        """Extract and store user preferences from the conversation."""
        # Implement preference extraction logic
        pass
```

> [!TIP]
> For a detailed example on how to create a custom memory component, see the [Adding Memory to an Agent](../../tutorials/agents/memory.md) tutorial.

#### External Memory Services
The framework supports integration with specialized memory services like Mem0:

```python
from agent_framework.mem0 import Mem0Provider

# Using Mem0 for advanced memory capabilities
memory_provider = Mem0Provider(
    api_key="your-mem0-api-key",
    user_id="user_123",
    application_id="my_app"
)

agent = ChatAgent(
    chat_client=OpenAIChatClient(),
    instructions="You are a helpful assistant with memory.",
    context_providers=memory_provider
)
```

### Thread Serialization and Persistence
The framework supports serializing entire thread states for persistence across application restarts:

```python
import json

# Create agent and thread
agent = ChatAgent(chat_client=OpenAIChatClient())
thread = agent.get_new_thread()

# Have conversation
await agent.run("Hello, my name is Alice", thread=thread)

# Serialize thread state
serialized_thread = await thread.serialize()
# Save to file/database
with open("thread_state.json", "w") as f:
    json.dump(serialized_thread, f)

# Later, restore the thread
with open("thread_state.json", "r") as f:
    thread_data = json.load(f)

restored_thread = await agent.deserialize_thread(thread_data)
# Continue conversation with full context
await agent.run("What's my name?", thread=restored_thread)
```

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Agent Tools](./agent-tools.md)
