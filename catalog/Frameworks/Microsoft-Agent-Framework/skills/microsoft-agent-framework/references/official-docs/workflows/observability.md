---
title: Microsoft Agent Framework Workflows - Observability
description: In-depth look at Observability in Microsoft Agent Framework Workflows.
zone_pivot_groups: programming-languages
author: TaoChenOSU
ms.topic: tutorial
ms.author: taochen
ms.date: 05/27/2026
ms.service: agent-framework
---

<!--
  Language parity table – keep in sync when adding/removing sections.

    | Section                      | C# | Python | Go | Notes                                          |
    |------------------------------|:--:|:------:|:--:|-------------------------------------------------|
    | Enable Observability         | ✅ |   ✅   | ✅ |                                                 |
    | Workflow Spans               | ✅ |   ✅   | ✅ | C#/Go have session/invoke split; Python has run |
    | Span Attributes              | ✅ |   ✅   | ✅ |                                                 |
    | Span Events                  | ✅ |   ✅   | ✅ | C#/Go have session events; Python does not      |
    | Links between Spans          | ✅ |   ✅   | ✅ |                                                 |
    | Edge Group Delivery Status   | ✅ |   ✅   | ✅ |                                                 |
    | Telemetry Configuration      | ✅ |   ✅   | ✅ | Different configuration mechanisms              |
    | Next Steps                   | ✅ |   ✅   | ✅ |                                                 |
-->

# Microsoft Agent Framework Workflows - Observability

Observability provides insights into the internal state and behavior of workflows during execution. This includes logging, metrics, and tracing capabilities that help monitor and debug workflows.

> [!TIP]
> Observability is a framework-wide feature and is not limited to workflows. For more information, see [Observability](../agents/observability.md).

Aside from the standard [GenAI telemetry](https://opentelemetry.io/docs/specs/semconv/gen-ai/), Agent Framework Workflows emits additional spans, logs, and metrics to provide deeper insights into workflow execution. These observability features help developers understand the flow of messages, the performance of executors, and any errors that might occur.

## Enable Observability

::: zone pivot="programming-language-csharp"

Please refer to [Enabling Observability](../agents/observability.md#enable-observability-c) for instructions on enabling observability in your applications.

::: zone-end

::: zone pivot="programming-language-python"

Please refer to [Enabling Observability](../agents/observability.md#enable-observability-python) for instructions on enabling observability in your applications.

::: zone-end

## Workflow Spans

::: zone pivot="programming-language-csharp"

The following spans are emitted during workflow execution:

| Span Name                          | Description                                                                                              |
|------------------------------------|----------------------------------------------------------------------------------------------------------|
| `workflow.build`                   | Emitted for each workflow build.                                                                          |
| `workflow.session`                 | Outer span representing the entire lifetime of a workflow execution, from start until stop or error. |
| `workflow_invoke`                  | Emitted for each input-to-halt cycle within a workflow session.                                      |
| `executor.process {executor_id}`   | Emitted for each executor processing a message. The executor ID is appended to the span name.            |
| `edge_group.process`              | Emitted for each edge group processing a message.                                                        |
| `message.send`                     | Emitted for each message sent from an executor to another executor.                                      |

::: zone-end

::: zone pivot="programming-language-python"

The following spans are emitted during workflow execution:

| Span Name                                  | Description                                                                                     |
|--------------------------------------------|-------------------------------------------------------------------------------------------------|
| `workflow.build`                           | Emitted for each workflow build.                                                                 |
| `workflow.run`                             | Emitted for each workflow execution.                                                             |
| `executor.process {executor_id}`           | Emitted for each executor processing a message. The executor ID is appended to the span name.   |
| `edge_group.process {edge_group_type}`     | Emitted for each edge group processing a message. The edge group type is appended to the span name. |
| `message.send`                             | Emitted for each message sent from an executor to another executor.                             |

::: zone-end

::: zone pivot="programming-language-go"

The following spans are emitted during workflow execution:

| Span Name | Description |
|-----------|-------------|
| `workflow.build` | Emitted for each workflow build. |
| `workflow.session` | Outer span representing the lifetime of a workflow execution session. |
| `workflow_invoke` | Emitted for each input-to-halt cycle within a workflow session. |
| `executor.process {executor_id}` | Emitted for each executor processing a message. The executor ID is appended to the span name. |
| `edge_group.process` | Emitted for each edge group processing a message. |
| `message.send` | Emitted for each message sent from one executor to another. |

::: zone-end

## Span Attributes

Spans carry attributes that provide additional context about the operation. The following attributes are set on workflow spans:

::: zone pivot="programming-language-csharp"

| Attribute                  | Span(s)                                        | Description                                                   |
|----------------------------|------------------------------------------------|---------------------------------------------------------------|
| `workflow.id`              | `workflow.build`, `workflow.session`           | The unique identifier of the workflow.                        |
| `workflow.name`            | `workflow.session`                             | The name of the workflow.                                     |
| `workflow.description`     | `workflow.session`                             | The description of the workflow.                              |
| `workflow.definition`      | `workflow.build`                               | The JSON definition of the workflow graph.                    |
| `session.id`               | `workflow.session`                             | The unique session identifier.                                |
| `executor.id`              | `executor.process`                             | The unique identifier of the executor.                        |
| `executor.type`            | `executor.process`                             | The type name of the executor.                                |
| `executor.input`           | `executor.process`                             | The input message. Only set when sensitive data is enabled.   |
| `executor.output`          | `executor.process`                             | The output of the executor. Only set when sensitive data is enabled. |
| `message.type`             | `executor.process`, `message.send`             | The type name of the message.                                 |
| `message.content`          | `message.send`                                 | The message content. Only set when sensitive data is enabled. |
| `message.source_id`        | `message.send`                                 | The ID of the executor that sent the message.                 |
| `message.target_id`        | `message.send`                                 | The ID of the target executor, if specified.                  |
| `edge_group.type`          | `edge_group.process`                           | The type of the edge group.                                   |
| `edge_group.delivered`     | `edge_group.process`                           | Whether the message was delivered (boolean).                  |
| `edge_group.delivery_status` | `edge_group.process`                         | The delivery outcome (see [Edge Group Delivery Status](#edge-group-delivery-status)). |
| `error.type`               | Any span on error                              | The exception type name.                                      |

::: zone-end

::: zone pivot="programming-language-python"

| Attribute                         | Span(s)                                 | Description                                                   |
|-----------------------------------|-----------------------------------------|---------------------------------------------------------------|
| `workflow.id`                     | `workflow.build`, `workflow.run`        | The unique identifier of the workflow.                        |
| `workflow.name`                   | `workflow.run`                          | The name of the workflow.                                     |
| `workflow.description`            | `workflow.run`                          | The description of the workflow.                              |
| `workflow.definition`             | `workflow.build`                        | The JSON definition of the workflow graph.                    |
| `workflow_builder.name`           | `workflow.build`                        | The name of the workflow builder.                             |
| `workflow_builder.description`    | `workflow.build`                        | The description of the workflow builder.                      |
| `executor.id`                     | `executor.process`                      | The unique identifier of the executor.                        |
| `executor.type`                   | `executor.process`                      | The type name of the executor.                                |
| `message.type`                    | `executor.process`, `message.send`      | The type name of the message.                                 |
| `message.payload_type`            | `executor.process`                      | The data type of the message payload.                         |
| `message.destination_executor_id` | `message.send`                          | The ID of the target executor, if specified.                  |
| `message.source_id`              | `edge_group.process`                    | The ID of the executor that sent the message.                 |
| `message.target_id`              | `edge_group.process`                    | The ID of the target executor, if specified.                  |
| `edge_group.type`                 | `edge_group.process`                    | The type of the edge group.                                   |
| `edge_group.id`                   | `edge_group.process`                    | The unique identifier of the edge group.                      |
| `edge_group.delivered`            | `edge_group.process`                    | Whether the message was delivered (boolean).                  |
| `edge_group.delivery_status`     | `edge_group.process`                    | The delivery outcome (see [Edge Group Delivery Status](#edge-group-delivery-status)). |

::: zone-end

::: zone pivot="programming-language-go"

| Attribute | Span(s) | Description |
|-----------|---------|-------------|
| `workflow.id` | `workflow.build`, `workflow.session`, `workflow_invoke` | The workflow start executor ID. |
| `workflow.name` | `workflow.session`, `workflow_invoke` | The workflow name, when set. |
| `workflow.description` | `workflow.session`, `workflow_invoke` | The workflow description, when set. |
| `workflow.definition` | `workflow.build` | The JSON definition of the workflow graph. |
| `session.id` | `workflow.session`, `workflow_invoke` | The workflow session identifier. |
| `executor.id` | `executor.process` | The executor ID. |
| `executor.implementation.id` | `executor.process` | The executor implementation ID. |
| `executor.input` | `executor.process` | The input message. Only set when sensitive data is enabled. |
| `executor.output` | `executor.process` | The executor output. Only set when sensitive data is enabled. |
| `message.type` | `executor.process` | The type name of the processed message. |
| `message.content` | `message.send` | The message content. Only set when sensitive data is enabled. |
| `message.source_id` | `edge_group.process`, `message.send` | The ID of the executor that sent the message. |
| `message.target_id` | `edge_group.process`, `message.send` | The target executor ID, when specified. |
| `edge_group.type` | `edge_group.process` | The type of edge group being processed. |
| `edge_group.delivered` | `edge_group.process` | Whether the message was delivered. |
| `edge_group.delivery_status` | `edge_group.process` | The delivery outcome (see [Edge Group Delivery Status](#edge-group-delivery-status)). |
| `error.type` | Any span on error | The exception type name. |
| `error.message` | Any span on error | The exception message. |

::: zone-end

## Span Events

Span events are structured log entries attached to spans, providing a timeline of key moments within each span.

::: zone pivot="programming-language-csharp"

| Event Name                    | Span(s)             | Description                                          |
|-------------------------------|----------------------|------------------------------------------------------|
| `build.started`               | `workflow.build`    | Emitted when the build process begins.                |
| `build.validation_completed`  | `workflow.build`    | Emitted when build validation passes.                 |
| `build.completed`             | `workflow.build`    | Emitted when the build completes successfully.        |
| `build.error`                 | `workflow.build`    | Emitted when the build fails.                         |
| `session.started`             | `workflow.session`  | Emitted when a workflow session begins.               |
| `session.completed`           | `workflow.session`  | Emitted when a workflow session completes.            |
| `session.error`               | `workflow.session`  | Emitted when a workflow session encounters an error.  |
| `workflow.started`            | `workflow_invoke`   | Emitted when a workflow invocation begins.            |
| `workflow.completed`          | `workflow_invoke`   | Emitted when a workflow invocation completes.         |
| `workflow.error`              | `workflow_invoke`   | Emitted when a workflow invocation encounters an error.|

::: zone-end

::: zone pivot="programming-language-python"

| Event Name                    | Span(s)          | Description                                       |
|-------------------------------|-------------------|---------------------------------------------------|
| `build.started`               | `workflow.build` | Emitted when the build process begins.             |
| `build.validation_completed`  | `workflow.build` | Emitted when build validation passes.              |
| `build.completed`             | `workflow.build` | Emitted when the build completes successfully.     |
| `build.error`                 | `workflow.build` | Emitted when the build fails.                      |
| `workflow.started`            | `workflow.run`   | Emitted when a workflow run begins.                |
| `workflow.completed`          | `workflow.run`   | Emitted when a workflow run completes.             |
| `workflow.error`              | `workflow.run`   | Emitted when a workflow run encounters an error.   |

::: zone-end

::: zone pivot="programming-language-go"

| Event Name | Span(s) | Description |
|------------|---------|-------------|
| `build.started` | `workflow.build` | Emitted when the build process begins. |
| `build.validation_completed` | `workflow.build` | Emitted when build validation passes. |
| `build.completed` | `workflow.build` | Emitted when the build completes successfully. |
| `build.error` | `workflow.build` | Emitted when the build fails. |
| `session.started` | `workflow.session` | Emitted when a workflow session begins. |
| `session.completed` | `workflow.session` | Emitted when a workflow session completes. |
| `session.error` | `workflow.session` | Emitted when a workflow session encounters an error. |
| `workflow.started` | `workflow_invoke` | Emitted when a workflow invocation begins. |
| `workflow.completed` | `workflow_invoke` | Emitted when a workflow invocation completes. |
| `workflow.error` | `workflow_invoke` | Emitted when a workflow invocation encounters an error. |

::: zone-end

## Links between Spans

When an executor sends a message to another executor, the `message.send` span is created as a child of the `executor.process` span. However, the `executor.process` span of the target executor is **not** a child of the `message.send` span because the execution is not nested. Instead, the `executor.process` span of the target executor is **linked** to the `message.send` span of the source executor. This linking creates a traceable path through the workflow execution without implying a nested call hierarchy.

The same linking approach applies to `edge_group.process` spans, which are linked to the source `message.send` spans for causality tracking. This supports fan-in scenarios where multiple source spans contribute to a single processing span.

## Edge Group Delivery Status

Edge group processing spans include delivery status attributes that indicate the outcome of message routing through each edge group. The `edge_group.delivery_status` attribute is set to one of the following values:

| Status                      | Description                                                      |
|-----------------------------|------------------------------------------------------------------|
| `delivered`                 | The message was delivered to the target executor.                |
| `dropped type mismatch`    | The target executor cannot handle the message type.              |
| `dropped target mismatch`  | The message specified a target that does not match this edge.    |
| `dropped condition false`  | The edge routing condition evaluated to false.                   |
| `exception`                 | An exception occurred during edge processing.                    |
| `buffered`                  | The message was buffered, waiting for additional messages (fan-in). |

The `edge_group.delivered` boolean attribute provides a quick check for whether the message was successfully delivered.

## Telemetry Configuration

::: zone pivot="programming-language-csharp"

Workflow telemetry can be enabled through the `WithOpenTelemetry` extension method on the workflow builder. The `WorkflowTelemetryOptions` class provides fine-grained control over which spans are emitted:

| Option                    | Default  | Description                                      |
|---------------------------|----------|--------------------------------------------------|
| `EnableSensitiveData`     | `false`  | Includes raw inputs, outputs, and message content in span attributes. |
| `DisableWorkflowBuild`    | `false`  | Disables `workflow.build` spans.                 |
| `DisableWorkflowRun`      | `false`  | Disables `workflow.session` and `workflow_invoke` spans. |
| `DisableExecutorProcess`  | `false`  | Disables `executor.process` spans.               |
| `DisableEdgeGroupProcess` | `false`  | Disables `edge_group.process` spans.             |
| `DisableMessageSend`      | `false`  | Disables `message.send` spans.                   |

> [!WARNING]
> Enabling sensitive data causes raw message content, executor inputs, and executor outputs to be included in telemetry. Only enable this in secure environments where telemetry data is appropriately protected.

::: zone-end

::: zone pivot="programming-language-python"

Workflow telemetry is enabled through the global `enable_instrumentation()` function. When instrumentation is enabled, all workflow spans are emitted automatically. The `configure_otel_providers()` function can be used to set up exporters for traces, metrics, and logs.

> [!WARNING]
> Review your telemetry pipeline configuration to ensure sensitive data is appropriately protected when exporting traces.

::: zone-end

::: zone pivot="programming-language-go"
## Workflow observability

Workflow telemetry can be enabled with `WithTelemetry` on the workflow builder. Use the workflow OpenTelemetry tracer package to connect spans to your OpenTelemetry provider.

### Enable workflow tracing

```go
import workflowotel "github.com/microsoft/agent-framework-go/workflow/observability/opentelemetry"

wf, err := workflow.NewBuilder(startExecutor).
    AddEdge(startExecutor, nextExecutor).
    WithTelemetry(
        workflowotel.New(workflowotel.Config{}),
        workflow.TelemetryOptions{EnableSensitiveData: true},
    ).
    Build()
```

`TelemetryOptions` can disable workflow build/run, executor process, edge group, or message send spans, and can include serialized inputs and outputs when `EnableSensitiveData` is set.

### Observe workflow events

Monitor workflow execution through event streams:

```go
for evt := range run.NewEvents() {
    switch e := evt.(type) {
    case workflow.ExecutorCompletedEvent:
        log.Printf("Executor %s completed", e.ExecutorID)
    }
}
```

::: zone-end
## Next Steps

- [Learn about state isolation in workflows](../concepts/workflows/state.md).
- [Learn how to visualize workflows](./visualization.md).
