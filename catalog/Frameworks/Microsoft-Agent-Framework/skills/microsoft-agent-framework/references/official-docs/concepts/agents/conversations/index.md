---
title: Conversations & Memory overview in Agent Framework
description: Learn the core AgentSession usage pattern and how to navigate sessions, context providers, and storage.
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 05/28/2026
ms.service: agent-framework
---

# Conversations & Memory overview

Use `AgentSession` to keep conversation context between invocations.

When a session uses service-managed storage, it can contain an opaque service-side session ID. OpenAI Responses and Conversations IDs are scoped to the backing API key or project by default; if a hosted agent uses the same key or project for multiple end users, store those IDs server-side and verify the authenticated user or tenant before resuming. For details, see [Session](./session.md).

## Core usage pattern

Most applications follow the same flow:

:::zone pivot="programming-language-csharp"

1. Create a session (`CreateSessionAsync()`)
2. Pass that session to each `RunAsync(...)`
3. Rehydrate from serialized state (`DeserializeSessionAsync(...)`)
4. Continue with a service conversation ID (varies by agent, e.g. `myChatClientAgent.CreateSessionAsync("existing-id")`)

:::zone-end

:::zone pivot="programming-language-python"

1. Create a session (`create_session()`)
2. Pass that session to each `run(...)`
3. Rehydrate by service conversation ID (`get_session(...)`) or from serialized state

:::zone-end

:::zone pivot="programming-language-go"

1. Create a session (`CreateSession(...)`)
2. Pass that session to each `RunText(...)` with `agent.WithSession(session)`
3. Rehydrate from serialized state with `json.Unmarshal(...)` into `agent.Session`

The Go `agent` package provides the core types for conversation state: `agent.Session` for key-value state tied to a conversation and `agent.ContextProvider` for context injection and persistence.

:::zone-end

:::zone pivot="programming-language-csharp"

```csharp
// Create and reuse a session
AgentSession session = await agent.CreateSessionAsync();

var first = await agent.RunAsync("My name is Alice.", session);
var second = await agent.RunAsync("What is my name?", session);

// Persist and restore later
var serialized = agent.SerializeSession(session);
AgentSession resumed = await agent.DeserializeSessionAsync(serialized);
```

:::zone-end

:::zone pivot="programming-language-python"

```python
# Create and reuse a session
session = agent.create_session()

first = await agent.run("My name is Alice.", session=session)
second = await agent.run("What is my name?", session=session)

# Rehydrate by service conversation ID when needed
service_session = agent.get_session(service_session_id="<service-conversation-id>")

# Persist and restore later
serialized = session.to_dict()
resumed = AgentSession.from_dict(serialized)
```

:::zone-end

:::zone pivot="programming-language-go"
```go
session, err := a.CreateSession(ctx)
if err != nil {
    panic(err)
}
```

### Use a session for multi-turn conversations

```go
resp, _ := a.RunText(ctx, "My name is Alice.", agent.WithSession(session)).Collect()
resp, _ = a.RunText(ctx, "What is my name?", agent.WithSession(session)).Collect()
```

### Persist sessions

Sessions can be serialized to JSON for storage and later resumed:

```go
data, err := json.Marshal(session)
if err != nil {
    panic(err)
}
// store data...

// later:
var resumed agent.Session
if err := json.Unmarshal(data, &resumed); err != nil {
    panic(err)
}

resp, err := a.RunText(ctx, "Continue from where we left off.", agent.WithSession(&resumed)).Collect()
```

:::zone-end
## Guide map

| Page | Focus |
|---|---|
| [Session](./session.md) | `AgentSession` structure and serialization |
| [Context Providers](./context-providers.md) | Built-in and custom context/history provider patterns |
| [Context Compaction](./compaction.md) | Efficiently manage conversation growth |
| [Storage](./storage.md) | Built-in storage modes and external persistence strategies |
| [Chat History Memory Provider](./chat-history-memory-provider.md) | Add chat history to agent context and persist new messages |

## Next steps

> [!div class="nextstepaction"]
> [Session](./session.md)
