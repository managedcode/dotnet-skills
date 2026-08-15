---
title: Context provider integrations
description: Browse Agent Framework context provider integrations for storage, memory, RAG, pre-processing, CodeAct, and other context patterns.
author: eavanvalkenburg
ms.topic: overview
ms.author: edvan
ms.date: 07/30/2026
ms.service: agent-framework
---

# Context provider integrations

This component groups external integrations that supply, transform, retrieve, or persist context around an agent invocation. Most attach through the context-provider abstraction. A provider page can also include a provider-hosted tool when that is the external system's implementation of the same context pattern; those pages identify the mechanism explicitly.

Organizing by the framework integration surface keeps one provider page for its related storage, memory, RAG, pre-processing, or CodeAct patterns instead of duplicating the provider across feature categories. Use the **Common patterns** column below to browse by the outcome you need.

The documentation distinguishes several common patterns:

- **Conversation storage** reloads and persists the exact message transcript.
- **Memory** extracts and recalls selected durable knowledge from prior interactions.
- **RAG** retrieves relevant information from an external knowledge source.
- **Pre-processing** transforms incoming files or other content before model invocation.
- **CodeAct** contributes a code-execution tool and manages the execution environment.

These patterns describe common uses, not hard limits. A provider can combine multiple patterns or implement a different behavior entirely.

For the lifecycle, built-in abstractions, and custom-provider implementation guidance, see [Context provider concepts](../../../concepts/agents/conversations/context-providers.md).

## Available integrations

| Provider | Common patterns | C# | Python | Go |
|---|---|:---:|:---:|:---:|
| [Azure AI Search](azure-ai-search.md) | RAG | ✅ | ✅ | ❌ |
| [Azure Content Understanding](azure-content-understanding.md) | Pre-processing | ❌ | ✅ | ❌ |
| [Azure Cosmos DB](azure-cosmos.md) | Conversation storage; memory | ✅ | ✅ | ❌ |
| [Hyperlight](hyperlight.md) | CodeAct | ✅ | ✅ | ❌ |
| [Local (.NET)](local.md) | CodeAct | ✅ | ❌ | ❌ |
| [Mem0](mem0.md) | Memory | ❌ | ✅ | ❌ |
| [Microsoft Foundry](microsoft-foundry.md) | RAG; memory | ✅ | ✅ | ❌ |
| [Monty](monty.md) | CodeAct | ❌ | ✅ | ❌ |
| [Neo4j](neo4j.md) | RAG; memory | ✅ | ✅ | ❌ |
| [Redis](redis.md) | RAG; conversation storage; memory | ✅ | ✅ | ❌ |
| [Valkey](valkey.md) | Conversation storage | ✅ | ❌ | ❌ |

## Next steps

> [!div class="nextstepaction"]
> [Learn how context providers work](../../../concepts/agents/conversations/context-providers.md)
