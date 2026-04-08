---
title: Using MCP Tools
description: Using MCP tools with agents
zone_pivot_groups: programming-languages
author: markwallace
ms.topic: reference
ms.author: markwallace
ms.date: 09/24/2025
ms.service: agent-framework
---

# Using MCP tools with Agents

Microsoft Agent Framework supports integration with Model Context Protocol (MCP) servers, allowing your agents to access external tools and services. This guide shows how to connect to an MCP server and use its tools within your agent.

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
AIAgent agent = new AzureOpenAIClient(
    new Uri(endpoint),
    new AzureCliCredential())
     .GetChatClient(deploymentName)
     .AsAIAgent(
         instructions: "You answer questions related to GitHub repositories only.",
         tools: [.. mcpTools.Cast<AITool>()]);

```

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

The full source code and instructions to run this sample is available at <https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/GettingStarted/ModelContextProtocol/Agent_MCP_Server>.

::: zone-end
::: zone pivot="programming-language-python"

The Python Agent Framework provides comprehensive support for integrating with Model Context Protocol (MCP) servers through multiple connection types. This allows your agents to access external tools and services seamlessly.

## MCP Tool Types

The Agent Framework supports three types of MCP connections:

### MCPStdioTool - Local MCP Servers

Use `MCPStdioTool` to connect to MCP servers that run as local processes using standard input/output:

```python
import asyncio
from agent_framework import ChatAgent, MCPStdioTool
from agent_framework.openai import OpenAIChatClient

async def local_mcp_example():
    """Example using a local MCP server via stdio."""
    async with (
        MCPStdioTool(
            name="calculator",
            command="uvx",
            args=["mcp-server-calculator"]
        ) as mcp_server,
        ChatAgent(
            chat_client=OpenAIChatClient(),
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
from agent_framework import ChatAgent, MCPStreamableHTTPTool
from agent_framework.azure import AzureAIAgentClient
from azure.identity.aio import AzureCliCredential

async def http_mcp_example():
    """Example using an HTTP-based MCP server."""
    async with (
        AzureCliCredential() as credential,
        MCPStreamableHTTPTool(
            name="Microsoft Learn MCP",
            url="https://learn.microsoft.com/api/mcp",
            headers={"Authorization": "Bearer your-token"},
        ) as mcp_server,
        ChatAgent(
            chat_client=AzureAIAgentClient(async_credential=credential),
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

### MCPWebsocketTool - WebSocket MCP Servers

Use `MCPWebsocketTool` to connect to MCP servers over WebSocket connections:

```python
import asyncio
from agent_framework import ChatAgent, MCPWebsocketTool
from agent_framework.openai import OpenAIChatClient

async def websocket_mcp_example():
    """Example using a WebSocket-based MCP server."""
    async with (
        MCPWebsocketTool(
            name="realtime-data",
            url="wss://api.example.com/mcp",
        ) as mcp_server,
        ChatAgent(
            chat_client=OpenAIChatClient(),
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

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Using workflows as Agents](../workflows/as-agents.md)
