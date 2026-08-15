---
title: Microsoft Foundry
description: Use Microsoft Foundry for hosted file-search RAG and managed semantic memory.
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 07/30/2026
ms.service: agent-framework
---

<!--
  Language parity table - keep in sync when adding/removing sections.

  | Section                  | C# | Python | Go | Notes                  |
  |--------------------------|:--:|:------:|:--:|------------------------|
  | Pattern comparison       | ✅ |   ✅   | ✅ | Shared                 |
  | File-search RAG tool     | ✅ |   ✅   | ❌ | Hosted tool, not a context provider |
  | Managed semantic memory  | ✅ |   ✅   | ❌ |                        |
  | Go availability          | ✅ |   ✅   | ✅ | Go zones are status only |
-->

# Microsoft Foundry

Microsoft Foundry supports two distinct context patterns. Both use Foundry-managed resources, but they attach to an agent differently and solve different problems.

| Pattern | Agent Framework mechanism | Behavior |
|---|---|---|
| File-search RAG | Provider-hosted file-search tool | Searches files and vector stores that your application explicitly uploads and manages in a Foundry project. |
| Managed semantic memory | `FoundryMemoryProvider` context provider | Extracts facts and summaries from conversations, stores them by scope, and retrieves relevant memories in later runs. |

For model inference and service-managed Foundry agents, see [Microsoft Foundry model provider](../model-providers/microsoft-foundry.md) and [Microsoft Foundry Agent Service](../agent-services/foundry.md).

## Use file-search RAG

Use this pattern when Foundry should own document ingestion and vector-store lifecycle for a curated knowledge base. File search is a hosted tool rather than a context provider; see the generic [file search](../../../agents/tools/file-search.md) guidance for tool behavior. Use [Azure AI Search](azure-ai-search.md) when the application's source of truth is an Azure AI Search index.

:::zone pivot="programming-language-csharp"

### Create a Foundry vector store and agent

Upload a knowledge-base file, create a vector store, attach `FileSearchTool`, and create a versioned `FoundryAgent`.

:::code language="csharp" source="~/../agent-framework-code/dotnet/samples/02-agents/AgentWithRAG/AgentWithRAG_Step04_FoundryServiceRAG/Program.cs" range="16-71":::

Reuse persistent vector stores for production knowledge bases instead of creating them for every process run.

:::zone-end

:::zone pivot="programming-language-python"

### Install the package

```bash
pip install agent-framework-foundry --pre
```

Create files and a vector store through the Foundry project OpenAI client, then pass the resulting file-search tool to the agent.

:::code language="python" source="~/../agent-framework-code/python/samples/02-agents/providers/foundry/foundry_chat_client_with_file_search.py" range="30-77":::

:::zone-end

:::zone pivot="programming-language-go"

> [!NOTE]
> Foundry file-search integration isn't currently documented for Agent Framework Go. See the [Agent Framework Go repository](https://github.com/microsoft/agent-framework-go) for the latest hosted-tool support.

:::zone-end

## Add managed semantic memory

Use `FoundryMemoryProvider` when an agent should recall durable user or application context across sessions. Foundry memory stores extracted facts and summaries separately from the full conversation transcript.

:::zone pivot="programming-language-csharp"

### Install the package

```bash
dotnet add package Microsoft.Agents.AI.Foundry --prerelease
```

Create `FoundryMemoryProvider` with a stable scope, ensure the memory store exists, and wait for asynchronous updates before relying on newly extracted memories.

:::code language="csharp" source="~/../agent-framework-code/dotnet/samples/02-agents/AgentWithMemory/AgentWithMemory_Step04_MemoryUsingFoundry/Program.cs" range="21-84":::

:::zone-end

:::zone pivot="programming-language-python"

### Install the package

```bash
pip install agent-framework-foundry --pre
```

Create the memory store through `AIProjectClient`, then attach `FoundryMemoryProvider` to the agent.

:::code language="python" source="~/../agent-framework-code/python/samples/02-agents/context_providers/azure_ai_foundry_memory.py" range="42-137":::

The sample disables service-side and local transcript loading so the later response demonstrates semantic memory rather than chat-history replay.

:::zone-end

:::zone pivot="programming-language-go"

> [!NOTE]
> Microsoft Foundry memory integration isn't currently available for Agent Framework Go. See the [Agent Framework Go repository](https://github.com/microsoft/agent-framework-go) for the latest status.

:::zone-end

## Production considerations

- Reuse persistent vector stores for production knowledge bases.
- Use application-owned stable memory scope identifiers and authorize access before selecting a scope.
- Wait for asynchronous extraction when a subsequent operation depends on newly written memory.
- Keep exact transcripts in a history provider when you need complete conversation records.
- Configure retention, region, and model deployments to match your compliance requirements.

## Next steps

> [!div class="nextstepaction"]
> [Neo4j](neo4j.md)

**Go deeper:**

- [Context provider concepts](../../../concepts/agents/conversations/context-providers.md)
- [Microsoft Foundry model provider](../model-providers/microsoft-foundry.md)
