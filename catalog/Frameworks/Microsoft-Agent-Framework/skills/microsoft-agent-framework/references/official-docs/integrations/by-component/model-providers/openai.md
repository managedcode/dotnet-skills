---
title: OpenAI
description: Learn how to use Microsoft Agent Framework with OpenAI services, including Chat Completions and Responses.
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: tutorial
ms.author: westey
ms.date: 07/30/2026
ms.service: agent-framework
---

# OpenAI

Microsoft Agent Framework supports OpenAI agents in C#, Python, and Go. C# and Python support two OpenAI client types — Responses and Chat Completion — while Go currently uses the Chat Completions provider. **Responses is the recommended primary client when available**: it targets the newer OpenAI Responses API and supports the full set of hosted tools (code interpreter, file search, web search, hosted MCP, image generation). Use Chat Completion when you need broad model compatibility, Go support, or have an existing Chat Completions integration to keep.

| Client Type | API | Best For |
|---|---|---|
| **Responses** (recommended) | [Responses API](https://developers.openai.com/api/reference/responses/overview) | Full-featured agents with hosted tools (code interpreter, file search, web search, hosted MCP) |
| **Chat Completion** | [Chat Completions API](https://developers.openai.com/api/reference/chat-completions/overview) | Simple agents, broad model support |


::: zone pivot="programming-language-csharp"

> [!NOTE]
> The OpenAI Assistants API is deprecated by OpenAI. New code should use the Responses client. If you are migrating from an existing Assistants-based app, see the [Semantic Kernel migration guide](../../../migration-guide/from-semantic-kernel/index.md).

## Getting Started

Add the required NuGet packages to your project.

```dotnetcli
dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
```

## Responses Client

The Responses client is the recommended primary client and provides the richest tool support including code interpreter, file search, web search, and hosted MCP.

```csharp
using Microsoft.Agents.AI;
using OpenAI;

OpenAIClient client = new OpenAIClient("<your_api_key>");
var responsesClient = client.GetResponsesClient();

AIAgent agent = responsesClient.AsAIAgent(
    model: "gpt-4o-mini",
    instructions: "You are a helpful coding assistant.",
    name: "CodeHelper");

Console.WriteLine(await agent.RunAsync("Write a Python function to sort a list."));
```

**Supported tools:** Function tools, tool approval, code interpreter, file search, web search, hosted MCP, local MCP tools.

## Chat Completion Client

The Chat Completion client provides a straightforward way to create agents using the Chat Completions API. Use it when you need broad model compatibility or have an existing Chat Completions integration.

```csharp
using Microsoft.Agents.AI;
using OpenAI;

OpenAIClient client = new OpenAIClient("<your_api_key>");
var chatClient = client.GetChatClient("gpt-4o-mini");

AIAgent agent = chatClient.AsAIAgent(
    instructions: "You are good at telling jokes.",
    name: "Joker");

Console.WriteLine(await agent.RunAsync("Tell me a joke about a pirate."));
```

**Supported tools:** Function tools, web search, local MCP tools.

## Assistants Client

> [!NOTE]
> The OpenAI Assistants API is [deprecated by OpenAI](https://developers.openai.com/api/docs/assistants/migration). The Agent Framework no longer documents an Assistants client — use the Responses client above for new code. For migrating an existing app, see the [Semantic Kernel migration guide](../../../migration-guide/from-semantic-kernel/index.md).

## Using the Agent

Both client types produce a standard `AIAgent` that supports the same agent operations (streaming, threads, middleware).

For more information, see the [Get Started tutorials](../../../get-started/your-first-agent.md).

## Tools

The OpenAI .NET clients expose different tool surfaces depending on which API they target. The same matrix applies to the matching Azure OpenAI clients on the [Azure OpenAI provider page](./azure-openai.md#tools).

| Tool | Responses | Chat Completion |
|---|:---:|:---:|
| [Function Tools](../../../agents/tools/function-tools.md) | ✅ | ✅ |
| [Tool Approval](../../../agents/tools/tool-approval.md) | ✅ | ✅ |
| [Code Interpreter](../../../agents/tools/code-interpreter.md) | ✅ | ❌ |
| [File Search](../../../agents/tools/file-search.md) | ✅ | ❌ |
| [Web Search](../../../agents/tools/web-search.md) | ✅ | ✅ |
| [Hosted MCP Tools](../../../agents/tools/hosted-mcp-tools.md) | ✅ | ❌ |
| [Local MCP Tools](../../../agents/tools/local-mcp-tools.md) | ✅ | ✅ |

> [!NOTE]
> **Tool Approval** is provided by the framework's function-invoking chat client, so it works with any function-tool call regardless of the underlying API.

::: zone-end
::: zone pivot="programming-language-python"

> [!NOTE]
> The OpenAI Assistants API is deprecated by OpenAI, and Python no longer ships an Assistants compatibility client/provider. Use `OpenAIChatClient` for Responses or `OpenAIChatCompletionClient` for Chat Completions. If you are migrating from a previous Agent Framework Python release, see the [Python significant changes guide](../../../support/upgrade/python-2026-significant-changes.md). If you are migrating from Semantic Kernel, see the [Semantic Kernel migration guide](../../../migration-guide/from-semantic-kernel/index.md).

> [!TIP]
> In Python, Azure OpenAI now uses the same `agent_framework.openai` clients shown here. Pass explicit Azure routing inputs such as `credential` or `azure_endpoint` when you want Azure routing, then set `api_version` for the Azure API surface you want to use. If `OPENAI_API_KEY` is configured, the generic clients stay on OpenAI even when `AZURE_OPENAI_*` variables are also present. If you already have a full `.../openai/v1` URL, use `base_url` instead of `azure_endpoint`. For Microsoft Foundry project endpoints and the Foundry Agent Service, see the [Microsoft Foundry provider page](./microsoft-foundry.md). For local runtimes, see [Foundry Local](./foundry-local.md).


## Installation

```bash
pip install agent-framework-openai
```

`agent-framework-openai` is the optional Python provider package for both direct OpenAI and Azure OpenAI usage.

## Configuration

The Python OpenAI chat clients use these environment-variable patterns:

# [Responses](#tab/oai-config-responses)

```bash
OPENAI_API_KEY="your-openai-api-key"
OPENAI_CHAT_MODEL="gpt-4o-mini"
# Optional shared fallback:
# OPENAI_MODEL="gpt-4o-mini"
```

# [Chat Completion](#tab/oai-config-chat-completion)

```bash
OPENAI_API_KEY="your-openai-api-key"
OPENAI_CHAT_COMPLETION_MODEL="gpt-4o-mini"
# Optional shared fallback:
# OPENAI_MODEL="gpt-4o-mini"
```

### Azure OpenAI with the same clients

Azure OpenAI now uses the same Python OpenAI clients as direct OpenAI. The preferred and clearest Azure pattern is to pass explicit Azure routing inputs such as `credential` or `azure_endpoint`, then set `api_version` for Azure once routing is selected. If `OPENAI_API_KEY` is set, the generic clients stay on OpenAI unless you pass those Azure routing inputs. If you only have `AZURE_OPENAI_*` settings, Azure environment fallback still works. `OpenAIChatClient` prefers `AZURE_OPENAI_CHAT_MODEL`, `OpenAIChatCompletionClient` prefers `AZURE_OPENAI_CHAT_COMPLETION_MODEL`, and both fall back to `AZURE_OPENAI_MODEL`.

Install `azure-identity` when you use `credential=` authentication:

```bash
pip install azure-identity
```

```bash
AZURE_OPENAI_ENDPOINT="https://<resource>.openai.azure.com"
AZURE_OPENAI_CHAT_MODEL="gpt-4o-mini"
# Optional shared fallback:
# AZURE_OPENAI_MODEL="gpt-4o-mini"
AZURE_OPENAI_API_VERSION="your-api-version"
```

```python
import asyncio
import os
from agent_framework import Agent
from agent_framework.openai import OpenAIChatClient
from azure.identity import AzureCliCredential

async def main():
    agent = Agent(
        client=OpenAIChatClient(
            model=os.environ["AZURE_OPENAI_CHAT_MODEL"],
            azure_endpoint=os.environ["AZURE_OPENAI_ENDPOINT"],
            api_version=os.getenv("AZURE_OPENAI_API_VERSION"),
            credential=AzureCliCredential(),
        ),
        name="AzureOpenAIResponsesAgent",
        instructions="You are a helpful assistant.",
    )

    result = await agent.run("Hello!")
    print(result)

asyncio.run(main())
```

If you already have a full Azure OpenAI URL that ends with `/openai/v1`, pass it as `base_url` instead of `azure_endpoint`. Keep `api_version` aligned to the Azure OpenAI API surface you are using. If `OPENAI_API_KEY` is also set in your environment, these explicit Azure inputs keep the client on Azure.

> [!NOTE]
> Use `OpenAIChatClient` for the Responses API. For Azure key auth, you can still pass `api_key`, but `credential=` is now the preferred Azure auth surface.

### Azure embeddings with the same client family

`OpenAIEmbeddingClient` follows the same routing rules as the chat clients. For Azure embeddings, pass the embedding deployment as `model` and prefer explicit Azure inputs:

```python
import os
from agent_framework.openai import OpenAIEmbeddingClient
from azure.identity import AzureCliCredential

client = OpenAIEmbeddingClient(
    model=os.environ["AZURE_OPENAI_EMBEDDING_MODEL"],
    azure_endpoint=os.environ["AZURE_OPENAI_ENDPOINT"],
    api_version=os.getenv("AZURE_OPENAI_API_VERSION"),
    credential=AzureCliCredential(),
)
```

## Create OpenAI Agents

# [Responses](#tab/oai-create-responses)

`OpenAIChatClient` uses the Responses API — the recommended primary client with hosted tool support.

```python
import asyncio
from agent_framework import Agent
from agent_framework.openai import OpenAIChatClient

async def main():
    agent = Agent(
        client=OpenAIChatClient(),
        name="FullFeaturedAgent",
        instructions="You are a helpful assistant with access to many tools.",
    )
    result = await agent.run("Write and run a Python script that calculates fibonacci numbers.")
    print(result)

asyncio.run(main())
```

**Supported tools:** Function tools, tool approval, code interpreter, file search, web search, hosted MCP, local MCP tools.

### Hosted Tools with Responses Client

The Responses client provides `get_*_tool()` methods for each hosted tool type:

```python
from agent_framework import Agent

async def hosted_tools_example():
    client = OpenAIChatClient()

    # Each tool is created via a client method
    code_interpreter = client.get_code_interpreter_tool()
    web_search = client.get_web_search_tool()
    file_search = client.get_file_search_tool(vector_store_ids=["vs_abc123"])
    mcp_tool = client.get_mcp_tool(
        name="GitHub",
        url="https://api.githubcopilot.com/mcp/",
        approval_mode="never_require",
    )

    agent = Agent(
        client=client,
        name="PowerAgent",
        instructions="You have access to code execution, web search, files, and GitHub.",
        tools=[code_interpreter, web_search, file_search, mcp_tool],
    )
    result = await agent.run("Search the web for Python best practices, then write a summary.")
    print(result)
```

# [Chat Completion](#tab/oai-create-chat-completion)

`OpenAIChatCompletionClient` uses the Chat Completions API — use it when you need broad model compatibility or have an existing Chat Completions integration.

```python
import asyncio
from agent_framework import Agent
from agent_framework.openai import OpenAIChatCompletionClient

async def main():
    agent = Agent(
        client=OpenAIChatCompletionClient(),
        name="HelpfulAssistant",
        instructions="You are a helpful assistant.",
    )
    result = await agent.run("Hello, how can you help me?")
    print(result)

asyncio.run(main())
```

**Supported tools:** Function tools, web search, local MCP tools.

### Web Search with Chat Completion

```python
from agent_framework import Agent

async def web_search_example():
    client = OpenAIChatCompletionClient()
    web_search = client.get_web_search_tool()

    agent = Agent(
        client=client,
        name="SearchBot",
        instructions="You can search the web for current information.",
        tools=web_search,
    )
    result = await agent.run("What are the latest developments in AI?")
    print(result)
```

> [!IMPORTANT]
> Python no longer ships an Assistants compatibility client/provider. For current Python code, use `OpenAIChatClient` for Responses API scenarios or `OpenAIChatCompletionClient` for Chat Completions. If you need a service-managed agent in Microsoft Foundry, see the [Microsoft Foundry provider page](./microsoft-foundry.md).

---

## Common Features

These client types support these standard agent features:

### Function Tools

```python
from agent_framework import Agent, tool

@tool
def get_weather(location: str) -> str:
    """Get the weather for a given location."""
    return f"The weather in {location} is sunny, 25°C."

async def example():
    agent = Agent(
        client=OpenAIChatClient(),
        instructions="You are a weather assistant.",
        tools=get_weather,
    )
    result = await agent.run("What's the weather in Tokyo?")
    print(result)
```

### Multi-Turn Conversations

```python
from agent_framework import Agent
from agent_framework.openai import OpenAIChatClient

async def thread_example():
    agent = Agent(
        client=OpenAIChatClient(),
        instructions="You are a helpful assistant.",
    )
    session = agent.create_session()

    result1 = await agent.run("My name is Alice", session=session)
    print(result1)
    result2 = await agent.run("What's my name?", session=session)
    print(result2)  # Remembers "Alice"
```

### Streaming

```python
from agent_framework import Agent
from agent_framework.openai import OpenAIChatClient

async def streaming_example():
    agent = Agent(
        client=OpenAIChatClient(),
        instructions="You are a creative storyteller.",
    )
    print("Agent: ", end="", flush=True)
    async for chunk in agent.run("Tell me a short story about AI.", stream=True):
        if chunk.text:
            print(chunk.text, end="", flush=True)
    print()
```

### Prompt caching

On models that support explicit prompt-cache breakpoints, `OpenAIChatClient` can use `prompt_cache_key`, `prompt_cache_options`, and `Content.additional_properties["prompt_cache_breakpoint"]` to control the reusable prefix. Cache writes can be billed separately on supported models.

OpenAI cache usage is normalized in `response.usage_details`:

- `cache_creation_input_token_count` - Input tokens written to the provider-managed cache.
- `cache_read_input_token_count` - Input tokens served from the cache.

When OpenTelemetry is enabled, these values map to `gen_ai.usage.cache_creation.input_tokens` and `gen_ai.usage.cache_read.input_tokens`.

:::code language="python" source="~/../agent-framework-code/python/samples/02-agents/providers/openai/client_prompt_caching.py" range="3-10,41-92":::

## Using the Agent

All client types produce a standard `Agent` that supports the same operations.

For more information, see the [Get Started tutorials](../../../get-started/your-first-agent.md).

## Tools

The Python OpenAI clients expose different tool surfaces depending on the underlying API. `OpenAIChatClient` (Responses) ships hosted tool factories via `client.get_*_tool(...)` — `get_code_interpreter_tool`, `get_file_search_tool`, `get_web_search_tool`, `get_image_generation_tool`, `get_shell_tool`, and `get_mcp_tool`. `OpenAIChatCompletionClient` only exposes `get_web_search_tool`. Both work with function tools and local MCP servers.

The same matrix applies when you point these clients at Azure OpenAI — see [Azure OpenAI](./azure-openai.md).

| Tool | `OpenAIChatClient` (Responses) | `OpenAIChatCompletionClient` (Chat Completion) |
|---|:---:|:---:|
| [Function Tools](../../../agents/tools/function-tools.md) | ✅ | ✅ |
| [Tool Approval](../../../agents/tools/tool-approval.md) | ✅ | ✅ |
| [Code Interpreter](../../../agents/tools/code-interpreter.md) | ✅ | ❌ |
| [File Search](../../../agents/tools/file-search.md) | ✅ | ❌ |
| [Web Search](../../../agents/tools/web-search.md) | ✅ | ✅ |
| Image Generation | ✅ (`get_image_generation_tool`) | ❌ |
| Hosted Shell | ✅ (`get_shell_tool`) | ❌ |
| [Hosted MCP Tools](../../../agents/tools/hosted-mcp-tools.md) | ✅ | ❌ |
| [Local MCP Tools](../../../agents/tools/local-mcp-tools.md) | ✅ | ✅ |

> [!NOTE]
> **Tool Approval** is handled by the framework's function-invoking chat client, so it works with any function-tool call regardless of the underlying API.

::: zone-end

::: zone pivot="programming-language-go"
## OpenAI Chat Completions

The `openaiprovider` package creates agents using the OpenAI Chat Completions API.

### Installation

```bash
go get github.com/microsoft/agent-framework-go
```

### Direct OpenAI

```go
import (
    "github.com/microsoft/agent-framework-go/agent"
    "github.com/microsoft/agent-framework-go/provider/openaiprovider"

    "github.com/openai/openai-go/v3"
)

a := openaiprovider.NewChatCompletionsAgent(
    openai.NewClient(), // uses OPENAI_API_KEY env var
    openaiprovider.AgentConfig{
        Model: "gpt-4o-mini",
        Instructions: "You are a helpful assistant.",
        Config: agent.Config{
            Name:         "MyAgent",
        },
    },
)

resp, err := a.RunText(ctx, "Tell me a joke.").Collect()
```

### Azure OpenAI

Use the same `openaiprovider` package with Azure credentials:

```go
import (
    "github.com/Azure/azure-sdk-for-go/sdk/azidentity"
    openai "github.com/openai/openai-go/v3"
    "github.com/openai/openai-go/v3/azure"
)

token, _ := azidentity.NewDefaultAzureCredential(nil)

a := openaiprovider.NewChatCompletionsAgent(
    openai.NewClient(
        azure.WithEndpoint(endpoint, apiVersion),
        azure.WithTokenCredential(token),
    ),
    openaiprovider.AgentConfig{
        Model: deployment,
        Instructions: "You are a helpful assistant.",
        Config: agent.Config{
        },
    },
)
```

> [!WARNING]
> `azidentity.NewDefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential, such as `azidentity.NewManagedIdentityCredential`, to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

### Custom options

Pass provider-specific options using `openaiprovider.ChatCompletionNewParams`:

```go
resp, err := a.RunText(ctx, "Hello!",
    openaiprovider.ChatCompletionNewParams(openai.ChatCompletionNewParams{
        Temperature: openai.Float(0.7),
    }),
).Collect()
```

**Supported tools:** Function tools, web search, local MCP tools.

> [!TIP]
> See the [OpenAI provider sample](https://github.com/microsoft/agent-framework-go/blob/main/examples/02-agents/providers/openai/main.go) and [Azure OpenAI sample](https://github.com/microsoft/agent-framework-go/blob/main/examples/02-agents/providers/azure/main.go) for complete examples.

::: zone-end
## Next steps

> [!div class="nextstepaction"]
> [Microsoft Foundry](./microsoft-foundry.md)
