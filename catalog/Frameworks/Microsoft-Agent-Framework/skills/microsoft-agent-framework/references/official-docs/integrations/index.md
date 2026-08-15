---
title: Agent Framework Integrations
description: Agent Framework Integrations
author: westey-m
ms.topic: article
ms.author: westey
ms.date: 07/28/2026
ms.service: agent-framework
zone_pivot_groups: programming-languages
---

<!--
  Language parity table - keep in sync when adding/removing sections.

  | Section                     | C# | Python | Go | Notes                  |
  |-----------------------------|:--:|:------:|:--:|------------------------|
  | Provider/component navigation | ✅ |   ✅   | ✅ | Shared                 |
  | Vector stores                | ✅ |   ✅   | ✅ | Go zone is status only |
-->

# Agent Framework Integrations

Microsoft Agent Framework has integrations with many different services, tools and protocols.

## Browse by provider

| Provider | Integration areas |
|---|---|
| [Microsoft Foundry](./by-provider/microsoft-foundry.md) | Models, managed agents, tools, RAG, memory, evaluation, observability, local models, and hosted agents |
| [Microsoft Azure](./by-provider/microsoft-azure.md) | Azure OpenAI, Azure AI Search, Azure Cosmos DB, Azure Content Understanding, Microsoft Purview, Azure Monitor, and Azure Functions |
| [OpenAI](./by-provider/openai.md) | Model inference, hosted tools, ChatKit, and OpenAI-compatible endpoints |
| [Anthropic](./by-provider/anthropic.md) | Claude models, the Claude Agent SDK, Foundry, Bedrock, and Vertex AI |
| [Amazon Web Services](./by-provider/amazon-web-services.md) | Amazon Bedrock and Anthropic Claude on Bedrock |
| [Google](./by-provider/google.md) | Google Gemini and Anthropic Claude on Vertex AI |
| [Ollama](./by-provider/ollama.md) | Local model inference through native and OpenAI-compatible clients |
| [Mistral](./by-provider/mistral.md) | Mistral text embeddings |

See [all provider ecosystems](./by-provider/index.md).

## Browse by component

- [Model providers](./by-component/model-providers/index.md)
- [Agent services](./by-component/agent-services/index.md)
- [Tools](./by-component/tools/index.md)
- [Context providers](./by-component/context-providers/index.md)
- [Middleware](./by-component/middleware/purview.md)
- [Evaluation](./by-component/evaluation/microsoft-foundry.md)
- UI: [AG-UI](./by-component/ui/ag-ui/index.md), [ChatKit](./by-component/ui/chatkit.md), and [DevUI](./by-component/ui/devui/index.md)
- [All component categories](./by-component/index.md)
- [Context provider concepts](../concepts/agents/conversations/context-providers.md)

## UI Framework integrations

| UI Framework                                                       | Release Status  |
| ------------------------------------------------------------------ | --------------- |
| [AG-UI](./by-component/ui/ag-ui/index.md)                                          | Preview         |
| [ChatKit](./by-component/ui/chatkit.md) | Preview |
| [DevUI](./by-component/ui/devui/index.md)             | Preview         |

## Middleware integrations

- [Microsoft Purview](./by-component/middleware/purview.md)

## Evaluation integrations

- [Microsoft Foundry](./by-component/evaluation/microsoft-foundry.md)

## Vector Stores

Microsoft Agent Framework supports integration with many different vector stores. These can be useful for doing Retrieval Augmented Generation (RAG) or storage of memories.

::: zone pivot="programming-language-csharp"

To integrate with vector stores, we rely on the 📦 [Microsoft.Extensions.VectorData.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.VectorData.Abstractions) package which provides a unified layer of abstractions for interacting with vector stores in .NET.
These abstractions let you write simple, high-level code against a single API, and swap out the underlying vector store with minimal changes to your application. Where Agent Framework components rely on a vector store, they use these abstractions to allow you to choose your preferred implementation.

> [!TIP]
> See the [Vector databases for .NET AI apps](/dotnet/ai/vector-stores/overview) documentation for more information on how to ingest data into a vector store, generate embeddings, and do vector or hybrid searches.

### Vector Store Abstraction Implementations

| Implementation                                                                                                               | C#                         | Uses officially supported SDK | Maintainer / Vendor |
| ---------------------------------------------------------------------------------------------------------------------------- | :------------------------: | :---------------------------: | :-----------------: |
| [Azure AI Search](/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/azure-ai-search-connector)  | ✅                         | ✅                           | Microsoft           |
| [Cosmos DB MongoDB (vCore)](/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/azure-cosmosdb-mongodb-connector) | ✅         | ✅                           | Microsoft           |
| [Cosmos DB No SQL](/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/azure-cosmosdb-nosql-connector) | ✅                    | ✅                           | Microsoft           |
| [Couchbase](/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/couchbase-connector)              | ✅                         | ✅                           | Couchbase           |
| [Elasticsearch](/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/elasticsearch-connector)      | ✅                         | ✅                           | Elastic             |
| [In-Memory](/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/inmemory-connector)               | ✅                         | N/A                           | Microsoft           |
| [MongoDB](/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/mongodb-connector)                  | ✅                         | ✅                           | Microsoft           |
| [Neon Serverless Postgres](https://neon.com) | Use [Postgres Connector](/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/postgres-connector) | ✅           | Microsoft           |
| [Oracle](/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/oracle-connector)                    | ✅                         | ✅                           | Oracle              |
| [Pinecone](/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/pinecone-connector)                | ✅                         | ❌                           | Microsoft           |
| [Postgres](/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/postgres-connector)                | ✅                         | ✅                           | Microsoft           |
| [Qdrant](/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/qdrant-connector)                    | ✅                         | ✅                           | Microsoft           |
| [Redis](/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/redis-connector)                      | ✅                         | ✅                           | Microsoft           |
| [SQL Server](/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/sql-connector)                   | ✅                         | ✅                           | Microsoft           |
| [SQLite](/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/sqlite-connector)                    | ✅                         | ✅                           | Microsoft           |
| [Volatile (In-Memory)](/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/volatile-connector)    | Deprecated (use In-Memory) | N/A                           | Microsoft           |
| [Weaviate](/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/weaviate-connector)                | ✅                         | ✅                           | Microsoft           |

> [!IMPORTANT]
> The vector store abstraction implementations are built by a variety of sources. Not all connectors are maintained by Microsoft. When considering an implementation, be sure to evaluate quality, licensing, support, etc. to ensure they meet your requirements. Also make sure you review each provider's documentation for detailed version compatibility information.

> [!IMPORTANT]
> Some implementations are internally using Database SDKs that are not officially supported by Microsoft or by the Database provider. The *Uses Officially supported SDK* column lists which are using officially supported SDKs and which are not.

::: zone-end

::: zone pivot="programming-language-python"

Agent Framework supports using Semantic Kernel's VectorStore collections to provide vector storage capabilities to agents.
See [the vector store connectors documentation](/semantic-kernel/concepts/vector-store-connectors) to learn how to set up different vector store collections.
See [Creating a search tool from a VectorStore](../agents/rag.md#creating-a-search-tool-from-vectorstore) for more information on how to use these for RAG.

::: zone-end

::: zone pivot="programming-language-go"

> [!NOTE]
> Go support for this feature is coming soon. See the [Agent Framework Go repository](https://github.com/microsoft/agent-framework-go) for the latest status.

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Browse integrations by component](./by-component/index.md)
