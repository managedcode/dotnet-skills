---
title: Hosting Agent Framework applications
description: Choose between Microsoft-managed Foundry Hosted Agents and self-hosting Agent Framework applications.
author: eavanvalkenburg
ms.topic: overview
ms.author: edvan
ms.date: 07/21/2026
ms.service: agent-framework
---

# Hosting Agent Framework applications

After you build an agent or workflow, first choose who operates its infrastructure. This is an operational choice between Microsoft-managed Foundry Hosted Agents and self-hosting; it is separate from the protocol that clients use to reach your agent.

## Choose a hosting model

| | [Foundry Hosted Agents](foundry-hosted-agent.md) | [Self-hosting](self-hosting/index.md) |
|---|---|---|
| **Who operates the infrastructure?** | Microsoft Foundry Agent Service runs the container, scaling, session lifecycle, and platform integration. | Your application runs in your web service, container, runtime, or existing infrastructure. |
| **What do you operate?** | Your agent code and Foundry configuration. | Routes, identity, authorization, request policy, storage, deployment, scaling, and native client libraries. |
| **Choose this when** | You want Microsoft-managed agent hosting. | You need application-level control or must integrate with your existing infrastructure. |
| **Start here** | [Host an agent in Foundry](foundry-hosted-agent.md) | [Self-host an Agent Framework application](self-hosting/index.md) |

Microsoft Foundry Hosted Agents is generally available. The current Python self-hosting packages are prerelease; see the self-hosting guide for package-specific lifecycle information.

For Azure Functions triggers, durable execution, or long-running orchestration, use the [Durable Extension](azure-functions.md). It is a self-managed hosting path with Durable Task infrastructure.

## Choose a protocol separately

The hosting model does not determine the protocol. For example, the OpenAI Responses protocol works with both models:

- **Foundry Hosted Agents** expose managed Responses and Invocations endpoints and support the Activity protocol for Microsoft 365 channels.
- **Self-hosting** lets your application use the Responses helpers to expose a `/responses` endpoint with its own framework, routing, and policy.

After choosing a host, select the client integration that fits your scenario:

- [OpenAI-compatible endpoints](self-hosting/openai-endpoints.md) for Responses and Chat Completions-compatible APIs.
- [A2A hosting](./self-hosting/a2a/server.md) to expose an Agent Framework agent through the Agent-to-Agent protocol.
- [A2A agent service](../integrations/by-component/agent-services/a2a.md) to invoke a remote A2A-compliant agent.
- [AG-UI](../integrations/by-component/ui/ag-ui/index.md) for web-based agent applications.
- [Telegram bots](self-hosting/telegram.md) for a self-hosted native Telegram Bot API integration.
- [MCP tools](self-hosting/mcp.md) for exposing an agent or workflow as a native MCP tool.

## Next steps

> [!div class="nextstepaction"]
> [Choose Foundry Hosted Agents](foundry-hosted-agent.md)

**Go deeper:**

- [Self-hosting](self-hosting/index.md)
- [Durable Extension](azure-functions.md)
