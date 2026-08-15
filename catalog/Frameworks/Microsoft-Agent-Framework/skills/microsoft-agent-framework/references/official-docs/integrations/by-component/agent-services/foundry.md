---
title: Microsoft Foundry Agent Service
description: Connect Agent Framework applications to Microsoft Foundry Prompt Agents and Hosted Agents with FoundryAgent.
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 07/28/2026
ms.service: agent-framework
---

<!--
  Language parity table - keep in sync when adding/removing sections.

  | Section                    | C# | Python | Go | Notes                  |
  |----------------------------|:--:|:------:|:--:|:------------------------|
  | Configuration              | ✅ |   ✅   | ❌ |                        |
  | Prompt Agent connection    | ✅ |   ✅   | ❌ |                        |
  | Hosted Agent connection    | ✅ |   ✅   | ❌ |                        |
  | Runtime behavior           | ❌ |   ✅   | ❌ | Python API details     |
  | Hosted session management  | ❌ |   ✅   | ❌ | Preview Python API     |
  | Sessions and streaming     | ✅ |   ✅   | ❌ |                        |
  | Go availability            | ✅ |   ✅   | ✅ | Go zone is status only |
-->

# Microsoft Foundry Agent Service

`FoundryAgent` connects Agent Framework to an agent definition managed by Microsoft Foundry Agent Service. The agent's model, instructions, hosted tools, and version are configured in Foundry; your application connects to that definition and uses the standard Agent Framework run, streaming, and session APIs.

Use this integration for:

- **Prompt Agents**, which are named and versioned server-side agent definitions.
- **Hosted Agents**, which are deployed agent applications reached through an agent-specific endpoint.

For direct model inference where your application owns the agent definition, see [Microsoft Foundry model provider](../model-providers/microsoft-foundry.md). To deploy an Agent Framework application as a Hosted Agent, see [Foundry Hosted Agents](../../../hosting/foundry-hosted-agent.md).

:::zone pivot="programming-language-csharp"

## Install the packages

```bash
dotnet add package Azure.AI.Projects --prerelease
dotnet add package Azure.Identity
dotnet add package Microsoft.Agents.AI.Foundry --prerelease
```

## Connect to a Prompt Agent

Create an `AIProjectClient` for the Foundry project and wrap an `AgentReference` as a `FoundryAgent`. Pin the version when the application must use a specific Prompt Agent definition.

```csharp
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using Microsoft.Agents.AI.Foundry;

var projectClient = new AIProjectClient(
    new Uri(Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")!),
    new DefaultAzureCredential());

FoundryAgent agent = projectClient.AsAIAgent(
    new AgentReference(
        Environment.GetEnvironmentVariable("FOUNDRY_AGENT_NAME")!,
        Environment.GetEnvironmentVariable("FOUNDRY_AGENT_VERSION")!));

Console.WriteLine(await agent.RunAsync("What can you help me with?"));
```

You can also retrieve a `ProjectsAgentRecord` to use its latest version or a `ProjectsAgentVersion` to use an explicitly retrieved version, then pass that object to `projectClient.AsAIAgent(...)`.

### Retrieve the latest Prompt Agent version

Use `AgentAdministrationClient` when the application should resolve the latest registered version by name.

```csharp
ProjectsAgentRecord agentRecord =
    await projectClient.AgentAdministrationClient.GetAgentAsync(
        Environment.GetEnvironmentVariable("FOUNDRY_AGENT_NAME")!);

FoundryAgent latestAgent = projectClient.AsAIAgent(agentRecord);
Console.WriteLine(await latestAgent.RunAsync("What can you help me with?"));
```

> [!IMPORTANT]
> A `FoundryAgent` uses the model, instructions, and hosted tools stored in its Foundry definition. Configure those capabilities in Foundry; the client can't replace them at run time.

> [!WARNING]
> `DefaultAzureCredential` is convenient for development. In production, prefer a specific credential such as `ManagedIdentityCredential` to avoid unintended credential probing.

## Connect to a Hosted Agent

Hosted Agents expose an agent-specific OpenAI endpoint. Build the endpoint from the project endpoint and registered agent name, then pass it to `AIProjectClient.AsAIAgent(...)`.

:::code language="csharp" source="~/../agent-framework-code/dotnet/samples/04-hosting/FoundryHostedAgents/responses/Using-Samples/SimpleAgent/Program.cs" range="13-23,39-42":::

The endpoint's administrator-controlled version selector determines the active Hosted Agent version.

:::zone-end

:::zone pivot="programming-language-python"

## Install the packages

```bash
pip install agent-framework-foundry
```

## Configuration

```bash
FOUNDRY_PROJECT_ENDPOINT="https://<your-project>.services.ai.azure.com"
FOUNDRY_AGENT_NAME="my-agent"
FOUNDRY_AGENT_VERSION="1.0"
```

Use `FOUNDRY_AGENT_VERSION` for Prompt Agents. Hosted Agents can omit it.

## Connect to a Prompt Agent

Provide the project endpoint, agent name, and agent version. The service supplies the stored model, instructions, and hosted-tool configuration.

:::code language="python" source="~/../agent-framework-code/python/samples/02-agents/providers/foundry/foundry_agent_basic.py" range="22-38":::

If a Prompt Agent declares a local function tool, pass the matching callable through `tools=` when constructing `FoundryAgent` so the client can execute it when requested. See the [Prompt Agent publish and connect sample](https://github.com/microsoft/agent-framework/blob/main/python/samples/02-agents/providers/foundry/foundry_prompt_agents.py).

## Connect to a Hosted Agent

Hosted Agents don't require `agent_version`. Connect with the project endpoint and registered agent name.

:::code language="python" source="~/../agent-framework-code/python/samples/02-agents/providers/foundry/foundry_agent_hosted.py" range="24-33":::

## What works and what doesn't with `FoundryAgent`

`FoundryAgent` connects to an agent definition that already exists in Foundry. The stored instructions and tool configuration are authoritative, so client-side behavior differs from an application-owned `Agent(client=FoundryChatClient(...))`.

### Tools

| Tool type passed to `FoundryAgent(...)` | Behavior |
|---|---|
| `FunctionTool` with a local Python callable | Supported only when the matching function definition already exists on the Foundry agent. The callable runs in the application process when Foundry requests it. |
| Hosted tools, including web search, code interpreter, file search, MCP, image generation, and [Microsoft Foundry Toolbox](../tools/foundry-toolbox.md) | Configure these on the Foundry agent definition. Passing them client-side doesn't add them to the service-managed agent. |

For Toolbox attachment and direct MCP consumption guidance, see [Microsoft Foundry Toolbox](../tools/foundry-toolbox.md).

You can't register a new model-visible tool at construction time. Passing a function callable only supplies the local implementation for a function that the Foundry agent already declares.

### Context providers

| Context provider behavior | Works with `FoundryAgent`? |
|---|---|
| Adds messages, such as retrieved memory, RAG snippets, or user profile information | Yes. The injected context is forwarded with the request. |
| Persists or observes the conversation | Yes. The provider runs locally around the request and response. |
| Adds tools dynamically | No, unless those tools are already declared on the Foundry agent definition. |

Use `Agent(client=FoundryChatClient(...))` when the application needs dynamic tool selection, skill loading, or any behavior that changes model-visible tools at run time.

### Run options

Because the Foundry agent definition is the source of truth, not every option passed through `default_options` or `agent.run(...)` is honored.

| Option | Prompt Agent behavior |
|---|---|
| `model` | Ignored. The model comes from the Foundry agent definition. |
| `tools`, `tool_choice`, `parallel_tool_calls` | Removed from the request. Tools must be declared on the Foundry agent definition. |
| `instructions` and system or developer messages | Ignored. The stored Foundry instructions are authoritative. |
| `conversation_id` | Used and mapped to the Foundry agent session when applicable. |
| `extra_body` | Forwarded and merged with the framework-provided agent reference. |
| Sampling parameters, metadata, `user`, `store`, and `response_format` | Forwarded, but the Foundry agent or model configuration can override or constrain them. |

Hosted Agents receive the same client-side filtering, but the deployed agent can accept, ignore, or reinterpret any forwarded option. Verify behavior against the specific Hosted Agent.

> [!TIP]
> Use `Agent(client=FoundryChatClient(...))` when you need per-run control over instructions, generation options, or tools.

## Manage a Hosted Agent service session

Hosted Agents that use service-side sessions require the preview Responses surface:

Create the service session explicitly when the application must bind it to a tenant or user, then wrap its identifier as an Agent Framework session.

:::code language="python" source="~/../agent-framework-code/python/samples/04-hosting/foundry-hosted-agents/responses/using_deployed_agent.py" range="38-107":::

> [!TIP]
> See the [`using_deployed_agent.py` sample](https://github.com/microsoft/agent-framework/blob/main/python/samples/04-hosting/foundry-hosted-agents/responses/using_deployed_agent.py) for a complete example.

## Set a custom HTTP timeout

`FoundryAgent` inherits the OpenAI SDK timeout by default. Pass `timeout=` in seconds when multi-turn conversations or network conditions require a different limit.

```python
from agent_framework.foundry import FoundryAgent
from azure.identity import AzureCliCredential

agent = FoundryAgent(
    project_endpoint="https://your-project.services.ai.azure.com",
    agent_name="my-prompt-agent",
    credential=AzureCliCredential(),
    timeout=120.0,
)
```

The timeout is applied to a per-agent copy of the HTTP client and doesn't affect other agents that share the same `AIProjectClient`.

:::zone-end

:::zone pivot="programming-language-go"

> [!NOTE]
> `FoundryAgent` integration for Prompt and Hosted Agents isn't currently available for Agent Framework Go. See the [Agent Framework Go repository](https://github.com/microsoft/agent-framework-go) for the latest status.

:::zone-end

## Run, stream, and continue conversations

After connecting, use the same APIs as other Agent Framework agents:

- Run a request with `RunAsync` or `run`.
- Stream updates with `RunStreamingAsync` or `run(..., stream=True)`.
- Reuse an `AgentSession` to continue a conversation.
- Use Foundry server-side conversation APIs when the conversation must be visible and persisted in the Foundry project.

Keep Foundry agent names, versions, endpoints, and conversation identifiers in trusted server-side state. Authorize the caller before resuming any existing conversation.

## Next steps

> [!div class="nextstepaction"]
> [Review Microsoft Foundry model provider](../model-providers/microsoft-foundry.md)
