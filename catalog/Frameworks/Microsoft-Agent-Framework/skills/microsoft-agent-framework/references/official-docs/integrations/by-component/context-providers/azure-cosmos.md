---
title: Azure Cosmos DB
description: Use Azure Cosmos DB for Agent Framework conversation history and long-term semantic memory.
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 07/28/2026
ms.service: agent-framework
---

<!--
  Language parity table - keep in sync when adding/removing sections.

  | Section                  | C# | Python | Go | Notes                                  |
  |--------------------------|:--:|:------:|:--:|----------------------------------------|
  | Pattern comparison       | ✅ |   ✅   | ✅ | Shared                                 |
  | Persistent chat history  | ✅ |   ✅   | ❌ |                                        |
  | Long-term memory         | ❌ |   ✅   | ❌ |                                        |
  | Go availability          | ✅ |   ✅   | ✅ | Go zones are status only               |
-->

# Azure Cosmos DB

Azure Cosmos DB supports two distinct context-provider patterns in Agent Framework. Choose the provider based on whether you need an exact transcript or extracted long-term knowledge.

| Pattern | Provider | Behavior |
|---|---|---|
| Conversation history | `CosmosChatHistoryProvider` (.NET) or `CosmosHistoryProvider` (Python) | Persists complete messages so a session can resume after a restart or on another application instance. |
| Long-term memory | `CosmosMemoryContextProvider` (Python) | Extracts facts, procedural knowledge, episodic memories, and summaries, then retrieves relevant memories for later runs. |

## Persist conversation history

:::zone pivot="programming-language-csharp"

### Install the packages

```bash
dotnet add package Microsoft.Agents.AI.CosmosNoSql --prerelease
dotnet add package Azure.Identity
```

### Configure Cosmos DB chat history

Use the managed-identity extension to attach `CosmosChatHistoryProvider` to `ChatClientAgentOptions`.

```csharp
using Azure.Identity;
using Microsoft.Agents.AI;

var options = new ChatClientAgentOptions
{
    ChatOptions = new() { Instructions = "You are a helpful assistant." }
}.WithCosmosDBChatHistoryProviderUsingManagedIdentity(
    accountEndpoint: Environment.GetEnvironmentVariable("AZURE_COSMOS_ENDPOINT")!,
    databaseId: Environment.GetEnvironmentVariable("AZURE_COSMOS_DATABASE_NAME")!,
    containerId: Environment.GetEnvironmentVariable("AZURE_COSMOS_CONTAINER_NAME")!,
    tokenCredential: new DefaultAzureCredential());

AIAgent agent = chatClient.AsAIAgent(options);
```

The default state initializer creates a conversation ID. Supply a `CosmosChatHistoryProvider.State` initializer when your application needs explicit conversation, tenant, and user routing. When tenant and user IDs are present, the provider uses a hierarchical partition key.

> [!WARNING]
> `DefaultAzureCredential` is convenient for development. In production, prefer a specific credential such as `ManagedIdentityCredential`.

:::zone-end

:::zone pivot="programming-language-python"

### Install the package

```bash
pip install agent-framework-azure-cosmos --pre
```

### Configure `CosmosHistoryProvider`

The Python provider accepts either an Azure credential or an account key and uses the `session_id` as the partition key.

:::code language="python" source="~/../agent-framework-code/python/samples/02-agents/conversations/cosmos_history_provider.py" range="56-87":::

Persist the serialized `AgentSession` in trusted application storage when clients need to recover the same session identifier later.

:::zone-end

:::zone pivot="programming-language-go"

> [!NOTE]
> Azure Cosmos DB history storage isn't currently available for Agent Framework Go. Implement a custom history provider or see the [Agent Framework Go repository](https://github.com/microsoft/agent-framework-go) for the latest status.

:::zone-end

## Add long-term semantic memory

:::zone pivot="programming-language-csharp"

> [!NOTE]
> The Azure Cosmos DB long-term memory provider is currently available for Python. Use the conversation-history provider above when a .NET application needs exact transcript persistence.

:::zone-end

:::zone pivot="programming-language-python"

### Prerequisites

- An Azure Cosmos DB account and database.
- A Microsoft Foundry project with chat and embedding model deployments.
- Azure identity access to both resources.

### Install the packages

```bash
pip install agent-framework-azure-cosmos-memory agent-framework-foundry --pre
```

### Configure the memory provider

The same Foundry project can supply the chat model, embeddings, and memory extraction model. Attach the provider through `context_providers`.

:::code language="python" source="~/../agent-framework-code/python/packages/azure-cosmos-memory/samples/basic_usage.py" range="41-82":::

A stable `user_id` keeps memory available across sessions and threads. Without one, the provider scopes memory to the current session ID.

### Memory processing

Memory extraction runs in the background after each turn. Use the provider as an async context manager or call `flush()` before shutdown so pending extraction completes before the clients close.

The provider also supports custom extraction prompts, processor cadence, confidence thresholds, memory types, and retrieval limits.

:::zone-end

:::zone pivot="programming-language-go"

> [!NOTE]
> Azure Cosmos DB long-term memory isn't currently available for Agent Framework Go. See the [Agent Framework Go repository](https://github.com/microsoft/agent-framework-go) for the latest status.

:::zone-end

## Production considerations

- Derive user, tenant, and session identifiers from authenticated application identity.
- Choose partition keys that distribute traffic while enforcing tenant isolation.
- Keep Cosmos DB and model resources in approved regions and apply least-privilege RBAC.
- Configure time-to-live, backup, retention, and deletion policies for both transcripts and extracted memories.
- Filter or redact sensitive content before persistence, and don't use extracted memories directly for authorization decisions.

## Next steps

> [!div class="nextstepaction"]
> [Browse context provider integrations](index.md)

**Go deeper:**

- [Context provider concepts](../../../concepts/agents/conversations/context-providers.md)
- [Conversation storage](../../../concepts/agents/conversations/storage.md)
