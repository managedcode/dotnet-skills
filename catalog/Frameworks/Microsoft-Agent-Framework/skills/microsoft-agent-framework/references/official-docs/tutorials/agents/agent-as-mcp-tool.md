---
title: Exposing an agent as an MCP tool
description: Learn how to expose an agent as a tool over the MCP protocol
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: tutorial
ms.author: westey
ms.date: 09/24/2025
ms.service: agent-framework
---

# Expose an agent as an MCP tool

::: zone pivot="programming-language-csharp"

This tutorial shows you how to expose an agent as a tool over the Model Context Protocol (MCP), so it can be used by other systems that support MCP tools.

## Prerequisites

For prerequisites see the [Create and run a simple agent](./run-agent.md#prerequisites) step in this tutorial.

## Install NuGet packages

To use Microsoft Agent Framework with Azure OpenAI, you need to install the following NuGet packages:

```dotnetcli
dotnet add package Azure.AI.OpenAI --prerelease
dotnet add package Azure.Identity
dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
```

To also add support for hosting a tool over the Model Context Protocol (MCP), add the following NuGet packages

```dotnetcli
dotnet add package Microsoft.Extensions.Hosting --prerelease
dotnet add package ModelContextProtocol --prerelease
```

## Expose an agent as an MCP tool

You can expose an `AIAgent` as an MCP tool by wrapping it in a function and using `McpServerTool`. You then need to register it with an MCP server. This allows the agent to be invoked as a tool by any MCP-compatible client.

First, create an agent that you'll expose as an MCP tool.

```csharp
using System;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using OpenAI;

AIAgent agent = new AzureOpenAIClient(
    new Uri("https://<myresource>.openai.azure.com"),
    new AzureCliCredential())
        .GetChatClient("gpt-4o-mini")
        .AsAIAgent(instructions: "You are good at telling jokes.", name: "Joker");
```

Turn the agent into a function tool and then an MCP tool. The agent name and description will be used as the mcp tool name and description.

```csharp
using ModelContextProtocol.Server;

McpServerTool tool = McpServerTool.Create(agent.AsAIFunction());
```

Setup the MCP server to listen for incoming requests over standard input/output and expose the MCP tool:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(settings: null);
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools([tool]);

await builder.Build().RunAsync();
```

This will start an MCP server that exposes the agent as a tool over the MCP protocol.

::: zone-end
::: zone pivot="programming-language-python"

This tutorial shows you how to expose an agent as a tool over the Model Context Protocol (MCP), so it can be used by other systems that support MCP tools.

## Prerequisites

For prerequisites and installing Python packages, see the [Create and run a simple agent](./run-agent.md) step in this tutorial.

## Expose an agent as an MCP server

You can expose an agent as an MCP server by using the `as_mcp_server()` method. This allows the agent to be invoked as a tool by any MCP-compatible client.

First, create an agent that you'll expose as an MCP server. You can also add tools to the agent:

```python
from typing import Annotated
from agent_framework.openai import OpenAIResponsesClient

def get_specials() -> Annotated[str, "Returns the specials from the menu."]:
    return """
        Special Soup: Clam Chowder
        Special Salad: Cobb Salad
        Special Drink: Chai Tea
        """

def get_item_price(
    menu_item: Annotated[str, "The name of the menu item."],
) -> Annotated[str, "Returns the price of the menu item."]:
    return "$9.99"

# Create an agent with tools
agent = OpenAIResponsesClient().as_agent(
    name="RestaurantAgent",
    description="Answer questions about the menu.",
    tools=[get_specials, get_item_price],
)
```

Turn the agent into an MCP server. The agent name and description will be used as the MCP server metadata:

```python
# Expose the agent as an MCP server
server = agent.as_mcp_server()
```

Setup the MCP server to listen for incoming requests over standard input/output:

```python
import anyio
from mcp.server.stdio import stdio_server

async def run():
    async def handle_stdin():
        async with stdio_server() as (read_stream, write_stream):
            await server.run(read_stream, write_stream, server.create_initialization_options())

    await handle_stdin()

if __name__ == "__main__":
    anyio.run(run)
```

This will start an MCP server that exposes the agent over the MCP protocol, allowing it to be used by MCP-compatible clients like VS Code GitHub Copilot Agents.

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Enabling observability for agents](./enable-observability.md)
