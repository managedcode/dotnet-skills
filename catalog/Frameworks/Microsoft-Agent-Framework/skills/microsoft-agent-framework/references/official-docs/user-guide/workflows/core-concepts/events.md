---
title: Microsoft Agent Framework Workflows Core Concepts - Events
description: In-depth look at Events in Microsoft Agent Framework Workflows.
zone_pivot_groups: programming-languages
author: TaoChenOSU
ms.topic: tutorial
ms.author: taochen
ms.date: 09/12/2025
ms.service: agent-framework
---

# Microsoft Agent Framework Workflows Core Concepts - Events

This document provides an in-depth look at the **Events** system of Workflows in Microsoft Agent Framework.

## Overview

There are built-in events that provide observability into the workflow execution.

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

::: zone-end

::: zone pivot="programming-language-python"

```python
# Workflow lifecycle events
WorkflowStartedEvent     # Workflow execution begins
WorkflowOutputEvent      # Workflow produces an output
WorkflowErrorEvent       # Workflow encounters an error
WorkflowWarningEvent     # Workflow encountered a warning

# Executor events
ExecutorInvokedEvent     # Executor starts processing
ExecutorCompletedEvent   # Executor finishes processing
ExecutorFailedEvent      # Executor encounters an error
AgentRunEvent            # An agent run produces output
AgentResponseUpdateEvent # An agent run produces a streaming update

# Superstep events
SuperStepStartedEvent    # Superstep begins
SuperStepCompletedEvent  # Superstep completes

# Request events
RequestInfoEvent         # A request is issued
```

::: zone-end

### Consuming Events

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
from agent_framework import (
    ExecutorCompleteEvent,
    ExecutorInvokeEvent,
    WorkflowOutputEvent,
    WorkflowErrorEvent,
)

async for event in workflow.run_stream(input_message):
    match event:
        case ExecutorInvokeEvent() as invoke:
            print(f"Starting {invoke.executor_id}")
        case ExecutorCompleteEvent() as complete:
            print(f"Completed {complete.executor_id}: {complete.data}")
        case WorkflowOutputEvent() as output:
            print(f"Workflow produced output: {output.data}")
            return
        case WorkflowErrorEvent() as error:
            print(f"Workflow error: {error.exception}")
            return
```

::: zone-end

## Custom Events

Users can define and emit custom events during workflow execution for enhanced observability.

::: zone pivot="programming-language-csharp"

```csharp
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Reflection;

internal sealed class CustomEvent(string message) : WorkflowEvent(message) { }

internal sealed class CustomExecutor() : ReflectingExecutor<CustomExecutor>("CustomExecutor"), IMessageHandler<string>
{
    public async ValueTask HandleAsync(string message, IWorkflowContext context)
    {
        await context.AddEventAsync(new CustomEvent($"Processing message: {message}"));
        // Executor logic...
    }
}
```

::: zone-end

::: zone pivot="programming-language-python"

```python
from agent_framework import (
    handler,
    Executor,
    WorkflowContext,
    WorkflowEvent,
)

class CustomEvent(WorkflowEvent):
    def __init__(self, message: str):
        super().__init__(message)

class CustomExecutor(Executor):

    @handler
    async def handle(self, message: str, ctx: WorkflowContext[str]) -> None:
        await ctx.add_event(CustomEvent(f"Processing message: {message}"))
        # Executor logic...
```

::: zone-end

## Next Steps

- [Learn how to use agents in workflows](./../using-agents.md) to build intelligent workflows.
- [Learn how to use workflows as agents](./../as-agents.md).
- [Learn how to handle requests and responses](./../requests-and-responses.md) in workflows.
- [Learn how to manage state](./../shared-states.md) in workflows.
- [Learn how to create checkpoints and resume from them](./../checkpoints.md).
- [Learn how to monitor workflows](./../observability.md).
- [Learn about state isolation in workflows](./../state-isolation.md).
- [Learn how to visualize workflows](./../visualization.md).
