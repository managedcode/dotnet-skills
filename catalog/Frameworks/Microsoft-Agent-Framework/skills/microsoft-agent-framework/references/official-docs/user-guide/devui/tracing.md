---
title: DevUI Tracing & Observability
description: Learn how to view OpenTelemetry traces in DevUI for debugging and monitoring your agents.
author: moonbox3
ms.topic: how-to
ms.author: evmattso
ms.date: 12/10/2025
ms.service: agent-framework
zone_pivot_groups: programming-languages
---

# Tracing & Observability

DevUI provides built-in support for capturing and displaying OpenTelemetry (OTel) traces emitted by the Agent Framework. DevUI does not create its own spans - it collects the spans that Agent Framework emits during agent and workflow execution, then displays them in the debug panel. This helps you debug agent behavior, understand execution flow, and identify performance issues.

::: zone pivot="programming-language-csharp"

## Coming Soon

DevUI documentation for C# is coming soon. Please check back later or refer to the Python documentation for conceptual guidance.

::: zone-end

::: zone pivot="programming-language-python"

## Enabling Tracing

Enable tracing when starting DevUI with the `--tracing` flag:

```bash
devui ./agents --tracing
```

This enables OpenTelemetry tracing for Agent Framework operations.

## Viewing Traces in DevUI

When tracing is enabled, the DevUI web interface displays trace information:

1. Run an agent or workflow through the UI
2. Open the debug panel (available in developer mode)
3. View the trace timeline showing:
   - Span hierarchy
   - Timing information
   - Agent/workflow events
   - Tool calls and results

## Trace Structure

Agent Framework emits traces following OpenTelemetry semantic conventions for GenAI. A typical trace includes:

```
Agent Execution
    LLM Call
        Prompt
        Response
    Tool Call
        Tool Execution
        Tool Result
    LLM Call
        Prompt
        Response
```

For workflows, traces show the execution path through executors:

```
Workflow Execution
    Executor A
        Agent Execution
            ...
    Executor B
        Agent Execution
            ...
```

## Programmatic Tracing

When using DevUI programmatically with `serve()`, tracing can be enabled:

```python
from agent_framework.devui import serve

serve(
    entities=[agent],
    tracing_enabled=True
)
```

## Integration with External Tools

DevUI captures and displays traces emitted by the Agent Framework - it does not create its own spans. These are standard OpenTelemetry traces that can also be exported to external observability tools like:

- Jaeger
- Zipkin
- Azure Monitor
- Datadog

To export traces to an external collector, set the `OTLP_ENDPOINT` environment variable:

```bash
export OTLP_ENDPOINT="http://localhost:4317"
devui ./agents --tracing
```

Without an OTLP endpoint, traces are captured locally and displayed only in the DevUI debug panel.

::: zone-end

## Related Documentation

For more details on Agent Framework observability:

- [Observability](../observability.md) - Comprehensive guide to agent tracing
- [Workflow Observability](../workflows/observability.md) - Workflow-specific tracing

## Next Steps

- [Security & Deployment](./security.md) - Secure your DevUI deployment
- [Samples](./samples.md) - Browse sample agents and workflows
