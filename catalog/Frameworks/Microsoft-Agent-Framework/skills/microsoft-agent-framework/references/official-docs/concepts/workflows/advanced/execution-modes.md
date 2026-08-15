---
title: Workflow Execution Modes
description: Deep dive into the OffThread and Lockstep execution modes for .NET workflows.
zone_pivot_groups: programming-languages
author: TaoChenOSU
ms.topic: article
ms.author: taochen
ms.date: 05/27/2026
ms.service: agent-framework
---

<!--
  Language parity table – keep in sync when adding/removing sections.

  | Section                | C# | Python | Go | Notes          |
  |------------------------|:--:|:------:|:--:|----------------|
  | Overview               | ✅ |   ❌   | ✅ | C#/Go support execution modes |
  | OffThread              | ✅ |   ❌   | ✅ |                |
  | Lockstep               | ✅ |   ❌   | ✅ |                |
  | Choosing a Mode        | ✅ |   ❌   | ✅ |                |
  | Non-Streaming          | ✅ |   ❌   | ✅ |                |
  | Not applicable notice  | ❌ |   ✅   | ❌ | Python-specific |
-->

# Workflow Execution Modes

::: zone pivot="programming-language-csharp"

When running a workflow in .NET, the **execution mode** controls how supersteps are processed and how events are delivered to the consumer. The `InProcessExecution` class exposes two execution modes: **OffThread** and **Lockstep**.

## Overview

| | OffThread (Default) | Lockstep |
|---|---|---|
| **Superstep execution** | Background thread | Consumer's thread |
| **Event delivery** | Immediate, as events are raised | Batched after each superstep completes |
| **Step execution** | Independent of event processing | Paused until batched events are consumed |
| **Concurrency** | Consumer reads events while supersteps run | Consumer and superstep execution alternate |
| **Best for** | Real-time streaming, production scenarios | Testing, debugging, deterministic ordering |

## OffThread

OffThread is the **default** execution mode. Supersteps run on a background thread, and events stream out immediately as they are raised via a channel-based implementation.

```csharp
// OffThread is the default — these are equivalent:
await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, input);
await using StreamingRun run = await InProcessExecution.OffThread.RunStreamingAsync(workflow, input);
```

### How it works

1. A background task runs supersteps continuously while messages are pending.
2. As executors yield outputs or events, the resulting `WorkflowEvent` objects are written to an unbounded `Channel<WorkflowEvent>`.
3. The consumer reads events from the channel via `WatchStreamAsync`, receiving them in real-time as they are produced.
4. When all supersteps are complete and no messages remain, the run halts with an `Idle` or `PendingRequests` status.

Because the superstep loop and the consumer run concurrently, events appear as soon as they are raised — there is no buffering delay. This makes OffThread ideal for streaming scenarios where low-latency event delivery matters, such as displaying token-by-token updates in a UI.

### Concurrent runs

OffThread also supports a **concurrent** variant that allows multiple runs to share the same workflow instance simultaneously:

```csharp
await using StreamingRun run = await InProcessExecution.Concurrent.RunStreamingAsync(workflow, input);
```

> [!IMPORTANT]
> Concurrent execution requires that all executors in the workflow be declared `crossRunShareable` (on the constructor) or be provided as factory methods.

## Lockstep

In Lockstep mode, supersteps run in the **consumer's thread** rather than on a background task. Events are accumulated during each superstep and emitted as a batch after the superstep completes.

```csharp
await using StreamingRun run = await InProcessExecution.Lockstep.RunStreamingAsync(workflow, input);
```

### How it works

1. The consumer calls `WatchStreamAsync`, which drives the execution loop.
2. A superstep runs to completion, and events are accumulated in a queue.
3. After the superstep finishes, all queued events are yielded to the consumer.
4. The next superstep begins only after the consumer has received all events from the previous one.

This alternating pattern means the consumer and the workflow engine never run simultaneously. Event delivery is deterministic — all events from a superstep are guaranteed to arrive before any events from the next superstep.

### When to use Lockstep

Lockstep is useful when:

- **Testing** — deterministic event ordering makes assertions straightforward.
- **Debugging** — step-through debugging is easier when execution stays on the consumer's thread.
- **Ordered processing** — scenarios where you need to fully process one superstep's events before the next superstep begins.

## Choosing an Execution Mode

For most production scenarios, the default **OffThread** mode is recommended. It provides the best responsiveness and allows the workflow to continue processing while the consumer handles events.

Use **Lockstep** when deterministic behavior is more important than performance, such as in unit tests or debugging sessions.

```csharp
// Production: OffThread (default)
await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, input);

// Testing: Lockstep for deterministic behavior
await using StreamingRun run = await InProcessExecution.Lockstep.RunStreamingAsync(workflow, input);
```

## Non-Streaming Execution

Both execution modes support non-streaming execution via `RunAsync`. In non-streaming mode, the workflow runs to completion and collects all events into a `Run` object rather than streaming them incrementally:

```csharp
Run run = await InProcessExecution.RunAsync(workflow, input);

// Access all emitted events
foreach (WorkflowEvent evt in run.OutgoingEvents)
{
    // Process events
}
```

Because non-streaming execution collects all events after completion, the real-time event delivery benefit of OffThread does not apply. The primary difference between modes in non-streaming scenarios is **threading**: OffThread runs supersteps on a background thread, freeing the calling thread while awaiting completion, whereas Lockstep runs supersteps on the caller's thread, blocking it until the workflow finishes.

Non-streaming execution uses the default OffThread mode. To use Lockstep with non-streaming execution:

```csharp
Run run = await InProcessExecution.Lockstep.RunAsync(workflow, input);
```

## Next steps

> [!div class="nextstepaction"]
> [Workflow Builder & Execution](../builder-and-execution.md)

::: zone-end

::: zone pivot="programming-language-python"

Execution modes are not applicable to Python workflows. Python workflows use a single execution model that handles superstep processing and event delivery through an asynchronous generator. This model is similar to the .NET Lockstep mode — steps don't advance unless the consumer is actively pulling events from the generator.

For information on running Python workflows, see [Workflow Builder & Execution](../builder-and-execution.md).

::: zone-end

::: zone pivot="programming-language-go"

When running a workflow in Go, the execution environment controls how supersteps are processed and how events are delivered to the consumer. The `workflow/inproc` package exposes three environments: `Default`/`OffThread`, `Lockstep`, and `Concurrent`.

## Overview

| | OffThread / Default | Lockstep | Concurrent |
|---|---|---|---|
| **Superstep execution** | Background goroutine | Driven by the event consumer | Background goroutine |
| **Event delivery** | Immediate, as events are raised | Batched as the stream is consumed | Immediate, as events are raised |
| **Best for** | Real-time streaming, production scenarios | Testing, debugging, deterministic ordering | Shared workflow instances with concurrent-safe bindings |

## OffThread

OffThread is the default execution mode. These are equivalent:

```go
stream, err := inproc.Default.RunStreaming(ctx, wf, input)
stream, err := inproc.OffThread.RunStreaming(ctx, wf, input)
```

### How it works

1. A background goroutine runs supersteps while messages are pending.
2. As executors yield outputs or events, workflow events are written to the stream.
3. The consumer reads events with `WatchStream`, receiving them as they are produced.
4. When all supersteps are complete and no messages remain, the run halts with an idle or pending-request status.

### Concurrent runs

Use `inproc.Concurrent` when all executor bindings in the workflow support concurrent shared execution:

```go
stream, err := inproc.Concurrent.RunStreaming(ctx, wf, input)
if err != nil {
    return err
}
defer stream.Close(ctx)
```

## Lockstep

In Lockstep mode, workflow execution advances as the consumer reads from the stream. This makes event ordering deterministic for tests and debugging.

```go
stream, err := inproc.Lockstep.RunStreaming(ctx, wf, input)
if err != nil {
    return err
}
defer stream.Close(ctx)

for evt, err := range stream.WatchStream(ctx) {
    if err != nil {
        return err
    }
    // inspect event
}
```

### How it works

1. The consumer calls `WatchStream`, which drives the execution loop.
2. A superstep runs to completion and events are accumulated.
3. The accumulated events are yielded to the consumer.
4. The next superstep begins only after the consumer receives the previous superstep's events.

### When to use Lockstep

Use Lockstep when deterministic behavior matters more than low-latency streaming, such as unit tests, debugging, or scenarios where you want to fully process one superstep's events before the next superstep begins.

## Choosing an Execution Mode

For most production scenarios, use `inproc.Default` or `inproc.OffThread`. Use `inproc.Lockstep` when deterministic event ordering is more important than streaming latency, such as in tests. Use `inproc.Concurrent` only when every binding in the workflow supports concurrent shared execution.

```go
// Production: OffThread (default)
stream, err := inproc.Default.RunStreaming(ctx, wf, input)
if err != nil {
    return err
}
defer stream.Close(ctx)

// Testing: Lockstep for deterministic behavior
testStream, err := inproc.Lockstep.RunStreaming(ctx, wf, input)
if err != nil {
    return err
}
defer testStream.Close(ctx)
```

## Non-Streaming Execution

All execution environments also support non-streaming `Run`, which executes until the next halt and stores emitted events on the returned run.

```go
run, err := inproc.Default.Run(ctx, wf, input)
if err != nil {
    return err
}

for evt := range run.NewEvents() {
    if output, ok := evt.(workflow.OutputEvent); ok {
        fmt.Printf("Final result: %v\n", output.Output)
    }
}
```

## Next steps

> [!div class="nextstepaction"]
> [Workflow Builder & Execution](../builder-and-execution.md)

::: zone-end