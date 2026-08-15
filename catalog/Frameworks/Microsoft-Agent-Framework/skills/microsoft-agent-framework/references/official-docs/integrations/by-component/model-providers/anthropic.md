---
title: Anthropic
description: Learn how to use the Microsoft Agent Framework with Anthropic's Claude models.
zone_pivot_groups: programming-languages
author:  rogerbarreto
ms.topic: tutorial
ms.author: rbarreto
ms.date: 07/28/2026
ms.service: agent-framework
---

# Anthropic

The Microsoft Agent Framework supports creating agents that use [Anthropic's Claude models](https://www.anthropic.com/claude).

## Direct model inference vs. the Claude Agent SDK

Anthropic support in Agent Framework has two distinct forms.

| Integration | Type | Agent loop and tools | Use when |
|---|---|---|---|
| Direct model inference (this page) | `AnthropicClient` and provider-hosted variants, wrapped with `Agent(client=...)` | Your application owns the Agent Framework loop, sessions, middleware, function tools, and supported Anthropic hosted tools. | You want Claude as the model behind a standard application-owned Agent Framework agent. |
| [Anthropic Claude Agent SDK](../agent-services/anthropic-claude.md) | `ClaudeAgent`, constructed directly | Claude's coding-agent runtime owns sessions, permissions, built-in file and shell tools, and MCP behavior. | You want Claude's managed coding-agent runtime and permission model. |

::: zone pivot="programming-language-csharp"

## Getting Started

Add the required NuGet packages to your project.

```powershell
dotnet add package Microsoft.Agents.AI.Anthropic --prerelease
```

If you're using Microsoft Foundry, also add:

```powershell
dotnet add package Anthropic.Foundry --prerelease
dotnet add package Azure.Identity
```

## Configuration

### Environment Variables

Set up the required environment variables for Anthropic authentication:

```powershell
# Required for Anthropic API access
$env:ANTHROPIC_API_KEY="your-anthropic-api-key"
$env:ANTHROPIC_CHAT_MODEL_NAME="claude-haiku-4-5"  # or your preferred model
```

You can get an API key from the [Anthropic Console](https://console.anthropic.com/).

### For Microsoft Foundry with API Key

```powershell
$env:ANTHROPIC_RESOURCE="your-foundry-resource-name"  # Subdomain before .services.ai.azure.com
$env:ANTHROPIC_API_KEY="your-anthropic-api-key"
$env:ANTHROPIC_CHAT_MODEL_NAME="claude-haiku-4-5"
```

### For Microsoft Foundry with Azure CLI

```powershell
$env:ANTHROPIC_RESOURCE="your-foundry-resource-name"  # Subdomain before .services.ai.azure.com
$env:ANTHROPIC_CHAT_MODEL_NAME="claude-haiku-4-5"
```

> [!NOTE]
> When using Microsoft Foundry with Azure CLI, make sure you're logged in with `az login` and have access to the Foundry resource. For more information, see the [Azure CLI documentation](/cli/azure/authenticate-azure-cli-interactively).

## Creating an Anthropic Agent

### Basic Agent Creation (Anthropic Public API)

The simplest way to create an Anthropic agent using the public API:

```csharp
var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
var deploymentName = Environment.GetEnvironmentVariable("ANTHROPIC_CHAT_MODEL_NAME") ?? "claude-haiku-4-5";

AnthropicClient client = new() { ApiKey = apiKey };

AIAgent agent = client.AsAIAgent(
    model: deploymentName,
    name: "HelpfulAssistant",
    instructions: "You are a helpful assistant.");

// Invoke the agent and output the text result.
Console.WriteLine(await agent.RunAsync("Hello, how can you help me?"));
```

### Using Anthropic on Foundry

After you've set up Anthropic on Microsoft Foundry, you can use it with API key authentication:

#### API key authentication

```csharp
var resource = Environment.GetEnvironmentVariable("ANTHROPIC_RESOURCE");
var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
var deploymentName = Environment.GetEnvironmentVariable("ANTHROPIC_CHAT_MODEL_NAME") ?? "claude-haiku-4-5";

AnthropicClient client = new AnthropicFoundryClient(
    new AnthropicFoundryApiKeyCredentials(apiKey, resource));

AIAgent agent = client.AsAIAgent(
    model: deploymentName,
    name: "FoundryAgent",
    instructions: "You are a helpful assistant using Anthropic on Microsoft Foundry.");

Console.WriteLine(await agent.RunAsync("How do I use Anthropic on Foundry?"));
```

#### Azure credential authentication

For environments where Azure Credentials are preferred:

```csharp
var resource = Environment.GetEnvironmentVariable("ANTHROPIC_RESOURCE");
var deploymentName = Environment.GetEnvironmentVariable("ANTHROPIC_CHAT_MODEL_NAME") ?? "claude-haiku-4-5";

AnthropicClient client = new AnthropicFoundryClient(
    new AnthropicFoundryIdentityTokenCredentials(
        new DefaultAzureCredential(),
        resource,
        ["https://ai.azure.com/.default"]));

AIAgent agent = client.AsAIAgent(
    model: deploymentName,
    name: "FoundryAgent",
    instructions: "You are a helpful assistant using Anthropic on Microsoft Foundry.");

Console.WriteLine(await agent.RunAsync("How do I use Anthropic on Foundry?"));
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

> [!TIP]
> See the [.NET samples](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples) for complete runnable examples.

## Tools

| Tool | Status | Notes |
|---|---|---|
| [Function Tools](../../../agents/tools/function-tools.md) | ✅ | Standard `AIFunction` instances via `AIFunctionFactory.Create(...)`. |
| [Tool Approval](../../../agents/tools/tool-approval.md) | ✅ | Provided by the function-invoking chat client; works with any function-tool call. |
| [Code Interpreter](../../../agents/tools/code-interpreter.md) | ❌ | Not supported by the .NET Anthropic client today. |
| [File Search](../../../agents/tools/file-search.md) | ❌ | Not supported. |
| [Web Search](../../../agents/tools/web-search.md) | ❌ | Not supported by the .NET Anthropic client today. |
| [Hosted MCP Tools](../../../agents/tools/hosted-mcp-tools.md) | ✅ | Supported. |
| [Local MCP Tools](../../../agents/tools/local-mcp-tools.md) | ✅ | Supported. |

## Extended thinking

Configure Anthropic reasoning through the raw message representation and consume `TextReasoningContent` from regular or streaming responses.

:::code language="csharp" source="~/../agent-framework-code/dotnet/samples/02-agents/AgentProviders/anthropic/Agent_Anthropic_Step02_Reasoning/Program.cs" range="11-59":::

## Anthropic Skills

Anthropic-managed skills can create files through the hosted code-execution environment. The sample lists available skills, configures the PowerPoint skill, and downloads the generated file.

:::code language="csharp" source="~/../agent-framework-code/dotnet/samples/02-agents/AgentProviders/anthropic/Agent_Anthropic_Step04_UsingSkills/Program.cs" range="20-80,93-118":::

## Using the Agent

The agent is a standard `AIAgent` and supports all standard agent operations.

See the [Agent getting started tutorials](../../../get-started/your-first-agent.md) for more information on how to run and interact with agents.

::: zone-end
::: zone pivot="programming-language-python"

## Prerequisites

Install the Microsoft Agent Framework Anthropic package.

```bash
pip install agent-framework-anthropic --pre
```

## Configuration

### Environment Variables

Set up the required environment variables for Anthropic authentication:

```bash
# Required for Anthropic API access
ANTHROPIC_API_KEY="your-anthropic-api-key"
ANTHROPIC_CHAT_MODEL="claude-sonnet-4-5-20250929"  # or your preferred model

# Optional: override the Anthropic API endpoint (e.g. for Foundry-compatible deployments)
ANTHROPIC_BASE_URL="https://your-custom-endpoint.com"
```

Alternatively, you can use a `.env` file in your project root:

```env
ANTHROPIC_API_KEY=your-anthropic-api-key
ANTHROPIC_CHAT_MODEL=claude-sonnet-4-5-20250929
# ANTHROPIC_BASE_URL=https://your-custom-endpoint.com  # optional
```

You can get an API key from the [Anthropic Console](https://console.anthropic.com/).

## Getting Started

Import the required classes from the Agent Framework:

```python
import asyncio
from agent_framework import Agent
from agent_framework.anthropic import AnthropicClient
```

## Creating an Anthropic Agent

### Basic Agent Creation

The simplest way to create an Anthropic agent:

```python
from agent_framework import Agent

async def basic_example():
    # Create an agent using Anthropic
    agent = Agent(
        client=AnthropicClient(),
        name="HelpfulAssistant",
        instructions="You are a helpful assistant.",
    )

    result = await agent.run("Hello, how can you help me?")
    print(result.text)
```

### Using Explicit Configuration

You can provide explicit configuration instead of relying on environment variables:

```python
from agent_framework import Agent

async def explicit_config_example():
    agent = Agent(
        client=AnthropicClient(
            model="claude-sonnet-4-5-20250929",
            api_key="your-api-key-here",
        ),
        name="HelpfulAssistant",
        instructions="You are a helpful assistant.",
    )

    result = await agent.run("What can you do?")
    print(result.text)
```

### Using a Custom Base URL

Pass `base_url` directly to `AnthropicClient` to point it at any Anthropic-compatible endpoint, such as a Foundry-hosted deployment. This lets you keep the same `AnthropicClient` code and only change the endpoint, rather than switching to `AnthropicFoundryClient`:

```python
from agent_framework import Agent

async def custom_base_url_example():
    agent = Agent(
        client=AnthropicClient(
            model="claude-haiku-4-5",
            api_key="your-api-key-here",
            base_url="https://your-foundry-resource.services.ai.azure.com/models/anthropic",
        ),
        name="HelpfulAssistant",
        instructions="You are a helpful assistant.",
    )

    result = await agent.run("What can you do?")
    print(result.text)
```

`base_url` falls back to the `ANTHROPIC_BASE_URL` environment variable when not passed explicitly.

### Using Anthropic on Foundry

After you've setup Anthropic on Foundry, ensure you have the following environment variables set:

```bash
ANTHROPIC_FOUNDRY_API_KEY="your-foundry-api-key"
ANTHROPIC_FOUNDRY_RESOURCE="your-foundry-resource-name"
ANTHROPIC_CHAT_MODEL="claude-haiku-4-5"
```
Then create the agent as follows:

```python
from agent_framework import Agent
from agent_framework.anthropic import AnthropicFoundryClient

async def foundry_example():
    agent = Agent(
        client=AnthropicFoundryClient(),
        name="FoundryAgent",
        instructions="You are a helpful assistant using Anthropic on Foundry.",
    )

    result = await agent.run("How do I use Anthropic on Foundry?")
    print(result.text)
```

> [!NOTE]
> If you prefer configuring a full Anthropic-compatible endpoint instead of a resource name, set `ANTHROPIC_FOUNDRY_BASE_URL` in addition to `ANTHROPIC_FOUNDRY_API_KEY`.

### Using Anthropic on Amazon Bedrock

`AnthropicBedrockClient` routes Claude model inference through Amazon Bedrock.

```bash
AWS_ACCESS_KEY_ID="<access-key>"
AWS_SECRET_ACCESS_KEY="<secret-key>"
AWS_REGION="us-east-1"
# Optional:
AWS_PROFILE="<profile>"
AWS_SESSION_TOKEN="<session-token>"
ANTHROPIC_BEDROCK_BASE_URL="<custom-endpoint>"
ANTHROPIC_CHAT_MODEL="anthropic.claude-3-5-sonnet-20241022-v2:0"
```

No runnable Agent Framework sample is currently published for `AnthropicBedrockClient`.

### Using Anthropic on Google Vertex AI

`AnthropicVertexClient` routes Claude model inference through Google Vertex AI.

```bash
CLOUD_ML_REGION="us-east5"
ANTHROPIC_VERTEX_PROJECT_ID="<google-cloud-project>"
ANTHROPIC_CHAT_MODEL="claude-sonnet-4@20250514"
# Optional:
ANTHROPIC_VERTEX_BASE_URL="<custom-endpoint>"
```

No runnable Agent Framework sample is currently published for `AnthropicVertexClient`.

## Tools

`AnthropicClient` exposes hosted Anthropic tool factories alongside standard function tool support. Use `client.get_*_tool(...)` to build a tool and pass it through `tools=` on `Agent(...)`.

| Tool | Factory / construction | Status | Notes |
|---|---|---|---|
| [Function Tools](../../../agents/tools/function-tools.md) | Pass any Python callable or `@ai_function` | ✅ | Invoked locally in your Python process. |
| [Tool Approval](../../../agents/tools/tool-approval.md) | Handled by the framework's function-invoking chat client | ✅ | Works with any function-tool call. |
| [Code Interpreter](../../../agents/tools/code-interpreter.md) | `client.get_code_interpreter_tool()` | ✅ | Required for [Anthropic Skills](#anthropic-skills). |
| [File Search](../../../agents/tools/file-search.md) | n/a | ❌ | Not exposed by the Anthropic API. |
| [Web Search](../../../agents/tools/web-search.md) | `client.get_web_search_tool()` | ✅ | Hosted Anthropic web search. |
| [Hosted MCP Tools](../../../agents/tools/hosted-mcp-tools.md) | `client.get_mcp_tool(name=..., url=...)` | ✅ | Remote MCP servers invoked by Anthropic. |
| [Local MCP Tools](../../../agents/tools/local-mcp-tools.md) | `MCPStreamableHTTPTool` / `MCPStdioTool` | ✅ | Runs in your process. |

For richer examples — combining hosted MCP, web search, extended thinking, and Anthropic Skills — see [Hosted Tools](#hosted-tools) below.

## Agent Features

```python
from typing import Annotated

def get_weather(
    location: Annotated[str, "The location to get the weather for."],
) -> str:
    """Get the weather for a given location."""
    conditions = ["sunny", "cloudy", "rainy", "stormy"]
    return f"The weather in {location} is {conditions[randint(0, 3)]} with a high of {randint(10, 30)}°C."

from agent_framework import Agent

async def tools_example():
    agent = Agent(
        client=AnthropicClient(),
        name="WeatherAgent",
        instructions="You are a helpful weather assistant.",
        tools=get_weather,  # Add tools to the agent
    )

    result = await agent.run("What's the weather like in Seattle?")
    print(result.text)
```

### Streaming Responses

Get responses as they are generated for better user experience:

```python
from agent_framework import Agent

async def streaming_example():
    agent = Agent(
        client=AnthropicClient(),
        name="WeatherAgent",
        instructions="You are a helpful weather agent.",
        tools=get_weather,
    )

    query = "What's the weather like in Portland and in Paris?"
    print(f"User: {query}")
    print("Agent: ", end="", flush=True)
    async for chunk in agent.run(query, stream=True):
        if chunk.text:
            print(chunk.text, end="", flush=True)
    print()
```

### Hosted Tools

Anthropic agents support hosted tools such as web search, MCP (Model Context Protocol), and code execution:

```python
from agent_framework import Agent
from agent_framework.anthropic import AnthropicClient

async def hosted_tools_example():
    client = AnthropicClient()
    agent = Agent(
        client=client,
        name="DocsAgent",
        instructions="You are a helpful agent for both Microsoft docs questions and general questions.",
        tools=[
            client.get_mcp_tool(
                name="Microsoft Learn MCP",
                url="https://learn.microsoft.com/api/mcp",
            ),
            client.get_web_search_tool(),
        ],
        default_options={"max_tokens": 20000},
    )

    result = await agent.run("Can you compare Python decorators with C# attributes?")
    print(result.text)
```

### Extended Thinking (Reasoning)

Anthropic supports extended thinking capabilities through the `thinking` feature, which allows the model to show its reasoning process:

```python
from agent_framework import Agent
from agent_framework.anthropic import AnthropicClient

async def thinking_example():
    client = AnthropicClient()
    agent = Agent(
        client=client,
        name="DocsAgent",
        instructions="You are a helpful agent.",
        tools=[client.get_web_search_tool()],
        default_options={
            "max_tokens": 20000,
            "thinking": {"type": "enabled", "budget_tokens": 10000}
        },
    )

    query = "Can you compare Python decorators with C# attributes?"
    print(f"User: {query}")
    print("Agent: ", end="", flush=True)

    async for chunk in agent.run(query, stream=True):
        for content in chunk.contents:
            if content.type == "text_reasoning":
                # Display thinking in a different color
                print(f"\033[32m{content.text}\033[0m", end="", flush=True)
            if content.type == "usage":
                print(f"\n\033[34m[Usage: {content.usage_details}]\033[0m\n", end="", flush=True)
        if chunk.text:
            print(chunk.text, end="", flush=True)
    print()
```

### Anthropic Skills

Anthropic provides managed skills that extend agent capabilities, such as creating PowerPoint presentations. Skills require the Code Interpreter tool to function:

```python
from agent_framework import Agent, Content
from agent_framework.anthropic import AnthropicClient

async def skills_example():
    # Create client with skills beta flag
    client = AnthropicClient(additional_beta_flags=["skills-2025-10-02"])

    # Create an agent with the pptx skill enabled
    # Skills require the Code Interpreter tool
    agent = Agent(
        client=client,
        name="PresentationAgent",
        instructions="You are a helpful agent for creating PowerPoint presentations.",
        tools=client.get_code_interpreter_tool(),
        default_options={
            "max_tokens": 20000,
            "thinking": {"type": "enabled", "budget_tokens": 10000},
            "container": {
                "skills": [{"type": "anthropic", "skill_id": "pptx", "version": "latest"}]
            },
        },
    )

    query = "Create a presentation about renewable energy with 5 slides"
    print(f"User: {query}")
    print("Agent: ", end="", flush=True)

    files: list[Content] = []
    async for chunk in agent.run(query, stream=True):
        for content in chunk.contents:
            match content.type:
                case "text":
                    print(content.text, end="", flush=True)
                case "text_reasoning":
                    print(f"\033[32m{content.text}\033[0m", end="", flush=True)
                case "hosted_file":
                    # Catch generated files
                    files.append(content)

    print("\n")

    # Download generated files
    if files:
        print("Generated files:")
        for idx, file in enumerate(files):
            file_content = await client.anthropic_client.beta.files.download(
                file_id=file.file_id,
                betas=["files-api-2025-04-14"]
            )
            filename = f"presentation-{idx}.pptx"
            with open(filename, "wb") as f:
                await file_content.write_to_file(f.name)
            print(f"File {idx}: {filename} saved to disk.")
```

### Complete example

```python
# Copyright (c) Microsoft. All rights reserved.

import asyncio
from random import randint
from typing import Annotated

from agent_framework import Agent, tool
from agent_framework.anthropic import AnthropicClient

"""
Anthropic Chat Agent Example

This sample demonstrates using Anthropic with an agent and a single custom tool.
"""


# NOTE: approval_mode="never_require" is for sample brevity. Use "always_require" in production; see samples/02-agents/tools/function_tool_with_approval.py and samples/02-agents/tools/function_tool_with_approval_and_sessions.py.
@tool(approval_mode="never_require")
def get_weather(
    location: Annotated[str, "The location to get the weather for."],
) -> str:
    """Get the weather for a given location."""
    conditions = ["sunny", "cloudy", "rainy", "stormy"]
    return f"The weather in {location} is {conditions[randint(0, 3)]} with a high of {randint(10, 30)}°C."


async def non_streaming_example() -> None:
    """Example of non-streaming response (get the complete result at once)."""
    print("=== Non-streaming Response Example ===")

    agent = Agent(
        client=AnthropicClient(),
        name="WeatherAgent",
        instructions="You are a helpful weather agent.",
        tools=get_weather,
    )

    query = "What's the weather like in Seattle?"
    print(f"User: {query}")
    result = await agent.run(query)
    print(f"Result: {result}\n")


async def streaming_example() -> None:
    """Example of streaming response (get results as they are generated)."""
    print("=== Streaming Response Example ===")

    agent = Agent(
        client=AnthropicClient(),
        name="WeatherAgent",
        instructions="You are a helpful weather agent.",
        tools=get_weather,
    )

    query = "What's the weather like in Portland and in Paris?"
    print(f"User: {query}")
    print("Agent: ", end="", flush=True)
    async for chunk in agent.run(query, stream=True):
        if chunk.text:
            print(chunk.text, end="", flush=True)
    print("\n")


async def main() -> None:
    print("=== Anthropic Example ===")

    await streaming_example()
    await non_streaming_example()


if __name__ == "__main__":
    asyncio.run(main())
```

## Using the Agent

The agent is a standard `Agent` and supports all standard agent operations.

See the [Agent getting started tutorials](../../../get-started/your-first-agent.md) for more information on how to run and interact with agents.

::: zone-end

::: zone pivot="programming-language-go"
## Anthropic

The `anthropicprovider` package creates agents using the Anthropic API.

### Installation

```bash
go get github.com/microsoft/agent-framework-go
```

### Create an Anthropic agent

```go
import (
    "github.com/microsoft/agent-framework-go/agent"
    "github.com/microsoft/agent-framework-go/provider/anthropicprovider"

    "github.com/anthropics/anthropic-sdk-go"
)

a := anthropicprovider.NewAgent(
    anthropic.NewClient(), // uses ANTHROPIC_API_KEY env var
    anthropicprovider.AgentConfig{
        Model: "claude-sonnet-4-5",
        Instructions: "You are a helpful assistant.",
        Config: agent.Config{
            Name:         "ClaudeAgent",
        },
    },
)

resp, err := a.RunText(ctx, "Tell me a joke.").Collect()
```

### Custom options

Pass Anthropic-specific parameters using `anthropicprovider.MessageNewParams`:

```go
resp, err := a.RunText(ctx, "Hello!",
    anthropicprovider.MessageNewParams(anthropic.MessageNewParams{
        MaxTokens: 500,
    }),
).Collect()
```

> [!TIP]
> See the [Anthropic sample](https://github.com/microsoft/agent-framework-go/blob/main/examples/02-agents/providers/anthrophic/main.go) for a complete example.

::: zone-end
## Next steps

> [!div class="nextstepaction"]
> [Ollama](./ollama.md)
