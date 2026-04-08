---
title: Microsoft Agent Framework Workflows Core Concepts - Workflows
description: In-depth look at Workflows.
zone_pivot_groups: programming-languages
author: TaoChenOSU
ms.topic: tutorial
ms.author: taochen
ms.date: 09/12/2025
ms.service: agent-framework
---

# Microsoft Agent Framework Workflows Core Concepts - Workflows

This document provides an in-depth look at the **Workflows** component of the Microsoft Agent Framework Workflow system.

## Overview

A Workflow ties everything together and manages execution. It's the orchestrator that coordinates executor execution, message routing, and event streaming.

### Building Workflows

::: zone pivot="programming-language-csharp"

Workflows are constructed using the `WorkflowBuilder` class, which provides a fluent API for defining the workflow structure:

```csharp
// Create executors
using Microsoft.Agents.AI.Workflows;

var processor = new DataProcessor();
var validator = new Validator();
var formatter = new Formatter();

// Build workflow
WorkflowBuilder builder = new(processor); // Set starting executor
builder.AddEdge(processor, validator);
builder.AddEdge(validator, formatter);
var workflow = builder.Build<string>(); // Specify input message type
```

::: zone-end

::: zone pivot="programming-language-python"

Workflows are constructed using the `WorkflowBuilder` class, which provides a fluent API for defining the workflow structure:

```python
from agent_framework import WorkflowBuilder

processor = DataProcessor()
validator = Validator()
formatter = Formatter()

# Build workflow
builder = WorkflowBuilder()
builder.set_start_executor(processor)  # Set starting executor
builder.add_edge(processor, validator)
builder.add_edge(validator, formatter)
workflow = builder.build()
```

::: zone-end

### Workflow Execution

Workflows support both streaming and non-streaming execution modes:

::: zone pivot="programming-language-csharp"

```csharp
using Microsoft.Agents.AI.Workflows;

// Streaming execution - get events as they happen
StreamingRun run = await InProcessExecution.StreamAsync(workflow, inputMessage);
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    if (evt is ExecutorCompleteEvent executorComplete)
    {
        Console.WriteLine($"{executorComplete.ExecutorId}: {executorComplete.Data}");
    }

    if (evt is WorkflowOutputEvent outputEvt)
    {
        Console.WriteLine($"Workflow completed: {outputEvt.Data}");
    }
}

// Non-streaming execution - wait for completion
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
from agent_framework import WorkflowOutputEvent

# Streaming execution - get events as they happen
async for event in workflow.run_stream(input_message):
    if isinstance(event, WorkflowOutputEvent):
        print(f"Workflow completed: {event.data}")

# Non-streaming execution - wait for completion
events = await workflow.run(input_message)
print(f"Final result: {events.get_outputs()}")
```

::: zone-end

### Workflow Validation

The framework performs comprehensive validation when building workflows:

- **Type Compatibility**: Ensures message types are compatible between connected executors
- **Graph Connectivity**: Verifies all executors are reachable from the start executor
- **Executor Binding**: Confirms all executors are properly bound and instantiated
- **Edge Validation**: Checks for duplicate edges and invalid connections

### Execution Model

The framework uses a modified [Pregel](https://kowshik.github.io/JPregel/pregel_paper.pdf) execution model, a Bulk Synchronous Parallel (BSP) approach with clear data flow semantics and superstep-based processing.

### Pregel-Style Supersteps

Workflow execution is organized into discrete supersteps. A superstep is an atomic unit of execution where:

1. All pending messages from the previous superstep are collected
2. Messages are routed to their target executors based on edge definitions
3. All target executors run concurrently within the superstep
4. The superstep waits for all executors to complete before advancing to the next superstep
5. Any new messages emitted by executors are queued for the next superstep

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

### Superstep Synchronization Barrier

The most important characteristic of the Pregel model is the synchronization barrier between supersteps. Within a single superstep, all triggered executors run in parallel, but the workflow will not advance to the next superstep until every executor in the current superstep completes.

This has important implications for fan-out patterns: if you fan out to multiple paths and one path contains a chain of executors while another is a single long-running executor, the chained path cannot advance to its next step until the long-running executor completes. All executors triggered in the same superstep must finish before any downstream executors can begin.

### Why Superstep Synchronization?

The BSP model provides important guarantees:

- **Deterministic execution**: Given the same input, the workflow always executes in the same order
- **Reliable checkpointing**: State can be saved at superstep boundaries for fault tolerance
- **Simpler reasoning**: No race conditions between supersteps; each superstep sees a consistent view of messages

### Working with the Superstep Model

If you need truly independent parallel paths that don't block each other, consider consolidating sequential steps into a single executor. Instead of chaining multiple executors (e.g., `step1 -> step2 -> step3`), combine that logic into one executor that performs all steps internally. This way, both parallel paths execute within a single superstep and complete in the time of the slowest path.

### Key Execution Characteristics

- **Superstep Isolation**: All executors in a superstep run concurrently without interfering with each other
- **Synchronization Barrier**: The workflow waits for all executors in a superstep to complete before advancing
- **Message Delivery**: Messages are delivered in parallel to all matching edges
- **Event Streaming**: Events are emitted in real-time as executors complete processing
- **Type Safety**: Runtime type validation ensures messages are routed to compatible handlers

## Next Step

- [Learn about events](./events.md) to understand how to monitor and observe workflow execution.
