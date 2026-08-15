---
title: Resettable Executors
description: How to implement IResettableExecutor to safely reuse stateful executors across workflow runs.
zone_pivot_groups: programming-languages
author: peibekwe
ms.topic: article
ms.author: peibekwe
ms.date: 05/27/2026
ms.service: agent-framework
---


# Resettable Executors

::: zone pivot="programming-language-csharp"

## Overview

Executors in workflows are often stateful — for example, they may accumulate messages, track turn counts, or cache intermediate results. When a workflow is reused across multiple runs with shared executor instances, leftover state from a previous run can leak into subsequent runs, causing unexpected behavior or data corruption.

The `IResettableExecutor` interface solves this by providing a contract for executors to clear their internal state between runs. The workflow runtime automatically calls `ResetAsync()` on shared executor instances when a run completes, ensuring a clean slate for the next run.

## The Problem

Consider an executor that collects messages during a workflow run:

```csharp
internal sealed partial class AggregationExecutor() : Executor("AggregationExecutor")
{
    private readonly List<string> _messages = [];

    [MessageHandler]
    private async ValueTask HandleAsync(string message, IWorkflowContext context)
    {
        this._messages.Add(message);
        // Process aggregated messages...
    }
}
```

If this executor is shared across workflow runs, `_messages` retains data from the previous run. The second run would see stale messages that don't belong to it.

## The IResettableExecutor Interface

`IResettableExecutor` defines a single method that the workflow runtime calls between runs:

```csharp
public interface IResettableExecutor
{
    ValueTask ResetAsync();
}
```

When an executor implements this interface, the runtime can safely reset it after each run, allowing the workflow to be reused without stale state.

## Implementing IResettableExecutor

To make a stateful executor resettable, implement the interface and clear all mutable state in `ResetAsync()`:

```csharp
internal sealed partial class AggregationExecutor()
    : Executor("AggregationExecutor"), IResettableExecutor
{
    private readonly List<string> _messages = [];

    [MessageHandler]
    private async ValueTask HandleAsync(string message, IWorkflowContext context)
    {
        this._messages.Add(message);
        // Process aggregated messages...
    }

    public ValueTask ResetAsync()
    {
        this._messages.Clear();
        return default;
    }
}
```

For a complete working example of a workflow that uses resettable executors, see the [WorkflowAsAnAgent sample](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/Agents/WorkflowAsAnAgent).

## When to Implement

Not all executors need to implement `IResettableExecutor`. Use this decision guide:

| Scenario | Implement? | Reason |
|----------|:----------:|--------|
| Executor has mutable state (lists, counters, caches) and is shared across runs | **Yes** | State from one run would leak into the next |
| Executor is stateless | No | Nothing to reset |
| Executor is created fresh per workflow (via a factory method) | No | Each run gets a new instance with clean state |
| Executor is declared as cross-run shareable (`declareCrossRunShareable: true`) | No | Cross-run shareable executors support concurrent use without resetting |

> [!WARNING]
> If a shared stateful executor does not implement `IResettableExecutor`, reusing the workflow throws an `InvalidOperationException`:
>
> `"Cannot reuse Workflow with shared Executor instances that do not implement IResettableExecutor."`

## How the Runtime Uses It

The workflow runtime manages the reset lifecycle automatically. You do not need to call `ResetAsync()` yourself. The sequence is:

1. **Ownership acquired** — when a workflow run starts, the runtime takes ownership of the workflow instance and notes which executors need resetting.
2. **Run executes** — executors process messages and may accumulate state.
3. **Ownership released** — when the run completes (or is disposed), the runtime releases ownership and calls `ResetAsync()` on all shared executor instances that implement `IResettableExecutor`.
4. **Ready for reuse** — after a successful reset, the workflow can be used for a new run.

If any shared executor fails to reset (because it does not implement the interface), the workflow is marked as non-reusable and subsequent runs will throw.

## Relationship to State Isolation

`IResettableExecutor` complements the helper-method pattern described in [State Management](../state.md). The two approaches serve different needs:

- **Helper methods** (creating fresh instances per run) provide the strongest isolation guarantees and are recommended as the default approach.
- **`IResettableExecutor`** is useful when you need to share executor instances across runs — for example, when executor construction is expensive or when a workflow is exposed as an agent and reused across multiple invocations.

Choose the approach that best fits your scenario. For most workflows, helper methods are sufficient. Use `IResettableExecutor` when sharing instances is a deliberate design choice.

::: zone-end

::: zone pivot="programming-language-python"

This concept does not apply to Python. For full state isolation, build fresh workflow and executor instances for each independent run. See [State Management](../state.md) for patterns and examples.

::: zone-end

::: zone pivot="programming-language-go"

Go executors can reset shared local state by providing `ResetFunc` on `workflow.Executor`. Bindings created with `workflow.BindNewExecutorFunc` create a fresh executor per workflow session and usually don't need reset hooks.

```go
var count int

counter := workflow.NewExecutor("Counter", func(input string) int {
    count++
    return count
}).Extend(&workflow.Executor{
    ResetFunc: func() error {
        count = 0
        return nil
    },
}).Bind()
```

::: zone-end
## Next steps

> [!div class="nextstepaction"]
> [State Management](../state.md)
