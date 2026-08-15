---
title: Tools Overview
description: Overview of tool types available in Agent Framework and provider support matrix.
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: reference
ms.author: edvan
ms.date: 07/01/2026
ms.service: agent-framework
---

# Tools Overview

Agent Framework supports many different types of tools that extend agent capabilities. Tools allow agents to interact with external systems, execute code, search data, and more.

## Tool Types

:::zone pivot="programming-language-csharp"

| Tool Type | Description |
|-----------|-------------|
| [Function Tools](./function-tools.md) | Custom code that agents can call during conversations |
| [Code Interpreter](./code-interpreter.md) | Execute code in a sandboxed environment |
| [File Search](./file-search.md) | Search through uploaded files |
| [Web Search](./web-search.md) | Search the web for information |
| [Hosted MCP Tools](./hosted-mcp-tools.md) | MCP servers invoked by the provider runtime |
| [Local MCP Tools](./local-mcp-tools.md) | MCP servers running locally or on custom hosts |
| [Microsoft Foundry Toolbox](../../integrations/by-component/tools/foundry-toolbox.md) | Named, versioned bundles of hosted tool configurations managed in a Foundry project |
| [Shell tools](../../integrations/by-component/tools/shell-tools.md) | Local and containerized shell execution with environment probing and policy controls |

:::zone-end

:::zone pivot="programming-language-python"

| Tool Type | Description |
|-----------|-------------|
| [Function Tools](./function-tools.md) | Custom code that agents can call during conversations |
| [Code Interpreter](./code-interpreter.md) | Execute code in a sandboxed environment |
| [File Search](./file-search.md) | Search through uploaded files |
| [Web Search](./web-search.md) | Search the web for information |
| [Hosted MCP Tools](./hosted-mcp-tools.md) | MCP servers invoked by the provider runtime |
| [Local MCP Tools](./local-mcp-tools.md) | MCP servers running locally or on custom hosts |
| [Microsoft Foundry Toolbox](../../integrations/by-component/tools/foundry-toolbox.md) | Named, versioned bundles of hosted tool configurations managed in a Foundry project |
| [Shell tools](../../integrations/by-component/tools/shell-tools.md) | Local and containerized shell execution with environment probing and policy controls |
| [Image Generation](../../integrations/by-component/model-providers/microsoft-foundry.md#image-generation) | Hosted image generation on the Foundry / OpenAI Responses runtime |
| [Shell](../../integrations/by-component/model-providers/openai.md#tools) | Hosted shell execution on the OpenAI Responses runtime — distinct from the GitHub Copilot CLI's built-in shell/file/URL runtime tools |
| [Bing Grounding](../../integrations/by-component/model-providers/microsoft-foundry.md#bing-grounding) | Web grounding via your own Grounding with Bing Search resource — experimental |
| [Bing Custom Search](../../integrations/by-component/model-providers/microsoft-foundry.md#bing-custom-search) | Bing grounding restricted to a curated domain list — preview |
| [Azure AI Search](../../integrations/by-component/model-providers/microsoft-foundry.md#azure-ai-search) | Query an Azure AI Search index through a Foundry connection — experimental |
| [SharePoint](../../integrations/by-component/model-providers/microsoft-foundry.md#sharepoint) | Ground answers in SharePoint content — preview |
| [Microsoft Fabric](../../integrations/by-component/model-providers/microsoft-foundry.md#microsoft-fabric) | Query a Fabric data agent — preview |
| [Memory Search](../../integrations/by-component/model-providers/microsoft-foundry.md#memory-search) | Search a Foundry-managed memory store — preview |
| [Computer Use](../../integrations/by-component/model-providers/microsoft-foundry.md#computer-use) | Drive a desktop or browser environment — preview |
| [Browser Automation](../../integrations/by-component/model-providers/microsoft-foundry.md#browser-automation) | Drive a browser via Azure Playwright — preview |
| [Agent-to-Agent (A2A) tool](../../integrations/by-component/model-providers/microsoft-foundry.md#agent-to-agent-a2a) | Call a remote A2A agent as a tool from a Foundry agent — preview |

> [!NOTE]
> Tools marked **experimental** or **preview** are documented on the relevant provider page and emit an `ExperimentalWarning` the first time they are used in a process.

:::zone-end

:::zone pivot="programming-language-go"

| Tool Type | Package | Description |
|---|---|---|
| [Function Tools](./function-tools.md) | `tool/functool` | Typed Go functions with JSON schemas that the agent can call |
| [Agent as Function Tool](#using-an-agent-as-a-function-tool) | `tool/agenttool` | Wrap an agent as a `tool.FuncTool` so another agent can call it |
| [Local MCP Tools](./local-mcp-tools.md) | `tool/mcptool` | Connect to MCP servers and expose their tools as `tool.FuncTool` values |
| [Web Search](./web-search.md) | `tool/hostedtool.WebSearch` | Declare provider-side web search when the backing service supports it |
| [File Search](./file-search.md) | `tool/hostedtool.FileSearch` | Declare provider-side file or vector-store search |
| [Code Interpreter](./code-interpreter.md) | `tool/hostedtool.CodeInterpreter` | Declare provider-side code execution |
| [Hosted MCP Tools](./hosted-mcp-tools.md) | `tool/hostedtool.MCPServer` | Declare an MCP server for the provider runtime to call |
| [Local shell tool](./function-tools.md#use-the-local-shell-tool) | `tool/shelltool` | Run local shell commands through a function tool that requires approval by default |

All tools implement the `tool.Tool` interface:

```go
type Tool interface {
    Name() string
    Description() string
}
```

Function tools additionally implement `tool.FuncTool`:

```go
import "context"

type FuncTool interface {
    Tool
    Schema() any
    ReturnSchema() any
    Call(ctx context.Context, arguments string) (any, error)
}
```

Most applications create function tools with `functool.New` or `functool.MustNew` rather than implementing `FuncTool` directly. The framework uses the Go function signature or struct tags to build the schema exposed to the model.

Pass tools to the agent via `agent.Config.Tools`:

```go
a := foundryprovider.NewAgent(endpoint, token, foundryprovider.ModelDeployment(model), foundryprovider.AgentConfig{
    Instructions: "You are a helpful assistant.",
    Config: agent.Config{
        Tools: []tool.Tool{weatherTool, calculatorTool},
    },
})
```

Or add tools per-run:

```go
resp, err := a.RunText(ctx, "What's the weather?", agent.WithTool(weatherTool)).Collect()
```

:::zone-end

## Tool Approval

[Tool Approval](./tool-approval.md) is a framework feature that lets you gate tool invocations through a human-in-the-loop decision before the model receives the result. It works with providers whose clients invoke tools locally; service-side hosted tools follow the provider's own approval behavior. See the [Tool Approval](./tool-approval.md) page for the full pattern, including how approvals interact with sessions, streaming, and middleware.

:::zone pivot="programming-language-go"

For Go, mark an invocable tool with `tool.ApprovalRequiredFunc` or use a tool that already implements `tool.ApprovalRequiredTool`, such as the local shell tool. Approval requests and responses flow through the tool auto-call middleware, so they work with providers that return local function calls.

:::zone-end

:::zone pivot="programming-language-csharp"

## Provider Support Matrix

The OpenAI and Azure OpenAI providers each offer two client types — Responses and Chat Completion — with different tool capabilities. Azure OpenAI clients mirror their OpenAI equivalents. [Copilot Studio](../../integrations/by-component/agent-services/copilot-studio.md) and [A2A](../../integrations/by-component/agent-services/a2a.md) agents run on a remote service so their capabilities are configured on the remote agent rather than through the Agent Framework client — they are not listed in the matrix.

| Tool Type | [Responses](../../integrations/by-component/model-providers/openai.md#tools) | [Chat Completion](../../integrations/by-component/model-providers/openai.md#tools) | [Foundry](../../integrations/by-component/model-providers/microsoft-foundry.md#tools) | [Anthropic](../../integrations/by-component/model-providers/anthropic.md#tools) | [Ollama](../../integrations/by-component/model-providers/ollama.md#tools) | [GitHub Copilot](../../integrations/by-component/agent-services/github-copilot.md#tools) |
|-----------|:---:|:---:|:---:|:---:|:---:|:---:|
| [Function Tools](./function-tools.md) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| [Code Interpreter](./code-interpreter.md) | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ |
| [File Search](./file-search.md) | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ |
| [Web Search](./web-search.md) | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| [Hosted MCP Tools](./hosted-mcp-tools.md) | ✅ | ❌ | ✅ | ✅ | ❌ | ✅ |
| [Local MCP Tools](./local-mcp-tools.md) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

> [!NOTE]
> The **Responses** and **Chat Completion** columns apply to both OpenAI and Azure OpenAI — the Azure variants mirror the same tool support as their OpenAI counterparts. The deprecated OpenAI **Assistants** API is no longer documented; for migration guidance see the [Semantic Kernel migration guide](../../migration-guide/from-semantic-kernel/index.md).

:::zone-end

:::zone pivot="programming-language-python"

## Provider Support Matrix

The OpenAI and Azure OpenAI providers each offer multiple client types with different tool capabilities. Azure OpenAI clients mirror their OpenAI equivalents. The Foundry column applies to `FoundryChatClient` — for `FoundryAgent`, the tools are configured on the Foundry agent definition (see [What works and what doesn't with `FoundryAgent`](../../integrations/by-component/agent-services/foundry.md#what-works-and-what-doesnt-with-foundryagent)). [Copilot Studio](../../integrations/by-component/agent-services/copilot-studio.md) and [A2A](../../integrations/by-component/agent-services/a2a.md) agents run on a remote service so their capabilities are configured on the remote agent rather than through the Agent Framework client — they are not listed in the matrix.

| Tool Type | [Responses](../../integrations/by-component/model-providers/openai.md#tools) | [Chat Completion](../../integrations/by-component/model-providers/openai.md#tools) | [Foundry](../../integrations/by-component/model-providers/microsoft-foundry.md#tools) | [Anthropic](../../integrations/by-component/model-providers/anthropic.md#tools) | [Ollama](../../integrations/by-component/model-providers/ollama.md#tools) | [Foundry Local](../../integrations/by-component/model-providers/foundry-local.md#tools) | [GitHub Copilot](../../integrations/by-component/agent-services/github-copilot.md#tools) |
|-----------|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| [Function Tools](./function-tools.md) | ✅ | ✅ | ✅ | ✅ | ⚠️¹ | ⚠️¹ | ✅ |
| [Code Interpreter](./code-interpreter.md) | ✅ | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ |
| [File Search](./file-search.md) | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ |
| [Web Search](./web-search.md) | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| [Image Generation](../../integrations/by-component/model-providers/microsoft-foundry.md#image-generation) | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ |
| Hosted Shell (`get_shell_tool`) | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Built-in shell / file system / URL fetch | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅² |
| [Hosted MCP Tools](./hosted-mcp-tools.md) | ✅ | ❌ | ✅ | ✅ | ❌ | ❌ | ✅ |
| [Local MCP Tools](./local-mcp-tools.md) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| [Microsoft Foundry Toolbox](../../integrations/by-component/tools/foundry-toolbox.md) | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ |
| [Bing Grounding](../../integrations/by-component/model-providers/microsoft-foundry.md#bing-grounding) (experimental) | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ |
| [Bing Custom Search](../../integrations/by-component/model-providers/microsoft-foundry.md#bing-custom-search) (preview) | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ |
| [Azure AI Search](../../integrations/by-component/model-providers/microsoft-foundry.md#azure-ai-search) (experimental) | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ |
| [SharePoint](../../integrations/by-component/model-providers/microsoft-foundry.md#sharepoint) (preview) | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ |
| [Microsoft Fabric](../../integrations/by-component/model-providers/microsoft-foundry.md#microsoft-fabric) (preview) | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ |
| [Memory Search](../../integrations/by-component/model-providers/microsoft-foundry.md#memory-search) (preview) | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ |
| [Computer Use](../../integrations/by-component/model-providers/microsoft-foundry.md#computer-use) (preview) | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ |
| [Browser Automation](../../integrations/by-component/model-providers/microsoft-foundry.md#browser-automation) (preview) | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ |
| [Agent-to-Agent (A2A) tool](../../integrations/by-component/model-providers/microsoft-foundry.md#agent-to-agent-a2a) (preview) | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ |

¹ Depends on the chosen local model supporting function calling.
² Built into the GitHub Copilot CLI runtime, gated by a permission handler. Different surface from OpenAI's `get_shell_tool`.

> [!NOTE]
> The **Responses** and **Chat Completion** columns apply to both OpenAI and Azure OpenAI — the Azure variants mirror the same tool support as their OpenAI counterparts. Local MCP Tools work with any provider that supports function tools.

:::zone-end

:::zone pivot="programming-language-go"

## Provider Support Matrix

The Go SDK exposes Microsoft Foundry through `foundryprovider` and OpenAI/Azure OpenAI through `openaiprovider`. Hosted tools in `tool/hostedtool` are declarations: the Go SDK sends them to the provider, and the provider decides whether that hosted capability is available.

| Tool Type | [Foundry](../../integrations/by-component/model-providers/microsoft-foundry.md#tools) | [Responses](../../integrations/by-component/model-providers/openai.md#tools) | [Chat Completions](../../integrations/by-component/model-providers/openai.md#tools) | [Anthropic](../../integrations/by-component/model-providers/anthropic.md#tools) |
|-----------|:---:|:---:|:---:|:---:|
| [Function Tools](./function-tools.md) | ✅ | ✅ | ✅ | ✅ |
| [Agent as Function Tool](#using-an-agent-as-a-function-tool) | ✅ | ✅ | ✅ | ✅ |
| [Local MCP Tools](./local-mcp-tools.md) | ✅ | ✅ | ✅ | ✅ |
| [Web Search](./web-search.md) | ✅ | ✅ | ✅ | ❌ |
| [File Search](./file-search.md) | ❌ | ✅ | ❌ | ❌ |
| [Code Interpreter](./code-interpreter.md) | ✅ | ✅ | ❌ | ❌ |
| [Hosted MCP Tools](./hosted-mcp-tools.md) | ❌ | ✅ | ❌ | ❌ |
| [Local shell tool](./function-tools.md#use-the-local-shell-tool) | ✅ | ✅ | ✅ | ✅ |

> [!NOTE]
> Local MCP tools and the local shell tool are function tools from the provider's point of view, so they follow function-tool support. Hosted tools such as `hostedtool.FileSearch`, `hostedtool.CodeInterpreter`, and `hostedtool.MCPServer` are executed by the AI service, not by the Go process.

:::zone-end

## Using an Agent as a Function Tool

You can use an agent as a function tool for another agent, enabling agent composition and more advanced workflows. The inner agent is converted to a function tool and provided to the outer agent, which can then call it as needed.

:::zone pivot="programming-language-csharp"

Call `.AsAIFunction()` on an `AIAgent` to convert it to a function tool that can be provided to another agent:

```csharp
// Create the inner agent with its own tools
AIAgent weatherAgent = new AIProjectClient(
    new Uri("<your-foundry-project-endpoint>"),
    new DefaultAzureCredential())
     .AsAIAgent(
        model: "gpt-4o-mini",
        instructions: "You answer questions about the weather.",
        name: "WeatherAgent",
        description: "An agent that answers questions about the weather.",
        tools: [AIFunctionFactory.Create(GetWeather)]);

// Create the main agent and provide the inner agent as a function tool
AIAgent agent = new AIProjectClient(
    new Uri("<your-foundry-project-endpoint>"),
    new DefaultAzureCredential())
     .AsAIAgent(
        model: "gpt-4o-mini",
        instructions: "You are a helpful assistant.",
        tools: [weatherAgent.AsAIFunction()]);

// The main agent can now call the weather agent as a tool
Console.WriteLine(await agent.RunAsync("What is the weather like in Amsterdam?"));
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

:::zone-end

:::zone pivot="programming-language-python"

Call `.as_tool()` on an agent to convert it to a function tool that can be provided to another agent:

```python
import os
from agent_framework.openai import OpenAIChatCompletionClient
from azure.identity import AzureCliCredential

# Create the inner agent with its own tools
weather_agent = OpenAIChatCompletionClient(
    model=os.environ["AZURE_OPENAI_CHAT_COMPLETION_MODEL"],
    azure_endpoint=os.environ["AZURE_OPENAI_ENDPOINT"],
    api_version=os.getenv("AZURE_OPENAI_API_VERSION"),
    credential=AzureCliCredential(),
).as_agent(
    name="WeatherAgent",
    description="An agent that answers questions about the weather.",
    instructions="You answer questions about the weather.",
    tools=get_weather
)

# Create the main agent and provide the inner agent as a function tool
main_agent = OpenAIChatCompletionClient(
    model=os.environ["AZURE_OPENAI_CHAT_COMPLETION_MODEL"],
    azure_endpoint=os.environ["AZURE_OPENAI_ENDPOINT"],
    api_version=os.getenv("AZURE_OPENAI_API_VERSION"),
    credential=AzureCliCredential(),
).as_agent(
    instructions="You are a helpful assistant.",
    tools=weather_agent.as_tool()
)

# The main agent can now call the weather agent as a tool
result = await main_agent.run("What is the weather like in Amsterdam?")
print(result.text)
```

You can also customize the tool name, description, and argument name:

```python
weather_tool = weather_agent.as_tool(
    name="WeatherLookup",
    description="Look up weather information for any location",
    arg_name="query",
    arg_description="The weather query or location"
)
```

:::zone-end

:::zone pivot="programming-language-go"

Wrap an agent with `agenttool.New` to make it available as a `tool.FuncTool` for another agent:

```go
import (
    "github.com/microsoft/agent-framework-go/agent"
    "github.com/microsoft/agent-framework-go/provider/foundryprovider"
    "github.com/microsoft/agent-framework-go/tool"
    "github.com/microsoft/agent-framework-go/tool/agenttool"
)

weatherAgent := foundryprovider.NewAgent(endpoint, token, foundryprovider.ModelDeployment(model), foundryprovider.AgentConfig{
    Instructions: "You answer questions about the weather.",
    Config: agent.Config{
        Name:        "WeatherAgent",
        Description: "An agent that answers weather questions.",
        Tools:       []tool.Tool{weatherTool},
    },
})

mainAgent := foundryprovider.NewAgent(endpoint, token, foundryprovider.ModelDeployment(model), foundryprovider.AgentConfig{
    Instructions: "You are a helpful assistant.",
    Config: agent.Config{
        Tools: []tool.Tool{agenttool.New(weatherAgent, agenttool.Config{})},
    },
})

resp, err := mainAgent.RunText(ctx, "Should I bring an umbrella to Amsterdam?").Collect()
```

You can also expose the same wrapped agent through MCP with `mcptool.AddTool`, because `agenttool.New` returns a function tool.

> [!TIP]
> See the [agent as function tool sample](https://github.com/microsoft/agent-framework-go/blob/main/examples/02-agents/agents/step12_as_function_tool/main.go) and the [agent as MCP tool sample](https://github.com/microsoft/agent-framework-go/blob/main/examples/02-agents/agents/step10_as_mcp_tool/main.go) for complete runnable examples.

:::zone-end

## Next steps

> [!div class="nextstepaction"]
> [Function Tools](./function-tools.md)
