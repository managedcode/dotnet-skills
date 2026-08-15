---
title: "Ollama"
description: "Learn how to use Ollama as a provider for Agent Framework agents."
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: reference
ms.author: edvan
ms.date: 07/28/2026
ms.service: agent-framework
---

# Ollama

Ollama allows you to run open-source models locally and use them with Agent Framework. This is ideal for development, testing, and scenarios where you need to keep data on-premises.

:::zone pivot="programming-language-csharp"

## Prerequisites

- Install and start [Ollama](https://ollama.com/).
- Download a model, such as `ollama pull llama3.2`.

## Installation

```bash
dotnet add package OllamaSharp
dotnet add package Microsoft.Agents.AI --prerelease
```

## Configuration

```bash
OLLAMA_ENDPOINT="http://localhost:11434"
OLLAMA_MODEL_NAME="llama3.2"
```

## Create an Ollama agent

:::code language="csharp" source="~/../agent-framework-code/dotnet/samples/02-agents/AgentProviders/ollama/Agent_With_Ollama/Program.cs" range="5-17":::

:::zone-end

:::zone pivot="programming-language-python"

## Prerequisites

Ensure [Ollama](https://ollama.com/) is installed and running locally with a model downloaded before running any examples:

```bash
ollama pull llama3.2
```

> [!NOTE]
> Not all models support function calling. For tool usage, try `llama3.2` or `qwen3:4b`.

## Installation

# [Native Ollama](#tab/ollama-native)

```bash
pip install agent-framework-ollama --pre
```

# [OpenAI Compatible](#tab/ollama-openai)

```bash
pip install agent-framework
```

---

## Configuration

# [Native Ollama](#tab/ollama-native)

```bash
OLLAMA_MODEL="llama3.2"
```

The native client connects to `http://localhost:11434` by default. Override it with the `OLLAMA_HOST` environment variable or the `host` constructor argument.

# [OpenAI Compatible](#tab/ollama-openai)

```bash
OLLAMA_ENDPOINT="http://localhost:11434/v1/"
OLLAMA_MODEL="llama3.2"
```

---

## Create Ollama Agents

# [Native Ollama](#tab/ollama-native)

`OllamaChatClient` provides native Ollama integration with full support for function tools and streaming.

```python
import asyncio
from agent_framework import Agent
from agent_framework.ollama import OllamaChatClient

async def main():
    agent = Agent(
        client=OllamaChatClient(),
        name="HelpfulAssistant",
        instructions="You are a helpful assistant running locally via Ollama.",
    )
    result = await agent.run("What is the largest city in France?")
    print(result)

asyncio.run(main())
```

# [OpenAI Compatible](#tab/ollama-openai)

You can also use `OpenAIChatClient` with a custom base URL pointing to your Ollama instance.

```python
import asyncio
import os
from agent_framework import Agent
from agent_framework.openai import OpenAIChatClient

async def main():
    agent = Agent(
        client=OpenAIChatClient(
            api_key="ollama",  # Placeholder, Ollama doesn't require an API key
            base_url=os.environ["OLLAMA_ENDPOINT"],
            model=os.environ["OLLAMA_MODEL"],
        ),
        name="HelpfulAssistant",
        instructions="You are a helpful assistant running locally via Ollama.",
    )
    result = await agent.run("What is the largest city in France?")
    print(result)

asyncio.run(main())
```

---

## Tools

The Python Ollama clients (`OllamaChatClient` and `OpenAIChatClient` pointed at an Ollama-compatible endpoint) support locally invoked tools. Hosted tool types do not exist because Ollama is a local model runtime.

| Tool | Status | Notes |
|---|---|---|
| [Function Tools](#function-tools) | ✅ | Standard Python callables or `@ai_function`. Whether the selected model can actually call them depends on the model itself. |
| [Tool Approval](../../../agents/tools/tool-approval.md) | ✅ | Provided by the framework's function-invoking chat client; works with any function-tool call. |
| [Code Interpreter](../../../agents/tools/code-interpreter.md) | ❌ | No hosted code interpreter. |
| [File Search](../../../agents/tools/file-search.md) | ❌ | No hosted file search. |
| [Web Search](../../../agents/tools/web-search.md) | ❌ | No hosted web search. |
| [Hosted MCP Tools](../../../agents/tools/hosted-mcp-tools.md) | ❌ | Ollama does not expose hosted MCP. |
| [Local MCP Tools](../../../agents/tools/local-mcp-tools.md) | ✅ | Runs in your process and works with any chat client. |

## Function Tools

# [Native Ollama](#tab/ollama-native)

```python
import asyncio
from datetime import datetime
from agent_framework import Agent
from agent_framework.ollama import OllamaChatClient

def get_time(location: str) -> str:
    """Get the current time."""
    return f"The current time in {location} is {datetime.now().strftime('%I:%M %p')}."

async def main():
    agent = Agent(
        client=OllamaChatClient(),
        name="TimeAgent",
        instructions="You are a helpful time agent.",
        tools=get_time,
    )
    result = await agent.run("What time is it in Seattle?")
    print(result)

asyncio.run(main())
```

# [OpenAI Compatible](#tab/ollama-openai)

```python
import asyncio
import os
from datetime import datetime
from agent_framework import Agent
from agent_framework.openai import OpenAIChatClient

def get_time(location: str) -> str:
    """Get the current time."""
    return f"The current time in {location} is {datetime.now().strftime('%I:%M %p')}."

async def main():
    agent = Agent(
        client=OpenAIChatClient(
            api_key="ollama",
            base_url=os.environ["OLLAMA_ENDPOINT"],
            model=os.environ["OLLAMA_MODEL"],
        ),
        name="TimeAgent",
        instructions="You are a helpful time agent.",
        tools=get_time,
    )
    result = await agent.run("What time is it in Seattle?")
    print(result)

asyncio.run(main())
```

---

## Streaming

```python
from agent_framework import Agent
from agent_framework.ollama import OllamaChatClient

async def streaming_example():
    agent = Agent(
        client=OllamaChatClient(),
        instructions="You are a helpful assistant.",
    )
    print("Agent: ", end="", flush=True)
    async for chunk in agent.run("Tell me about Python.", stream=True):
        if chunk.text:
            print(chunk.text, end="", flush=True)
    print()
```

:::zone-end

:::zone pivot="programming-language-go"

> [!NOTE]
> Go support for this feature is coming soon. See the [Agent Framework Go repository](https://github.com/microsoft/agent-framework-go) for the latest status.

:::zone-end
## Next steps

> [!div class="nextstepaction"]
> [GitHub Copilot](../agent-services/github-copilot.md)
