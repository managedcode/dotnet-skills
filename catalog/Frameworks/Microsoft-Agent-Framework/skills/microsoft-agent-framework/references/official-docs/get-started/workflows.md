---
title: "Step 5: Workflows"
description: "Chain multiple steps together in a sequential workflow."
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: tutorial
ms.author: edvan
ms.date: 07/08/2026
ms.service: agent-framework
---

# Step 5: Workflows

Workflows let you chain multiple steps together — each step processes data and passes it to the next.

:::zone pivot="programming-language-csharp"

Define workflow steps (executors):

```csharp
using Microsoft.Agents.AI.Workflows;

// Step 1: Convert text to uppercase
Func<string, string> uppercaseFunc = s => s.ToUpperInvariant();
var uppercase = uppercaseFunc.BindAsExecutor("UppercaseExecutor");

// Step 2: Reverse the string and yield output
class ReverseTextExecutor() : Executor<string, string>("ReverseTextExecutor")
{
    public override ValueTask<string> HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(string.Concat(message.Reverse()));
    }
}
ReverseTextExecutor reverse = new();
```

Build and run the workflow:

```csharp
WorkflowBuilder builder = new(uppercase);
builder.AddEdge(uppercase, reverse).WithOutputFrom(reverse);
var workflow = builder.Build();

await using Run run = await InProcessExecution.RunAsync(workflow, "Hello, World!");
foreach (WorkflowEvent evt in run.NewEvents)
{
    if (evt is ExecutorCompletedEvent executorComplete)
    {
        Console.WriteLine($"{executorComplete.ExecutorId}: {executorComplete.Data}");
    }
}
```

> [!TIP]
> See [here](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/01-get-started/05_first_workflow) for a full runnable sample application.

:::zone-end

:::zone pivot="programming-language-python"

Define workflow steps (executors) and connect them with edges:

:::code language="python" source="~/../agent-framework-code/python/samples/01-get-started/07_first_graph_workflow.py" id="create_workflow" highlight="22":::

Build and run the workflow:

:::code language="python" source="~/../agent-framework-code/python/samples/01-get-started/07_first_graph_workflow.py" id="run_workflow" highlight="3":::

> [!TIP]
> See the [full sample](https://github.com/microsoft/agent-framework/blob/main/python/samples/01-get-started/07_first_graph_workflow.py) for the complete runnable file.

:::zone-end

:::zone pivot="programming-language-go"

Define workflow steps (executors) and connect them with edges:

```go
package main

import (
    "context"
    "fmt"
    "slices"
    "strings"

    "github.com/microsoft/agent-framework-go/workflow"
    "github.com/microsoft/agent-framework-go/workflow/inproc"
)

func main() {
    // Step 1: Convert text to uppercase.
    uppercase := workflow.NewExecutor("UppercaseExecutor", func(input string) string {
        return strings.ToUpper(input)
    }).Bind()

    // Step 2: Reverse the string.
    reverse := workflow.NewExecutor("ReverseExecutor", func(input string) string {
        runes := []rune(input)
        slices.Reverse(runes)
        return string(runes)
    }).Bind()

    // Build the workflow by connecting executors sequentially.
    wf, err := workflow.NewBuilder(uppercase).
        AddEdge(uppercase, reverse).
        WithOutputFrom(reverse).
        Build()
    if err != nil {
        panic(err)
    }

    // Execute the workflow with sample input.
    run, err := inproc.Default.Run(context.Background(), wf, "Hello, World!")
    if err != nil {
        panic(err)
    }
    for evt := range run.NewEvents() {
        if evt, ok := evt.(workflow.ExecutorCompletedEvent); ok {
            fmt.Printf("%s: %v\n", evt.ExecutorID, evt.Result)
        }
    }
}
```

> [!TIP]
> See the [full sample](https://github.com/microsoft/agent-framework-go/blob/main/examples/01-get-started/05_first_workflow/main.go) for the complete runnable file.

:::zone-end

## Next steps

> [!div class="nextstepaction"]
> [Step 6: Agent Harness](./harness.md)

**Go deeper:**

- [Workflows](../concepts/workflows/index.md) — understand workflow architecture
- [Sequential workflows](../workflows/orchestrations/sequential.md) — linear step-by-step patterns
- [Agents in workflows](../workflows/agents-in-workflows.md) — using agents as workflow steps
