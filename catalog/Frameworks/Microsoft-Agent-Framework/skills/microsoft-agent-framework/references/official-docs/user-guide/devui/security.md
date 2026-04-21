---
title: DevUI Security & Deployment
description: Learn about security best practices and deployment options for DevUI.
author: moonbox3
ms.topic: how-to
ms.author: evmattso
ms.date: 12/10/2025
ms.service: agent-framework
zone_pivot_groups: programming-languages
---

# Security & Deployment

DevUI is designed as a **sample application for local development**. This page covers security considerations and best practices if you need to expose DevUI beyond localhost.

> [!WARNING]
> DevUI is not intended for production use. For production deployments, build your own custom interface using the Agent Framework SDK with appropriate security measures.

::: zone pivot="programming-language-csharp"

## Coming Soon

DevUI documentation for C# is coming soon. Please check back later or refer to the Python documentation for conceptual guidance.

::: zone-end

::: zone pivot="programming-language-python"

## UI Modes

DevUI offers two modes that control access to features:

### Developer Mode (Default)

Full access to all features:

- Debug panel with trace information
- Hot reload for rapid development (`/v1/entities/{id}/reload`)
- Deployment tools (`/v1/deployments`)
- Verbose error messages for debugging

```bash
devui ./agents  # Developer mode is the default
```

### User Mode

Simplified, restricted interface:

- Chat interface and conversation management
- Entity listing and basic info
- Developer APIs disabled (hot reload, deployment)
- Generic error messages (details logged server-side)

```bash
devui ./agents --mode user
```

## Authentication

Enable Bearer token authentication with the `--auth` flag:

```bash
devui ./agents --auth
```

When authentication is enabled:
- For **localhost**: A token is auto-generated and displayed in the console
- For **network-exposed** deployments: You must provide a token via `DEVUI_AUTH_TOKEN` environment variable or `--auth-token` flag

```bash
# Auto-generated token (localhost only)
devui ./agents --auth

# Custom token via CLI
devui ./agents --auth --auth-token "your-secure-token"

# Custom token via environment variable
export DEVUI_AUTH_TOKEN="your-secure-token"
devui ./agents --auth --host 0.0.0.0
```

All API requests must include a valid Bearer token in the `Authorization` header:

```bash
curl http://localhost:8080/v1/entities \
  -H "Authorization: Bearer your-token-here"
```

## Recommended Deployment Configuration

If you need to expose DevUI to end users (not recommended for production):

```bash
devui ./agents --mode user --auth --host 0.0.0.0
```

This configuration:

- Restricts developer-facing APIs
- Requires authentication
- Binds to all network interfaces

## Security Features

DevUI includes several security measures:

| Feature | Description |
|---------|-------------|
| Localhost binding | Binds to 127.0.0.1 by default |
| User mode | Restricts developer APIs |
| Bearer authentication | Optional token-based auth |
| Local entity loading | Only loads entities from local directories or in-memory |
| No remote execution | No remote code execution capabilities |

## Best Practices

### Credentials Management

- Store API keys and secrets in `.env` files
- Never commit `.env` files to source control
- Use `.env.example` files to document required variables

```bash
# .env.example (safe to commit)
OPENAI_API_KEY=your-api-key-here
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/

# .env (never commit)
OPENAI_API_KEY=sk-actual-key
AZURE_OPENAI_ENDPOINT=https://my-resource.openai.azure.com/
```

### Network Security

- Keep DevUI bound to localhost for development
- Use a reverse proxy (nginx, Caddy) if external access is needed
- Enable HTTPS through the reverse proxy
- Implement proper authentication at the proxy level

### Entity Security

- Review all agent/workflow code before running
- Only load entities from trusted sources
- Be cautious with tools that have side effects (file access, network calls)

## Resource Cleanup

Register cleanup hooks to properly close credentials and resources on shutdown:

```python
from azure.identity.aio import DefaultAzureCredential
from agent_framework import ChatAgent
from agent_framework.azure import AzureOpenAIChatClient
from agent_framework_devui import register_cleanup, serve

credential = DefaultAzureCredential()
client = AzureOpenAIChatClient()
agent = ChatAgent(name="MyAgent", chat_client=client)

# Register cleanup hook - credential will be closed on shutdown
register_cleanup(agent, credential.close)
serve(entities=[agent])
```

## MCP Tools Considerations

When using MCP (Model Context Protocol) tools with DevUI:

```python
# Correct - DevUI handles cleanup automatically
mcp_tool = MCPStreamableHTTPTool(url="http://localhost:8011/mcp", chat_client=chat_client)
agent = ChatAgent(tools=mcp_tool)
serve(entities=[agent])
```

> [!IMPORTANT]
> Don't use `async with` context managers when creating agents with MCP tools for DevUI. Connections will close before execution. MCP tools use lazy initialization and connect automatically on first use.

::: zone-end

## Next Steps

- [Samples](./samples.md) - Browse sample agents and workflows
- [API Reference](./api-reference.md) - Learn about the API endpoints
