---
title: Redis
description: Use Redis for Agent Framework RAG, searchable memory, and conversation history.
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 07/30/2026
ms.service: agent-framework
---

<!--
  Language parity table - keep in sync when adding/removing sections.

  | Section              | C# | Python | Go | Notes                   |
  |----------------------|:--:|:------:|:--:|:-------------------------|
  | Redis RAG            | ✅ |   ❌   | ❌ | .NET uses TextSearchProvider |
  | Searchable memory    | ❌ |   ✅   | ❌ | Python Redis package     |
  | Conversation history | ❌ |   ✅   | ❌ | Python Redis package     |
  | Availability         | ✅ |   ✅   | ✅ | Go is status only        |
  | Next steps           | ✅ |   ✅   | ✅ | Language-specific        |
-->

# Redis

Redis supports different context patterns across SDKs. In .NET, connect Redis-backed search to the generic `TextSearchProvider` for RAG. The Agent Framework Redis package provides searchable memory and conversation-history providers for Python.

| Pattern | API | SDK | Behavior |
|---|---|---|---|
| RAG | `TextSearchProvider` with a Redis search adapter | .NET | Retrieves relevant Redis content before invocation or through an on-demand search tool. |
| Searchable memory | `RedisContextProvider` | Python | Extracts conversational details and retrieves relevant context with full-text or hybrid vector search. |
| Conversation history | `RedisHistoryProvider` | Python | Persists and reloads the exact message transcript for a session. |

:::zone pivot="programming-language-csharp"

## Add RAG with `TextSearchProvider`

Use the provider-independent [`TextSearchProvider`](../../../agents/rag.md#using-textsearchprovider) pattern for .NET. Implement its search adapter with the Redis client or vector-store connector selected by your application, map the Redis results to `TextSearchProvider.TextSearchResult`, and attach the provider through `AIContextProviders`.

This approach supports Redis-backed RAG without requiring a Redis-specific Agent Framework context-provider package.

:::zone-end

:::zone pivot="programming-language-python"

## Install the package

```bash
pip install agent-framework-redis --pre
```

## Add searchable memory

Use this pattern when an agent should recall selected relevant information rather than replay every previous message.

### Prerequisites

- A Redis deployment with RediSearch support, such as Redis Stack or a compatible managed service.
- A Microsoft Foundry project and model deployment for the sample agent.
- An embedding provider when you enable hybrid vector search.

### Configure searchable memory

Use `application_id`, `agent_id`, and `user_id` to partition memories. Add a Redis vectorizer and vector-field settings when you want hybrid retrieval.

:::code language="python" source="~/../agent-framework-code/python/samples/02-agents/context_providers/redis/redis_basics.py" range="121-148":::

### Attach memory to an agent

Add the provider to `context_providers`. The provider stores conversational details after a run and surfaces relevant context before later runs.

:::code language="python" source="~/../agent-framework-code/python/samples/02-agents/context_providers/redis/redis_basics.py" range="207-230":::

## Persist conversation history

Use this pattern when a session must recover its complete transcript after an application restart or on another instance.

### Prerequisites

- A Redis deployment reachable through `REDIS_URL`.
- TLS and authenticated Redis users for production deployments.

Attach `RedisHistoryProvider` through `context_providers`. The provider stores messages for the session and can limit the retained message count.

:::code language="python" source="~/../agent-framework-code/python/samples/02-agents/conversations/redis_history_provider.py" range="28-60":::

Use a stable session ID and persist the serialized `AgentSession` in trusted application storage when clients must resume the same logical conversation after a process restart.

:::zone-end

:::zone pivot="programming-language-go"

> [!NOTE]
> Redis context-provider integration isn't currently documented for Agent Framework Go. See the [Agent Framework Go repository](https://github.com/microsoft/agent-framework-go) for the latest status.

:::zone-end

## Production considerations

- Derive tenant, search, memory, and session scopes from authenticated application identity, not model output.
- Use TLS, Redis authentication, and network isolation.
- Use separate key prefixes or deployments where tenant isolation requires it.
- Configure persistence, backups, retention, and eviction for the required durability.
- Treat retrieved memory as untrusted input and mitigate indirect prompt injection.
- Redact sensitive content before persisting messages or indexing searchable content.

## Next steps

:::zone pivot="programming-language-csharp"

> [!div class="nextstepaction"]
> [Use RAG with `TextSearchProvider`](../../../agents/rag.md#using-textsearchprovider)

:::zone-end

:::zone pivot="programming-language-python"

> [!div class="nextstepaction"]
> [Mem0](mem0.md)

:::zone-end

:::zone pivot="programming-language-go"

> [!div class="nextstepaction"]
> [Learn how context providers work](../../../concepts/agents/conversations/context-providers.md)

:::zone-end

**Go deeper:**

- [RAG](../../../agents/rag.md)
- [Context provider concepts](../../../concepts/agents/conversations/context-providers.md)
- [Conversation storage](../../../concepts/agents/conversations/storage.md)
