# Threads, Chat History, and Memory

## `AgentThread` Is The Real Conversation State

`AIAgent` instances are reusable and effectively stateless. The durable, resumable part of the interaction lives in `AgentThread`.

```csharp
AgentThread thread = await agent.GetNewThreadAsync();

AgentResponse first = await agent.RunAsync("My name is Alice.", thread);
AgentResponse second = await agent.RunAsync("What is my name?", thread);
```

If you run without a thread, the framework creates a throwaway thread for that single invocation.

## Thread Lifecycle

1. Create the thread from the agent with `GetNewThreadAsync()`.
2. Reuse that thread for follow-up runs.
3. Serialize the entire thread for persistence.
4. Resume the thread with the same agent configuration.
5. Clean up any provider-owned remote thread resources through the provider SDK if required.

## Compatibility Rules

- Treat `AgentThread` as opaque provider-owned state.
- Do not assume a thread created by one agent can safely be reused with another.
- Do not assume that two agents backed by similar models share the same thread semantics.
- If you change provider, mode, tool setup, or history store configuration, assume old serialized threads are incompatible until proven otherwise.

This is especially important for service-backed thread IDs. A response-chain ID from one backend cannot be replayed against another backend.

## Conversation Storage Models

| Model | Typical Backends | What The Serialized Thread Contains | Your Responsibility |
| --- | --- | --- | --- |
| In-memory history | Chat Completions-style agents | Full message list plus store state | Limit prompt growth and persist serialized thread |
| Service-backed history | Foundry Agents, Assistants, many Responses modes | Remote conversation or response-chain ID | Track remote lifecycle and provider cleanup |
| Third-party message store | Custom `ChatMessageStore` over non-service-backed agents | Store-specific state and identifiers | Implement retrieval, storage, and reduction |

## In-Memory History

With in-memory history:

- the thread holds the actual chat messages
- each new call sends the relevant history back to the model
- you can inspect or mutate the messages if you knowingly rely on in-memory storage

This is the common path for Chat Completions-style agents and many custom `IChatClient` integrations.

## Reducers And Prompt Growth

The built-in `InMemoryChatMessageStore` can use a reducer to manage context size.

```csharp
AIAgent agent = openAIClient.GetChatClient(modelName).AsAIAgent(new ChatClientAgentOptions
{
    Name = "Joker",
    ChatOptions = new() { Instructions = "You are good at telling jokes." },
    ChatMessageStoreFactory = (ctx, ct) => new ValueTask<ChatMessageStore>(
        new InMemoryChatMessageStore(
            new MessageCountingChatReducer(12),
            ctx.SerializedState,
            ctx.JsonSerializerOptions,
            InMemoryChatMessageStore.ChatReducerTriggerEvent.AfterMessageAdded))
});
```

Use reducers when:

- the service does not own history
- the conversation can grow indefinitely
- the model context window matters

Remember that reducers apply only to the built-in in-memory store. If the provider owns history, provider rules win.

## Custom `ChatMessageStore`

Use a custom store when:

- you need persistent chat history outside process memory
- the provider does not already own history
- you need repo-specific control over storage, partition keys, or retention

Implementation rules:

- every thread needs a unique store key
- the store must serialize enough state to be reopened later
- `InvokingAsync` should return the messages to send to the model
- `InvokedAsync` should persist newly produced messages
- the store should police history size if prompt growth matters

If the provider already manages thread history, your custom store will be ignored.

## Long-Term Memory And Context Providers

Use `AIContextProvider` for memory that is more than raw chat history.

Typical uses:

- user profile and preferences
- RAG or retrieval augmentation
- memory extraction after a run
- dynamic instruction injection
- request-scoped auxiliary tools

The main hooks are:

- `InvokingAsync` to inject context before the run
- `InvokedAsync` to inspect the completed interaction and extract memory afterward

This is the correct extension point for semantic memory, not ad hoc mutation of thread internals.

## Serialize The Entire Thread

Always persist the whole thread, not only the visible message text.

```csharp
JsonElement serialized = thread.Serialize();
AgentThread resumed = await agent.DeserializeThreadAsync(serialized);
```

Why this matters:

- service-backed threads may only contain remote IDs
- custom stores may attach their own serialized state
- context providers may attach memory state
- future agent runs may depend on state that is not visible in plain messages

## Cleanup Responsibilities

For some providers, creating a thread or response chain creates remote state in the service. Agent Framework does not centralize deletion because not all providers support deletion and not all threads are remote resources.

If you require cleanup:

- keep track of provider-specific remote identifiers
- delete remote threads through the provider SDK
- do not assume `AgentThread` itself exposes universal cleanup APIs

## Practical Rules

- Create threads from the agent that will actually use them.
- Store serialized threads in your own persistence layer after important turns.
- Resume with the same provider mode and tool configuration.
- Keep history reduction explicit when the provider does not own history.
- Use context providers for memory augmentation, not hidden global state.

## Common Failure Modes

- Reusing one serialized thread with a differently configured agent.
- Storing only visible chat messages and losing provider-specific thread state.
- Assuming service-backed history can be summarized or trimmed locally without provider involvement.
- Using a custom message store and forgetting to serialize its own keying state.
- Treating context providers as if they were a replacement for thread persistence.

## Source Pages

- `references/official-docs/user-guide/agents/multi-turn-conversation.md`
- `references/official-docs/user-guide/agents/agent-memory.md`
- `references/official-docs/tutorials/agents/persisted-conversation.md`
- `references/official-docs/tutorials/agents/third-party-chat-history-storage.md`
- `references/official-docs/tutorials/agents/memory.md`
