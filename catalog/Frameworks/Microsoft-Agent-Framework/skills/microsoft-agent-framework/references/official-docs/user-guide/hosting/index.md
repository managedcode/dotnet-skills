---
title: Agent Framework Integrations
description: Current capability map for Agent Framework integrations.
ms.topic: article
ms.date: 01/27/2026
ms.service: agent-framework
---

# Agent Framework Integrations

The watched `/agent-framework/user-guide/hosting/` URL now resolves to the broader integrations hub. This page is not an ASP.NET Core hosting-only contract.

## Current .NET Capability Map

| Capability | .NET Surface | Current Status |
| --- | --- | --- |
| Hosted agents | Microsoft Foundry Hosted Agents | The linked Agent Framework sample is Python; verify .NET support separately |
| UI and governance | AG-UI, DevUI, Purview | Preview in the integration hub |
| Chat history | `InMemoryChatHistoryProvider`, Cosmos DB NoSQL provider | In-memory released; Cosmos DB NoSQL preview |
| Memory | Chat-history memory provider, Neo4j memory provider | Chat-history provider released; Neo4j preview |
| RAG | `TextSearchProvider`, Neo4j GraphRAG provider | Text search released; Neo4j preview |
| Vector stores | `Microsoft.Extensions.VectorData.Abstractions` implementations | Provider ownership, support, and SDK maturity vary |

## Selection Rules

- Separate a provider's capability from its hosting model. History, memory, RAG, vector storage, UI, and remote protocols are different concerns.
- Confirm that a listed integration has a .NET implementation before copying Python examples.
- Treat preview providers as explicit architecture dependencies and verify package maturity, persistence behavior, licensing, and operational ownership.
- Use ASP.NET Core protocol adapters only after choosing the core `AIAgent` or workflow runtime.

## Local Deep Dives

- OpenAI-compatible ASP.NET Core hosting: `openai-integration.md`
- A2A hosting: `agent-to-agent-integration.md`
- AG-UI: `../../integrations/ag-ui/index.md`
- Agent threads and context providers: `../../user-guide/agents/agent-memory.md`

Live source: https://learn.microsoft.com/agent-framework/user-guide/hosting/
