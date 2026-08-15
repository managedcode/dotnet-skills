---
title: Valkey
description: Persist Agent Framework .NET conversation history with Valkey.
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 07/28/2026
ms.service: agent-framework
---

# Valkey

`ValkeyChatHistoryProvider` persists .NET agent conversation history in Valkey lists. It works with Valkey and compatible Redis OSS servers without requiring a search module.

This integration uses the conversation-storage pattern: it reloads exact messages rather than extracting or retrieving semantic memories.

This integration stores the full transcript; it doesn't extract semantic memories or provide vector retrieval.

## Install the packages

```bash
dotnet add package Microsoft.Agents.AI.Valkey --prerelease
dotnet add package Valkey.Glide
```

## Configure persistent history

Create the Valkey connection, choose a conversation key in the state initializer, and attach the provider through `ChatHistoryProvider`.

:::code language="csharp" source="~/../agent-framework-code/dotnet/samples/02-agents/AgentWithMemory/AgentWithMemory_Step03_MemoryUsingValkey/Program.cs" range="18-48":::

`KeyPrefix` separates application data, and `MaxMessages` bounds the retained transcript. Use an application-owned stable conversation ID when history must be resumed after a restart.

## Production considerations

- Use encrypted connections, authenticated users, and network isolation.
- Define persistence and eviction policies that match your durability requirements.
- Store conversation identifiers in trusted server-side state and verify ownership before loading history.
- Use separate key prefixes or deployments when tenant isolation requires it.

## Next steps

> [!div class="nextstepaction"]
> [Conversation storage](../../../concepts/agents/conversations/storage.md)
