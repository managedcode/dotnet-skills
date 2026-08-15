---
title: Microsoft Azure integrations
description: Find Agent Framework guidance for Azure OpenAI, Azure AI Search, Azure Cosmos DB, Azure Content Understanding, Microsoft Purview, Azure Functions, and Azure Monitor.
author: eavanvalkenburg
ms.topic: overview
ms.author: edvan
ms.date: 07/29/2026
ms.service: agent-framework
---

# Microsoft Azure integrations

Microsoft Azure services extend Agent Framework with model inference, retrieval, memory, conversation storage, content processing, observability, and durable hosting. Use this page to find the dedicated guide for each Azure service.

## Choose a Microsoft Azure integration

| Scenario | Guide |
|---|---|
| Use Azure OpenAI models through the Responses or Chat Completions API. | [Azure OpenAI](../by-component/model-providers/azure-openai.md) |
| Retrieve grounding data from an Azure AI Search index. | [Azure AI Search](../by-component/context-providers/azure-ai-search.md) |
| Add extracted, searchable long-term memory backed by Azure Cosmos DB. | [Azure Cosmos DB](../by-component/context-providers/azure-cosmos.md#add-long-term-semantic-memory) |
| Persist complete conversation history in Azure Cosmos DB. | [Azure Cosmos DB](../by-component/context-providers/azure-cosmos.md#persist-conversation-history) |
| Analyze documents, images, audio, and video before sending content to an agent. | [Azure Content Understanding](../by-component/context-providers/azure-content-understanding.md) |
| Apply Microsoft Purview policy checks through Agent Framework middleware. | [Microsoft Purview](../by-component/middleware/purview.md) |
| Run durable agents and workflows with Azure Functions and the Durable Extension. | [Azure Functions and Durable Extension](../../hosting/azure-functions.md) |
| Export Agent Framework traces, metrics, and logs to Azure Monitor. | [Agent observability](../../agents/observability.md) |
| Use Microsoft Foundry projects, managed agents, hosted tools, and related services. | [Microsoft Foundry integrations](microsoft-foundry.md) |

## Next steps

> [!div class="nextstepaction"]
> [Use Azure OpenAI](../by-component/model-providers/azure-openai.md)
