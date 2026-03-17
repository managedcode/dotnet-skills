---
title: Anthropic Agents
description: Learn how to use the Microsoft Agent Framework with Anthropic's Claude models.
zone_pivot_groups: programming-languages
author:  rogerbarreto
ms.topic: tutorial
ms.author: rbarreto
ms.date: 12/12/2025
ms.service: agent-framework
---

# Anthropic Agents

The Microsoft Agent Framework supports creating agents that use [Anthropic's Claude models](https://www.anthropic.com/claude).

::: zone pivot="programming-language-csharp"

## Getting Started

Add the required NuGet packages to your project.

```powershell
dotnet add package Microsoft.Agents.AI.Anthropic --prerelease
```

If you're using Azure Foundry, also add:

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
$env:ANTHROPIC_DEPLOYMENT_NAME="claude-haiku-4-5"  # or your preferred model
```

You can get an API key from the [Anthropic Console](https://console.anthropic.com/).

### For Azure Foundry with API Key

```powershell
$env:ANTHROPIC_RESOURCE="your-foundry-resource-name"  # Subdomain before .services.ai.azure.com
$env:ANTHROPIC_API_KEY="your-anthropic-api-key"
$env:ANTHROPIC_DEPLOYMENT_NAME="claude-haiku-4-5"
```

### For Azure Foundry with Azure CLI

```powershell
$env:ANTHROPIC_RESOURCE="your-foundry-resource-name"  # Subdomain before .services.ai.azure.com
$env:ANTHROPIC_DEPLOYMENT_NAME="claude-haiku-4-5"
```

> [!NOTE]
> When using Azure Foundry with Azure CLI, make sure you're logged in with `az login` and have access to the Azure Foundry resource. For more information, see the [Azure CLI documentation](/cli/azure/authenticate-azure-cli-interactively).

## Creating an Anthropic Agent

### Basic Agent Creation (Anthropic Public API)

The simplest way to create an Anthropic agent using the public API:

```csharp
var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
var deploymentName = Environment.GetEnvironmentVariable("ANTHROPIC_DEPLOYMENT_NAME") ?? "claude-haiku-4-5";

AnthropicClient client = new() { APIKey = apiKey };

AIAgent agent = client.AsAIAgent(
    model: deploymentName,
    name: "HelpfulAssistant",
    instructions: "You are a helpful assistant.");

// Invoke the agent and output the text result.
Console.WriteLine(await agent.RunAsync("Hello, how can you help me?"));
```

### Using Anthropic on Azure Foundry with API Key

After you've set up Anthropic on Azure Foundry, you can use it with API key authentication:

```csharp
var resource = Environment.GetEnvironmentVariable("ANTHROPIC_RESOURCE");
var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
var deploymentName = Environment.GetEnvironmentVariable("ANTHROPIC_DEPLOYMENT_NAME") ?? "claude-haiku-4-5";

AnthropicClient client = new AnthropicFoundryClient(
    new AnthropicFoundryApiKeyCredentials(apiKey, resource));

AIAgent agent = client.AsAIAgent(
    model: deploymentName,
    name: "FoundryAgent",
    instructions: "You are a helpful assistant using Anthropic on Azure Foundry.");

Console.WriteLine(await agent.RunAsync("How do I use Anthropic on Foundry?"));
```

### Using Anthropic on Azure Foundry with Azure Credentials (Azure Cli Credential example)

For environments where Azure Credentials are preferred:

```csharp
var resource = Environment.GetEnvironmentVariable("ANTHROPIC_RESOURCE");
var deploymentName = Environment.GetEnvironmentVariable("ANTHROPIC_DEPLOYMENT_NAME") ?? "claude-haiku-4-5";

AnthropicClient client = new AnthropicFoundryClient(
    new AnthropicAzureTokenCredential(new AzureCliCredential(), resource));

AIAgent agent = client.AsAIAgent(
    model: deploymentName,
    name: "FoundryAgent",
    instructions: "You are a helpful assistant using Anthropic on Azure Foundry.");

Console.WriteLine(await agent.RunAsync("How do I use Anthropic on Foundry?"));

/// <summary>
/// Provides methods for invoking the Azure hosted Anthropic models using <see cref="TokenCredential"/> types.
/// </summary>
public sealed class AnthropicAzureTokenCredential(TokenCredential tokenCredential, string resourceName) : IAnthropicFoundryCredentials
{
    /// <inheritdoc/>
    public string ResourceName { get; } = resourceName;

    /// <inheritdoc/>
    public void Apply(HttpRequestMessage requestMessage)
    {
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue(
                scheme: "bearer",
                parameter: tokenCredential.GetToken(new TokenRequestContext(scopes: ["https://ai.azure.com/.default"]), CancellationToken.None)
                    .Token);
    }
}
```

## Using the Agent

The agent is a standard `AIAgent` and supports all standard agent operations.

See the [Agent getting started tutorials](../../../tutorials/overview.md) for more information on how to run and interact with agents.

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
ANTHROPIC_CHAT_MODEL_ID="claude-sonnet-4-5-20250929"  # or your preferred model
```

Alternatively, you can use a `.env` file in your project root:

```env
ANTHROPIC_API_KEY=your-anthropic-api-key
ANTHROPIC_CHAT_MODEL_ID=claude-sonnet-4-5-20250929
```

You can get an API key from the [Anthropic Console](https://console.anthropic.com/).

## Getting Started

Import the required classes from the Agent Framework:

```python
import asyncio
from agent_framework.anthropic import AnthropicClient
```

## Creating an Anthropic Agent

### Basic Agent Creation

The simplest way to create an Anthropic agent:

```python
async def basic_example():
    # Create an agent using Anthropic
    agent = AnthropicClient().as_agent(
        name="HelpfulAssistant",
        instructions="You are a helpful assistant.",
    )

    result = await agent.run("Hello, how can you help me?")
    print(result.text)
```

### Using Explicit Configuration

You can provide explicit configuration instead of relying on environment variables:

```python
async def explicit_config_example():
    agent = AnthropicClient(
        model_id="claude-sonnet-4-5-20250929",
        api_key="your-api-key-here",
    ).as_agent(
        name="HelpfulAssistant",
        instructions="You are a helpful assistant.",
    )

    result = await agent.run("What can you do?")
    print(result.text)
```

### Using Anthropic on Foundry

After you've setup Anthropic on Foundry, ensure you have the following environment variables set:

```bash
ANTHROPIC_FOUNDRY_API_KEY="your-foundry-api-key"
ANTHROPIC_FOUNDRY_RESOURCE="your-foundry-resource-name"
```
Then create the agent as follows:

```python
from agent_framework.anthropic import AnthropicClient
from anthropic import AsyncAnthropicFoundry

async def foundry_example():
    agent = AnthropicClient(
        anthropic_client=AsyncAnthropicFoundry()
    ).as_agent(
        name="FoundryAgent",
        instructions="You are a helpful assistant using Anthropic on Foundry.",
    )

    result = await agent.run("How do I use Anthropic on Foundry?")
    print(result.text)
```

> Note:
> This requires `anthropic>=0.74.0` to be installed.

## Agent Features

### Function Tools

Equip your agent with custom functions:

```python
from typing import Annotated

def get_weather(
    location: Annotated[str, "The location to get the weather for."],
) -> str:
    """Get the weather for a given location."""
    conditions = ["sunny", "cloudy", "rainy", "stormy"]
    return f"The weather in {location} is {conditions[randint(0, 3)]} with a high of {randint(10, 30)}°C."

async def tools_example():
    agent = AnthropicClient().as_agent(
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
async def streaming_example():
    agent = AnthropicClient().as_agent(
        name="WeatherAgent",
        instructions="You are a helpful weather agent.",
        tools=get_weather,
    )

    query = "What's the weather like in Portland and in Paris?"
    print(f"User: {query}")
    print("Agent: ", end="", flush=True)
    async for chunk in agent.run_stream(query):
        if chunk.text:
            print(chunk.text, end="", flush=True)
    print()
```

### Hosted Tools

Anthropic agents support hosted tools such as web search, MCP (Model Context Protocol), and code execution:

```python
from agent_framework import HostedMCPTool, HostedWebSearchTool

async def hosted_tools_example():
    agent = AnthropicClient().as_agent(
        name="DocsAgent",
        instructions="You are a helpful agent for both Microsoft docs questions and general questions.",
        tools=[
            HostedMCPTool(
                name="Microsoft Learn MCP",
                url="https://learn.microsoft.com/api/mcp",
            ),
            HostedWebSearchTool(),
        ],
        max_tokens=20000,
    )

    result = await agent.run("Can you compare Python decorators with C# attributes?")
    print(result.text)
```

### Extended Thinking (Reasoning)

Anthropic supports extended thinking capabilities through the `thinking` feature, which allows the model to show its reasoning process:

```python
from agent_framework import TextReasoningContent, UsageContent
from agent_framework.anthropic import AnthropicClient

async def thinking_example():
    agent = AnthropicClient().as_agent(
        name="DocsAgent",
        instructions="You are a helpful agent.",
        tools=[HostedWebSearchTool()],
        default_options={
            "max_tokens": 20000,
            "thinking": {"type": "enabled", "budget_tokens": 10000}
        },
    )

    query = "Can you compare Python decorators with C# attributes?"
    print(f"User: {query}")
    print("Agent: ", end="", flush=True)

    async for chunk in agent.run_stream(query):
        for content in chunk.contents:
            if isinstance(content, TextReasoningContent):
                # Display thinking in a different color
                print(f"\033[32m{content.text}\033[0m", end="", flush=True)
            if isinstance(content, UsageContent):
                print(f"\n\033[34m[Usage: {content.details}]\033[0m\n", end="", flush=True)
        if chunk.text:
            print(chunk.text, end="", flush=True)
    print()
```

### Anthropic Skills

Anthropic provides managed skills that extend agent capabilities, such as creating PowerPoint presentations. Skills require the Code Interpreter tool to function:

```python
from agent_framework import HostedCodeInterpreterTool, HostedFileContent
from agent_framework.anthropic import AnthropicClient

async def skills_example():
    # Create client with skills beta flag
    client = AnthropicClient(additional_beta_flags=["skills-2025-10-02"])

    # Create an agent with the pptx skill enabled
    # Skills require the Code Interpreter tool
    agent = client.as_agent(
        name="PresentationAgent",
        instructions="You are a helpful agent for creating PowerPoint presentations.",
        tools=HostedCodeInterpreterTool(),
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

    files: list[HostedFileContent] = []
    async for chunk in agent.run_stream(query):
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

## Using the Agent

The agent is a standard `BaseAgent` and supports all standard agent operations.

See the [Agent getting started tutorials](../../../tutorials/overview.md) for more information on how to run and interact with agents.

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Microsoft Foundry Agents](./microsoft-foundry-agents.md)
