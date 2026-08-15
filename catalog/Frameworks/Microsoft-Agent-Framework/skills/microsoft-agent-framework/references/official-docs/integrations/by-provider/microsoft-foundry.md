---
title: Microsoft Foundry integrations
description: Find Agent Framework guidance for Microsoft Foundry models, agents, RAG, memory, evaluation, local models, and hosted agents.
author: eavanvalkenburg
ms.topic: overview
ms.author: edvan
ms.date: 07/28/2026
ms.service: agent-framework
---

# Microsoft Foundry integrations

Microsoft Foundry supports several Agent Framework scenarios across model inference, managed agents, tools, data grounding, memory, evaluation, observability, and hosting. Use this page to find the guide for the capability you want. Each linked page remains the source of truth for setup, SDK availability, and samples.

## Choose a Microsoft Foundry integration

| Scenario | Guide |
|---|---|
| Use models deployed to a Foundry project while your application owns the agent definition and orchestration. | [Microsoft Foundry model provider](../by-component/model-providers/microsoft-foundry.md) |
| Connect to a Prompt Agent or Hosted Agent managed by Microsoft Foundry Agent Service. | [Microsoft Foundry Agent Service](../by-component/agent-services/foundry.md) |
| Use a standalone Azure OpenAI resource for model inference. | [Azure OpenAI](../by-component/model-providers/azure-openai.md) |
| Run supported Microsoft Foundry models on your local machine. | [Foundry Local](../by-component/model-providers/foundry-local.md) |
| Configure provider-hosted tools and grounding tools. | [Microsoft Foundry tools](../by-component/model-providers/microsoft-foundry.md#tools) |
| Reuse named, versioned bundles of hosted tool configurations. | [Microsoft Foundry Toolbox](../by-component/tools/foundry-toolbox.md) |
| Use Anthropic Claude models deployed through a Foundry resource. | [Anthropic on Foundry](../by-component/model-providers/anthropic.md#using-anthropic-on-foundry) |
| Ground an agent with Foundry files, vector stores, and file search. | [Microsoft Foundry context providers](../by-component/context-providers/microsoft-foundry.md#use-file-search-rag) |
| Store and retrieve service-managed semantic memory. | [Microsoft Foundry context providers](../by-component/context-providers/microsoft-foundry.md#add-managed-semantic-memory) |
| Evaluate agents, workflows, traces, and responses with the managed evaluation service. | [Microsoft Foundry evaluation](../by-component/evaluation/microsoft-foundry.md) |
| Export Agent Framework telemetry to Azure Monitor through a Foundry project. | [Microsoft Foundry observability](../../agents/observability.md#microsoft-foundry-setup) |
| Deploy an Agent Framework application as a containerized managed agent. | [Foundry Hosted Agents](../../hosting/foundry-hosted-agent.md) |

## Related platform

- [Microsoft Azure integrations](microsoft-azure.md)

## Next steps

> [!div class="nextstepaction"]
> [Choose a Microsoft Foundry model provider](../by-component/model-providers/microsoft-foundry.md)
