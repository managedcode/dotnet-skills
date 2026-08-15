---
title: Agent Harness
description: Understand how the Agent Framework Harness composes an agentic runtime and how to create and customize a harness agent.
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: article
ms.author: westey
ms.date: 07/29/2026
ms.service: agent-framework
---

<!--
  Language parity table - keep in sync when adding/removing sections.

  | Section               | C# | Python | Go | Notes                  |
  |-----------------------|:--:|:------:|:--:|:------------------------|
  | Architecture          | ✅ |   ✅   | ✅ | Shared                 |
  | Capability matrix     | ✅ |   ✅   | ✅ | Shared                 |
  | Create and customize  | ✅ |   ✅   | ❌ | Go status guidance     |
  | Sample terminal UX    | ✅ |   ✅   | ❌ | Go status guidance     |
-->

# Agent Harness

An *agent harness* is the runtime scaffolding that turns a language model into an agent that can perform work. It drives model and tool calls, manages conversation state and context, applies approval policies, and can keep the agent progressing through a multi-step task.

Agent Framework provides an opinionated, batteries-included Harness for research, coding, data analysis, and other long-running work. You provide a chat client and customize only the capabilities your application needs.

## Architecture

The Harness composes existing Agent Framework building blocks rather than defining a separate agent runtime:

1. **Chat client** — connects the agent to a model.
1. **Chat pipeline** — adds function invocation, message injection, per-service-call history persistence, and optional compaction.
1. **Agent and context providers** — add session-scoped instructions, tools, memory, todo state, operating modes, and optional capabilities.
1. **Middleware and decorators** — add approval handling, observability, and optional bounded looping.
1. **Application UX** — streams responses, displays progress, and collects input such as tool approvals.

The resulting object remains a normal Agent Framework agent: a `HarnessAgent` that derives from `AIAgent` in .NET, or an `Agent` returned by `create_harness_agent` in Python. Its sessions use the same [session](./agents/conversations/session.md#use-sessions-with-harnessed-agent) and [context provider](./agents/conversations/context-providers.md#use-context-providers-with-harnessed-agent) abstractions as other agents.

## Harness capability matrix

| Capability | Harness behavior | Canonical guidance |
|---|---|---|
| Function invocation | Enabled with a configurable per-request iteration limit. | [Function tools](../agents/tools/function-tools.md#use-function-tools-with-harnessed-agent) |
| Per-service-call history persistence | Persists history after each model call in a tool-calling run. | [Sessions](./agents/conversations/session.md#use-sessions-with-harnessed-agent) |
| Compaction | Enabled when token limits or a custom strategy are supplied. | [Compaction](./agents/conversations/compaction.md#use-compaction-with-harnessed-agent) |
| Todo tracking | Enabled by default. | [Planning and todos](../agents/planning-and-todos.md#use-planning-and-todos-with-harnessed-agent) |
| Agent modes | Plan and execute modes are enabled by default. | [Planning and todos](../agents/planning-and-todos.md#use-planning-and-todos-with-harnessed-agent) |
| File memory and file access | Session file memory is enabled by default; shared file access is opt-in. | [Context providers](./agents/conversations/context-providers.md#use-context-providers-with-harnessed-agent) |
| Tool approval | Standing approvals and auto-approval rules are enabled by default. | [Tool approval](../agents/tools/tool-approval.md#use-tool-approval-with-harnessed-agent) |
| OpenTelemetry | Agent observability is enabled by default. | [Observability](../agents/observability.md#use-observability-with-harnessed-agent) |
| Web search | Added by default where the selected chat client supports it. | [Web search](../agents/tools/web-search.md#use-web-search-with-harnessed-agent) |
| Agent Skills | Enabled by default in .NET; opt-in through a provider or paths in Python. | [Agent Skills](../agents/skills.md#use-agent-skills-with-harnessed-agent) |
| Background agents | Optional parallel delegation to named child agents. | [Background agents](../agents/background-agents.md#use-background-agents-with-harnessed-agent) |
| Shell execution | Composed from the shell package; the Python factory can wire it automatically. | [Shell tools](../integrations/by-component/tools/shell-tools.md#use-shell-tools-with-harnessed-agent) |
| Looping | Optional bounded re-invocation driven by evaluators or predicates. | [Agent looping](../agents/looping.md#use-looping-with-harnessed-agent) |

Background-agent delegation is separate from provider-managed [background responses](../agents/background-responses.md#use-background-responses-with-harnessed-agent). Background agents run child agents on delegated tasks; background responses poll or resume one provider request by using a continuation token.

::: zone pivot="programming-language-csharp"

## Create a harness agent

The `Microsoft.Agents.AI.Harness` package exposes `HarnessAgent` in the `Microsoft.Agents.AI` namespace. Create one from any `IChatClient` with `AsHarnessAgent`, or construct `HarnessAgent` directly:

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

AIAgent agent = chatClient.AsHarnessAgent();

AgentResponse response = await agent.RunAsync("Plan a weekend trip to Seattle.");
Console.WriteLine(response.Text);
```

Use `HarnessAgentOptions` to set harness-level operating guidance, agent-specific instructions, and feature options:

```csharp
AIAgent agent = chatClient.AsHarnessAgent(new HarnessAgentOptions
{
    Name = "research-agent",
    HarnessInstructions = "Use tools deliberately and report verified results.",
    ChatOptions = new ChatOptions
    {
        Instructions = "You are a research assistant focused on academic sources.",
    },
    MaxContextWindowTokens = 128_000,
    MaxOutputTokens = 16_384,
});
```

`HarnessAgent.DefaultInstructions` supplies the default harness guidance. `HarnessInstructions` appears before `ChatOptions.Instructions`.

## Customize the composition

Default capabilities have targeted options, including `DisableTodoProvider`, `DisableAgentModeProvider`, `DisableFileMemory`, `DisableAgentSkillsProvider`, `DisableWebSearch`, `DisableToolAutoApproval`, `DisableOpenTelemetry`, and `DisableCompaction`.

Add custom context providers with `AIContextProviders`. Opt in to file access with `FileAccessStore`, background delegation with `BackgroundAgents`, and looping with `LoopEvaluators`.

::: zone-end

::: zone pivot="programming-language-python"

## Create a harness agent

The `create_harness_agent` factory returns a fully configured `Agent`:

```python
from agent_framework import create_harness_agent
from agent_framework.openai import OpenAIChatClient

agent = create_harness_agent(
    client=OpenAIChatClient(model="gpt-4o"),
)

session = agent.create_session()
response = await agent.run("Plan a weekend trip to Seattle.", session=session)
print(response.text)
```

Set harness-level and agent-specific instructions separately:

```python
agent = create_harness_agent(
    client=client,
    name="research-agent",
    harness_instructions="Use tools deliberately and report verified results.",
    agent_instructions="You are a research assistant focused on academic sources.",
    max_context_window_tokens=128_000,
    max_output_tokens=16_384,
)
```

`DEFAULT_HARNESS_INSTRUCTIONS` supplies the default harness guidance. `harness_instructions` appears before `agent_instructions`.

## Customize the composition

Disable defaults with options such as `disable_todo`, `disable_mode`, `disable_file_memory`, `disable_web_search`, `disable_tool_auto_approval`, and `disable_compaction`.

Replace built-in providers with `todo_provider` or `mode_provider`, and add providers with `context_providers`. Skills are opt-in through `skills_provider` or `skills_paths`; file access, background agents, shell tooling, and looping are also opt-in.

> [!NOTE]
> `create_harness_agent` is released. Background agents, file access, and looping remain experimental, and shell tooling comes from the pre-release `agent-framework-tools` package.

::: zone-end

::: zone pivot="programming-language-go"

> [!NOTE]
> A packaged Go Harness isn't currently available. Compose the corresponding Go agent, context-provider, compaction, and middleware packages directly. See the [Agent Framework Go repository](https://github.com/microsoft/agent-framework-go) for current support.

::: zone-end

## Sample terminal UX

The Harness doesn't prescribe an application interface. The repository includes sample terminal applications that stream output, display todos and the current mode, surface tool-approval prompts, and provide commands such as `/todos`, `/mode`, and `/exit`.

> [!IMPORTANT]
> These console projects are samples, not shipped framework components. Use them as runnable examples or as a starting point for your own terminal experience.

::: zone pivot="programming-language-csharp"

The .NET sample entry point is `HarnessConsole.RunAgentAsync`:

```csharp
using Harness.Shared.Console;

await HarnessConsole.RunAgentAsync(
    agent,
    userPrompt: "Ask me anything to get started.");
```

Customize the sample with observers, tool formatters, command handlers, and `HarnessConsoleOptions`. See the [.NET Harness samples](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Harness).

::: zone-end

::: zone pivot="programming-language-python"

The Python sample uses the Textual-based `console` package beside the Harness samples:

```python
from console import run_agent_async

await run_agent_async(agent)
```

Customize the sample with observers, formatters, commands, and UI components. See the [Python Harness samples](https://github.com/microsoft/agent-framework/tree/main/python/samples/02-agents/harness).

::: zone-end

::: zone pivot="programming-language-go"

The repository doesn't currently include a packaged Go Harness terminal sample.

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Plan work and track todos](../agents/planning-and-todos.md#use-planning-and-todos-with-harnessed-agent)

### Go deeper

- [Looping](../agents/looping.md#use-looping-with-harnessed-agent)
- [Background agents](../agents/background-agents.md#use-background-agents-with-harnessed-agent)
- [Compaction](./agents/conversations/compaction.md#use-compaction-with-harnessed-agent)
- [Shell tools](../integrations/by-component/tools/shell-tools.md#use-shell-tools-with-harnessed-agent)
