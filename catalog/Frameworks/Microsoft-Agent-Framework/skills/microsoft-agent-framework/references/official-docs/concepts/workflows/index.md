---
title: Workflow concepts
description: Understand Agent Framework workflow APIs, graph primitives, execution, state, and advanced composition.
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: overview
ms.author: edvan
ms.date: 07/30/2026
ms.service: agent-framework
---

<!--
  Language parity table - keep in sync when adding/removing sections.

  | Section                 | C# | Python | Go | Notes                           |
  |-------------------------|:--:|:------:|:--:|:--------------------------------|
  | Workflow APIs           | ✅ |   ✅   | ✅ | Python also has a functional API |
  | Graph and runtime model | ✅ |   ✅   | ✅ | Shared                          |
  | Advanced execution      | ✅ |   ✅   | ✅ | Shared                          |
  | Next steps              | ✅ |   ✅   | ✅ | Language-specific destination   |
-->

# Workflow concepts

Agent Framework workflows define explicit, inspectable execution paths for coordinating code, agents, state, events, and human input. The framework provides functional and graph-based APIs over the same workflow run model.

## Workflow APIs

All SDKs support graph-based workflows. Python additionally provides an experimental functional workflow API.

::: zone pivot="programming-language-csharp"

The .NET SDK uses the graph-based `WorkflowBuilder` API. It connects typed executors through edges and conditions, supports fan-out and fan-in execution, emits workflow and executor events, and checkpoints progress at superstep boundaries. Compatible workflows can be exposed through the standard agent interface with `AsAIAgent()`.

- [Workflow Builder and execution](builder-and-execution.md) explains how to build and run .NET workflow graphs.
- [Executors](executors.md), [edges](edges.md), [events](events.md), and [state management](state.md) describe the graph runtime primitives.

::: zone-end

::: zone pivot="programming-language-python"

- [Functional Workflow API](functional.md) uses Python functions and native control flow.
- [Workflow Builder and execution](builder-and-execution.md) constructs and runs type-validated workflow graphs.

Both APIs produce the same observable workflow results. Choose the API that matches the execution model you want to express:

| | Functional (`@workflow`) | Graph (`WorkflowBuilder`) |
|---|---|---|
| **Control flow** | Native Python (`if`, loops, `asyncio.gather`) | Edges and conditions |
| **Best for** | Sequential pipelines, custom loops, and ad-hoc parallelism | Fixed graphs, fan-out/fan-in, and type-validated message routing |
| **Parallelism** | `asyncio.gather` | Parallel edge groups and superstep execution |
| **Observability** | Per-step events with `@step` | Per-executor events |
| **Human-in-the-loop** | `ctx.request_info()` | `RequestInfoExecutor` |
| **Checkpointing** | Per-`@step` result caching | Superstep-boundary checkpoints |
| **Agent wrapping** | `.as_agent()` on `FunctionalWorkflow` | `.as_agent()` on `Workflow` |

::: zone-end

::: zone pivot="programming-language-go"

The Go SDK uses the graph-based `workflow.NewBuilder` API. It connects bound executors through edges, conditions, and fan-out or fan-in groups, then runs the graph through an execution environment such as `inproc.Default`. Agent-oriented workflows can be exposed through the standard agent interface with `agentworkflow.New(...)`.

- [Workflow Builder and execution](builder-and-execution.md) explains how to build and run Go workflow graphs.
- [Executors](executors.md), [edges](edges.md), [events](events.md), and [state management](state.md) describe the graph runtime primitives.

::: zone-end

## Graph and runtime model

- [Executors](executors.md) receive inputs, perform work, and emit outputs.
- [Edges](edges.md) route values between executors.
- [Events](events.md) expose workflow lifecycle and execution activity.
- [State management](state.md) controls durable and run-scoped workflow state.

## Advanced execution

- [Agent Executor](advanced/agent-executor.md) integrates agents into workflow graphs.
- [Workflow Execution Modes](advanced/execution-modes.md) explains streaming and non-streaming execution.
- [Resettable Executors](advanced/resettable-executors.md) describes executors that reset between runs.
- [Sub-Workflows](advanced/sub-workflows.md) composes workflows as executors in larger graphs.

For feature-oriented guidance such as checkpoints, human-in-the-loop, visualization, and orchestrations, see [Workflow Capabilities](../../workflows/index.md).

## Next steps

::: zone pivot="programming-language-csharp"

> [!div class="nextstepaction"]
> [Build and run a workflow](builder-and-execution.md)

::: zone-end

::: zone pivot="programming-language-python"

> [!div class="nextstepaction"]
> [Choose a workflow API](functional.md)

::: zone-end

::: zone pivot="programming-language-go"

> [!div class="nextstepaction"]
> [Build and run a workflow](builder-and-execution.md)

::: zone-end
