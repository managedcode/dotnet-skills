---
title: DevUI Samples
description: Browse sample agents and workflows for use with DevUI.
author: moonbox3
ms.topic: reference
ms.author: evmattso
ms.date: 04/01/2026
ms.service: agent-framework
zone_pivot_groups: programming-languages
---

# Samples

This page provides links to sample agents and workflows designed for use with DevUI.

::: zone pivot="programming-language-csharp"

## Coming Soon

DevUI samples for C# are coming soon. Please check back later or refer to the Python samples for guidance.

::: zone-end

::: zone pivot="programming-language-python"

## Getting Started Samples

The Agent Framework repository includes sample agents and workflows in the `python/samples/02-agents/devui/` directory:

| Sample | Description |
|--------|-------------|
| [agent_weather](https://github.com/microsoft/agent-framework/tree/main/python/samples/02-agents/devui/agent_weather) | A weather agent using Microsoft Foundry |
| [agent_foundry](https://github.com/microsoft/agent-framework/tree/main/python/samples/02-agents/devui/agent_foundry) | Minimal agent using Microsoft Foundry |
| [workflow_declarative](https://github.com/microsoft/agent-framework/tree/main/python/samples/02-agents/devui/workflow_declarative) | YAML-defined workflow |
| [workflow_fanout](https://github.com/microsoft/agent-framework/tree/main/python/samples/02-agents/devui/workflow_fanout) | Workflow demonstrating fan-out/fan-in patterns |
| [workflow_spam](https://github.com/microsoft/agent-framework/tree/main/python/samples/02-agents/devui/workflow_spam) | Workflow for spam detection |
| [workflow_with_agents](https://github.com/microsoft/agent-framework/tree/main/python/samples/02-agents/devui/workflow_with_agents) | Multiple agents in a workflow |

## Running with DevUI

```bash
# Discover all samples
devui .

# Or run a specific sample
devui ./weather_agent_azure
```

## In-Memory Mode

The `in_memory_mode.py` script demonstrates running agents without directory discovery:

```bash
python in_memory_mode.py
```

This opens the browser with pre-configured agents and a basic workflow, showing how to use `serve()` programmatically.

## Sample Gallery

When DevUI starts with no discovered entities, it displays a **sample gallery** with curated examples. From the gallery, you can:

1. Browse available samples
2. View sample descriptions and requirements
3. Download samples to your local machine
4. Run samples directly

## Creating Your Own Samples

Follow the [Directory Discovery](./directory-discovery.md) guide to create your own agents and workflows compatible with DevUI.

### Minimal Agent Template

```python
# my_agent/__init__.py
from agent_framework import Agent
from agent_framework.openai import OpenAIChatClient

agent = Agent(
    name="my_agent",
    client=OpenAIChatClient(),
    instructions="You are a helpful assistant."
)
```

### Minimal Workflow Template

```python
# my_workflow/__init__.py
from agent_framework import WorkflowBuilder, WorkflowContext, executor
from typing_extensions import Never


@executor(id="my_executor")
async def my_executor(message: str, ctx: WorkflowContext[Never, str]) -> None:
    await ctx.yield_output(message)


workflow = WorkflowBuilder(start_executor=my_executor).build()
```

## Related Resources

- [DevUI Package README](https://github.com/microsoft/agent-framework/tree/main/python/packages/devui) - Full package documentation
- [Agent Framework Samples](https://github.com/microsoft/agent-framework/tree/main/python/samples) - All Python samples
- [Workflow Samples](https://github.com/microsoft/agent-framework/tree/main/python/samples/03-workflows) - Workflow-specific samples

::: zone-end

::: zone pivot="programming-language-go"

> [!NOTE]
> Go support for this feature is coming soon. See the [Agent Framework Go repository](https://github.com/microsoft/agent-framework-go) for the latest status.

::: zone-end
## Next Steps

- [Overview](./index.md) - Return to DevUI overview
- [Directory Discovery](./directory-discovery.md) - Learn about directory structure
- [API Reference](./api-reference.md) - Explore the API
