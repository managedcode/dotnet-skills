---
title: Using MCP Tools
description: Using MCP tools with agents
zone_pivot_groups: programming-languages
author: moonbox3
ms.topic: reference
ms.author: evmattso
ms.date: 07/30/2026
ms.service: agent-framework
---

# Using MCP tools with Agents

Model Context Protocol is an open standard that defines how applications provide tools and contextual data to large language models (LLMs). It enables consistent, scalable integration of external tools into model workflows.

Microsoft Agent Framework supports integration with Model Context Protocol (MCP) servers, allowing your agents to access external tools and services. This guide shows how to connect to an MCP server and use its tools within your agent.

## Considerations for using third-party MCP servers

Your use of Model Context Protocol servers is subject to the terms between you and the service provider. When you connect to a non-Microsoft service, some of your data (such as prompt content) is passed to the non-Microsoft service, or your application might receive data from the non-Microsoft service. You're responsible for your use of non-Microsoft services and data, along with any charges associated with that use.

The remote MCP servers that you decide to use with the MCP tool described in this article were created by third parties, not Microsoft. Microsoft hasn't tested or verified these servers. Microsoft has no responsibility to you or others in relation to your use of any remote MCP servers.

We recommend that you carefully review and track what MCP servers you add to your Agent Framework based applications. We also recommend that you rely on servers hosted by trusted service providers themselves rather than proxies.

The MCP tool allows you to pass custom headers, such as authentication keys or schemas, that a remote MCP server might need. We recommend that you review all data that's shared with remote MCP servers and that you log the data for auditing purposes. Be cognizant of non-Microsoft practices for retention and location of data.

> [!IMPORTANT]
> You can specify per-run headers by including them in tool resources at each run, or configure a `header_provider` on Python local MCP tools. Review any API keys, OAuth access tokens, or other credentials shared with remote MCP servers.

For more information on MCP security, see:

- [Security Best Practices](https://modelcontextprotocol.io/specification/draft/basic/security_best_practices) on the Model Context Protocol website.
- [Understanding and mitigating security risks in MCP implementations](https://techcommunity.microsoft.com/blog/microsoft-security-blog/understanding-and-mitigating-security-risks-in-mcp-implementations/4404667) in the Microsoft Security Community Blog.

::: zone pivot="programming-language-csharp"

The .NET version of Agent Framework can be used together with the [official MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) to allow your agent to call MCP tools.

The following sample shows how to:

1. Set up and MCP server
1. Retrieve the list of available tools from the MCP Server
1. Convert the MCP tools to `AIFunction`'s so they can be added to an agent
1. Invoke the tools from an agent using function calling

### Setting Up an MCP Client

First, create an MCP client that connects to your desired MCP server:

```csharp
// Create an MCPClient for the GitHub server
await using var mcpClient = await McpClientFactory.CreateAsync(new StdioClientTransport(new()
{
    Name = "MCPServer",
    Command = "npx",
    Arguments = ["-y", "--verbose", "@modelcontextprotocol/server-github"],
}));
```

In this example:

- **Name**: A friendly name for your MCP server connection
- **Command**: The executable to run the MCP server (here using npx to run a Node.js package)
- **Arguments**: Command-line arguments passed to the MCP server

### Retrieving Available Tools

Once connected, retrieve the list of tools available from the MCP server:

```csharp
// Retrieve the list of tools available on the GitHub server
var mcpTools = await mcpClient.ListToolsAsync().ConfigureAwait(false);
```

The `ListToolsAsync()` method returns a collection of tools that the MCP server exposes. These tools are automatically converted to AITool objects that can be used by your agent.

### Create an Agent with MCP Tools

Create your agent and provide the MCP tools during initialization:

```csharp
AIAgent agent = new AIProjectClient(
    new Uri(endpoint),
    new DefaultAzureCredential())
     .AsAIAgent(
         model: deploymentName,
         instructions: "You answer questions related to GitHub repositories only.",
         tools: [.. mcpTools.Cast<AITool>()]);

```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

Key points:

- **Instructions**: Provide clear instructions that align with the capabilities of your MCP tools
- **Tools**: Cast the MCP tools to `AITool` objects and spread them into the tools array
- The agent will automatically have access to all tools provided by the MCP server

### Using the Agent

Once configured, your agent can automatically use the MCP tools to fulfill user requests:

```csharp
// Invoke the agent and output the text result
Console.WriteLine(await agent.RunAsync("Summarize the last four commits to the microsoft/semantic-kernel repository?"));
```

The agent will:

1. Analyze the user's request
1. Determine which MCP tools are needed
1. Call the appropriate tools through the MCP server
1. Synthesize the results into a coherent response

### Environment Configuration

Make sure to set up the required environment variables:

```csharp
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ??
    throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-4o-mini";
```

### Resource Management

Always properly dispose of MCP client resources:

```csharp
await using var mcpClient = await McpClientFactory.CreateAsync(...);
```

Using `await using` ensures the MCP client connection is properly closed when it goes out of scope.

### Common MCP Servers

Popular MCP servers include:

- `@modelcontextprotocol/server-github`: Access GitHub repositories and data
- `@modelcontextprotocol/server-filesystem`: File system operations
- `@modelcontextprotocol/server-sqlite`: SQLite database access

Each server provides different tools and capabilities that extend your agent's functionality.
This integration allows your agents to seamlessly access external data and services while maintaining the security and standardization benefits of the Model Context Protocol.

> [!TIP]
> The full source code and instructions to run this sample is available at <https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/ModelContextProtocol/Agent_MCP_Server>.

::: zone-end
::: zone pivot="programming-language-python"

This allows your agents to access external tools and services seamlessly.

> [!NOTE]
> On minimal Python installs, MCP support might need to be installed manually. Install `mcp --pre` to use `MCPStdioTool`, `MCPStreamableHTTPTool`, or `Agent.as_mcp_server()`. Install `mcp[ws] --pre` if you also need `MCPWebsocketTool`.

## MCP Tool Types

The Agent Framework supports three types of MCP connections:

### MCPStdioTool - Local MCP Servers

Use `MCPStdioTool` to connect to MCP servers that run as local processes using standard input/output:

```python
import asyncio
from agent_framework import Agent, MCPStdioTool
from agent_framework.openai import OpenAIChatClient

async def local_mcp_example():
    """Example using a local MCP server via stdio."""
    async with (
        MCPStdioTool(
            name="calculator",
            command="uvx",
            args=["mcp-server-calculator"]
        ) as mcp_server,
        Agent(
            client=OpenAIChatClient(),
            name="MathAgent",
            instructions="You are a helpful math assistant that can solve calculations.",
        ) as agent,
    ):
        result = await agent.run(
            "What is 15 * 23 + 45?",
            tools=mcp_server
        )
        print(result)

if __name__ == "__main__":
    asyncio.run(local_mcp_example())
```

### MCPStreamableHTTPTool - HTTP/SSE MCP Servers

Use `MCPStreamableHTTPTool` to connect to MCP servers over HTTP with Server-Sent Events:

```python
import asyncio
from agent_framework import Agent, MCPStreamableHTTPTool
from agent_framework.foundry import FoundryChatClient
from azure.identity.aio import AzureCliCredential

async def http_mcp_example():
    """Example using an HTTP-based MCP server."""
    async with AzureCliCredential() as credential:
        client = FoundryChatClient(credential=credential)
        async with (
            MCPStreamableHTTPTool(
                name="Microsoft Learn MCP",
                url="https://learn.microsoft.com/api/mcp",
            ) as mcp_server,
            Agent(
                client=client,
                name="DocsAgent",
                instructions="You help with Microsoft documentation questions.",
            ) as agent,
        ):
            result = await agent.run(
                "How to create an Azure storage account using az cli?",
                tools=mcp_server
            )
            print(result)

if __name__ == "__main__":
    asyncio.run(http_mcp_example())
```

For authenticated HTTP endpoints, use `header_provider` so credentials are added only to same-origin requests. During a tool call, the provider receives the values from `function_invocation_kwargs`. For ambient requests such as the initialize handshake, tool or prompt discovery, and background pings, it receives an empty dictionary.

If the server requires authentication during connection, capture or refresh the required credential in the provider instead of depending only on per-run values. A provider that raises `KeyError` because a per-run value is unavailable lets an ambient request continue without that header; this pattern works only when the server permits unauthenticated initialization and discovery. Other provider errors are surfaced.

### MCPWebsocketTool - WebSocket MCP Servers

Use `MCPWebsocketTool` to connect to MCP servers over WebSocket connections:

```python
import asyncio
from agent_framework import Agent, MCPWebsocketTool
from agent_framework.openai import OpenAIChatClient

async def websocket_mcp_example():
    """Example using a WebSocket-based MCP server."""
    async with (
        MCPWebsocketTool(
            name="realtime-data",
            url="wss://api.example.com/mcp",
        ) as mcp_server,
        Agent(
            client=OpenAIChatClient(),
            name="DataAgent",
            instructions="You provide real-time data insights.",
        ) as agent,
    ):
        result = await agent.run(
            "What is the current market status?",
            tools=mcp_server
        )
        print(result)

if __name__ == "__main__":
    asyncio.run(websocket_mcp_example())
```

## Popular MCP Servers

Common MCP servers you can use with Python Agent Framework:

- **Calculator**: `uvx mcp-server-calculator` - Mathematical computations
- **Filesystem**: `uvx mcp-server-filesystem` - File system operations
- **GitHub**: `npx @modelcontextprotocol/server-github` - GitHub repository access
- **SQLite**: `uvx mcp-server-sqlite` - Database operations

Each server provides different tools and capabilities that extend your agent's functionality while maintaining the security and standardization benefits of the Model Context Protocol.

### Complete example

```python
# Copyright (c) Microsoft. All rights reserved.

import asyncio
import os

from agent_framework import Agent, MCPStreamableHTTPTool
from agent_framework.openai import OpenAIChatClient

"""
MCP Authentication Example

This example demonstrates a `header_provider` that authenticates both connection-time and tool-call requests.

For more authentication examples including OAuth 2.0 flows, see:
- https://github.com/modelcontextprotocol/python-sdk/tree/main/examples/clients/simple-auth-client
- https://github.com/modelcontextprotocol/python-sdk/tree/main/examples/servers/simple-auth
"""


async def api_key_auth_example() -> None:
    """Example of using API key authentication with MCP server."""
    mcp_server_url = os.getenv("MCP_SERVER_URL", "your-mcp-server-url")
    api_key = os.getenv("MCP_API_KEY")
    if not api_key:
        raise ValueError("MCP_API_KEY environment variable must be set.")

    async with Agent(
        client=OpenAIChatClient(),
        name="Agent",
        instructions="You are a helpful assistant.",
        tools=MCPStreamableHTTPTool(
            name="MCP tool",
            description="MCP tool description",
            url=mcp_server_url,
            header_provider=lambda _kwargs: {"Authorization": f"Bearer {api_key}"},
        ),
    ) as agent:
        query = "What tools are available to you?"
        print(f"User: {query}")
        result = await agent.run(query)
        print(f"Agent: {result.text}")

if __name__ == "__main__":
    asyncio.run(api_key_auth_example())
```

::: zone-end

::: zone pivot="programming-language-go"

## MCP Tool Types

The `mcptool` package lets agents use tools from Model Context Protocol (MCP) servers.

### Connect to an MCP server

```go
import (
    "github.com/microsoft/agent-framework-go/tool/mcptool"

    "github.com/modelcontextprotocol/go-sdk/mcp"
)

session, err := mcptool.Connect(ctx, &mcp.StreamableClientTransport{
    Endpoint: "https://learn.microsoft.com/api/mcp",
})
if err != nil {
    panic(err)
}
defer session.Close()
```

### List and use MCP tools

```go
tools, err := mcptool.ListTools(ctx, session)
if err != nil {
    panic(err)
}

a := foundryprovider.NewAgent(endpoint, token, foundryprovider.ModelDeployment(model), foundryprovider.AgentConfig{
    Instructions: "You are a helpful assistant.",
    Config: agent.Config{
        Tools: tools,
    },
})

resp, err := a.RunText(ctx, "How to create an Azure storage account using az cli?").Collect()
```

### Supported transports

- **HTTP/SSE** - `mcp.StreamableClientTransport{Endpoint: "https://..."}`
- **Stdio** - Launch a local MCP server process

> [!TIP]
> See the [MCP tools sample](https://github.com/microsoft/agent-framework-go/blob/main/examples/02-agents/mcp/agent_mcp_server/main.go) for a complete runnable example.

::: zone-end

## Exposing an Agent as an MCP Server

You can expose an agent as an MCP server, allowing it to be used as a tool by any MCP-compatible client (such as VS Code GitHub Copilot Agents or other agents). The agent's name and description become the MCP server metadata.

::: zone pivot="programming-language-csharp"

Wrap the agent in a function tool using `.AsAIFunction()`, create an `McpServerTool`, and register it with an MCP server:

```csharp
using System;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

// Create the agent
AIAgent agent = new AIProjectClient(
    new Uri("<your-foundry-project-endpoint>"),
    new DefaultAzureCredential())
        .AsAIAgent(
            model: "gpt-4o-mini",
            instructions: "You are good at telling jokes.",
            name: "Joker");

// Convert the agent to an MCP tool
McpServerTool tool = McpServerTool.Create(agent.AsAIFunction());

// Set up the MCP server over stdio
HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(settings: null);
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools([tool]);

await builder.Build().RunAsync();
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

Install the required NuGet packages:

```dotnetcli
dotnet add package Microsoft.Extensions.Hosting --prerelease
dotnet add package ModelContextProtocol --prerelease
```

::: zone-end
::: zone pivot="programming-language-python"

Call `.as_mcp_server()` on an agent to expose it as an MCP server:

> [!NOTE]
> Python `agent.as_mcp_server()` also depends on the optional `mcp` package. If you use a slim/core-based install, run `pip install mcp --pre` first.

```python
from agent_framework.openai import OpenAIChatClient
from typing import Annotated

def get_specials() -> Annotated[str, "Returns the specials from the menu."]:
    return "Special Soup: Clam Chowder, Special Salad: Cobb Salad"

# Create an agent with tools
agent = OpenAIChatClient().as_agent(
    name="RestaurantAgent",
    description="Answer questions about the menu.",
    tools=[get_specials],
)

# Expose the agent as an MCP server
server = agent.as_mcp_server()
```

Set up the MCP server to listen over standard input/output:

```python
import anyio
from mcp.server.stdio import stdio_server

async def run():
    async with stdio_server() as (read_stream, write_stream):
        await server.run(read_stream, write_stream, server.create_initialization_options())

if __name__ == "__main__":
    anyio.run(run)
```

::: zone-end

::: zone pivot="programming-language-go"

Wrap the agent with `agenttool.New`, register it with an MCP server using `mcptool.AddTool`, and run the server over stdio:

```go
import (
    "context"

    "github.com/microsoft/agent-framework-go/agent"
    "github.com/microsoft/agent-framework-go/provider/foundryprovider"
    "github.com/microsoft/agent-framework-go/tool/agenttool"
    "github.com/microsoft/agent-framework-go/tool/mcptool"
    "github.com/modelcontextprotocol/go-sdk/mcp"
)

jokeAgent := foundryprovider.NewAgent(endpoint, token, foundryprovider.ModelDeployment(model), foundryprovider.AgentConfig{
    Instructions: "You are good at telling jokes.",
    Config: agent.Config{
        Name:        "Joker",
        Description: "An agent that tells jokes.",
    },
})

server := mcp.NewServer(&mcp.Implementation{
    Name:    "agent-mcp-server",
    Version: "1.0.0",
}, nil)

mcptool.AddTool(server, agenttool.New(jokeAgent, agenttool.Config{}))

if err := server.Run(context.Background(), &mcp.StdioTransport{}); err != nil {
    panic(err)
}
```

> [!TIP]
> See the [agent as MCP tool sample](https://github.com/microsoft/agent-framework-go/blob/main/examples/02-agents/agents/step10_as_mcp_tool/main.go) for a complete runnable example.

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Conversations & Memory](../../concepts/agents/conversations/index.md)
