---
title: DevUI Directory Discovery
description: Learn how to structure your agents and workflows for automatic discovery by DevUI.
author: moonbox3
ms.topic: how-to
ms.author: evmattso
ms.date: 12/10/2025
ms.service: agent-framework
zone_pivot_groups: programming-languages
---

# Directory Discovery

DevUI can automatically discover agents and workflows from a directory structure. This enables you to organize multiple entities and launch them all with a single command.

::: zone pivot="programming-language-csharp"

## Coming Soon

DevUI documentation for C# is coming soon. Please check back later or refer to the Python documentation for conceptual guidance.

::: zone-end

::: zone pivot="programming-language-python"

## Directory Structure

For your agents and workflows to be discovered by DevUI, they must be organized in a specific directory structure. Each entity must have an `__init__.py` file that exports the required variable (`agent` or `workflow`).

```
entities/
    weather_agent/
        __init__.py      # Must export: agent = ChatAgent(...)
        agent.py         # Agent implementation (optional, can be in __init__.py)
        .env             # Optional: API keys, config vars
    my_workflow/
        __init__.py      # Must export: workflow = WorkflowBuilder()...
        workflow.py      # Workflow implementation (optional)
        .env             # Optional: environment variables
    .env                 # Optional: shared environment variables
```

## Agent Example

Create a directory for your agent with the required `__init__.py`:

**`weather_agent/__init__.py`**:

```python
from agent_framework import ChatAgent
from agent_framework.openai import OpenAIChatClient

def get_weather(location: str) -> str:
    """Get weather for a location."""
    return f"Weather in {location}: 72F and sunny"

agent = ChatAgent(
    name="weather_agent",
    chat_client=OpenAIChatClient(),
    tools=[get_weather],
    instructions="You are a helpful weather assistant."
)
```

The key requirement is that the `__init__.py` file must export a variable named `agent` (for agents) or `workflow` (for workflows).

## Workflow Example

**`my_workflow/__init__.py`**:

```python
from agent_framework.workflows import WorkflowBuilder

workflow = (
    WorkflowBuilder()
    .add_executor(...)
    .add_edge(...)
    .build()
)
```

## Environment Variables

DevUI automatically loads `.env` files if present:

1. **Entity-level `.env`**: Placed in the agent/workflow directory, loaded only for that entity
2. **Parent-level `.env`**: Placed in the entities root directory, loaded for all entities

Example `.env` file:

```bash
OPENAI_API_KEY=sk-...
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/
```

> [!TIP]
> Create a `.env.example` file to document required environment variables without exposing actual values. Never commit `.env` files with real credentials to source control.

## Launching with Directory Discovery

Once your directory structure is set up, launch DevUI:

```bash
# Discover all entities in ./entities directory
devui ./entities

# With custom port
devui ./entities --port 9000

# With auto-reload for development
devui ./entities --reload
```

## Sample Gallery

When DevUI starts with no discovered entities, it displays a **sample gallery** with curated examples from the Agent Framework repository. You can:

- Browse available sample agents and workflows
- Download samples to review and customize
- Run samples locally to get started quickly

## Troubleshooting

### Entity not discovered

- Ensure the `__init__.py` file exports `agent` or `workflow` variable
- Check for syntax errors in your Python files
- Verify the directory is directly under the path passed to `devui`

### Environment variables not loaded

- Ensure the `.env` file is in the correct location
- Check file permissions
- Use `--reload` flag to pick up changes during development

::: zone-end

## Next Steps

- [API Reference](./api-reference.md) - Learn about the OpenAI-compatible API
- [Tracing & Observability](./tracing.md) - Debug your agents with traces
