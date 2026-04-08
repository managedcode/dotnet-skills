---
title: Microsoft Agent Framework Workflows - Visualization
description: In-depth look at Visualization in Microsoft Agent Framework Workflows.
zone_pivot_groups: programming-languages
author: TaoChenOSU
ms.topic: tutorial
ms.author: taochen
ms.date: 09/12/2025
ms.service: agent-framework
---

# Microsoft Agent Framework Workflows - Visualization

Sometimes a workflow that has multiple executors and complex interactions can be hard to understand from just reading the code. Visualization can help you see the structure of the workflow more clearly, so that you can verify that it has the intended design.

::: zone pivot="programming-language-csharp"

Workflow visualization can be achieved via extension methods on the `Workflow` class: `ToMermaidString()`, and `ToDotString()`, which generate Mermaid diagram format and Graphviz DOT format respectively.

```csharp
using Microsoft.Agents.AI.Workflows;

// Create a workflow with a fan-out and fan-in pattern
var workflow = new WorkflowBuilder()
    .SetStartExecutor(dispatcher)
    .AddFanOutEdges(dispatcher, [researcher, marketer, legal])
    .AddFanInEdges([researcher, marketer, legal], aggregator)
    .Build();

// Mermaid diagram
Console.WriteLine(workflow.ToMermaidString());

// DiGraph string
Console.WriteLine(workflow.ToDotString());
```

To create an image file from the DOT format, you can use GraphViz tools with the following command:

```bash
dotnet run | tail -n +20 | dot -Tpng -o workflow.png
```

> [!TIP]
> To export visualization images you need to [install GraphViz](https://graphviz.org/download/).

For a complete working implementation with visualization, see the [Visualization sample](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/GettingStarted/Workflows/Visualization).

::: zone-end

::: zone pivot="programming-language-python"

Workflow visualization is done via a `WorkflowViz` object that can be instantiated with a `Workflow` object. The `WorkflowViz` object can then generate visualizations in different formats, such as Graphviz DOT format or Mermaid diagram format.

Creating a `WorkflowViz` object is straightforward:

```python
from agent_framework import WorkflowBuilder, WorkflowViz

# Create a workflow with a fan-out and fan-in pattern
workflow = (
    WorkflowBuilder()
    .set_start_executor(dispatcher)
    .add_fan_out_edges(dispatcher, [researcher, marketer, legal])
    .add_fan_in_edges([researcher, marketer, legal], aggregator)
    .build()
)

viz = WorkflowViz(workflow)
```

Then, you can create visualizations in different formats:

```python
# Mermaid diagram
print(viz.to_mermaid())
# DiGraph string
print(viz.to_digraph())
# Export to a file
print(viz.export(format="svg"))
# Different formats are also supported
print(viz.export(format="png"))
print(viz.export(format="pdf"))
print(viz.export(format="dot"))
# Export with custom filenames
print(viz.export(format="svg", filename="my_workflow.svg"))
# Convenience methods
print(viz.save_svg("workflow.svg"))
print(viz.save_png("workflow.png"))
print(viz.save_pdf("workflow.pdf"))
```

> [!TIP]
> For basic text output (Mermaid and DOT), no additional dependencies are needed. For image export, you need to install the `graphviz` Python package by running: `pip install graphviz>=0.20.0` and [install GraphViz](https://graphviz.org/download/).

For a complete working implementation with visualization, see the [Concurrent with Visualization sample](https://github.com/microsoft/agent-framework/blob/main/python/samples/getting_started/workflows/visualization/concurrent_with_visualization.py).

::: zone-end

The exported diagram will look similar to the following for the example workflow:

```mermaid
flowchart TD
  dispatcher["dispatcher (Start)"];
  researcher["researcher"];
  marketer["marketer"];
  legal["legal"];
  aggregator["aggregator"];
  fan_in__aggregator__e3a4ff58((fan-in))
  legal --> fan_in__aggregator__e3a4ff58;
  marketer --> fan_in__aggregator__e3a4ff58;
  researcher --> fan_in__aggregator__e3a4ff58;
  fan_in__aggregator__e3a4ff58 --> aggregator;
  dispatcher --> researcher;
  dispatcher --> marketer;
  dispatcher --> legal;
```

or in Graphviz DOT format:

![Workflow Diagram](./resources/images/workflow-viz.svg)

## Visualization Features

### Node Styling

- **Start executors**: Green background with "(Start)" label
- **Regular executors**: Blue background with executor ID
- **Fan-in nodes**: Golden background with ellipse shape (DOT) or double circles (Mermaid)

### Edge Styling

- **Normal edges**: Solid arrows
- **Conditional edges**: Dashed/dotted arrows with "conditional" labels
- **Fan-out/Fan-in**: Automatic routing through intermediate nodes

### Layout Options

- **Top-down layout**: Clear hierarchical flow visualization
- **Subgraph clustering**: Nested workflows shown as grouped clusters
- **Automatic positioning**: GraphViz handles optimal node placement