---
title: Web Search
description: Learn how to use the Web Search tool with Agent Framework agents.
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: reference
ms.author: edvan
ms.date: 07/01/2026
ms.service: agent-framework
---

# Web Search

Web Search allows agents to search the web for up-to-date information. This tool enables agents to answer questions about current events, find documentation, and access information beyond their training data.

> [!NOTE]
> Web Search availability depends on the underlying agent provider. See [Providers Overview](../../integrations/by-component/model-providers/index.md) for provider-specific support.

:::zone pivot="programming-language-csharp"

The following example shows how to create an agent with the Web Search tool:

```csharp
using System;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

// Requires: dotnet add package Microsoft.Agents.AI.Foundry --prerelease
var endpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is not set.");
var deploymentName = Environment.GetEnvironmentVariable("FOUNDRY_MODEL") ?? "gpt-5.4-mini";

// Create an agent with hosted web search.
AIAgent agent = new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential())
    .AsAIAgent(
        model: deploymentName,
        instructions: "You are a helpful assistant that can search the web for current information.",
        tools: [new HostedWebSearchTool()]);

Console.WriteLine(await agent.RunAsync("What is the current weather in Seattle?"));
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

:::zone-end

:::zone pivot="programming-language-python"

The following example shows how to create an agent with the Web Search tool:

```python
# Copyright (c) Microsoft. All rights reserved.

import asyncio

from agent_framework import Agent
from agent_framework.openai import OpenAIChatClient

"""
OpenAI Responses Client with Web Search Example

This sample demonstrates using get_web_search_tool() with OpenAI Responses Client
for direct real-time information retrieval and current data access.
"""


async def main() -> None:
    client = OpenAIChatClient()

    # Create web search tool with location context
    web_search_tool = client.get_web_search_tool(
        user_location={"city": "Seattle", "country": "US"},
    )

    agent = Agent(
        client=client,
        instructions="You are a helpful assistant that can search the web for current information.",
        tools=[web_search_tool],
    )

    message = "What is the current weather? Do not ask for my current location."
    stream = False
    print(f"User: {message}")

    if stream:
        print("Assistant: ", end="")
        async for chunk in agent.run(message, stream=True):
            if chunk.text:
                print(chunk.text, end="")
        print("")
    else:
        response = await agent.run(message)
        print(f"Assistant: {response}")


if __name__ == "__main__":
    asyncio.run(main())
```

:::zone-end

:::zone pivot="programming-language-go"
## Web search

The `hostedtool.WebSearch` type enables server-side web search when using a provider that supports it.

```go
import "github.com/microsoft/agent-framework-go/tool/hostedtool"

webSearch := &hostedtool.WebSearch{}

a := foundryprovider.NewAgent(endpoint, token, foundryprovider.ModelDeployment(model), foundryprovider.AgentConfig{
    Config: agent.Config{
        Tools: []tool.Tool{webSearch},
    },
})
```

> [!NOTE]
> Web search is a hosted tool — the search is performed by the AI service, not locally.

:::zone-end

<a id="use-web-search-with-harnessed-agent"></a>

## Use web search with Harness Agent

:::zone pivot="programming-language-csharp"

For a plain agent, add `HostedWebSearchTool` to the agent's tools, as shown earlier. `HarnessAgent` adds one `HostedWebSearchTool` by default, so no tool registration is required:

```csharp
using Microsoft.Agents.AI;

AIAgent agent = chatClient.AsHarnessAgent(new HarnessAgentOptions
{
    ChatOptions = new()
    {
        Instructions = "Use web search for current information and cite the sources you used.",
    },
});
```

Set `DisableWebSearch = true` when the selected provider doesn't support hosted web search or when you want to register a provider-specific search tool yourself through `ChatOptions.Tools`. If you add your own web-search tool without disabling the default, the agent receives both tools.

Web search is hosted by the model provider; there is no local search-client lifecycle for the Harness to manage. Availability, supported models, search parameters, data residency, and billing depend on the `IChatClient` provider. Unsupported clients can reject the hosted tool when the request is sent.

Treat search queries and results as data crossing an external trust boundary. Don't include secrets in queries, and treat retrieved pages as untrusted content that can contain indirect prompt injection. Verify important claims and citations before taking actions.

`HarnessAgent` is available from the `Microsoft.Agents.AI.Harness` package.

:::zone-end

:::zone pivot="programming-language-python"

For a plain agent, call `client.get_web_search_tool(...)` and pass the returned tool to `Agent`, as shown earlier. `create_harness_agent` calls `client.get_web_search_tool()` with no arguments by default when the client implements `SupportsWebSearchTool`:

```python
from agent_framework import create_harness_agent

agent = create_harness_agent(client=client)
```

If the client doesn't implement `SupportsWebSearchTool`, the factory logs a warning and continues without web search. Set `disable_web_search=True` to suppress automatic registration and the warning.

To pass provider-specific settings, disable the default and register the configured tool explicitly:

```python
agent = create_harness_agent(
    client=client,
    disable_web_search=True,
    tools=[
        client.get_web_search_tool(
            user_location={"city": "Seattle", "country": "US"},
            search_context_size="medium",
        )
    ],
)
```

The provider owns hosted-search execution and lifecycle. Supported parameters, models, data handling, and billing depend on the client implementation. Don't put secrets in queries, treat retrieved content as untrusted input, and verify important claims and citations before taking actions.

`create_harness_agent` is released in `agent-framework-core`; web search remains available only through clients that implement `SupportsWebSearchTool`.

:::zone-end

:::zone pivot="programming-language-go"

A packaged Go Harness isn't currently available. Add `hostedtool.WebSearch` to a plain Go agent as shown earlier.

:::zone-end

## Next steps

> [!div class="nextstepaction"]
> [Hosted MCP Tools](./hosted-mcp-tools.md)
