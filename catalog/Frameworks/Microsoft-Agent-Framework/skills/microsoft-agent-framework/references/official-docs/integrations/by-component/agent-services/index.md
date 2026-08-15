---
title: Agent services
description: Compare managed and remote agent services available to Agent Framework applications.
author: eavanvalkenburg
ms.topic: reference
ms.author: edvan
ms.date: 07/28/2026
ms.service: agent-framework
---

# Agent services

Agent services provide a remote or managed agent runtime rather than only model inference. The service can own the agent definition, hosted tools, permissions, sessions, or execution lifecycle while Agent Framework exposes a consistent run interface to your application.

For inference clients where your application owns the agent definition and orchestration, see [Model Providers](../model-providers/index.md).

## Available agent services

| Agent service | C# | Python | Go | What the service owns |
|---|:---:|:---:|:---:|---|
| [Microsoft Foundry](./foundry.md) | ✅ | ✅ | ❌ | Prompt or Hosted Agent definition, versions, hosted tools, conversations, and service-side execution |
| [GitHub Copilot](./github-copilot.md) | ✅ | ✅ | ✅ | Coding-agent runtime, sessions, permissions, built-in shell/file/URL capabilities, and MCP connections |
| [Copilot Studio](./copilot-studio.md) | ✅ | ✅ | ❌ | Published agent topics, knowledge, actions, plugins, and remote execution |
| [Anthropic Claude](./anthropic-claude.md) | ❌ | ✅ | ❌ | Claude Agent SDK runtime, sessions, permissions, built-in tools, and MCP connections |
| [A2A](./a2a.md) | ✅ | ✅ | ✅ | Remote A2A-compliant agent definition, tools, sessions, tasks, and execution |

## Related integrations

- [Model Providers](../model-providers/index.md) for model inference clients.
- [Foundry Hosted Agents](../../../hosting/foundry-hosted-agent.md) for deploying an Agent Framework application as a managed container.

## Next steps

> [!div class="nextstepaction"]
> [Microsoft Foundry](./foundry.md)
