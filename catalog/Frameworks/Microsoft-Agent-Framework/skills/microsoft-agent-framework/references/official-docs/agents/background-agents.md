---
title: Background Agents
description: Delegate concurrent work to background agents and manage task creation, completion, result retrieval, continuation, and cleanup.
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
  | Manual composition              | ✅ |   ✅   | ❌ | Go status guidance   |
  | Task lifecycle                  | ✅ |   ✅   | ✅ | Shared               |
  | Manual loop integration         | ✅ |   ✅   | ❌ | Go status guidance   |
  | Harness Agent setup           | ✅ |   ✅   | ❌ | Go status guidance   |
  | Security considerations         | ✅ |   ✅   | ✅ | Shared               |
-->

# Background agents

Background agents let a parent agent delegate independent tasks to named child agents. Each task runs concurrently in its own child-agent session, while the parent keeps a task ID that it can use to wait, retrieve results, continue work, or release the task.

> [!IMPORTANT]
> Background agents are experimental.

Background agents are different from [background responses](./background-responses.md). A background response represents one provider request that the application polls or resumes. A background-agent task invokes another Agent Framework agent and later feeds that agent's text result back to the parent.

## Set up background agents manually

Each child agent must have a nonempty, case-insensitively unique name. Give child agents focused instructions and only the tools needed for their delegated role.

::: zone pivot="programming-language-csharp"

Import `BackgroundAgentsProvider` and add it to a regular agent through `ChatClientAgentOptions.AIContextProviders`:

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

var backgroundProvider = new BackgroundAgentsProvider(
    [webSearchAgent, codeAnalysisAgent]);

AIAgent parentAgent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    Name = "research-coordinator",
    AIContextProviders = [backgroundProvider],
});

AgentSession session = await parentAgent.CreateSessionAsync();
```

`BackgroundAgentsProviderOptions` customizes the provider instructions and agent-list formatting.

::: zone-end

::: zone pivot="programming-language-python"

```python
from agent_framework import Agent, BackgroundAgentsProvider

background_provider = BackgroundAgentsProvider(
    [web_search_agent, code_analysis_agent]
)

parent_agent = Agent(
    client=client,
    name="research-coordinator",
    context_providers=[background_provider],
)
session = parent_agent.create_session()
```

Pass `instructions=` to `BackgroundAgentsProvider` to replace its instructions. Include `{background_agents}` where the formatted child-agent list should appear.

::: zone-end

::: zone pivot="programming-language-go"

> [!NOTE]
> The packaged background-agent provider described on this page isn't currently available in Go.

::: zone-end

## Task lifecycle

The provider adds the same model-facing tools in .NET and Python:

| Tool | Lifecycle action |
|---|---|
| `background_agents_start_task` | Start a nonblocking task on a named agent and return its integer task ID. |
| `background_agents_wait_for_first_completion` | Wait until the first task in a supplied set reaches a terminal state. |
| `background_agents_get_task_results` | Return completed text, a failure message, or the current status. |
| `background_agents_get_all_tasks` | List IDs, statuses, agent names, and descriptions. |
| `background_agents_continue_task` | Run follow-up input in the existing child session after a task completes or fails. |
| `background_agents_clear_completed_task` | Remove a terminal task and release its child session. |

A typical parent-agent sequence is:

1. Start every independent task before waiting, so the tasks run concurrently.
1. Wait for the first completion, retrieve that result, and repeat until no tasks are running.
1. Continue a completed or failed task when follow-up work needs its existing conversation context.
1. Clear terminal tasks after retrieving their results unless they will be continued.

Task status is `running`, `completed`, `failed`, or `lost`. A task becomes lost when its in-process task handle or child session is unavailable, such as after a process restart or session restore. Serializable task metadata can remain in the parent session, but in-flight work and child-session handles don't survive that boundary.

There is no cancellation tool in the provider. Let running tasks reach a terminal state before clearing them.

Reuse the same parent session across turns. Each task receives a dedicated child session. Continuing a terminal task reuses that child session; clearing it removes the task metadata and releases the child-session handle.

Task results are returned to the parent as text. The provider doesn't proxy a child's structured tool-approval request back through the parent, so configure child agents to complete delegated work without interactive approval or handle their approvals inside the child-agent host.

## Add automatic waiting manually

::: zone pivot="programming-language-csharp"

Wrap the manually composed parent with `LoopAgent`. `BackgroundTaskCompletionLoopEvaluator` continues only while a task remains in the `Running` state:

```csharp
AIAgent loopingParent = new LoopAgent(
    parentAgent,
    new BackgroundTaskCompletionLoopEvaluator(),
    new LoopAgentOptions { MaxIterations = 10 });
```

The evaluator stops for completed, failed, and lost tasks.

::: zone-end

::: zone pivot="programming-language-python"

Add `AgentLoopMiddleware` to the regular parent and pair the background-task predicate with its next-message helper:

```python
from agent_framework import (
    Agent,
    AgentLoopMiddleware,
    background_tasks_running,
    background_tasks_running_message,
)

parent_agent = Agent(
    client=client,
    context_providers=[background_provider],
    middleware=[
        AgentLoopMiddleware(
            background_tasks_running(),
            next_message=background_tasks_running_message,
            max_iterations=10,
        )
    ],
)
```

The predicate continues only while persisted task state still reports a running task.

::: zone-end

::: zone pivot="programming-language-go"

Automatic background-task loop integration isn't currently available in Go.

::: zone-end

## Use background agents with Harness Agent

Use this setup when you also want the Harness Agent's default planning, memory, approval, and observability pipeline.

::: zone pivot="programming-language-csharp"

Set `HarnessAgentOptions.BackgroundAgents`. Add the completion evaluator when the parent should keep running until delegated work is no longer running:

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

var options = new HarnessAgentOptions
{
    Name = "research-coordinator",
    BackgroundAgents = [webSearchAgent, codeAnalysisAgent],
    LoopEvaluators = [new BackgroundTaskCompletionLoopEvaluator()],
    LoopAgentOptions = new LoopAgentOptions { MaxIterations = 10 },
};

HarnessAgent parentAgent = chatClient.AsHarnessAgent(options);
// Equivalent construction: new HarnessAgent(chatClient, options)
AgentSession session = await parentAgent.CreateSessionAsync();
```

Use `HarnessAgentOptions.BackgroundAgentsProviderOptions` to customize provider instructions and agent-list formatting. Omitting `LoopEvaluators` keeps background delegation available without automatic re-invocation.

::: zone-end

::: zone pivot="programming-language-python"

Supply `background_agents` to `create_harness_agent`. Pair it with a bounded loop when the parent should wait automatically:

```python
from agent_framework import (
    background_tasks_running,
    background_tasks_running_message,
    create_harness_agent,
)

parent_agent = create_harness_agent(
    client=client,
    name="research-coordinator",
    background_agents=[web_search_agent, code_analysis_agent],
    loop_should_continue=background_tasks_running(),
    loop_next_message=background_tasks_running_message,
    loop_max_iterations=10,
)
session = parent_agent.create_session()
```

Use `background_agents_instructions` to replace the provider instructions. The Python harness enables tool auto-approval middleware by default, so pass `session` on every run.

::: zone-end

::: zone pivot="programming-language-go"

> [!NOTE]
> Harness Agent background delegation isn't currently available in Go.

::: zone-end

## Security considerations

Only register child agents you trust. The parent can send them text derived from private or untrusted context, and their results are added back to the parent's context. A compromised child can exfiltrate delegated input or return indirect prompt-injection content.

## Next steps

> [!div class="nextstepaction"]
> [Plan work and track todos](./planning-and-todos.md)

### Go deeper

- [Agent looping](./looping.md)
- [Background responses](./background-responses.md)
- [Sessions](../concepts/agents/conversations/session.md)
- [Agent Harness](../concepts/harness.md)
