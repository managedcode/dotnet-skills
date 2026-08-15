---
title: Planning and Todos
description: Structure long-running agent work with todo and agent-mode providers, custom persistence, and plan-execute patterns.
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: article
ms.author: westey
ms.date: 07/29/2026
ms.service: agent-framework
---

<!--
  Language parity table - keep in sync when adding/removing sections.

  | Section                         | C# | Python | Go | Notes                |
  |---------------------------------|:--:|:------:|:--:|:----------------------|
  | Todo tools and modes            | ✅ |   ✅   | ✅ | Shared               |
  | Manual composition              | ✅ |   ✅   | ❌ | Go status guidance   |
  | Change modes                    | ✅ |   ✅   | ❌ | Go status guidance   |
  | Manual loop integration         | ✅ |   ✅   | ❌ | Go status guidance   |
  | Harness Agent setup           | ✅ |   ✅   | ❌ | Go status guidance   |
-->

# Planning and todos

Two context providers support long-running work:

- A **todo provider** stores trackable work items and gives the agent tools to add, complete, remove, and inspect them.
- An **agent mode provider** stores the current operating mode and gives the agent tools to read or change it.

Compose these providers directly when you only need planning, or use the Harness Agent to enable both as part of its broader default pipeline.

## Todo tools

The .NET and Python providers expose the same model-facing tools:

| Tool | Purpose |
|---|---|
| `todos_add` | Add one or more items with a title and optional description. |
| `todos_complete` | Mark one or more items complete and include a completion reason. |
| `todos_remove` | Remove items that are no longer relevant. |
| `todos_get_remaining` | Return incomplete items. |
| `todos_get_all` | Return complete and incomplete items. |

The provider injects the current todo list before each run, so the agent can resume outstanding work.

## Plan and execute modes

`AgentModeProvider` supplies `plan` and `execute` modes by default:

1. **Plan** is interactive. The agent analyzes requirements, creates todos, asks clarifying questions, presents a plan, and asks before changing modes.
1. **Execute** is autonomous. The agent works through the plan, makes reasonable choices when details are ambiguous, and marks todos complete.

The provider exposes `mode_get` and `mode_set`. Its instructions tell the model to use `mode_set` only when the user explicitly allows the transition. Applications can also change the mode directly, which causes the provider to inject a mode-change notification on the next run.

## Set up planning and todos manually

::: zone pivot="programming-language-csharp"

Import and construct the providers, then add them through `ChatClientAgentOptions.AIContextProviders`:

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

var todoProvider = new TodoProvider();
var modeProvider = new AgentModeProvider(
    new AgentModeProviderOptions
    {
        DefaultMode = "plan",
    });

AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    AIContextProviders = [todoProvider, modeProvider],
});

AgentSession session = await agent.CreateSessionAsync();
```

Customize mode names and instructions with `AgentModeProviderOptions.Modes`. The .NET todo provider stores state in `AgentSession.StateBag`. `TodoProviderOptions` can replace its instructions, suppress the injected todo-list message, or provide a custom message builder.

The default `plan` instructions include writing the plan to file memory. If the manually composed agent doesn't provide file-memory tools, customize the mode instructions or add a suitable memory provider.

### Change modes from the application

```csharp
await modeProvider.SetModeAsync(session, "execute");
```

Use `GetModeAsync` to read the current mode.

::: zone-end

::: zone pivot="programming-language-python"

Import and construct the providers, then add them to a regular `Agent`:

```python
from agent_framework import (
    Agent,
    AgentModeProvider,
    TodoFileStore,
    TodoProvider,
)

todo_provider = TodoProvider(
    store=TodoFileStore("./todo-state"),
)
mode_provider = AgentModeProvider(
    default_mode="plan",
)

agent = Agent(
    client=client,
    context_providers=[todo_provider, mode_provider],
)

session = agent.create_session()
```

`TodoProvider` uses `TodoSessionStore` by default. Use `TodoFileStore` or a custom `TodoStore` when todo state must be stored outside the session payload. Customize modes with `AgentModeProvider(mode_instructions={...})`.

The default `plan` instructions include writing the plan to file memory. If the manually composed agent doesn't provide file-memory tools, customize `mode_instructions` or add a suitable memory provider.

### Change modes from the application

```python
from agent_framework import get_agent_mode, set_agent_mode

set_agent_mode(
    session,
    "execute",
    source_id=mode_provider.source_id,
    available_modes=mode_provider.available_modes,
)

current_mode = get_agent_mode(
    session,
    source_id=mode_provider.source_id,
    default_mode=mode_provider.default_mode,
    available_modes=mode_provider.available_modes,
)
```

::: zone-end

::: zone pivot="programming-language-go"

> [!NOTE]
> The packaged todo and agent-mode providers described on this page aren't currently available in Go.

::: zone-end

## Run the plan to completion manually

Todo tracking records progress but doesn't by itself re-invoke the agent. Combine it with a bounded [agent loop](./looping.md) when execute mode should continue until every todo is complete:

::: zone pivot="programming-language-csharp"

Wrap the manually composed agent with `LoopAgent`. `TodoCompletionLoopEvaluator` can restrict looping to selected modes:

```csharp
AIAgent loopingAgent = new LoopAgent(
    agent,
    new TodoCompletionLoopEvaluator(
        new TodoCompletionLoopEvaluatorOptions
        {
            Modes = ["execute"],
        }),
    new LoopAgentOptions { MaxIterations = 10 });
```

::: zone-end

::: zone pivot="programming-language-python"

Add `AgentLoopMiddleware` to the regular agent and use `todos_remaining()` with a mode filter:

```python
from agent_framework import (
    Agent,
    AgentLoopMiddleware,
    todos_remaining,
    todos_remaining_message,
)

agent = Agent(
    client=client,
    context_providers=[todo_provider, mode_provider],
    middleware=[
        AgentLoopMiddleware(
            todos_remaining(looping_modes=["execute"]),
            next_message=todos_remaining_message,
            max_iterations=10,
        )
    ],
)
```

::: zone-end

::: zone pivot="programming-language-go"

Todo-driven loop integration isn't currently available in Go.

::: zone-end

## Use planning and todos with Harness Agent

Use this setup when you also want the Harness Agent's preconfigured history, memory, approval, and observability pipeline.

::: zone pivot="programming-language-csharp"

`HarnessAgent` enables `TodoProvider` and `AgentModeProvider` by default. Configure the mode provider and optional todo-driven loop through `HarnessAgentOptions`:

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

var options = new HarnessAgentOptions
{
    AgentModeProviderOptions = new AgentModeProviderOptions
    {
        DefaultMode = "plan",
    },
    LoopEvaluators =
    [
        new TodoCompletionLoopEvaluator(
            new TodoCompletionLoopEvaluatorOptions
            {
                Modes = ["execute"],
            }),
    ],
    LoopAgentOptions = new LoopAgentOptions { MaxIterations = 10 },
};

HarnessAgent agent = chatClient.AsHarnessAgent(options);
// Equivalent construction: new HarnessAgent(chatClient, options)
AgentSession session = await agent.CreateSessionAsync();
```

Set `DisableTodoProvider` or `DisableAgentModeProvider` to remove a default provider. To use a configured `TodoProvider`, disable the default and add your instance through `AIContextProviders`. You can resolve enabled providers through `agent.GetService<TProvider>()`.

::: zone-end

::: zone pivot="programming-language-python"

`create_harness_agent` enables both providers by default. Supply configured instances to replace them and add an optional todo-driven loop:

```python
from agent_framework import (
    AgentModeProvider,
    TodoFileStore,
    TodoProvider,
    create_harness_agent,
    todos_remaining,
    todos_remaining_message,
)

todo_provider = TodoProvider(store=TodoFileStore("./todo-state"))
mode_provider = AgentModeProvider(default_mode="plan")

agent = create_harness_agent(
    client=client,
    todo_provider=todo_provider,
    mode_provider=mode_provider,
    loop_should_continue=todos_remaining(looping_modes=["execute"]),
    loop_next_message=todos_remaining_message,
    loop_max_iterations=10,
)
session = agent.create_session()
```

Set `disable_todo` or `disable_mode` to remove a default provider. The Python harness enables tool auto-approval middleware by default, so pass `session` on every run.

::: zone-end

::: zone pivot="programming-language-go"

> [!NOTE]
> Harness Agent planning and todo providers aren't currently available in Go.

::: zone-end

## Session behavior

Use the same [session](../concepts/agents/conversations/session.md) across turns. Mode state is session-backed in both SDKs. .NET todo state is stored in `AgentSession.StateBag`; Python uses `TodoSessionStore` by default, while `TodoFileStore` or a custom `TodoStore` can externalize todo persistence.

Changing mode from application code queues a one-time mode-change notification for the next run. The model-facing `mode_set` tool doesn't queue that extra notification because the model already observed its own tool call.

The plan-to-execute confirmation is instruction-level behavior, not a tool-approval request. The todo and mode tools themselves don't require function approval; application code can change modes directly when your host has already obtained the required permission.

## Next steps

> [!div class="nextstepaction"]
> [Understand the Agent Harness composition](../concepts/harness.md)

### Go deeper

- [Agent looping](./looping.md)
- [Sessions](../concepts/agents/conversations/session.md)
- [Context providers](../concepts/agents/conversations/context-providers.md)
