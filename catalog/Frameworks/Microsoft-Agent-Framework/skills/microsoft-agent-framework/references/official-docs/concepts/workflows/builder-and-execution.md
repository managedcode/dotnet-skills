---
title: Microsoft Agent Framework Workflows - Workflow Builder & Execution
description: Building and executing workflows with the WorkflowBuilder.
zone_pivot_groups: programming-languages
author: TaoChenOSU
ms.topic: article
ms.author: taochen
ms.date: 05/27/2026
ms.service: agent-framework
---

<!--
  Language parity table – keep in sync when adding/removing sections.

    | Section              | C# | Python | Go | Notes |
    |----------------------|:--:|:------:|:--:|-------|
    | Building Workflows   | ✅ |   ✅   | ✅ |       |
    | Workflow Execution   | ✅ |   ✅   | ✅ |       |
    | Workflow Validation  | ✅ |   ✅   | ✅ | Shared prose, no zone pivot |
    | Execution Model      | ✅ |   ✅   | ✅ | Shared prose, no zone pivot |
-->

# Workflow Builder & Execution

A Workflow ties [executors](./executors.md) and [edges](./edges.md) together into a directed graph and manages execution. It coordinates executor invocation, message routing, and event streaming.

## Building Workflows

::: zone pivot="programming-language-csharp"

Workflows are constructed using the `WorkflowBuilder` class, which provides a fluent API for defining the workflow structure:

```csharp
using Microsoft.Agents.AI.Workflows;

var processor = new DataProcessor();
var validator = new Validator();
var formatter = new Formatter();

// Build workflow
WorkflowBuilder builder = new(processor); // Set starting executor
builder.AddEdge(processor, validator);
builder.AddEdge(validator, formatter);
var workflow = builder.Build();
```

::: zone-end

::: zone pivot="programming-language-python"

Workflows are constructed using the `WorkflowBuilder` class:

```python
from agent_framework import WorkflowBuilder

processor = DataProcessor()
validator = Validator()
formatter = Formatter()

# Build workflow
builder = WorkflowBuilder(start_executor=processor)
builder.add_edge(processor, validator)
builder.add_edge(validator, formatter)
workflow = builder.build()
```

::: zone-end

::: zone pivot="programming-language-go"

The `workflow` package provides a graph-based execution model where executors are connected by edges.

- **Executor** - A processing unit that receives input and produces output
- **Edge** - Connects the output of one executor to the input of another
- **Builder** - Constructs workflows by defining executors and edges
- **Run** - Executes a workflow with given input

```go
import (
    "github.com/microsoft/agent-framework-go/workflow"
    "github.com/microsoft/agent-framework-go/workflow/inproc"
)

uppercase := workflow.NewExecutor("UppercaseExecutor", func(input string) string {
    return strings.ToUpper(input)
}).Bind()

reverse := workflow.NewExecutor("ReverseExecutor", func(input string) string {
    runes := []rune(input)
    slices.Reverse(runes)
    return string(runes)
}).Bind()

wf, err := workflow.NewBuilder(uppercase).
    AddEdge(uppercase, reverse).
    WithOutputFrom(reverse).
    Build()
if err != nil {
    return err
}
```

::: zone-end

## Workflow Execution

Workflows support both streaming and non-streaming execution modes:

::: zone pivot="programming-language-csharp"

```csharp
using Microsoft.Agents.AI.Workflows;

// Streaming execution — get events as they happen
StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, inputMessage);
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    if (evt is ExecutorCompletedEvent executorComplete)
    {
        Console.WriteLine($"{executorComplete.ExecutorId}: {executorComplete.Data}");
    }

    if (evt is WorkflowOutputEvent outputEvt)
    {
        Console.WriteLine($"Workflow completed: {outputEvt.Data}");
    }
}

// Non-streaming execution — wait for completion
Run result = await InProcessExecution.RunAsync(workflow, inputMessage);
foreach (WorkflowEvent evt in result.NewEvents)
{
    if (evt is WorkflowOutputEvent outputEvt)
    {
        Console.WriteLine($"Final result: {outputEvt.Data}");
    }
}
```

::: zone-end

::: zone pivot="programming-language-python"

```python
# Streaming execution — get events as they happen
async for event in workflow.run(input_message, stream=True):
    if event.type == "output":
        print(f"Workflow completed: {event.data}")

# Non-streaming execution — wait for completion
events = await workflow.run(input_message)
print(f"Final result: {events.get_outputs()}")
```

::: zone-end

::: zone pivot="programming-language-go"

Use `RunStreaming` when you want events as they happen:

```go
stream, err := inproc.Default.RunStreaming(context.Background(), wf, "Hello, World!")
if err != nil {
    return err
}
defer stream.Close(context.Background())

for evt, err := range stream.WatchStream(context.Background()) {
    if err != nil {
        return err
    }
    if output, ok := evt.(workflow.OutputEvent); ok {
        fmt.Printf("Workflow completed: %v\n", output.Output)
    }
}
```

Use `Run` when you want to wait for workflow completion and then inspect the collected events:

```go
run, err := inproc.Default.Run(context.Background(), wf, "Hello, World!")
if err != nil {
    return err
}

for evt := range run.NewEvents() {
    if output, ok := evt.(workflow.OutputEvent); ok {
        fmt.Printf("Final result: %v\n", output.Output)
    }
}
```

You can also inspect executor events collected by a non-streaming run:

```go
for evt := range run.NewEvents() {
    if evt, ok := evt.(workflow.ExecutorCompletedEvent); ok {
        fmt.Printf("%s: %v\n", evt.ExecutorID, evt.Result)
    }
}
```

> [!TIP]
> See the [workflow examples](https://github.com/microsoft/agent-framework-go/tree/main/examples/03-workflows) for complete runnable samples.

::: zone-end
## Workflow Validation

The framework performs comprehensive validation when building workflows:

- **Type Compatibility**: Ensures message types are compatible between connected executors
- **Graph Connectivity**: Verifies all executors are reachable from the start executor
- **Executor Binding**: Confirms all executors are properly bound and instantiated
- **Edge Validation**: Checks for duplicate edges and invalid connections

## Execution Model: Supersteps

The framework uses a modified [Pregel](https://kowshik.github.io/JPregel/pregel_paper.pdf) execution model — a Bulk Synchronous Parallel (BSP) approach with superstep-based processing.

### How Supersteps Work

Workflow execution is organized into discrete supersteps. Each superstep:

1. Collects all pending messages from the previous superstep
2. Routes messages to target executors based on edge definitions
3. Runs all target executors concurrently within the superstep
4. Waits for all executors to complete before advancing (synchronization barrier)
5. Queues any new messages emitted by executors for the next superstep

```text
Superstep N:
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│  Collect All    │───▶│  Route Messages │───▶│  Execute All    │
│  Pending        │    │  Based on Type  │    │  Target         │
│  Messages       │    │  & Conditions   │    │  Executors      │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                                                       │
                                                       │ (barrier: wait for all)
┌─────────────────┐    ┌─────────────────┐             │
│  Start Next     │◀───│  Emit Events &  │◀────────────┘
│  Superstep      │    │  New Messages   │
└─────────────────┘    └─────────────────┘
```

### Synchronization Barrier

The most important characteristic is the synchronization barrier between supersteps. Within a single superstep, all triggered executors run in parallel, but the workflow does not advance to the next superstep until every executor completes.

This affects fan-out patterns: if you fan out to multiple paths — one with a chain of executors and another with a single long-running executor — the chained path cannot advance until the long-running executor completes.

### Why Supersteps?

The BSP model provides important guarantees:

- **Deterministic execution**: Given the same input, the workflow always executes in the same order
- **Reliable checkpointing**: State can be saved at superstep boundaries for fault tolerance
- **Simpler reasoning**: No race conditions between supersteps; each sees a consistent view of messages

### Working with the Superstep Model

If you need truly independent parallel paths that don't block each other, consolidate sequential steps into a single executor. Instead of chaining `step1 → step2 → step3`, combine that logic into one executor. Both parallel paths then execute within a single superstep.

## Next steps

> [!div class="nextstepaction"]
> [Agents in Workflows](../../workflows/agents-in-workflows.md)

**Related topics:**

- [Executors](./executors.md) — processing units in a workflow
- [Edges](./edges.md) — connections between executors
- [Events](./events.md) — workflow observability
- [State Management](./state.md)
