---
title: GitHub Copilot
description: Learn how to use Microsoft Agent Framework with the GitHub Copilot SDK.
zone_pivot_groups: programming-languages
author: dmytrostruk
ms.topic: tutorial
ms.author: dmytrostruk
ms.date: 07/30/2026
ms.service: agent-framework
---

<!--
    Language parity table - keep in sync when adding/removing sections.

    | Section                       | C# | Python | Go | Notes                                      |
    |-------------------------------|:--:|:------:|:--:|--------------------------------------------|
    | Getting Started / Installation| ✅ |   ✅   | ✅ | Python and Go use package/module install.  |
    | Configuration                 | ❌ |   ✅   | ❌ | Python-specific environment variable table.|
    | Bring your own key (BYOK)     | ❌ |   ✅   | ❌ | Python forwards Copilot SDK provider configuration. |
    | Create an Agent               | ✅ |   ✅   | ✅ |                                            |
    | Function Tools                | ✅ |   ✅   | ✅ |                                            |
    | Context Providers             | ❌ |   ✅   | ❌ | Python-specific content in this page.      |
    | Streaming Responses           | ✅ |   ✅   | ✅ |                                            |
    | Session Management            | ✅ |   ✅   | ✅ |                                            |
    | Permissions                   | ✅ |   ✅   | ✅ |                                            |
    | Tool Approval                 | ✅ |   ✅   | ❌ | Go uses standard tool approval (see Tools).|
    | MCP Servers                   | ✅ |   ✅   | ✅ |                                            |
    | Observability                 | ❌ |   ✅   | ❌ | Python-specific content in this page.      |
    | Tools                         | ✅ |   ✅   | ✅ |                                            |
    | Using the Agent               | ✅ |   ✅   | ✅ |                                            |
-->

# GitHub Copilot

Microsoft Agent Framework supports creating agents that use the [GitHub Copilot SDK](https://github.com/github/copilot-sdk) as their backend. GitHub Copilot agents provide access to powerful coding-oriented AI capabilities, including shell command execution, file operations, URL fetching, and Model Context Protocol (MCP) server integration.

> [!IMPORTANT]
> GitHub Copilot agents require an authenticated GitHub Copilot runtime. Some SDKs use an installed CLI, while the Go SDK uses the bundled runtime by default. For security, it is recommended to run agents with shell or file permissions in a containerized environment (Docker/Dev Container).

::: zone pivot="programming-language-csharp"

## Getting Started

Add the required NuGet packages to your project.

```dotnetcli
dotnet add package Microsoft.Agents.AI.GitHub.Copilot
```

## Create a GitHub Copilot Agent

As a first step, create a `CopilotClient` and start it. Then use the `AsAIAgent` extension method to create an agent.

```csharp
using GitHub.Copilot;
using Microsoft.Agents.AI;

await using CopilotClient copilotClient = new();
await copilotClient.StartAsync();

AIAgent agent = copilotClient.AsAIAgent(sessionConfig: null);

Console.WriteLine(await agent.RunAsync("What is Microsoft Agent Framework?"));
```

### With Tools and Instructions

You can provide function tools and custom instructions when creating the agent:

```csharp
using GitHub.Copilot;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

AIFunction weatherTool = AIFunctionFactory.Create((string location) =>
{
    return $"The weather in {location} is sunny with a high of 25C.";
}, "GetWeather", "Get the weather for a given location.");

await using CopilotClient copilotClient = new();
await copilotClient.StartAsync();

AIAgent agent = copilotClient.AsAIAgent(
    tools: [weatherTool],
    instructions: "You are a helpful weather agent.");

Console.WriteLine(await agent.RunAsync("What's the weather like in Seattle?"));
```

## Agent Features

### Streaming Responses

Get responses as they are generated:

```csharp
await using CopilotClient copilotClient = new();
await copilotClient.StartAsync();

AIAgent agent = copilotClient.AsAIAgent(sessionConfig: null);

await foreach (AgentResponseUpdate update in agent.RunStreamingAsync("Tell me a short story."))
{
    Console.Write(update);
}

Console.WriteLine();
```

### Session Management

Maintain conversation context across multiple interactions using sessions:

```csharp
await using CopilotClient copilotClient = new();
await copilotClient.StartAsync();

await using GitHubCopilotAgent agent = new(
    copilotClient,
    instructions: "You are a helpful assistant. Keep your answers short.");

AgentSession session = await agent.CreateSessionAsync();

// First turn
await agent.RunAsync("My name is Alice.", session);

// Second turn - agent remembers the context
AgentResponse response = await agent.RunAsync("What is my name?", session);
Console.WriteLine(response); // Should mention "Alice"
```

### Permissions

By default, the agent cannot execute shell commands, read/write files, or fetch URLs. To enable these capabilities, provide a permission handler via `SessionConfig`:

```csharp
static Task<PermissionDecision> PromptPermission(
    PermissionRequest request, PermissionInvocation invocation)
{
    Console.WriteLine($"\n[Permission Request: {request.Kind}]");
    Console.Write("Approve? (y/n): ");

    string? input = Console.ReadLine()?.Trim().ToUpperInvariant();
    PermissionDecision decision = input is "Y" or "YES"
        ? PermissionDecision.ApproveOnce()
        : PermissionDecision.Reject();

    return Task.FromResult(decision);
}

await using CopilotClient copilotClient = new();
await copilotClient.StartAsync();

SessionConfig sessionConfig = new()
{
    OnPermissionRequest = PromptPermission,
};

AIAgent agent = copilotClient.AsAIAgent(sessionConfig);

Console.WriteLine(await agent.RunAsync("List all files in the current directory"));
```

### Tool Approval

Because the GitHub Copilot SDK owns the tool-calling loop, approval for custom function tools is enforced through the SDK's native pre-execution hook rather than the standard Agent Framework approval round-trip. When you register a tool wrapped in `ApprovalRequiredAIFunction`, the agent installs a default `OnPreToolUse` hook that returns `"ask"` for that tool and routes the decision to your `OnPermissionRequest` handler:

```csharp
using GitHub.Copilot;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

AIFunction deleteFile = AIFunctionFactory.Create(
    (string path) => $"Deleted {path}.",
    "DeleteFile",
    "Deletes a file.");

await using CopilotClient copilotClient = new();
await copilotClient.StartAsync();

SessionConfig sessionConfig = new()
{
    // Wrapping the tool marks it approval-required; the agent turns this into an "ask" at OnPreToolUse.
    Tools = [new ApprovalRequiredAIFunction(deleteFile)],

    // OnPermissionRequest decides the "asked" tools (and Copilot's built-in shell/file/URL prompts).
    OnPermissionRequest = PromptPermission,
};

AIAgent agent = copilotClient.AsAIAgent(sessionConfig);

Console.WriteLine(await agent.RunAsync("Delete the file temp.txt"));
```

> [!WARNING]
> If you provide your own `OnPreToolUse` hook via `SessionConfig.Hooks`, it takes precedence and the agent does **not** install its default approval hook. You are then fully responsible for enforcing approval for any `ApprovalRequiredAIFunction` you register (for example, by returning a `"deny"` or `"ask"` decision). The agent logs a warning naming any approval-required tool your hook must handle.

### MCP Servers

Connect to local (stdio) or remote (HTTP) MCP servers for extended capabilities:

```csharp
await using CopilotClient copilotClient = new();
await copilotClient.StartAsync();

SessionConfig sessionConfig = new()
{
    OnPermissionRequest = PromptPermission,
    McpServers = new Dictionary<string, McpServerConfig>
    {
        // Local stdio server
        ["filesystem"] = new McpStdioServerConfig
        {
            Command = "npx",
            Args = ["-y", "@modelcontextprotocol/server-filesystem", "."],
            Tools = ["*"],
        },
        // Remote HTTP server
        ["microsoft-learn"] = new McpHttpServerConfig
        {
            Url = "https://learn.microsoft.com/api/mcp",
            Tools = ["*"],
        },
    },
};

AIAgent agent = copilotClient.AsAIAgent(sessionConfig);

Console.WriteLine(await agent.RunAsync("Search Microsoft Learn for 'Azure Functions' and summarize the top result"));
```

> [!TIP]
> See the [.NET samples](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples) for complete runnable examples.

## Tools

| Tool | Status | Notes |
|---|---|---|
| [Function Tools](../../../agents/tools/function-tools.md) | ✅ | Standard `AIFunction` instances. |
| [Tool Approval](../../../agents/tools/tool-approval.md) | ✅ | Provided by the framework's function-invoking chat client; works with any function-tool call. |
| [Code Interpreter](../../../agents/tools/code-interpreter.md) | ❌ | Not a Copilot CLI capability. |
| [File Search](../../../agents/tools/file-search.md) | ❌ | Not a Copilot CLI capability. |
| [Web Search](../../../agents/tools/web-search.md) | ❌ | Not exposed as a hosted tool. |
| Shell / file system / URL fetching | ✅ | Built into the Copilot CLI runtime and gated by the [Permissions](#permissions) handler you supply. |
| [Hosted MCP Tools](../../../agents/tools/hosted-mcp-tools.md) | ✅ | Remote (HTTP) MCP servers configured via `SessionConfig.McpServers`. See [MCP Servers](#mcp-servers). |
| [Local MCP Tools](../../../agents/tools/local-mcp-tools.md) | ✅ | Local (stdio) MCP servers configured via `SessionConfig.McpServers`. See [MCP Servers](#mcp-servers). |

## Using the Agent

The agent is a standard `AIAgent` and supports all standard `AIAgent` operations.

For more information on how to run and interact with agents, see the [Agent getting started tutorials](../../../get-started/your-first-agent.md).

::: zone-end
::: zone pivot="programming-language-python"

## Prerequisites

Install the Microsoft Agent Framework GitHub Copilot package.

```bash
pip install agent-framework-github-copilot
```

## Configuration

The agent can be optionally configured using the following environment variables:

| Variable | Description |
|----------|-------------|
| `GITHUB_COPILOT_CLI_PATH` | Path to the Copilot CLI executable |
| `GITHUB_COPILOT_MODEL` | Model to use (e.g., `gpt-5`, `claude-sonnet-4`) |
| `GITHUB_COPILOT_TIMEOUT` | Request timeout in seconds |
| `GITHUB_COPILOT_LOG_LEVEL` | CLI log level |
| `GITHUB_COPILOT_BASE_DIRECTORY` | Directory for CLI session state and config (defaults to `~/.copilot`) |

## Getting Started

Import the required classes from Agent Framework:

```python
import asyncio
from agent_framework.github import GitHubCopilotAgent, GitHubCopilotOptions
```

## Create a GitHub Copilot Agent

### Basic Agent Creation

The simplest way to create a GitHub Copilot agent:

```python
async def basic_example():
    agent = GitHubCopilotAgent(
        instructions="You are a helpful assistant.",
    )

    async with agent:
        result = await agent.run("What is Microsoft Agent Framework?")
        print(result)
```

### With Explicit Configuration

You can provide explicit configuration through `default_options`:

```python
async def explicit_config_example():
    agent = GitHubCopilotAgent(
        instructions="You are a helpful assistant.",
        default_options={
            "model": "gpt-5",
            "timeout": 120,
        },
    )

    async with agent:
        result = await agent.run("What can you do?")
        print(result)
```

> [!TIP]
> `default_options` (and per-run `options`) forwards any parameter accepted by the Copilot SDK's `create_session` — for example `reasoning_effort`, `context_tier`, `enable_citations`, `provider` (bring-your-own-key), or `skill_directories` — not just the keys shown here. Unknown parameter names raise a `TypeError`, so typos are caught rather than silently ignored.

### Bring your own key (BYOK)

Use the Copilot SDK's BYOK support to route model requests through your own OpenAI, Azure OpenAI, Anthropic, or OpenAI-compatible endpoint instead of the GitHub Copilot backend. Pass a `ProviderConfig` through `GitHubCopilotOptions(provider=...)`, and set the same model identifier in both the provider configuration and the session-level `model` option.

The runnable sample uses these environment variables:

| Variable | Description |
|----------|-------------|
| `BYOK_PROVIDER_TYPE` | Provider type: `openai`, `azure`, or `anthropic`. Defaults to `openai`. |
| `BYOK_BASE_URL` | Base URL for the provider endpoint. |
| `BYOK_API_KEY` | Static API key for the provider endpoint. |
| `BYOK_MODEL_ID` | Model identifier to request. Defaults to `gpt-4o`. |

> [!WARNING]
> BYOK uses static credentials and doesn't provide automatic token refresh. Keep API keys out of source control and load them from environment variables or a secret store. Usage and billing are tracked by your provider rather than GitHub.

:::code language="python" source="~/../agent-framework-code/python/samples/02-agents/providers/github_copilot/github_copilot_with_byok.py" range="22-57":::

## Agent Features

### Context Providers

Python `GitHubCopilotAgent` also supports `context_providers=[...]`. Providers run before and after each invocation, so provider-added messages and instructions are included in the Copilot prompt and history providers can observe the final response.

```python
from agent_framework import InMemoryHistoryProvider

agent = GitHubCopilotAgent(
    instructions="You are a helpful coding assistant.",
    context_providers=[InMemoryHistoryProvider()],
)
```

You can combine built-in history providers with custom context providers. For implementation patterns, see [Context Providers](../../../concepts/agents/conversations/context-providers.md).

### Function Tools

Equip your agent with custom functions:

```python
from typing import Annotated
from pydantic import Field

def get_weather(
    location: Annotated[str, Field(description="The location to get the weather for.")],
) -> str:
    """Get the weather for a given location."""
    return f"The weather in {location} is sunny with a high of 25C."

async def tools_example():
    agent = GitHubCopilotAgent(
        instructions="You are a helpful weather agent.",
        tools=[get_weather],
    )

    async with agent:
        result = await agent.run("What's the weather like in Seattle?")
        print(result)
```

### Streaming Responses

Get responses as they are generated for better user experience:

```python
async def streaming_example():
    agent = GitHubCopilotAgent(
        instructions="You are a helpful assistant.",
    )

    async with agent:
        print("Agent: ", end="", flush=True)
        async for chunk in agent.run("Tell me a short story.", stream=True):
            if chunk.text:
                print(chunk.text, end="", flush=True)
        print()
```

### Thread Management

Maintain conversation context across multiple interactions:

```python
async def thread_example():
    agent = GitHubCopilotAgent(
        instructions="You are a helpful assistant.",
    )

    async with agent:
        session = agent.create_session()

        # First interaction
        result1 = await agent.run("My name is Alice.", session=session)
        print(f"Agent: {result1}")

        # Second interaction - agent remembers the context
        result2 = await agent.run("What's my name?", session=session)
        print(f"Agent: {result2}")  # Should remember "Alice"
```

### Permissions

By default, the agent cannot execute shell commands, read/write files, or fetch URLs. To enable these capabilities, provide a permission handler:

```python
import asyncio

from copilot.generated.rpc import PermissionDecisionDeniedInteractivelyByUser
from copilot.session import PermissionHandler, PermissionRequestResult
from copilot.session_events import PermissionRequest


async def prompt_permission(
    request: PermissionRequest, context: dict[str, str]
) -> PermissionRequestResult:
    print(f"\n[Permission Request: {request.kind}]")
    response = (await asyncio.to_thread(input, "Approve? (y/n): ")).strip().lower()
    if response in ("y", "yes"):
        return PermissionHandler.approve_all(request, context)
    return PermissionDecisionDeniedInteractivelyByUser()

async def permissions_example():
    agent = GitHubCopilotAgent(
        instructions="You are a helpful assistant that can execute shell commands.",
        default_options={
            "on_permission_request": prompt_permission,
        },
    )

    async with agent:
        result = await agent.run("List the Python files in the current directory")
        print(result)
```

For trusted environments where all permissions should be auto-approved, use the built-in `PermissionHandler.approve_all`:

```python
from copilot.session import PermissionHandler

agent = GitHubCopilotAgent(
    default_options={
        "on_permission_request": PermissionHandler.approve_all,
    },
)
```

Permission handlers support both sync and async callbacks. Use `asyncio.to_thread` for interactive prompts in async handlers to avoid blocking the event loop.

### Tool Approval

Because the GitHub Copilot SDK owns the tool-calling loop, approval for custom function tools is enforced through the SDK's native pre-execution hook rather than the standard Agent Framework approval round-trip. When you register a tool declared with `approval_mode="always_require"` and do not supply your own `on_pre_tool_use` hook, the agent installs a default hook that returns `"ask"` for that tool and routes the decision to your `on_permission_request` handler:

```python
from agent_framework import tool
from agent_framework.github import GitHubCopilotAgent, GitHubCopilotOptions
from copilot.session import PermissionHandler


@tool(approval_mode="always_require")
def delete_file(path: str) -> str:
    """Delete a file."""
    return f"Deleted {path}."


agent = GitHubCopilotAgent(
    tools=[delete_file],
    # The "ask" decision is routed here; approve or deny the call.
    default_options=GitHubCopilotOptions(on_permission_request=PermissionHandler.approve_all),
)
```

> [!WARNING]
> If you provide your own `on_pre_tool_use` hook, it takes precedence and the agent does **not** install its default approval hook. You are then fully responsible for enforcing approval for any `approval_mode="always_require"` tool (for example, by returning a `"deny"` or `"ask"` decision). The agent logs a warning naming any approval-required tool your hook must handle. With the default deny-all permission handler, an `always_require` tool is denied unless you wire an approving `on_permission_request`.

### MCP Servers

Connect to local (stdio) or remote (HTTP) MCP servers for extended capabilities:

```python
from copilot.session import MCPServerConfig, PermissionHandler

async def mcp_example():
    mcp_servers: dict[str, MCPServerConfig] = {
        # Local stdio server
        "filesystem": {
            "type": "stdio",
            "command": "npx",
            "args": ["-y", "@modelcontextprotocol/server-filesystem", "."],
            "tools": ["*"],
        },
        # Remote HTTP server
        "microsoft-learn": {
            "type": "http",
            "url": "https://learn.microsoft.com/api/mcp",
            "tools": ["*"],
        },
    }

    agent = GitHubCopilotAgent(
        instructions="You are a helpful assistant with access to the filesystem and Microsoft Learn.",
        default_options={
            "on_permission_request": PermissionHandler.approve_all,
            "mcp_servers": mcp_servers,
        },
    )

    async with agent:
        result = await agent.run("Search Microsoft Learn for 'Azure Functions' and summarize the top result")
        print(result)
```

### Observability

`GitHubCopilotAgent` has OpenTelemetry tracing built-in. Call `configure_otel_providers()` once at startup to enable spans, metrics and logs for every run:

```python
from agent_framework.observability import configure_otel_providers
from agent_framework.github import GitHubCopilotAgent

configure_otel_providers(enable_console_exporters=True)

async with GitHubCopilotAgent() as agent:
    response = await agent.run("Hello!")
```

If you need the underlying agent without the telemetry layer (for example to wrap it in a custom one), import `RawGitHubCopilotAgent` from `agent_framework.github`.

For OTLP exporters and richer examples, see the [observability samples](https://github.com/microsoft/agent-framework/tree/main/python/samples/02-agents/observability).

## Tools

| Tool | Status | Notes |
|---|---|---|
| [Function Tools](../../../agents/tools/function-tools.md) | ✅ | Standard Python callables or `@ai_function`. |
| [Tool Approval](../../../agents/tools/tool-approval.md) | ✅ | Provided by the framework's function-invoking chat client; works with any function-tool call. |
| [Code Interpreter](../../../agents/tools/code-interpreter.md) | ❌ | Not a Copilot CLI capability. |
| [File Search](../../../agents/tools/file-search.md) | ❌ | Not a Copilot CLI capability. |
| [Web Search](../../../agents/tools/web-search.md) | ❌ | Not exposed as a hosted tool. |
| Shell / file system / URL fetching | ✅ | Built into the Copilot CLI runtime and gated by the [Permissions](#permissions-1) handler you provide. |
| [Hosted MCP Tools](../../../agents/tools/hosted-mcp-tools.md) | ✅ | Remote (HTTP) MCP servers configured via `default_options["mcp_servers"]`. See [MCP Servers](#mcp-servers-1). |
| [Local MCP Tools](../../../agents/tools/local-mcp-tools.md) | ✅ | Local (stdio) MCP servers configured via `default_options["mcp_servers"]`. See [MCP Servers](#mcp-servers-1). |

## Using the Agent

The agent is a standard `BaseAgent` and supports all standard agent operations.

For more information on how to run and interact with agents, see the [Agent getting started tutorials](../../../get-started/your-first-agent.md).

::: zone-end

::: zone pivot="programming-language-go"

## Getting Started

Install the Microsoft Agent Framework Go module and the GitHub Copilot SDK for Go. The Agent Framework Go SDK requires Go 1.25 or later.

```bash
go get github.com/microsoft/agent-framework-go github.com/github/copilot-sdk/go
```

## Create a GitHub Copilot Agent

Create and start a `copilot.Client`, then pass it to `copilotprovider.NewAgent`.

```go
import (
    "context"
    "fmt"

    copilot "github.com/github/copilot-sdk/go"
    "github.com/microsoft/agent-framework-go/provider/copilotprovider"
)

ctx := context.Background()

copilotClient := copilot.NewClient(nil)
if err := copilotClient.Start(ctx); err != nil {
    panic(err)
}
defer func() { _ = copilotClient.Stop() }()

copilotAgent := copilotprovider.NewAgent(
    copilotClient,
    copilotprovider.AgentConfig{
        Instructions: "You are a helpful assistant.",
    },
)

response, err := copilotAgent.RunText(ctx, "What is Microsoft Agent Framework?").Collect()
if err != nil {
    panic(err)
}
fmt.Println(response)
```

### With Tools and Instructions

You can provide function tools and custom instructions when creating the agent:

```go
import (
    "context"
    "fmt"

    copilot "github.com/github/copilot-sdk/go"
    "github.com/microsoft/agent-framework-go/agent"
    "github.com/microsoft/agent-framework-go/provider/copilotprovider"
    "github.com/microsoft/agent-framework-go/tool"
    "github.com/microsoft/agent-framework-go/tool/functool"
)

weatherTool := functool.MustNew(
    functool.Config{
        Name:        "GetWeather",
        Description: "Get the weather for a given location.",
    },
    func(_ context.Context, location string) (string, error) {
        return fmt.Sprintf("The weather in %s is sunny with a high of 25C.", location), nil
    },
)

copilotAgent := copilotprovider.NewAgent(
    copilotClient,
    copilotprovider.AgentConfig{
        Instructions: "You are a helpful weather agent.",
        Config: agent.Config{
            Tools: []tool.Tool{weatherTool},
        },
    },
)

response, err := copilotAgent.RunText(ctx, "What's the weather like in Seattle?").Collect()
if err != nil {
    panic(err)
}
fmt.Println(response)
```

## Agent Features

### Streaming Responses

Get responses as they are generated:

```go
for update, err := range copilotAgent.RunText(ctx, "Tell me a short story.", agent.Stream(true)) {
    if err != nil {
        panic(err)
    }
    fmt.Print(update)
}

fmt.Println()
```

### Session Management

Maintain conversation context across multiple interactions using sessions:

```go
session, err := copilotAgent.CreateSession(ctx)
if err != nil {
    panic(err)
}

// First turn
response, err := copilotAgent.RunText(ctx, "My name is Alice.", agent.WithSession(session)).Collect()
if err != nil {
    panic(err)
}
fmt.Println(response)

// Second turn - the agent remembers the context
response, err = copilotAgent.RunText(ctx, "What is my name?", agent.WithSession(session)).Collect()
if err != nil {
    panic(err)
}
fmt.Println(response)
```

### Permissions

By default, the agent cannot execute shell commands, read/write files, or fetch URLs. To enable these capabilities, provide a permission handler via `copilot.SessionConfig`:

```go
import (
    "bufio"
    "fmt"
    "os"
    "strings"

    copilot "github.com/github/copilot-sdk/go"
    "github.com/github/copilot-sdk/go/rpc"
    "github.com/microsoft/agent-framework-go/provider/copilotprovider"
)

func promptPermission(request copilot.PermissionRequest, _ copilot.PermissionInvocation) (rpc.PermissionDecision, error) {
    fmt.Printf("\n[Permission Request: %s]\n", request.Kind())
    fmt.Print("Approve? (y/n): ")

    input, _ := bufio.NewReader(os.Stdin).ReadString('\n')
    input = strings.TrimSpace(strings.ToUpper(input))
    if input == "Y" || input == "YES" {
        return &rpc.PermissionDecisionApproveOnce{}, nil
    }
    return &rpc.PermissionDecisionReject{}, nil
}

copilotAgent := copilotprovider.NewAgent(
    copilotClient,
    copilotprovider.AgentConfig{
        SessionConfig: &copilot.SessionConfig{
            OnPermissionRequest: promptPermission,
        },
    },
)

response, err := copilotAgent.RunText(ctx, "List all files in the current directory").Collect()
if err != nil {
    panic(err)
}
fmt.Println(response)
```

### MCP Servers

Connect to local (stdio) or remote (HTTP) MCP servers for extended capabilities:

```go
import (
    copilot "github.com/github/copilot-sdk/go"
    "github.com/microsoft/agent-framework-go/provider/copilotprovider"
)

mcpServers := map[string]copilot.MCPServerConfig{
    // Local stdio server
    "filesystem": copilot.MCPStdioServerConfig{
        Command: "npx",
        Args:    []string{"-y", "@modelcontextprotocol/server-filesystem", "."},
        Tools:   []string{"*"},
    },
    // Remote HTTP server
    "microsoft-learn": copilot.MCPHTTPServerConfig{
        URL:   "https://learn.microsoft.com/api/mcp",
        Tools: []string{"*"},
    },
}

copilotAgent := copilotprovider.NewAgent(
    copilotClient,
    copilotprovider.AgentConfig{
        Instructions: "You are a helpful assistant with access to the filesystem and Microsoft Learn.",
        SessionConfig: &copilot.SessionConfig{
            OnPermissionRequest: promptPermission,
            MCPServers:          mcpServers,
        },
    },
)

response, err := copilotAgent.RunText(ctx, "Search Microsoft Learn for 'Azure Functions' and summarize the top result").Collect()
if err != nil {
    panic(err)
}
fmt.Println(response)
```

> [!TIP]
> See the [Go GitHub Copilot sample](https://github.com/microsoft/agent-framework-go/tree/main/examples/02-agents/providers/github-copilot/main.go) for a complete runnable example.

## Tools

| Tool | Status | Notes |
|---|---|---|
| [Function Tools](../../../agents/tools/function-tools.md) | ✅ | Standard Go `tool.Tool` instances, including `functool` functions. |
| [Tool Approval](../../../agents/tools/tool-approval.md) | ✅ | Function tools can use the standard Go tool approval support; Copilot runtime permissions are handled by `SessionConfig.OnPermissionRequest`. |
| [Code Interpreter](../../../agents/tools/code-interpreter.md) | ❌ | Not a Copilot CLI capability. |
| [File Search](../../../agents/tools/file-search.md) | ❌ | Not a Copilot CLI capability. |
| [Web Search](../../../agents/tools/web-search.md) | ❌ | Not exposed as a hosted tool. |
| Shell / file system / URL fetching | ✅ | Built into the Copilot CLI runtime and gated by the [Permissions](#permissions-2) handler you provide. |
| [Hosted MCP Tools](../../../agents/tools/hosted-mcp-tools.md) | ✅ | Remote (HTTP) MCP servers configured via `copilot.SessionConfig.MCPServers`. See [MCP Servers](#mcp-servers-2). |
| [Local MCP Tools](../../../agents/tools/local-mcp-tools.md) | ✅ | Local (stdio) MCP servers configured via `copilot.SessionConfig.MCPServers`. See [MCP Servers](#mcp-servers-2). |

## Using the Agent

The agent is a standard `*agent.Agent` and supports all standard agent operations.

For more information on how to run and interact with agents, see the [Agent getting started tutorials](../../../get-started/your-first-agent.md).

::: zone-end
## Next steps

> [!div class="nextstepaction"]
> [Copilot Studio](./copilot-studio.md)
