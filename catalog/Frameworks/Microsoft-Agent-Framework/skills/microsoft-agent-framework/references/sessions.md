# Sessions, Chat History, and Memory

## `AgentSession` Is The Conversation State

`AIAgent` instances are reusable and should remain effectively stateless. The resumable interaction state lives in an `AgentSession` created by the agent that owns it.

```csharp
AgentSession session = await agent.CreateSessionAsync();

AgentResponse first = await agent.RunAsync("My name is Alice.", session);
AgentResponse second = await agent.RunAsync("What is my name?", session);
```

Concrete session implementations can add provider-specific state such as a remote conversation ID. The base session also exposes a `StateBag` for session-scoped application or provider state. Treat the whole object as opaque and use the owning agent's APIs to persist it.

## Lifecycle And Ownership

1. Create the session from the agent with `CreateSessionAsync()`.
2. Reuse that session for follow-up runs.
3. Serialize the session after important turns.
4. Restore it through the same compatible agent configuration.
5. Release provider-owned remote resources or background work when the provider requires it.

```csharp
var serialized = agent.SerializeSession(session);
AgentSession resumed = await agent.DeserializeSessionAsync(serialized);
```

Compatibility and authorization rules:

- Do not assume a session created by one agent, provider mode, tool set, or history configuration is reusable with another.
- Service-side identifiers such as `resp_*`, `conv_*`, A2A context IDs, or task IDs are opaque references, not user authorization tokens.
- In a multi-user service that shares a provider key or project, keep remote IDs in trusted server-side storage, map them from an application-owned session ID, and verify the authenticated user or tenant before resuming.
- Persist the entire serialized session rather than only visible messages; the session can contain remote IDs, provider state, context-provider state, approvals, todos, or background-task state.

## Local Versus Service-Managed History

| Model | Typical shape | Application responsibility |
| --- | --- | --- |
| Local history | `InMemoryChatHistoryProvider` stores messages in session state | Persist the session, bound prompt growth, and choose retention |
| Service-managed history | A concrete session stores a remote conversation or response ID | Protect ownership mapping, manage remote lifecycle, and follow provider retention |
| Custom history provider | Application storage loads and saves messages around each run | Own partitioning, concurrency, reduction, retention, and failure behavior |

For local history, retrieve or configure the provider through the agent rather than mutating session internals:

```csharp
var provider = agent.GetService<InMemoryChatHistoryProvider>();
List<ChatMessage>? messages = provider?.GetMessages(session);
```

Use a reducer when local history can exceed the model context window:

```csharp
AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    Name = "Assistant",
    ChatOptions = new() { Instructions = "Be concise." },
    ChatHistoryProvider = new InMemoryChatHistoryProvider(
        new InMemoryChatHistoryProviderOptions
        {
            ChatReducer = new MessageCountingChatReducer(20)
        })
});
```

Reducers apply to the configured local history provider. A service-managed conversation follows the provider's reduction and retention rules.

## Existing Service Conversations

Only construct a session from an existing remote identifier after resolving that identifier from trusted application storage and checking ownership.

```csharp
AgentSession chatSession = await chatClientAgent.CreateSessionAsync(conversationId);
AgentSession a2aSession = await a2aAgent.CreateSessionAsync(contextId, taskId);
```

Do not echo raw service IDs to an untrusted client and accept them back as sufficient proof that the caller owns the conversation.

## Harness Sessions

Harness uses the same `AgentSession` lifecycle. Reuse one session across turns so history, todos, operating mode, file memory, surfaced approvals, and background-task state remain connected.

```csharp
HarnessAgent agent = chatClient.AsHarnessAgent();
AgentSession session = await agent.CreateSessionAsync();

await agent.RunAsync("Plan the migration.", session);
await agent.RunAsync("Continue with the next step.", session);

var serialized = await agent.SerializeSessionAsync(session);
AgentSession resumed = await agent.DeserializeSessionAsync(serialized);
```

Harness persists local history after each service call inside a tool loop. Configure `HarnessAgentOptions.ChatHistoryProvider` when the default `InMemoryChatHistoryProvider` does not meet durability or retention requirements.

## Long-Term Memory And Context Providers

Use context or memory providers for data that is not raw chat history, such as a user profile, retrieved knowledge, dynamic instructions, or request-scoped auxiliary tools. Keep the source ID and session-state keys stable, and decide whether the provider loads messages, injects context, or extracts state after a run.

Memory does not replace session persistence. Persist session state after important turns and keep durable user knowledge in an application-owned store with explicit authorization and retention.

## Validation

- One authenticated user or tenant cannot resume another user's service-side conversation ID.
- Serialize/deserialize round trips preserve the selected provider's session and history state.
- Restored sessions use a compatible agent, provider mode, tools, and history-provider configuration.
- Local history has an explicit reducer or other growth bound.
- Harness approvals and background work remain bound to the session that surfaced them.
- Provider cleanup or `ReleaseSessionAsync` paths run when remote or background resources require release.

## Source Pages

- `references/official-docs/concepts/agents/conversations/session.md`
- `references/official-docs/concepts/agents/conversations/storage.md`
- `references/official-docs/concepts/agents/conversations/chat-history-memory-provider.md`
- `references/official-docs/concepts/agents/conversations/context-providers.md`
- `references/official-docs/get-started/multi-turn.md`
- `references/official-docs/get-started/memory.md`
- `references/official-docs/concepts/harness.md`
