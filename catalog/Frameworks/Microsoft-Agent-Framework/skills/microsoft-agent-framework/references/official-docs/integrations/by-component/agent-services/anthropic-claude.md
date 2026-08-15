---
title: Anthropic Claude
description: Use the Anthropic Claude Agent SDK as an Agent Framework Python agent service.
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 07/28/2026
ms.service: agent-framework
---

# Anthropic Claude

`agent-framework-claude` wraps the Claude Agent SDK as `ClaudeAgent`. It uses Claude's managed agent runtime, sessions, permission model, built-in tools, and MCP support while exposing the Agent Framework run and streaming interfaces.

This integration is distinct from the [Anthropic model provider](../model-providers/anthropic.md), which uses Claude as the model behind an application-owned Agent Framework agent.

## Prerequisites

- Install and configure the Claude Code CLI.
- Choose a Claude model and permission mode.
- Run the agent in a constrained working directory when enabling file or shell tools.

## Install the package

```bash
pip install agent-framework-claude --pre
```

## Configuration

| Variable | Purpose |
|---|---|
| `CLAUDE_AGENT_MODEL` | Claude model used by the managed runtime. |
| `CLAUDE_AGENT_PERMISSION_MODE` | Default permission mode for built-in and MCP tools. |
| `CLAUDE_AGENT_CLI_PATH` | Optional explicit path to the Claude Code CLI. |
| `CLAUDE_AGENT_CWD` | Working directory exposed to the runtime. |
| `CLAUDE_AGENT_MAX_TURNS` | Optional maximum number of agent turns. |
| `CLAUDE_AGENT_MAX_BUDGET_USD` | Optional cost budget for a run. |

## Create a `ClaudeAgent`

`ClaudeAgent` supports regular and streaming runs and can expose Agent Framework function tools.

:::code language="python" source="~/../agent-framework-code/python/samples/02-agents/providers/anthropic/anthropic_claude_basic.py" range="35-69":::

Additional samples demonstrate:

- Claude built-in file and shell tools.
- Interactive permission handling.
- Local and remote MCP servers.
- Session persistence and resumption.
- URL fetching and multiple permission rules.

## Permission considerations

- Start with the least-permissive Claude Agent SDK permission mode that supports the task.
- Require explicit approval for shell, file, network, or other side-effecting operations.
- Don't expose credentials through environment variables or readable files in the agent working directory.

## Next steps

> [!div class="nextstepaction"]
> [A2A agent service](../agent-services/a2a.md)
