---
title: Storage
description: Learn built-in storage modes and how to persist session state or plug in external storage.
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 07/01/2026
ms.service: agent-framework
---

# Storage

Storage controls where conversation history lives, how much history is loaded, and how reliably sessions can be resumed.

## Built-in storage modes

Agent Framework supports two regular storage modes:

| Mode | What is stored | Typical usage |
|---|---|---|
| Local session state | Full chat history in `AgentSession.state` (for example via `InMemoryHistoryProvider`) | Services that don't require server-side conversation persistence |
| Service-managed storage | Conversation state in the service; `AgentSession.service_session_id` points to it | Services with native persistent conversation support |

## In-memory chat history storage

When a provider doesn't require server-side chat history, Agent Framework keeps history locally in the session and sends relevant messages on each run.

:::zone pivot="programming-language-csharp"

```csharp
AIAgent agent = new OpenAIClient("<your_api_key>")
    .GetChatClient(modelName)
    .AsAIAgent(instructions: "You are a helpful assistant.", name: "Assistant");

AgentSession session = await agent.CreateSessionAsync();
Console.WriteLine(await agent.RunAsync("Tell me a joke about a pirate.", session));

// When in-memory chat history storage is used, it's possible to access the chat history
// that is stored in the session via the provider attached to the agent.
var provider = agent.GetService<InMemoryChatHistoryProvider>();
List<ChatMessage>? messages = provider?.GetMessages(session);
```

:::zone-end

:::zone pivot="programming-language-python"

```python
from agent_framework import InMemoryHistoryProvider
from agent_framework.openai import OpenAIChatClient

agent = OpenAIChatClient().as_agent(
    name="StorageAgent",
    instructions="You are a helpful assistant.",
    context_providers=[InMemoryHistoryProvider("memory", load_messages=True)],
)

session = agent.create_session()
await agent.run("Remember that I like Italian food.", session=session)
```

:::zone-end

:::zone pivot="programming-language-go"

Go stores local chat history in `agent.Session` through an `agent.HistoryProvider`. If you don't configure a history provider, Agent Framework creates a default in-memory provider that is used when you pass an explicit local session. Configure one explicitly when you want a stable source ID or custom filters.

```go
history := agent.NewInMemoryHistoryProvider(agent.InMemoryHistoryProviderConfig{
    SourceID: "chat_history",
})

a := foundryprovider.NewAgent(endpoint, token, foundryprovider.ModelDeployment(model), foundryprovider.AgentConfig{
    Instructions: "You are a helpful assistant.",
    Config: agent.Config{
        Name:            "StorageAgent",
        HistoryProvider: history,
    },
})

session, err := a.CreateSession(ctx)
if err != nil {
    panic(err)
}

_, err = a.RunText(ctx, "Remember that I like Italian food.", agent.WithSession(session)).Collect()
_, err = a.RunText(ctx, "What kind of food do I like?", agent.WithSession(session)).Collect()
```

:::zone-end

## Reducing in-memory history size

If history grows too large for model limits, apply a reducer.

:::zone pivot="programming-language-csharp"

```csharp
AIAgent agent = new OpenAIClient("<your_api_key>")
    .GetChatClient(modelName)
    .AsAIAgent(new ChatClientAgentOptions
    {
        Name = "Assistant",
        ChatOptions = new() { Instructions = "You are a helpful assistant." },
        ChatHistoryProvider = new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions
        {
            ChatReducer = new MessageCountingChatReducer(20)
        })
    });
```

:::zone-end

:::zone pivot="programming-language-go"

Use a `HistoryProvider` filter to limit the history messages loaded into the next request. For example, keep only the most recent 20 history messages:

```go
history := agent.NewInMemoryHistoryProvider(agent.InMemoryHistoryProviderConfig{
    SourceID: "chat_history",
    ProvideOutputMessageFilter: func(_ context.Context, messages []*message.Message) ([]*message.Message, error) {
        if len(messages) <= 20 {
            return messages, nil
        }

        return messages[len(messages)-20:], nil
    },
})
```

For semantic or token-aware reduction, use a compaction strategy before the run instead of relying only on message counts.

:::zone-end

> [!NOTE]
> Reducer configuration applies to in-memory history providers. For service-managed history, reduction behavior is provider/service specific.

## Service-managed storage

When the service manages conversation history, the session stores a remote conversation identifier.

For OpenAI Responses and Conversations, service-side IDs such as `resp_*` and `conv_*` are opaque and scoped to the backing API key or project by default. This is usually sufficient when that key or project is already scoped to one application, user, or tenant. If you host an agent for multiple end users with the same backing key or project, keep those IDs in trusted server-side storage, map them from your own session IDs, and verify ownership before resuming a conversation.

:::zone pivot="programming-language-csharp"

```csharp
AIAgent agent = new OpenAIClient("<your_api_key>")
    .GetOpenAIResponseClient(modelName)
    .AsAIAgent(instructions: "You are a helpful assistant.", name: "Assistant");

AgentSession session = await agent.CreateSessionAsync();
Console.WriteLine(await agent.RunAsync("Tell me a joke about a pirate.", session));

// In this case, since we know we are working with a ChatClientAgent, we can cast
// the AgentSession to a ChatClientAgentSession to retrieve the remote conversation
// identifier.
ChatClientAgentSession typedSession = (ChatClientAgentSession)session;
Console.WriteLine(typedSession.ConversationId);
```

:::zone-end

:::zone pivot="programming-language-python"

```python
# Rehydrate when the service already has the conversation state.
session = agent.get_session(service_session_id="<service-conversation-id>")
response = await agent.run("Continue this conversation.", session=session)
```

:::zone-end

:::zone pivot="programming-language-go"

Go stores provider-specific conversation identifiers in `session.ServiceID()`. Create a session with an existing service conversation ID when you need to resume service-managed history:

```go
session, err := a.CreateSession(ctx, agent.WithServiceID("<service-conversation-id>"))
if err != nil {
    panic(err)
}

_, err = a.RunText(ctx, "Continue this conversation.", agent.WithSession(session)).Collect()
```

When a provider creates or updates the remote conversation identifier during a run, the session is updated and you can inspect it after the call:

```go
fmt.Println(session.ServiceID())
```

Configured local history providers are skipped for service-managed sessions so the service remains the source of conversation history.

:::zone-end

## Per-service-call local history persistence

Tool-calling runs can make multiple model calls before a single `agent.run()` completes. By default, local history providers persist once after the full run. If you want local history to mirror service-managed conversations more closely, set `require_per_service_call_history_persistence=True` so history providers run around each model call instead.

:::zone pivot="programming-language-python"

```python
from agent_framework import Agent, InMemoryHistoryProvider
from agent_framework.openai import OpenAIChatClient

agent = Agent(
    client=OpenAIChatClient(),
    name="StorageAgent",
    instructions="You are a helpful assistant.",
    context_providers=[InMemoryHistoryProvider("memory", load_messages=True)],
    require_per_service_call_history_persistence=True,
)
```

> [!IMPORTANT]
> Use this mode only for framework-managed local history. If the run is already bound to a service-managed conversation (for example via `session.service_session_id` or `options={"conversation_id": ...}`), Agent Framework raises an error instead of mixing the two persistence models.
>
> This mode is especially useful when middleware can terminate immediately after a tool call: persisting per model call keeps local history aligned with what a service-managed conversation would keep.

:::zone-end

:::zone pivot="programming-language-go"

Go history providers run around an agent invocation. There isn't a separate per-service-call persistence switch; if a tool loop makes multiple provider calls inside one run, persist local history after the full run or implement a custom provider/middleware for your application's storage needs.

:::zone-end

## Third-party/Custom storage pattern

For database/Redis/blob-backed history, implement a custom history provider.

Key guidance:

- Store messages under a session-scoped key.
- Keep returned history within model context limits.
- Persist provider-specific identifiers in the session state.
:::zone pivot="programming-language-csharp"

The base class for history providers is `Microsoft.Agents.AI.ChatHistoryProvider`.
History providers participate in the agent pipeline, have the ability to contribute to or override agent input messages
and can store new messages.
`ChatHistoryProvider` has various virtual methods that can be overridden to implement your own custom history provider.
See the different implementation options below for more information on what to override.

### `ChatHistoryProvider` state

A `ChatHistoryProvider` instance is attached to an agent and the same instance would be used for all sessions.
This means that the `ChatHistoryProvider` should not store any session specific state in the provider instance.
The `ChatHistoryProvider` may have a reference to a database client in a field, but shouldn't have a database key for
the chat history in a field.

Instead, the `ChatHistoryProvider` can store any session specific values, like database keys, messages, or anything else that is relevant
in the `AgentSession` itself.  The virtual methods on `ChatHistoryProvider` are all passed a reference to the current `AIAgent` and `AgentSession`.

To enable easily storing typed state in the `AgentSession`, a utility class is provided:

```csharp
// First define a type containing the properties to store in state
internal class MyCustomState
{
    public string? DbKey { get; set; }
}

// Create the helper
var sessionStateHelper = new ProviderSessionState<MyCustomState>(
    // stateInitializer is called when there is no state in the session for this ChatHistoryProvider yet
    stateInitializer: currentSession => new MyCustomState() { DbKey = Guid.NewGuid().ToString() },
    // The key under which to store state in the session for this provider. Make sure it does not clash with the keys of other providers.
    stateKey: this.GetType().Name,
    // An optional jsonSerializerOptions to control the serialization/deserialization of the custom state object
    jsonSerializerOptions: myJsonSerializerOptions);

// Using the helper you can read state:
MyCustomState state = sessionStateHelper.GetOrInitializeState(session);
Console.WriteLine(state.DbKey);

// And write state:
sessionStateHelper.SaveState(session, state);
```

### Simple `ChatHistoryProvider` implementation

The simplest `ChatHistoryProvider` implementation would typically override two methods:

- **ChatHistoryProvider.ProvideChatHistoryAsync** - Load relevant chat history and return the loaded messages.
- **ChatHistoryProvider.StoreChatHistoryAsync** - Store request and response messages, all of which should be new.

Here is an example of a simple `ChatHistoryProvider` that stores the chat history directly in the session state.

```csharp
public sealed class SimpleInMemoryChatHistoryProvider : ChatHistoryProvider
{
    private readonly ProviderSessionState<State> _sessionState;

    public SimpleInMemoryChatHistoryProvider(
        Func<AgentSession?, State>? stateInitializer = null,
        string? stateKey = null)
    {
        this._sessionState = new ProviderSessionState<State>(
            stateInitializer ?? (_ => new State()),
            stateKey ?? this.GetType().Name);
    }

    public override string StateKey => this._sessionState.StateKey;

    protected override ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(InvokingContext context, CancellationToken cancellationToken = default) =>
        // return all messages in the session state
        new(this._sessionState.GetOrInitializeState(context.Session).Messages);

    protected override ValueTask StoreChatHistoryAsync(InvokedContext context, CancellationToken cancellationToken = default)
    {
        var state = this._sessionState.GetOrInitializeState(context.Session);

        // Add both request and response messages to the session state.
        var allNewMessages = context.RequestMessages.Concat(context.ResponseMessages ?? []);
        state.Messages.AddRange(allNewMessages);

        this._sessionState.SaveState(context.Session, state);

        return default;
    }

    public sealed class State
    {
        [JsonPropertyName("messages")]
        public List<ChatMessage> Messages { get; set; } = [];
    }
}
```

### Advanced `ChatHistoryProvider` implementation

A more advanced implementation could choose to override the following methods:

- **ChatHistoryProvider.InvokingCoreAsync** - Called before the agent invokes the LLM and allows the request message list to be modified.
- **ChatHistoryProvider.InvokedCoreAsync** - Called after the agent had invoked the LLM and allows access to all request and response messages.

`ChatHistoryProvider` provides base implementations of `InvokingCoreAsync` and `InvokedCoreAsync`.

The `InvokingCoreAsync` base implementation does the following:

- calls `ProvideChatHistoryAsync` to get the messages that should be used as chat history for the run
- runs an optional filter `Func` `provideOutputMessageFilter` on messages returned by `ProvideChatHistoryAsync`. This filter `Func` can be supplied via the `ChatHistoryProvider` constructor.
- merges the filtered messages returned by `ProvideChatHistoryAsync` with the messages passed into the agent by the caller, to produce the agent request messages. Chat history is prepended to agent input messages.
- stamps all filtered messages returned by `ProvideChatHistoryAsync` with source information, indicating that these messages are coming from chat history.

The `InvokedCoreAsync` base does the following:

- checks if the run failed and if so, returns without doing any further processing.
- filters the agent request messages to exclude messages that were produced by a `ChatHistoryProvider`, since we want to only store new messages and not those that were produced by the `ChatHistoryProvider` in the first place. Note that this filter can be overridden via the `storeInputMessageFilter` parameter on the `ChatHistoryProvider` constructor.
- passes the filtered request messages and all response messages to `StoreChatHistoryAsync` for storage.

It's possible to override these methods to implement an `ChatHistoryProvider`, however this requires the implementer to implement the base functionality themself as appropriate.
Here is an example of such an implementation.

```csharp
public sealed class AdvancedInMemoryChatHistoryProvider : ChatHistoryProvider
{
    private readonly ProviderSessionState<State> _sessionState;

    public AdvancedInMemoryChatHistoryProvider(
        Func<AgentSession?, State>? stateInitializer = null,
        string? stateKey = null)
    {
        this._sessionState = new ProviderSessionState<State>(
            stateInitializer ?? (_ => new State()),
            stateKey ?? this.GetType().Name);
    }

    public override string StateKey => this._sessionState.StateKey;

    protected override ValueTask<IEnumerable<ChatMessage>> InvokingCoreAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        // Retrieve the chat history from the session state.
        var chatHistory = this._sessionState.GetOrInitializeState(context.Session).Messages;

        // Stamp the messages with this class as the source, so that they can be filtered out later if needed when storing the agent input/output.
        var stampedChatHistory = chatHistory.Select(message => message.WithAgentRequestMessageSource(AgentRequestMessageSourceType.ChatHistory, this.GetType().FullName!));

        // Merge the original input with the chat history to produce a combined agent input.
        return new(stampedChatHistory.Concat(context.RequestMessages));
    }

    protected override ValueTask InvokedCoreAsync(InvokedContext context, CancellationToken cancellationToken = default)
    {
        if (context.InvokeException is not null)
        {
            return default;
        }

        // Since we are receiving all messages that were contributed earlier, including those from chat history, we need to filter out the messages that came from chat history
        // so that we don't store message we already have in storage.
        var filteredRequestMessages = context.RequestMessages.Where(m => m.GetAgentRequestMessageSourceType() != AgentRequestMessageSourceType.ChatHistory);

        var state = this._sessionState.GetOrInitializeState(context.Session);

        // Add both request and response messages to the state.
        var allNewMessages = filteredRequestMessages.Concat(context.ResponseMessages ?? []);
        state.Messages.AddRange(allNewMessages);

        this._sessionState.SaveState(context.Session, state);

        return default;
    }

    public sealed class State
    {
        [JsonPropertyName("messages")]
        public List<ChatMessage> Messages { get; set; } = [];
    }
}
```

:::zone-end
:::zone pivot="programming-language-python"
- In Python, only one history provider should use `load_messages=True`.

```python
from agent_framework.openai import OpenAIChatClient

history = DatabaseHistoryProvider(db_client)
agent = OpenAIChatClient().as_agent(
    name="StorageAgent",
    instructions="You are a helpful assistant.",
    context_providers=[history],
)

session = agent.create_session()
await agent.run("Store this conversation.", session=session)
```

:::zone-end

:::zone pivot="programming-language-go"

In Go, implement `agent.HistoryProvider` when you want database, Redis, blob, or file-backed history. The default helper created by `agent.NewHistoryProvider` loads prior messages in `Provide` and persists new request/response messages in `Store`. Keep any storage keys in the session so the provider instance can be reused across sessions.

```go
import (
    "context"
    "fmt"
    "time"

    "github.com/microsoft/agent-framework-go/agent"
    "github.com/microsoft/agent-framework-go/message"
)

type MessageStore interface {
    LoadMessages(context.Context, string) ([]*message.Message, error)
    AppendMessages(context.Context, string, []*message.Message) error
}

func NewDatabaseHistoryProvider(store MessageStore) agent.HistoryProvider {
    const stateKey = "database_history.key"

    historyKey := func(session *agent.Session) string {
        var key string
        if ok, _ := session.Get(stateKey, &key); ok && key != "" {
            return key
        }

        key = fmt.Sprintf("history-%d", time.Now().UnixNano())
        session.Set(stateKey, key)
        return key
    }

    return agent.NewHistoryProvider(agent.HistoryProviderConfig{
        SourceID: "database_history",
        Provide: func(ctx context.Context, invoking agent.InvokingContext) ([]*message.Message, error) {
            session, _ := agent.GetOption(invoking.Options, agent.WithSession)
            if session == nil {
                return nil, nil
            }

            return store.LoadMessages(ctx, historyKey(session))
        },
        Store: func(ctx context.Context, invoked agent.InvokedContext) error {
            session, _ := agent.GetOption(invoked.Options, agent.WithSession)
            if session == nil {
                return nil
            }

            allMessages := make([]*message.Message, 0, len(invoked.RequestMessages)+len(invoked.ResponseMessages))
            allMessages = append(allMessages, invoked.RequestMessages...)
            allMessages = append(allMessages, invoked.ResponseMessages...)

            return store.AppendMessages(ctx, historyKey(session), allMessages)
        },
    })
}
```

Attach the custom provider to the agent:

```go
a := foundryprovider.NewAgent(endpoint, token, foundryprovider.ModelDeployment(model), foundryprovider.AgentConfig{
    Instructions: "You are a helpful assistant.",
    Config: agent.Config{
        Name:            "StorageAgent",
        HistoryProvider: NewDatabaseHistoryProvider(store),
    },
})
```

Do not combine a configured local `HistoryProvider` with a service-managed session. Use either local history storage or the provider's remote conversation state for a given session.

:::zone-end

## Persisting sessions across restarts

Persist the full session object, not only message text.

:::zone pivot="programming-language-csharp"

```csharp
JsonElement serialized = agent.SerializeSession(session);
// Store serialized payload in durable storage.
AgentSession resumed = await agent.DeserializeSessionAsync(serialized);
```

:::zone-end

:::zone pivot="programming-language-python"

```python
serialized = session.to_dict()
# Store serialized payload in durable storage.
resumed = AgentSession.from_dict(serialized)
```

:::zone-end

:::zone pivot="programming-language-go"

Sessions can be persisted through JSON serialization. Store the entire `agent.Session`, not only message text or a history key.

```go
data, err := json.Marshal(session)
if err != nil {
    panic(err)
}
if err := os.WriteFile("session.json", data, 0o644); err != nil {
    panic(err)
}

loaded, err := os.ReadFile("session.json")
if err != nil {
    panic(err)
}

var resumed agent.Session
if err := json.Unmarshal(loaded, &resumed); err != nil {
    panic(err)
}

_, err = a.RunText(ctx, "Continue this conversation.", agent.WithSession(&resumed)).Collect()
```

For database-backed storage, serialize the session to `[]byte` and store it with your preferred backend:

```go
data, _ := json.Marshal(session)
db.Set(sessionID, data)

data, _ := db.Get(sessionID)
var resumed agent.Session
_ = json.Unmarshal(data, &resumed)
```

> [!TIP]
> See the [third-party session storage sample](https://github.com/microsoft/agent-framework-go/blob/main/examples/02-agents/agents/step07_3rdparty_session_storage/main.go) for a complete example.

:::zone-end

> [!IMPORTANT]
> Treat `AgentSession` as an opaque state object and restore it with the same agent/provider configuration that created it. Store serialized sessions and any service-side session IDs as trusted application state. In hosted or multi-tenant apps, bind each stored session to the authenticated user or tenant before allowing it to resume.

:::zone pivot="programming-language-python"
> [!TIP]
> Use an additional audit/eval history provider (`load_messages=False`, `store_context_messages=True`) to capture enriched context plus input/output without affecting primary history loading.
:::zone-end

## Next steps

> [!div class="nextstepaction"]
> [Compaction](./compaction.md)
