---
title: Microsoft Agent Framework Overview
description: "Build AI agents and multi-agent workflows in .NET, Python, and Go with Microsoft Agent Framework."
zone_pivot_groups: programming-languages
ms.topic: overview
ms.date: 07/29/2026
ms.service: agent-framework
author: moonbox3
ms.author: evmattso
ms.reviewer: ssalgado
---

# Microsoft Agent Framework

Agent Framework brings together four primary areas:

| | Description |
|---|---|
| **[Agents](../concepts/agents/index.md)** | Individual agents that use LLMs to process inputs, call [tools](../agents/tools/index.md) and [MCP servers](../agents/tools/hosted-mcp-tools.md), and generate responses. Supports Microsoft Foundry, Anthropic, Azure OpenAI, OpenAI, Ollama, and [more](../integrations/by-component/model-providers/index.md). |
| **[Harness Agent](../concepts/harness.md)** | An opinionated agent with batteries-included capabilities for long, multi-step tasks — planning and todo tracking, context compaction, file access and memory, don't-ask-again tool approval, and observability. |
| **[Workflows](../concepts/workflows/index.md)** | Functional and graph-based workflows that connect agents and functions through explicit execution paths. |
| **[Integrations](../integrations/index.md)** | Connections to model providers, agent services, tools, context providers, middleware, evaluation services, and UI frameworks, organized by provider and component. |

The framework also provides foundational building
blocks, including model clients (chat completions and responses), an agent session for state management, context providers for agent memory,
middleware for intercepting agent actions, and MCP clients for tool integration.
Together, these components give you the flexibility and power to build
interactive, robust, and safe AI applications.


:::zone pivot="programming-language-go"

> [!IMPORTANT]
> The Agent Framework for Go is in public preview. Declarative agents, RAG, CodeAct, and functional workflows are not yet available. File issues on GitHub (https://github.com/microsoft/agent-framework-go/issues).

:::zone-end

## Get started

:::zone pivot="programming-language-csharp"

```dotnetcli
dotnet add package Microsoft.Agents.AI.Foundry --prerelease
```

```csharp
using System;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;

AIAgent agent = new AIProjectClient(
        new Uri("https://your-foundry-service.services.ai.azure.com/api/projects/your-foundry-project"),
        new AzureCliCredential())
    .AsAIAgent(
        model: "gpt-5.4-mini",
        instructions: "You are a friendly assistant. Keep your answers brief.");

Console.WriteLine(await agent.RunAsync("What is the largest city in France?"));
```

:::zone-end

:::zone pivot="programming-language-python"

```bash
pip install agent-framework
```

```python
from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient
from azure.identity import AzureCliCredential

agent = Agent(
    client=FoundryChatClient(
        project_endpoint="https://your-foundry-service.services.ai.azure.com/api/projects/your-foundry-project",
        model="gpt-5.4-mini",
        credential=AzureCliCredential(),
    ),
    name="HelloAgent",
    instructions="You are a friendly assistant. Keep your answers brief.",
)
```

```python
# Non-streaming: get the complete response at once
result = await agent.run("What is the largest city in France?")
print(f"Agent: {result}")
```
:::zone-end

:::zone pivot="programming-language-go"

```bash
go get github.com/microsoft/agent-framework-go
```

```go
package main

import (
    "context"
    "fmt"
    "os"

    "github.com/microsoft/agent-framework-go/agent"
    "github.com/microsoft/agent-framework-go/provider/foundryprovider"

    "github.com/Azure/azure-sdk-for-go/sdk/azidentity"
)

func main() {
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
            Instructions: "You are a friendly assistant. Keep your answers brief.",
            Config: agent.Config{
                Name: "HelloAgent",
            },
        },
    )

    resp, err := a.RunText(context.Background(), "What is the largest city in France?").Collect()
    fmt.Println(resp, err)
}
```

:::zone-end

That's it — an agent that calls an LLM and returns a response. From here you can [add tools](../agents/tools/index.md), [multi-turn conversations](../concepts/agents/conversations/session.md), [middleware](../concepts/agents/middleware/index.md), and [workflows](#when-to-use-agents-vs-workflows) to build production applications.

:::zone pivot="programming-language-python"

> [!NOTE]
> Agent Framework does **not** automatically load `.env` files. To use a `.env` file, call `load_dotenv()` at the start of your application, or set environment variables directly in your shell or IDE.

:::zone-end

> [!div class="nextstepaction"]
> [Get Started — full tutorial](../get-started/your-first-agent.md)

## When to use agents vs workflows

| Use an agent when… | Use a workflow when… |
|---|---|
| The task is open-ended or conversational | The process has well-defined steps |
| You need autonomous tool use and planning | You need explicit control over execution order |
| A single LLM call (possibly with tools) suffices | Multiple agents or functions must coordinate |

_If you can write a function to handle the task, do that instead of using an AI agent._

## Why Agent Framework?

Agent Framework combines AutoGen's simple agent abstractions with Semantic Kernel's enterprise features — session-based state management, type safety, middleware, telemetry — and adds graph-based workflows for explicit multi-agent orchestration.

[Semantic Kernel](https://github.com/microsoft/semantic-kernel)
and [AutoGen](https://github.com/microsoft/autogen) pioneered the concepts of AI agents and multi-agent orchestration.
The Agent Framework is the direct successor, created by the same teams. It combines AutoGen's simple abstractions for single- and multi-agent patterns with Semantic Kernel's enterprise-grade features such as session-based state management, type safety, filters,
telemetry, and extensive model and embedding support. Beyond merging the two,
Agent Framework introduces workflows that give developers explicit control over
multi-agent execution paths, plus a robust state management system
for long-running and human-in-the-loop scenarios.
In short, Agent Framework is the next generation of
both Semantic Kernel and AutoGen.

To learn more about migrating from either Semantic Kernel or AutoGen,
see the [Migration Guide from Semantic Kernel](../migration-guide/from-semantic-kernel/index.md)
and [Migration Guide from AutoGen](../migration-guide/from-autogen/index.md).

Both Semantic Kernel and AutoGen have benefited significantly from the open-source community,
and the same is expected for Agent Framework. Microsoft Agent Framework welcomes contributions and will keep improving with new features and capabilities.

> [!IMPORTANT]
> If you use Microsoft Agent Framework to build applications that operate with any third-party servers, agents, code, or non-Azure Direct models ("Third-Party Systems"), you do so at your own risk. Third-Party Systems are Non-Microsoft Products under the Microsoft Product Terms and are governed by their own third-party license terms. You are responsible for any usage and associated costs.
>
> We recommend reviewing all data being shared with and received from Third-Party Systems and being cognizant of third-party practices for handling, sharing, retention and location of data. It is your responsibility to manage whether your data will flow outside of your organization's Azure compliance and geographic boundaries and any related implications, and that appropriate permissions, boundaries and approvals are provisioned.
>
> You are responsible for carefully reviewing and testing applications you build using Microsoft Agent Framework in the context of your specific use cases, and making all appropriate decisions and customizations. This includes implementing your own responsible AI mitigations such as metaprompt, content filters, or other safety systems, and ensuring your applications meet appropriate quality, reliability, security, and trustworthiness standards. See also: [Transparency FAQ](https://github.com/microsoft/agent-framework/blob/main/TRANSPARENCY_FAQS.md)

## Next steps

> [!div class="nextstepaction"]
> [Step 1: Your First Agent](../get-started/your-first-agent.md)

**Go deeper:**

- [Agents](../concepts/agents/index.md) — runtime and execution, agent types, conversations, middleware, and safety
- [Agent Harness](../concepts/harness.md) — architecture, capability composition, and customization for long-running work
- [Workflows](../concepts/workflows/index.md) — functional and graph APIs, execution, state, and advanced composition
- [Integrations](../integrations/index.md) — providers and components for models, agent services, tools, context, middleware, evaluation, and UI
