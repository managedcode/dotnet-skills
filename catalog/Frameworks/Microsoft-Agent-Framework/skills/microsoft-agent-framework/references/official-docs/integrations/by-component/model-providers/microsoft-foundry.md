---
title: Microsoft Foundry model provider
description: Learn how to use Microsoft Agent Framework for direct model inference through Microsoft Foundry project endpoints.
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: tutorial
ms.author: westey
ms.date: 07/30/2026
ms.service: agent-framework
---

# Microsoft Foundry model provider

Microsoft Agent Framework supports direct model inference from Microsoft Foundry project endpoints while your application owns the agent definition, tools, and orchestration.

For service-managed Prompt and Hosted Agents, see [Microsoft Foundry Agent Service](../agent-services/foundry.md).

::: zone pivot="programming-language-csharp"

## Getting Started

Add the required NuGet packages to your project.

```dotnetcli
dotnet add package Azure.Identity
dotnet add package Microsoft.Agents.AI.Foundry --prerelease
```

## Two integration patterns

The Microsoft Foundry integration exposes two distinct usage patterns:

| Pattern | Produced type | Description | Use when |
|---|---|---|---|
| **Responses Agent** | `ChatClientAgent` | Your app programmatically provides a model, instructions, and tools at runtime via `AIProjectClient.AsAIAgent(...)`. No server-side agent resource is created. | You own the agent definition and want a simple, flexible setup. This is the pattern used in most samples. |
| **Foundry Agent** (Prompt or Hosted) | `FoundryAgent` | Server-managed — Prompt Agents are named and versioned definitions; Hosted Agents are deployed applications reached through an agent-specific endpoint. | Foundry owns the agent definition or hosted runtime. See [Microsoft Foundry Agent Service](../agent-services/foundry.md). |

## Responses Agent (direct inference)

Use `AsAIAgent` on `AIProjectClient` directly with a model and instructions. This is the recommended starting point for most scenarios.

```csharp
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;

AIAgent agent = new AIProjectClient(
    new Uri("<your-foundry-project-endpoint>"),
    new DefaultAzureCredential())
        .AsAIAgent(
            model: "gpt-4o-mini",
            name: "Joker",
            instructions: "You are good at telling jokes.");

Console.WriteLine(await agent.RunAsync("Tell me a joke about a pirate."));
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

This path is code-first and does not create a server-managed agent resource.

## Using the agent

The Responses Agent is a standard `AIAgent` and supports sessions, tools, middleware, and streaming.

```csharp
AgentSession session = await agent.CreateSessionAsync();
Console.WriteLine(await agent.RunAsync("Tell me a joke.", session));
Console.WriteLine(await agent.RunAsync("Now make it funnier.", session));
```

For more information on how to run and interact with agents, see the [Agent getting started tutorials](../../../get-started/your-first-agent.md).

## Tools

Foundry Responses Agents created from `AIProjectClient.AsAIAgent(...)` support the standard Agent Framework tool surface. See the [Tools overview](../../../agents/tools/index.md) for the complete feature matrix.

| Tool | Notes |
|---|---|
| [Function Tools](../../../agents/tools/function-tools.md) | Supported. |
| [Tool Approval](../../../agents/tools/tool-approval.md) | Supported. Provided by the framework's function-invoking chat client. |
| [Code Interpreter](../../../agents/tools/code-interpreter.md) | Supported. |
| [File Search](../../../agents/tools/file-search.md) | Supported. |
| [Hosted MCP Tools](../../../agents/tools/hosted-mcp-tools.md) | Supported. |
| [Local MCP Tools](../../../agents/tools/local-mcp-tools.md) | Supported. |
| [Microsoft Foundry Toolbox](../tools/foundry-toolbox.md) | Supported. |

::: zone-end
::: zone pivot="programming-language-python"

## Foundry in Python

In Python, all Foundry-specific clients now live under `agent_framework.foundry`.

- `agent-framework-foundry` provides the cloud Foundry connectors: `FoundryChatClient`, `FoundryAgent`, `FoundryEmbeddingClient`, and `FoundryMemoryProvider`.
- `agent-framework-foundry-local` provides `FoundryLocalClient` for local model execution.

> [!IMPORTANT]
> This page covers Microsoft Foundry project and models endpoints. For the Foundry Agent Service, see [Microsoft Foundry Agent Service](../agent-services/foundry.md). If you have a standalone Azure OpenAI resource endpoint (`https://<your-resource>.openai.azure.com`), use the Python guidance on the [OpenAI provider page](./openai.md). If you want to run supported models locally, see the [Foundry Local provider page](./foundry-local.md).

## Foundry chat and agent patterns in Python

| Scenario | Python shape | Use when |
|---|---|---|
| Plain inference with the Foundry Responses endpoint | `Agent(client=FoundryChatClient(...))` | Your app owns the agent definition, tools, and conversation loop, and you want a model deployed in a Foundry project. |
| Service-managed agents in the Foundry Agent Service | `FoundryAgent(...)` | You want to connect to a PromptAgent or HostedAgent that is created and configured in the Foundry portal or through the service APIs. |

## Installation

```bash
pip install agent-framework-foundry
```

The same `agent-framework-foundry` package also includes `FoundryEmbeddingClient` for Foundry models-endpoint embeddings.

## Configuration

### `FoundryChatClient`

```bash
FOUNDRY_PROJECT_ENDPOINT="https://<your-project>.services.ai.azure.com"
FOUNDRY_MODEL="gpt-4o-mini"
```

### `FoundryEmbeddingClient`

```bash
FOUNDRY_MODELS_ENDPOINT="https://<apim-instance>.azure-api.net/<foundry-instance>/models"
FOUNDRY_MODELS_API_KEY="<api-key>"
FOUNDRY_EMBEDDING_MODEL="text-embedding-3-small"
FOUNDRY_IMAGE_EMBEDDING_MODEL="Cohere-embed-v3-english"  # optional
```

`FoundryChatClient` uses the project endpoint. `FoundryEmbeddingClient` uses the separate models endpoint.

### Choose the right Python client

| Scenario | Preferred client | Notes |
|---|---|---|
| Azure OpenAI resource | `OpenAIChatCompletionClient` / `OpenAIChatClient` | Use the [OpenAI provider page](./openai.md). |
| Microsoft Foundry project inference | `Agent(client=FoundryChatClient(...))` | Uses the Foundry Responses endpoint. |
| Microsoft Foundry service-managed agent | `FoundryAgent` | Recommended for Prompt Agents and HostedAgents. |
| Microsoft Foundry models-endpoint embeddings | `FoundryEmbeddingClient` | Uses `FOUNDRY_MODELS_ENDPOINT` plus `FOUNDRY_EMBEDDING_MODEL` / `FOUNDRY_IMAGE_EMBEDDING_MODEL`. |
| Foundry Local runtime | `Agent(client=FoundryLocalClient(...))` | See [Foundry Local](./foundry-local.md). |

## Create an agent with `FoundryChatClient`

`FoundryChatClient` connects to a deployed model in a Foundry project and uses the Responses endpoint. Pair it with a standard `Agent` when your app should own instructions, tools, and session handling.

```python
from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient
from azure.identity import AzureCliCredential

agent = Agent(
    client=FoundryChatClient(
        project_endpoint="https://your-project.services.ai.azure.com",
        model="gpt-4o-mini",
        credential=AzureCliCredential(),
    ),
    name="FoundryWeatherAgent",
    instructions="You are a helpful assistant.",
)
```

`FoundryChatClient` is the Foundry-first Python path for direct inference and supports tools, structured outputs, and streaming.

## Tools

`FoundryChatClient` ships static factory methods for each hosted Foundry tool. The factories return SDK tool objects you pass to `tools=` on `Agent` or directly to `client.get_response(..., tools=[...])`. For service-managed agent tools, see [Microsoft Foundry Agent Service](../agent-services/foundry.md#what-works-and-what-doesnt-with-foundryagent).

The factories are class methods, so you do not need an instance to create a tool:

```python
from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient
from azure.identity import AzureCliCredential

agent = Agent(
    client=FoundryChatClient(credential=AzureCliCredential()),
    instructions="You can search the web and run code.",
    tools=[
        FoundryChatClient.get_web_search_tool(),
        FoundryChatClient.get_code_interpreter_tool(),
    ],
)
```

### Tool support

The table below lists every tool the Python `FoundryChatClient` exposes today.

| Tool | Factory on `FoundryChatClient` | Status | Detail |
|---|---|---|---|
| [Function Tools](../../../agents/tools/function-tools.md) | n/a — pass any Python callable or `@ai_function` | GA | Invoked locally in your Python process. |
| [Tool Approval](../../../agents/tools/tool-approval.md) | n/a — wraps existing tools | GA | Works with hosted MCP and function tools. |
| [Code Interpreter](../../../agents/tools/code-interpreter.md) | `get_code_interpreter_tool` | GA | Sandboxed code execution on Foundry. |
| [File Search](../../../agents/tools/file-search.md) | `get_file_search_tool` | GA | Search uploaded files via Foundry vector stores. |
| [Web Search](../../../agents/tools/web-search.md) | `get_web_search_tool` | GA | Bing-backed web grounding managed by Microsoft. Azure OpenAI models only. |
| [Image Generation](#image-generation) | `get_image_generation_tool` | GA | Image generation hosted on Foundry. |
| [Hosted MCP](../../../agents/tools/hosted-mcp-tools.md) | `get_mcp_tool` | GA | Remote MCP server invoked by Foundry. |
| [Local MCP](../../../agents/tools/local-mcp-tools.md) | n/a — use `MCPStreamableHTTPTool` / `MCPStdioTool` | GA | Runs in your process; works with any client. |
| [Microsoft Foundry Toolbox](../tools/foundry-toolbox.md) | `MCPStreamableHTTPTool` or `FoundryToolbox` | Beta | Consumed over MCP from `FoundryChatClient`; attached server-side on `FoundryAgent`. |
| [Bing Grounding](#bing-grounding) | `get_bing_grounding_tool` | Experimental | Bring-your-own Grounding with Bing Search resource. |
| [Bing Custom Search](#bing-custom-search) | `get_bing_custom_search_tool` | Preview | Bing grounding restricted to a curated domain list. |
| [Azure AI Search](#azure-ai-search) | `get_azure_ai_search_tool` | Experimental | Search an Azure AI Search index via a Foundry connection. |
| [SharePoint](#sharepoint) | `get_sharepoint_tool` | Preview | Ground answers in SharePoint content. |
| [Microsoft Fabric](#microsoft-fabric) | `get_fabric_tool` | Preview | Query a Fabric data agent. |
| [Memory Search](#memory-search) | `get_memory_search_tool` | Preview | Search a Foundry-managed memory store. |
| [Computer Use](#computer-use) | `get_computer_use_tool` | Preview | Let the agent drive a desktop or browser environment. |
| [Browser Automation](#browser-automation) | `get_browser_automation_tool` | Preview | Drive a browser via an Azure Playwright connection. |
| [Agent-to-Agent (A2A)](#agent-to-agent-a2a) | `get_a2a_tool` | Preview | Call another A2A agent as a tool. |

> [!NOTE]
> **Experimental** factories wrap GA Foundry SDK types but the wrappers themselves may change before GA. **Preview** factories wrap Foundry SDK types whose underlying capability is in preview and may change or be removed. Both emit an `ExperimentalWarning` the first time they are used in a process.

### Web search variants

Foundry exposes three Bing-backed grounding options. Pick the one that matches your scenario:

- `get_web_search_tool` (GA) — zero-setup default; Bing resource managed by Microsoft. Azure OpenAI models only. Limited to `user_location` and `search_context_size`.
- `get_bing_grounding_tool` (experimental) — bring your own Grounding with Bing Search Azure resource. Supports `count`, `freshness`, `market`, `set_lang`, and non-OpenAI Foundry models.
- `get_bing_custom_search_tool` (preview) — bring your own Bing Custom Search instance to restrict grounding to a curated set of domains.

All three send search data outside the Azure compliance boundary. See the [web grounding overview](/azure/foundry/agents/how-to/tools/web-overview) for the full comparison.

```python
client = FoundryChatClient(credential=AzureCliCredential())

# Default (GA): minimal configuration
web_search = client.get_web_search_tool(
    user_location={"city": "Amsterdam", "country": "NL"},
    search_context_size="medium",
)
```

### Image generation

`get_image_generation_tool` configures Foundry's hosted image generation tool. The model produces image content in the response — there are no extra files to manage.

```python
image_gen = FoundryChatClient.get_image_generation_tool(
    model="gpt-image-1",
    size="1024x1024",
    output_format="png",
    quality="high",
)
```

### Bing grounding

`get_bing_grounding_tool` wraps the Grounding with Bing Search Foundry tool. You create the Grounding with Bing Search resource yourself and add it as a Foundry project connection, then pass the connection ID.

```python
bing = FoundryChatClient.get_bing_grounding_tool(
    connection_id="/subscriptions/.../connections/my-bing",
    market="en-US",
    freshness="Day",
    count=10,
)
```

### Bing custom search

`get_bing_custom_search_tool` restricts grounding to the allow-list defined on a Bing Custom Search resource.

```python
bing_custom = FoundryChatClient.get_bing_custom_search_tool(
    connection_id="/subscriptions/.../connections/my-bing-custom",
    instance_name="docs-only",
    market="en-US",
)
```

### Azure AI Search

`get_azure_ai_search_tool` lets the agent query an Azure AI Search index through a Foundry project connection.

```python
ai_search = FoundryChatClient.get_azure_ai_search_tool(
    index_connection_id="/subscriptions/.../connections/my-search",
    index_name="product-docs",
    query_type="vector_semantic_hybrid",
    top_k=5,
)
```

### SharePoint

`get_sharepoint_tool` grounds answers in SharePoint content reachable through a Foundry SharePoint connection.

```python
sharepoint = FoundryChatClient.get_sharepoint_tool(
    connection_id="/subscriptions/.../connections/my-sharepoint",
)
```

### Microsoft Fabric

`get_fabric_tool` connects the agent to a Microsoft Fabric data agent via a Foundry connection so the agent can answer questions over your Fabric data.

```python
fabric = FoundryChatClient.get_fabric_tool(
    connection_id="/subscriptions/.../connections/my-fabric",
)
```

### Memory search

`get_memory_search_tool` lets the agent search a Foundry-managed memory store, optionally scoped to a user or tenant.

```python
memory = FoundryChatClient.get_memory_search_tool(
    memory_store_name="user-preferences",
    scope="{{$userId}}",
)
```

### Computer use

`get_computer_use_tool` configures the Computer Use preview tool — the model can drive a desktop or browser environment by issuing pointer and keyboard actions.

```python
computer = FoundryChatClient.get_computer_use_tool(
    environment="browser",
    display_width=1280,
    display_height=800,
)
```

### Browser automation

`get_browser_automation_tool` wires the agent into an Azure Playwright Testing resource via a Foundry connection. The agent can drive a real browser through Playwright.

```python
browser = FoundryChatClient.get_browser_automation_tool(
    connection_id="/subscriptions/.../connections/my-playwright",
)
```

### Agent-to-Agent (A2A)

`get_a2a_tool` exposes a remote A2A agent as a tool so a Foundry agent can call it. Provide either a `base_url` (and optionally `agent_card_path`) or a `project_connection_id` for a stored A2A connection.

```python
a2a = FoundryChatClient.get_a2a_tool(
    base_url="https://remote-agent.example.com",
    agent_card_path="/.well-known/agent-card.json",
)
```

For general A2A discovery, sessions, and streaming guidance, see the [A2A agent service](../agent-services/a2a.md).

## Create embeddings with `FoundryEmbeddingClient`

Use `FoundryEmbeddingClient` when you want text or image embeddings from a Foundry models endpoint.

```python
from agent_framework.foundry import FoundryEmbeddingClient

async with FoundryEmbeddingClient() as client:
    result = await client.get_embeddings(["hello from Agent Framework"])
    print(result[0].dimensions)
```

## Using the agent

`FoundryChatClient` integrates with the standard Python `Agent` experience, including tool calling, sessions, and streaming responses. For local runtimes, use the separate [Foundry Local provider page](./foundry-local.md).

For named, versioned bundles of hosted tool configurations, see [Microsoft Foundry Toolbox](../tools/foundry-toolbox.md).

::: zone-end

::: zone pivot="programming-language-go"

## Foundry in Go

The Go SDK provides Microsoft Foundry agents through `github.com/microsoft/agent-framework-go/provider/foundryprovider`.

See the [Foundry Go samples](https://github.com/microsoft/agent-framework-go/tree/main/examples/02-agents/providers/foundry) for direct inference, function tools, hosted tools, MCP, and server-agent examples.

The package supports two agent targets:

| Target | Go shape | Use when |
|---|---|---|
| Project-backed model deployment | `foundryprovider.ModelDeployment("gpt-4o-mini")` | Your app owns instructions, tools, and conversation flow. |
| Existing server-side Foundry agent | `foundryprovider.ServerAgent("my-agent")` | The agent definition is already configured in Foundry. |

## Configuration

Set your Foundry project endpoint and model deployment:

```bash
FOUNDRY_PROJECT_ENDPOINT="https://<your-project>.services.ai.azure.com/api/projects/<project-id>"
FOUNDRY_MODEL="gpt-4o-mini"
```

## Project-backed Foundry agent

Use `ModelDeployment` when you want to create an Agent Framework agent in code and pass instructions, tools, middleware, and context providers from your Go application.

```go
import (
    "context"
    "os"

    "github.com/Azure/azure-sdk-for-go/sdk/azidentity"
    "github.com/microsoft/agent-framework-go/agent"
    "github.com/microsoft/agent-framework-go/provider/foundryprovider"
)

endpoint := os.Getenv("FOUNDRY_PROJECT_ENDPOINT")
model := os.Getenv("FOUNDRY_MODEL")

token, err := azidentity.NewDefaultAzureCredential(nil)
if err != nil {
    panic(err)
}

a := foundryprovider.NewAgent(
    endpoint,
    token,
    foundryprovider.ModelDeployment(model),
    foundryprovider.AgentConfig{
        Instructions: "You are good at telling jokes.",
        Config: agent.Config{
            Name: "Joker",
        },
    },
)

resp, err := a.RunText(context.Background(), "Tell me a joke about a pirate.").Collect()
```

## Existing server-side Foundry agent

Use `ServerAgent` when you want to invoke an agent already configured in Foundry. The server-side agent owns its instructions and tools, so `AgentConfig.Instructions` is ignored for this target.

```go
a := foundryprovider.NewAgent(
    endpoint,
    token,
    foundryprovider.ServerAgent("my-agent"),
    foundryprovider.AgentConfig{
        Config: agent.Config{
            Name: "my-agent",
        },
    },
)

resp, err := a.RunText(ctx, "Summarize the current project status.").Collect()
```

## Tools

Project-backed Foundry agents support the standard Go Agent Framework tool surface for local tools and supported hosted tool declarations.

| Tool | Status | Notes |
|---|---|---|
| [Function Tools](../../../agents/tools/function-tools.md) | Supported | Functions run in your Go process. |
| [Tool Approval](../../../agents/tools/tool-approval.md) | Supported | Works with local function tools through the tool auto-call loop. |
| [Code Interpreter](../../../agents/tools/code-interpreter.md) | Supported | Use `&hostedtool.CodeInterpreter{}`. |
| [Web Search](../../../agents/tools/web-search.md) | Supported | Use `&hostedtool.WebSearch{}`. |
| [Local MCP Tools](../../../agents/tools/local-mcp-tools.md) | Supported | Use `tool/mcptool` to connect to an MCP server and expose its tools locally. |
| [Hosted MCP Tools](../../../agents/tools/hosted-mcp-tools.md) | Not currently documented for Go Foundry | Use local MCP tools when you need MCP servers with Go Foundry agents. |
| [Microsoft Foundry Toolbox](../tools/foundry-toolbox.md) | Not currently exposed through a Go helper. |

For local function tools, add `tool.Tool` values through `agent.Config.Tools`:

```go
a := foundryprovider.NewAgent(
    endpoint,
    token,
    foundryprovider.ModelDeployment(model),
    foundryprovider.AgentConfig{
        Instructions: "You are a helpful assistant.",
        Config: agent.Config{
            Tools: []tool.Tool{weatherTool},
        },
    },
)
```

For hosted code execution, pass the hosted tool declaration:

```go
a := foundryprovider.NewAgent(
    endpoint,
    token,
    foundryprovider.ModelDeployment(model),
    foundryprovider.AgentConfig{
        Instructions: "You solve problems with code.",
        Config: agent.Config{
            Tools: []tool.Tool{&hostedtool.CodeInterpreter{}},
        },
    },
)
```

## Client headers and served model

Foundry accepts `x-client-*` headers per run. Add them with `foundryprovider.WithClientHeader` or `foundryprovider.WithClientHeaders`:

```go
resp, err := a.RunText(
    ctx,
    "Hello!",
    foundryprovider.WithClientHeader("x-client-scenario", "docs"),
).Collect()
```

When Foundry returns the `x-ms-served-model` response header, the Go provider adds it to response/update additional properties as `ServedModel`.

```go
if servedModel, ok := resp.AdditionalProperties["ServedModel"].(string); ok {
    fmt.Println(servedModel)
}
```

## Foundry memory provider

Use `foundryprovider.NewMemoryProvider` when you want an Agent Framework agent to retrieve from and update a Foundry-managed memory store around each run.

```go
import (
    "log/slog"

    "github.com/microsoft/agent-framework-go/agent"
    "github.com/microsoft/agent-framework-go/provider/foundryprovider"
)

memoryProvider := foundryprovider.NewMemoryProvider(
    endpoint,
    tokenCredential,
    "memory-store-sample",
    func(*agent.Session) string { return "user-123" },
    foundryprovider.MemoryProviderConfig{
        Logger: slog.Default(),
    },
)

a := foundryprovider.NewAgent(
    endpoint,
    tokenCredential,
    foundryprovider.ModelDeployment(model),
    foundryprovider.AgentConfig{
        Instructions: "Use known memories about the user when responding.",
        Config: agent.Config{
            Name:             "FoundryMemoryAgent",
            ContextProviders: []agent.ContextProvider{memoryProvider},
        },
    },
)
```

The endpoint must be a project-scoped Microsoft Foundry endpoint, and the memory store must already exist in that project. The scope callback should return a stable user, tenant, or conversation partition key.

> [!TIP]
> See the [Foundry memory Go sample](https://github.com/microsoft/agent-framework-go/blob/main/examples/02-agents/agents/step22_foundry_memory/main.go) for a complete runnable example.

## Current Go gaps

Go support does not currently include Foundry hosted deployment/lifecycle/admin APIs, embeddings clients, or Go-specific helpers for [Microsoft Foundry Toolbox](../tools/foundry-toolbox.md). Use the Foundry portal or service SDKs for those operations.

::: zone-end
## Next steps

> [!div class="nextstepaction"]
> [Foundry Local](./foundry-local.md)
