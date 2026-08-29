# Model Context Protocol and External Boundaries

## Keep The Protocols Separate

| Need | Correct Protocol | Why |
| --- | --- | --- |
| Expose tools or contextual data to models and agents | MCP | Tool and context transport |
| Let one remote agent talk to another remote agent | A2A | Agent-to-agent delegation and discovery |
| Drive a rich human-facing web or mobile UI | AG-UI | Interactive UI protocol with streaming and state |

The most common architectural mistake is to blur these:

- MCP is not a remote-agent protocol.
- A2A is not a tool protocol.
- AG-UI is not MCP over HTTP with a prettier client.

## What MCP Means In Agent Framework

Agent Framework can attach remote MCP servers as tools for agents. In practice that means:

1. configure an MCP client or tool resource
2. add the resulting tool surface to the agent
3. run the agent normally

The agent sees MCP as tool capability, not as a separate execution runtime.

Microsoft Agent Framework `dotnet-1.19.0` moves its long-running MCP task support to the MCP `2026-07-28` Tasks extension. Do not combine the former task contract with a current peer: align Agent Framework and MCP SDK versions, negotiate the extension, persist the owning `AgentSession`, and test start, resume, cancellation, and terminal results end to end.

## The Security Model Matters More Than The API

The official docs are very explicit here:

- review every third-party MCP server
- prefer servers run by trusted providers over random proxies
- review what prompt data is being sent
- log what the server receives and returns when possible
- inject headers and auth only at run time

The framework supports custom headers specifically so you can pass run-scoped auth, which is the safe default.

## Header And Credential Rules

Custom headers should be:

- injected per run
- short-lived where possible
- excluded from durable session state
- excluded from source code and static agent definitions

Common safe pattern:

- agent definition is stable
- MCP auth arrives via request-scoped tool resources
- the current run gets only the headers it needs

## MCP Versus Hosted Tools

There are two distinct cases:

1. your agent uses an MCP server directly as an external tool source
2. your provider offers hosted MCP-like capabilities as managed service tools

Do not assume those behave the same way operationally. Hosted provider tools inherit provider behavior; remote MCP servers inherit the trust and failure modes of the remote server.

## Agent As MCP Tool

You can expose an agent itself as an MCP tool so that MCP clients can call it.

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

McpServerTool tool = McpServerTool.Create(agent.AsAIFunction());

HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(settings: null);
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools([tool]);

await builder.Build().RunAsync();
```

Use this when:

- you want the agent to behave like a callable tool in the MCP ecosystem
- conversational agent semantics are not required by the caller

Use A2A instead when the remote thing should remain an agent with its own protocol semantics and discovery model.

## Deployment Checklist

- Restrict MCP servers to the smallest trusted set.
- Keep auth request-scoped.
- Audit the prompt and tool data exchanged with remote servers.
- Treat MCP output as untrusted input before using it in downstream tools.
- Do not persist third-party secrets inside session state.

## When To Avoid MCP

Avoid MCP when:

- you really need remote-agent semantics rather than tool semantics
- your frontend protocol is the real problem and AG-UI is the right answer
- the external system is too sensitive to expose through a broad tool interface

## Source Pages

- `references/official-docs/agents/tools/index.md`
- `references/official-docs/agents/tools/local-mcp-tools.md`
- `references/official-docs/agents/tools/hosted-mcp-tools.md`
- `references/official-docs/agents/tools/hosted-mcp-tools.md`
