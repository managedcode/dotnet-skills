---
title: Microsoft Agent Framework Workflows Core Concepts - Edges
description: In-depth look at Edges in Microsoft Agent Framework Workflows.
zone_pivot_groups: programming-languages
author: TaoChenOSU
ms.topic: tutorial
ms.author: taochen
ms.date: 09/12/2025
ms.service: agent-framework
---

# Microsoft Agent Framework Workflows Core Concepts - Edges

This document provides an in-depth look at the **Edges** component of the Microsoft Agent Framework Workflow system.

## Overview

Edges define how messages flow between executors with optional conditions. They represent the connections in the workflow graph and determine the data flow paths.

### Types of Edges

The framework supports several edge patterns:

1. **Direct Edges**: Simple one-to-one connections between executors
2. **Conditional Edges**: Edges with conditions that determine when messages should flow
3. **Fan-out Edges**: One executor sending messages to multiple targets
4. **Fan-in Edges**: Multiple executors sending messages to a single target

#### Direct Edges

The simplest form of connection between two executors:

::: zone pivot="programming-language-csharp"

```csharp
using Microsoft.Agents.AI.Workflows;

WorkflowBuilder builder = new(sourceExecutor);
builder.AddEdge(sourceExecutor, targetExecutor);
```

::: zone-end

::: zone pivot="programming-language-python"

```python
from agent_framework import WorkflowBuilder

builder = WorkflowBuilder()
builder.add_edge(source_executor, target_executor)
builder.set_start_executor(source_executor)
workflow = builder.build()
```

::: zone-end

#### Conditional Edges

Edges that only activate when certain conditions are met:

::: zone pivot="programming-language-csharp"

```csharp
// Route based on message content
builder.AddEdge(
    source: spamDetector,
    target: emailProcessor,
    condition: result => result is SpamResult spam && !spam.IsSpam
);

builder.AddEdge(
    source: spamDetector,
    target: spamHandler,
    condition: result => result is SpamResult spam && spam.IsSpam
);
```

::: zone-end

::: zone pivot="programming-language-python"

```python
from agent_framework import WorkflowBuilder

builder = WorkflowBuilder()
builder.add_edge(spam_detector, email_processor, condition=lambda result: isinstance(result, SpamResult) and not result.is_spam)
builder.add_edge(spam_detector, spam_handler, condition=lambda result: isinstance(result, SpamResult) and result.is_spam)
builder.set_start_executor(spam_detector)
workflow = builder.build()
```

::: zone-end

#### Switch-case Edges

Route messages to different executors based on conditions:

::: zone pivot="programming-language-csharp"

```csharp
builder.AddSwitch(routerExecutor, switchBuilder =>
    switchBuilder
        .AddCase(
            message => message.Priority < Priority.Normal,
            executorA
        )
        .AddCase(
            message => message.Priority < Priority.High,
            executorB
        )
        .SetDefault(executorC)
);
```

::: zone-end

::: zone pivot="programming-language-python"

```python
from agent_framework import (
    Case,
    Default,
    WorkflowBuilder,
)

builder = WorkflowBuilder()
builder.set_start_executor(router_executor)
builder.add_switch_case_edge_group(
    router_executor,
    [
        Case(
            condition=lambda message: message.priority < Priority.NORMAL,
            target=executor_a,
        ),
        Case(
            condition=lambda message: message.priority < Priority.HIGH,
            target=executor_b,
        ),
        Default(target=executor_c)
    ],
)
workflow = builder.build()
```

::: zone-end

#### Fan-out Edges

Distribute messages from one executor to multiple targets:

::: zone pivot="programming-language-csharp"

```csharp
// Send to all targets
builder.AddFanOutEdge(splitterExecutor, targets: [worker1, worker2, worker3]);

// Send to specific targets based on target selector function
builder.AddFanOutEdge(
    source: routerExecutor,
    targetSelector: (message, targetCount) => message.Priority switch
    {
        Priority.High => [0], // Route to first worker only
        Priority.Normal => [1, 2], // Route to workers 2 and 3
        _ => Enumerable.Range(0, targetCount) // Route to all workers
    },
    targets: [highPriorityWorker, normalWorker1, normalWorker2]
);
```

::: zone-end

::: zone pivot="programming-language-python"

```python
from agent_framework import WorkflowBuilder

builder = WorkflowBuilder()
builder.set_start_executor(splitter_executor)
builder.add_fan_out_edges(splitter_executor, [worker1, worker2, worker3])
workflow = builder.build()

# Send to specific targets based on partitioner function
builder = WorkflowBuilder()
builder.set_start_executor(splitter_executor)
builder.add_fan_out_edges(
    splitter_executor,
    [worker1, worker2, worker3],
    selection_func=lambda message, target_ids: (
        [0] if message.priority == Priority.HIGH else
        [1, 2] if message.priority == Priority.NORMAL else
        list(range(target_count))
    )
)
workflow = builder.build()
```

::: zone-end

#### Fan-in Edges

Collect messages from multiple sources into a single target:

::: zone pivot="programming-language-csharp"

```csharp
// Aggregate results from multiple workers
builder.AddFanInEdge(aggregatorExecutor, sources: [worker1, worker2, worker3]);
```

::: zone-end

::: zone pivot="programming-language-python"

```python
builder.add_fan_in_edge([worker1, worker2, worker3], aggregator_executor)
```

::: zone-end

## Next Step

- [Learn about Workflows](./workflows.md) to understand how to build and execute workflows.
