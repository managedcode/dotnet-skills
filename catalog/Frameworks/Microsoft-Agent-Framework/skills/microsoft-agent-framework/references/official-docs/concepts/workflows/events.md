---
title: Microsoft Agent Framework Workflows - Events
description: In-depth look at Events in Microsoft Agent Framework Workflows.
zone_pivot_groups: programming-languages
author: TaoChenOSU
ms.topic: article
ms.author: taochen
ms.date: 05/27/2026
ms.service: agent-framework
---

<!--
  Language parity table – keep in sync when adding/removing sections.

    | Section              | C#  | Python | Go | Notes                                          |
    |----------------------|:---:|:------:|:--:|-------------------------------------------------|
    | Built-in Event Types | ✅  |   ✅   | ✅ | Python has extra status/failed; C# has separate |
    |                      |     |        |    | AgentResponse classes vs Python "data" type     |
    | Consuming Events     | ✅  |   ✅   | ✅ |                                                 |
    | Defining Custom      | ✅  |   ✅   | ✅ |                                                 |
    | Emitting Custom      | ✅  |   ✅   | ✅ |                                                 |
    | Consuming Custom     | ✅  |   ✅   | ✅ |                                                 |
-->

# Events

The workflow event system provides observability into workflow execution. Events are emitted at key points during execution and can be consumed in real-time via streaming.

## Built-in Event Types

::: zone pivot="programming-language-csharp"

```csharp
// Workflow lifecycle events
WorkflowStartedEvent     // Workflow execution begins
WorkflowOutputEvent      // Workflow outputs data
WorkflowErrorEvent       // Workflow encounters an error
WorkflowWarningEvent     // Workflow encountered a warning

// Executor events
ExecutorInvokedEvent     // Executor starts processing
ExecutorCompletedEvent   // Executor finishes processing
ExecutorFailedEvent      // Executor encounters an error
AgentResponseEvent       // An agent run produces output
AgentResponseUpdateEvent // An agent run produces a streaming update

// Superstep events
SuperStepStartedEvent    // Superstep begins
SuperStepCompletedEvent  // Superstep completes

// Request events
RequestInfoEvent         // A request is issued
```

> [!NOTE]
> When agents use approval-required tools, `RequestInfoEvent` typically carries a `ToolApprovalRequestContent` payload for tool calls that require human approval. See [Human-in-the-Loop](../../workflows/human-in-the-loop.md) for details on handling these events.

::: zone-end

::: zone pivot="programming-language-python"

```python
# All events use the unified WorkflowEvent class with a type discriminator:

# Workflow lifecycle events
WorkflowEvent.type == "started"             # Workflow execution begins
WorkflowEvent.type == "status"              # Workflow state changed (use .state)
WorkflowEvent.type == "output"              # Workflow produces a terminal (final) output
WorkflowEvent.type == "intermediate"        # Workflow produces an intermediate (observational) output
WorkflowEvent.type == "failed"              # Workflow terminated with error (use .details)
WorkflowEvent.type == "error"               # Non-fatal error from user code
WorkflowEvent.type == "warning"             # Workflow encountered a warning

# Executor events
WorkflowEvent.type == "executor_invoked"    # Executor starts processing
WorkflowEvent.type == "executor_completed"  # Executor finishes processing
WorkflowEvent.type == "executor_failed"     # Executor encounters an error
WorkflowEvent.type == "data"                # Deprecated alias for "intermediate"

# Superstep events
WorkflowEvent.type == "superstep_started"   # Superstep begins
WorkflowEvent.type == "superstep_completed" # Superstep completes

# Request events
WorkflowEvent.type == "request_info"        # A request is issued
```

> [!NOTE]
> When agents use approval-required tools, `request_info` events typically carry a `Content` payload with `type == "function_approval_request"` for tool calls that require human approval. See [Human-in-the-Loop](../../workflows/human-in-the-loop.md) for details on handling these events.

> [!NOTE]
> `"output"` and `"intermediate"` are the two output discriminators. An executor designated as a **terminal output source** emits `"output"` events (consumed by `WorkflowRunResult.get_outputs()`). One designated as an **intermediate output source** emits `"intermediate"` events (consumed by `WorkflowRunResult.get_intermediate_outputs()`). The `"data"` type is a deprecated alias for `"intermediate"` and will be removed in a future release; prefer filtering on `"intermediate"` in new code.

::: zone-end

## Consuming Events

::: zone pivot="programming-language-csharp"

```csharp
using Microsoft.Agents.AI.Workflows;

await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    switch (evt)
    {
        case ExecutorInvokedEvent invoke:
            Console.WriteLine($"Starting {invoke.ExecutorId}");
            break;

        case ExecutorCompletedEvent complete:
            Console.WriteLine($"Completed {complete.ExecutorId}: {complete.Data}");
            break;

        case WorkflowOutputEvent output:
            Console.WriteLine($"Workflow output: {output.Data}");
            return;

        case WorkflowErrorEvent error:
            Console.WriteLine($"Workflow error: {error.Exception}");
            return;
    }
}
```

::: zone-end

::: zone pivot="programming-language-python"

```python
from agent_framework import WorkflowEvent

async for event in workflow.run(input_message, stream=True):
    if event.type == "executor_invoked":
        print(f"Starting {event.executor_id}")
    elif event.type == "executor_completed":
        print(f"Completed {event.executor_id}: {event.data}")
    elif event.type == "intermediate":
        print(f"Intermediate output from {event.executor_id}: {event.data}")
    elif event.type == "output":
        print(f"Terminal output: {event.data}")
        return
    elif event.type == "error":
        print(f"Workflow error: {event.data}")
        return
```

::: zone-end

## Custom Events

Custom events let executors emit domain-specific signals during workflow execution tailored to your application's needs. Some example use cases include:

- **Track progress** — report intermediate steps so callers can show status updates.
- **Emit diagnostics** — surface warnings, metrics, or debug information without changing the workflow output.
- **Relay domain data** — push structured payloads (e.g., database writes, tool calls) to listeners in real time.

### Defining Custom Events

::: zone pivot="programming-language-csharp"

Define a custom event by subclassing `WorkflowEvent`. The base constructor accepts an optional `object? data` payload that is exposed through the `Data` property.

```csharp
using Microsoft.Agents.AI.Workflows;

// Simple event with a string payload
internal sealed class ProgressEvent(string step) : WorkflowEvent(step) { }

// Event with a structured payload
internal sealed class MetricsEvent(MetricsData metrics) : WorkflowEvent(metrics) { }
```

::: zone-end

::: zone pivot="programming-language-python"

In Python, create custom events using the `WorkflowEvent` class directly with a custom type discriminator string. The `type` and `data` parameters carry all the information.

```python
from agent_framework import WorkflowEvent

# Create a custom event with a custom type string and payload
event = WorkflowEvent(type="progress", data="Step 1 complete")

# Custom event with a structured payload
event = WorkflowEvent(type="metrics", data={"latency_ms": 42, "tokens": 128})
```

> [!NOTE]
> The event types `"started"`, `"status"`, and `"failed"` are reserved for framework lifecycle notifications. If an executor attempts to emit one of these types, the event is ignored and a warning is logged.

::: zone-end

::: zone pivot="programming-language-go"

Define a custom event by creating a type that implements the `workflow.Event` interface. The `Data` method returns the event payload.

```go
type ProgressEvent struct {
    Step string
}

func (e ProgressEvent) Data() any {
    return e.Step
}
```

::: zone-end

### Emitting Custom Events

::: zone pivot="programming-language-csharp"

Emit custom events from an executor's message handler by calling `AddEventAsync` on the `IWorkflowContext`:

```csharp
using Microsoft.Agents.AI.Workflows;

internal sealed class ProgressEvent(string step) : WorkflowEvent(step) { }

internal sealed partial class CustomExecutor() : Executor("CustomExecutor")
{
    [MessageHandler]
    private async ValueTask HandleAsync(string message, IWorkflowContext context)
    {
        await context.AddEventAsync(new ProgressEvent("Validating input"));

        // Executor logic...

        await context.AddEventAsync(new ProgressEvent("Processing complete"));
    }
}
```

::: zone-end

::: zone pivot="programming-language-python"

Emit custom events from a handler by calling `add_event` on the `WorkflowContext`:

```python
from agent_framework import (
    handler,
    Executor,
    WorkflowContext,
    WorkflowEvent,
)

class CustomExecutor(Executor):

    @handler
    async def handle(self, message: str, ctx: WorkflowContext[str]) -> None:
        await ctx.add_event(WorkflowEvent(type="progress", data="Validating input"))

        # Executor logic...

        await ctx.add_event(WorkflowEvent(type="progress", data="Processing complete"))
```

::: zone-end

::: zone pivot="programming-language-go"

Emit custom events from an executor handler by calling `AddEvent` on the `workflow.Context`:

```go
customExecutor := workflow.NewExecutor("CustomExecutor", func(ctx *workflow.Context, message string) error {
    if err := ctx.AddEvent(ProgressEvent{Step: "Validating input"}); err != nil {
        return err
    }

    // Executor logic...

    return ctx.AddEvent(ProgressEvent{Step: "Processing complete"})
}).Bind()
```

::: zone-end

### Consuming Custom Events

::: zone pivot="programming-language-csharp"

Use pattern matching to filter for your custom event type in the event stream:

```csharp
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    switch (evt)
    {
        case ProgressEvent progress:
            Console.WriteLine($"Progress: {progress.Data}");
            break;

        case WorkflowOutputEvent output:
            Console.WriteLine($"Done: {output.Data}");
            return;
    }
}
```

::: zone-end

::: zone pivot="programming-language-python"

Filter on the custom type discriminator string:

```python
async for event in workflow.run(input_message, stream=True):
    if event.type == "progress":
        print(f"Progress: {event.data}")
    elif event.type == "output":
        print(f"Done: {event.data}")
        return
```

::: zone-end

::: zone pivot="programming-language-go"

Use a type switch or type assertion to filter for your custom event type in the event stream:

```go
for evt, err := range run.WatchStream(ctx) {
    if err != nil {
        return err
    }

    switch e := evt.(type) {
    case ProgressEvent:
        fmt.Printf("Progress: %v\n", e.Data())
    case workflow.OutputEvent:
        fmt.Printf("Done: %v\n", e.Output)
        return nil
    }
}
```

::: zone-end

::: zone pivot="programming-language-go"
## Events

Workflows emit events during execution. Events can be observed through the run object.

### Observe events

```go
run, err := inproc.Default.Run(ctx, wf, input)
for evt := range run.NewEvents() {
    switch e := evt.(type) {
    case workflow.ExecutorCompletedEvent:
        fmt.Printf("Executor %s completed: %v\n", e.ExecutorID, e.Result)
    case workflow.OutputEvent:
        fmt.Printf("Output from %s: %v\n", e.ExecutorID, e.Output)
    }
}
```

### Streaming events

For streaming workflows, use `inproc.Default.RunStreaming` and `WatchStream`:

```go
run, err := inproc.Default.RunStreaming(ctx, wf, input)
for evt, err := range run.WatchStream(ctx) {
    if err != nil {
        panic(err)
    }
    // process streaming events
}
```

::: zone-end
## Next steps

> [!div class="nextstepaction"]
> [Workflow Builder & Execution](./builder-and-execution.md)

**Related topics:**

- [Agents in Workflows](../../workflows/agents-in-workflows.md)
- [State Management](./state.md)
- [Checkpoints & Resuming](../../workflows/checkpoints.md)
- [Observability](../../workflows/observability.md)
