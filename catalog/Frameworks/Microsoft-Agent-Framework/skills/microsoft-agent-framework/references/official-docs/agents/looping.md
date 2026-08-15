---
title: Agent Looping
description: Re-invoke agents safely with bounded loops, completion evaluators, AI judges, progress feedback, and approval escape behavior.
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: article
ms.author: westey
ms.date: 07/30/2026
ms.service: agent-framework
---

<!--
  Language parity table - keep in sync when adding/removing sections.

  | Section                         | C# | Python | Go | Notes                |
  |---------------------------------|:--:|:------:|:--:|:----------------------|
  | Manual composition              | ✅ |   ✅   | ❌ | Go status guidance   |
  | Evaluators and AI judge         | ✅ |   ✅   | ❌ |                      |
  | Context, progress, and output   | ✅ |   ✅   | ❌ |                      |
  | Harness Agent setup           | ✅ |   ✅   | ❌ | Includes approval/session behavior; Go status guidance |
-->

# Agent looping

Agent looping re-invokes an agent until a completion condition is satisfied. Use it for iterative refinement, todo completion, waiting for background tasks, or evaluating whether an answer meets explicit criteria.

Always bound autonomous loops. A completion condition can fail, a model can stall, and an evaluator can be probabilistic.

> [!IMPORTANT]
> Agent looping is experimental.

## Set up looping manually

Use the direct composition API when you want looping without the other Harness Agent defaults.

::: zone pivot="programming-language-csharp"

Import the loop types and wrap any `AIAgent` with `LoopAgent`. Its default maximum is 10 agent invocations:

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

AIAgent baseAgent = chatClient.AsAIAgent();
AIAgent agent = new LoopAgent(
    baseAgent,
    new CompletionMarkerLoopEvaluator("DONE"),
    new LoopAgentOptions { MaxIterations = 5 });
```

::: zone-end

::: zone pivot="programming-language-python"

Import `AgentLoopMiddleware` and add it to a regular `Agent`. The default maximum is 10 agent runs:

```python
from agent_framework import Agent, AgentLoopMiddleware


def needs_more_work(*, last_result, **kwargs):
    return "DONE" not in last_result.text


agent = Agent(
    client=client,
    middleware=[
        AgentLoopMiddleware(
            needs_more_work,
            max_iterations=5,
        )
    ],
)
```

The predicate can be synchronous or asynchronous. Return `True` to continue, `False` to stop, or `(continue, feedback)` to pass feedback to the next iteration.

::: zone-end

::: zone pivot="programming-language-go"

> [!NOTE]
> The packaged looping capability described on this page isn't currently available in Go.

::: zone-end

## Choose a completion condition

::: zone pivot="programming-language-csharp"

`LoopAgent` accepts one evaluator or an ordered collection:

| Evaluator | Continues while |
|---|---|
| `CompletionMarkerLoopEvaluator` | The latest response doesn't contain the configured marker. |
| `TodoCompletionLoopEvaluator` | A resolved `TodoProvider` still has incomplete items, optionally in selected agent modes. |
| `BackgroundTaskCompletionLoopEvaluator` | A resolved `BackgroundAgentsProvider` still has running tasks. |
| `AIJudgeLoopEvaluator` | A separate judge client says the original request isn't fully answered. |
| `DelegateLoopEvaluator` | Your callback returns `LoopEvaluation.Continue(...)`. |

When multiple evaluators are configured, they run in order. The first evaluator that requests another iteration supplies its feedback; the loop stops only when all evaluators decline to continue.

### Use an AI judge

The judge receives the original request and latest agent response. If it finds a gap, its analysis becomes feedback for the next iteration:

```csharp
var evaluator = new AIJudgeLoopEvaluator(
    judgeClient,
    new AIJudgeLoopEvaluatorOptions
    {
        Criteria =
        [
            "Answer every part of the request.",
            "Support conclusions with evidence.",
        ],
    });

AIAgent loopAgent = new LoopAgent(
    agent,
    evaluator,
    new LoopAgentOptions { MaxIterations = 4 });
```

Only use a judge endpoint you trust with the original request and generated response.

### Control context and output

By default, `LoopAgent` reuses one session and sends the winning evaluator's latest feedback as the next input. `FreshContextPerIteration = true` instead rebuilds each pass from the original request plus an aggregated feedback log and resets or restores the session.

Non-streaming runs return an aggregated transcript by default. Set `NonStreamingReturnsLastResponseOnly = true` to return only the final response. Streaming always emits every iteration and any visible on-behalf-of feedback messages.

::: zone-end

::: zone pivot="programming-language-python"

The predicate receives keyword arguments including `iteration`, `last_result`, `messages`, `original_messages`, `session`, `agent`, `progress`, and `feedback`. The helpers `todos_remaining()` and `background_tasks_running()` provide built-in todo and background-task conditions. Pair them with `todos_remaining_message` or `background_tasks_running_message` to generate a targeted next input.

### Use an AI judge

`AgentLoopMiddleware.with_judge` builds a judge-driven loop. Judge loops default to five iterations:

```python
from agent_framework import Agent, AgentLoopMiddleware

loop = AgentLoopMiddleware.with_judge(
    judge_client,
    criteria=[
        "Answer every part of the request.",
        "Support conclusions with evidence.",
    ],
    max_iterations=4,
)

agent = Agent(
    client=client,
    middleware=[loop],
)
```

The judge's reasoning is fed back to the agent when more work is required. Only use a judge endpoint you trust with the original request and generated response.

### Control context, progress, and output

For advanced loops, construct `AgentLoopMiddleware` directly:

- `record_feedback` creates a concise progress entry after each work iteration.
- `progress` exposes accumulated entries to callbacks.
- `inject_progress=True` adds progress to the next iteration's input.
- `fresh_context=True` restarts from the original task and progress log and restores an attached session to its pre-loop snapshot.
- `return_final_only=True` returns only the last response for non-streaming runs.

Pass `max_iterations=None` only when the completion predicate is guaranteed to terminate.

::: zone-end

::: zone pivot="programming-language-go"

The packaged completion conditions and judge integration described on this page aren't currently available in Go.

::: zone-end

## Use looping with Harness Agent

Use the Harness Agent setup when you also want its preconfigured history, planning, memory, approval, and observability pipeline.

::: zone pivot="programming-language-csharp"

Set `HarnessAgentOptions.LoopEvaluators`. The harness applies `LoopAgent` as its outermost agent decorator:

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

var options = new HarnessAgentOptions
{
    LoopEvaluators =
    [
        new CompletionMarkerLoopEvaluator("DONE"),
    ],
    LoopAgentOptions = new LoopAgentOptions
    {
        MaxIterations = 5,
    },
};

HarnessAgent agent = chatClient.AsHarnessAgent(options);
// Equivalent construction: new HarnessAgent(chatClient, options)
AgentSession session = await agent.CreateSessionAsync();
```

An empty or `null` `LoopEvaluators` collection leaves the harness single-shot.

### Approval and session behavior

`LoopAgent` stops before evaluating its completion condition when an iteration returns a pending tool-approval request. It returns the request to the caller instead of hiding it behind another autonomous iteration. After the caller supplies the approval response through the normal [tool approval](./tools/tool-approval.md) flow, the agent can continue.

`LoopAgent` doesn't add approval handling itself. The Harness Agent applies the loop outside `ToolApprovalAgent`, allowing pending approval requests to escape the loop.

Reuse the same `AgentSession` across calls to continue the conversation. Loop iterations share that session by default. With `FreshContextPerIteration = true`, `LoopAgent` resets or restores caller-supplied session state where supported. Service-owned conversation storage can retain history when the serialized session contains only a remote conversation identifier.

::: zone-end

::: zone pivot="programming-language-python"

Supply `loop_should_continue` to `create_harness_agent`; `loop_max_iterations` defaults to 10:

```python
from agent_framework import create_harness_agent


def needs_more_work(*, last_result, **kwargs):
    return "DONE" not in last_result.text


agent = create_harness_agent(
    client=client,
    loop_should_continue=needs_more_work,
    loop_max_iterations=5,
)
session = agent.create_session()
```

`loop_next_message` customizes the next input. With no `loop_should_continue`, the factory doesn't add a loop and ignores the other loop arguments.

### Approval and session behavior

`AgentLoopMiddleware` stops before evaluating its continuation predicate when an iteration returns a pending tool-approval request. It returns the request to the caller instead of hiding it behind another autonomous iteration. After the caller supplies the approval response through the normal [tool approval](./tools/tool-approval.md) flow, the agent can continue.

`AgentLoopMiddleware` doesn't add `ToolApprovalMiddleware` itself. The Harness Agent places the loop outside its approval middleware, allowing pending approval requests to escape the loop. Create and pass an `AgentSession` on every Harness Agent run while tool auto-approval is enabled.

Reuse the same `AgentSession` across calls to continue the conversation. Loop iterations share that session by default. With `fresh_context=True`, the middleware restores the attached session to its pre-loop snapshot between iterations. Service-owned conversation storage can retain history when the serialized session contains only a remote conversation identifier.

::: zone-end

::: zone pivot="programming-language-go"

> [!NOTE]
> Harness Agent looping isn't currently available in Go, so its approval and session behavior doesn't apply.

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Delegate work to background agents](./background-agents.md)

### Go deeper

- [Planning and todos](./planning-and-todos.md)
- [Tool approval](./tools/tool-approval.md)
- [Agent Harness](../concepts/harness.md)
