---
title: Dapr
description: Use the Dapr Conversation building block as an Agent Framework .NET model provider.
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 07/28/2026
ms.service: agent-framework
---

# Dapr

The Dapr Conversation building block routes model inference through a Dapr sidecar and exposes an `IChatClient` that can back an Agent Framework .NET agent. The model provider and credentials are configured in the Dapr Conversation component rather than directly in the agent process.

## Prerequisites

- .NET 10 or later.
- Docker and the Dapr CLI.
- A configured Dapr Conversation component, such as an Ollama-backed component.

## Install the packages

```bash
dotnet add package Dapr.AI.Microsoft.Extensions
dotnet add package Microsoft.Agents.AI --prerelease
```

## Configuration

```bash
DAPR_GRPC_ENDPOINT="http://localhost:3501"
```

`DAPR_GRPC_ENDPOINT` is optional and defaults to `http://localhost:3501`. Set `ConversationComponentName` in application code to the name of the Dapr Conversation component, such as `ollama`.

## Create a Dapr-backed agent

Configure the Dapr sidecar endpoint and Conversation component through dependency injection, resolve the `IChatClient`, and convert it to an agent.

:::code language="csharp" source="~/../agent-framework-code/dotnet/samples/02-agents/AgentProviders/dapr/Agent_With_Dapr/Program.cs" range="14-36":::

Provider capabilities depend on the Dapr Conversation component and the model behind it.

## Tools

Tool support is inherited from the configured Dapr Conversation component and model.

| Tool | Status | Notes |
|---|:---:|---|
| [Function Tools](../../../agents/tools/function-tools.md) | Varies | Requires function-calling support from the configured component and model. |
| [Tool Approval](../../../agents/tools/tool-approval.md) | Varies | Available when the model produces function-tool calls. |
| Provider-hosted tools | ❌ | Dapr doesn't add a separate Agent Framework hosted-tool surface. |
| [Local MCP Tools](../../../agents/tools/local-mcp-tools.md) | ✅ | Runs in the application process. |

## Next steps

> [!div class="nextstepaction"]
> [Model Providers overview](index.md)
