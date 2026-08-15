---
title: Self-host agents as MCP tools
description: Expose an Agent Framework agent or workflow as a native MCP tool from an application-owned server.
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 07/21/2026
ms.service: agent-framework
---

# Self-host agents as MCP tools

:::zone pivot="programming-language-csharp"

> [!NOTE]
> Self-hosting MCP tool support in .NET is coming soon.

:::zone-end

:::zone pivot="programming-language-go"

> [!NOTE]
> Self-hosting MCP tool support is not currently available for Go.

:::zone-end

:::zone pivot="programming-language-python"

Use `agent-framework-hosting-mcp` to expose an Agent Framework agent or workflow as a tool on the native [Model Context Protocol](https://modelcontextprotocol.io/) SDK. The package does not choose a web framework or wrap the MCP SDK server lifecycle; your application still owns the `Server`, handler registration, transport, session-key policy, authentication, authorization, and deployment.

```bash
pip install --pre agent-framework-hosting-mcp
```

## Convert at the protocol boundary

`mcp_to_run(...)` converts validated MCP tool arguments into Agent Framework messages and selected chat options, and `mcp_from_run(...)` converts a completed response into native MCP `ContentBlock` values. Use these two functions directly when an application's tool contract needs a fully custom native schema and handler:

:::code language="python" source="~/../agent-framework-code/python/samples/04-hosting/mcp/manual_app.py" range="63-97":::

Only argument names listed in `chat_option_arguments` are copied into `run["options"]`; other MCP arguments stay available on the message's raw representation but aren't forwarded to the model client.

## Host an agent as one generated tool

`AgentMCPTool` derives the native tool name, description, and schema from an agent, and keeps listing, parsing, execution, and result conversion aligned so the two can't drift:

:::code language="python" source="~/../agent-framework-code/python/samples/04-hosting/mcp/agent_app.py" range="59-82":::

`AgentMCPTool` uses the agent's name and description unless overridden. `parameters` adds app-owned JSON Schema properties that stay available in the raw MCP arguments, and `chat_option_parameters` adds properties whose values are explicitly copied into Agent Framework chat options.

## Persist a session per call

Pass an existing `AgentState` and a `session_id_parameter` to let repeated calls with the same opaque, app-defined `session_id` continue one conversation:

:::code language="python" source="~/../agent-framework-code/python/samples/04-hosting/mcp/session_app.py" range="90-107":::

`AgentMCPTool` only performs the `AgentState` session get/run/set sequence; your application must authenticate or authorize the session identifier and serialize concurrent calls for the same session, as the sample does with a per-session `asyncio.Lock`. This isn't `previous_response_id`-style branching — an application that needs to fork a conversation should accept separate source and destination IDs, copy the source session, and store the result under the destination key.

## Host a workflow as a tool

`WorkflowMCPTool` derives one native MCP tool from a workflow's start-executor input type and converts completed workflow outputs. Dataclass, Pydantic, and other object-shaped inputs become top-level MCP arguments; primitive inputs are wrapped in a configurable argument name:

:::code language="python" source="~/../agent-framework-code/python/samples/04-hosting/mcp/workflow_app.py" range="66-70":::

Workflow instances preserve execution state, so applications that need independent calls should supply a `WorkflowState` factory with `cache_target=False`, as shown above. Checkpoint restoration, human-in-the-loop responses, and continuation identifiers remain application-owned; if a workflow requests external input, the adapter raises instead of returning an empty successful tool result.

For the complete set of runnable servers — including the FastMCP variant that derives its schema from a decorated function — see the [MCP hosting samples](https://github.com/microsoft/agent-framework/tree/main/python/samples/04-hosting/mcp).

> [!IMPORTANT]
> Treat the MCP session identifier and any app-defined `session_id` argument as untrusted input. Authenticate and authorize the caller before using either to load or save session state, and derive durable partitioning from the authenticated tenant, user, or workspace rather than the raw value.

## Next steps

> [!div class="nextstepaction"]
> [Learn about A2A hosting](a2a/index.md)

**Go deeper:**

- [Self-hosting overview](index.md)
- [OpenAI Responses](responses.md)
- [Telegram](telegram.md)

:::zone-end
