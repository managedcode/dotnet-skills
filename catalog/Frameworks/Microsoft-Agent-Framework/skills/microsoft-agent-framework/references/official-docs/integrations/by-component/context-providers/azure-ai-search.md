---
title: Azure AI Search
description: Ground Agent Framework agents with documents retrieved from Azure AI Search.
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 07/28/2026
ms.service: agent-framework
---

<!--
  Language parity table - keep in sync when adding/removing sections.

  | Section            | C# | Python | Go | Notes                                  |
  |--------------------|:--:|:------:|:--:|----------------------------------------|
  | Search integration | ✅ |   ✅   | ❌ |                                        |
  | Semantic retrieval | ✅ |   ✅   | ❌ | Python has dedicated context provider  |
  | Agentic retrieval  | ❌ |   ✅   | ❌ | Azure AI Search Knowledge Bases        |
  | Go availability    | ✅ |   ✅   | ✅ | Go zone is status only                 |
-->

# Azure AI Search

Azure AI Search grounds Agent Framework agents with content from a search index. In Python, `AzureAISearchContextProvider` supports semantic and agentic retrieval. In .NET, connect an Azure AI Search client to `TextSearchProvider`.

This integration uses the RAG pattern: it retrieves relevant external content before model invocation without treating that content as conversational memory.

:::zone pivot="programming-language-csharp"

## Connect Azure AI Search to `TextSearchProvider`

Create a `SearchClient`, map search hits to `TextSearchProvider.TextSearchResult`, and attach the provider through `AIContextProviders`.

:::code language="csharp" source="~/../agent-framework-code/dotnet/samples/04-hosting/FoundryHostedAgents/responses/Hosted-AzureSearchRag/Program.cs" range="23-68,84-110":::

The sample hosts the resulting agent in Foundry, but the search adapter works with a regular `ChatClientAgent`.

:::zone-end

:::zone pivot="programming-language-python"

## Install the packages

```bash
pip install agent-framework-azure-ai-search agent-framework-foundry --pre
```

## Use semantic retrieval

Semantic mode performs search against an existing index and can combine keyword and vector retrieval.

:::code language="python" source="~/../agent-framework-code/python/samples/02-agents/context_providers/azure_ai_search/search_context_semantic.py" range="50-113":::

## Use agentic retrieval

Agentic mode uses an Azure AI Search Knowledge Base for query planning and multi-hop retrieval.

:::code language="python" source="~/../agent-framework-code/python/samples/02-agents/context_providers/azure_ai_search/search_context_agentic.py" range="64-146":::

Some agentic output and reasoning options require the preview `azure-search-documents` package.

:::zone-end

:::zone pivot="programming-language-go"

> [!NOTE]
> Azure AI Search doesn't currently have a dedicated Agent Framework Go integration. Implement retrieval as a custom tool or context provider, or see the [Agent Framework Go repository](https://github.com/microsoft/agent-framework-go) for the latest status.

:::zone-end

## Production considerations

- Prefer Microsoft Entra authentication or managed identity over search keys.
- Apply tenant-aware filters and index isolation.
- Treat retrieved content as untrusted input and mitigate indirect prompt injection.
- Preserve source metadata when the agent should cite documents.

## Next steps

> [!div class="nextstepaction"]
> [Microsoft Foundry](microsoft-foundry.md)
