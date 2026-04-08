---
title: Create a Simple Sequential Workflow
description: Learn how to create a simple sequential workflow.
zone_pivot_groups: programming-languages
author: TaoChenOSU
ms.topic: tutorial
ms.author: taochen
ms.date: 09/29/2025
ms.service: agent-framework
---

# Create a Simple Sequential Workflow

This tutorial demonstrates how to create a simple sequential workflow using Agent Framework Workflows.

Sequential workflows are the foundation of building complex AI agent systems. This tutorial shows how to create a simple two-step workflow where each step processes data and passes it to the next step.

::: zone pivot="programming-language-csharp"

## Overview

In this tutorial, you'll create a workflow with two executors:

1. **Uppercase Executor** - Converts input text to uppercase
2. **Reverse Text Executor** - Reverses the text and outputs the final result

The workflow demonstrates core concepts like:

- Creating a custom executor with one handler
- Creating a custom executor from a function
- Using `WorkflowBuilder` to connect executors with edges
- Processing data through sequential steps
- Observing workflow execution through events

### Concepts Covered

- [Executors](../../user-guide/workflows/core-concepts/executors.md)
- [Direct Edges](../../user-guide/workflows/core-concepts/edges.md#direct-edges)
- [Workflow Builder](../../user-guide/workflows/core-concepts/workflows.md)
- [Events](../../user-guide/workflows/core-concepts/events.md)

## Prerequisites

- [.NET 8.0 SDK or later](https://dotnet.microsoft.com/download)
- No external AI services required for this basic example
- A new console application

## Step-by-Step Implementation

The following sections show how to build the sequential workflow step by step.

### Step 1: Install NuGet packages

First, install the required packages for your .NET project:

```dotnetcli
dotnet add package Microsoft.Agents.AI.Workflows --prerelease
```

### Step 2: Define the Uppercase Executor

Define an executor that converts text to uppercase:

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows;

/// <summary>
/// First executor: converts input text to uppercase.
/// </summary>
Func<string, string> uppercaseFunc = s => s.ToUpperInvariant();
var uppercase = uppercaseFunc.BindExecutor("UppercaseExecutor");
```

**Key Points:**

- Create a function that takes a string and returns the uppercase version
- Use `BindExecutor()` to create an executor from the function

### Step 3: Define the Reverse Text Executor

Define an executor that reverses the text:

```csharp
/// <summary>
/// Second executor: reverses the input text and completes the workflow.
/// </summary>
internal sealed class ReverseTextExecutor() : Executor<string, string>("ReverseTextExecutor")
{
    public override ValueTask<string> HandleAsync(string input, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        // Reverse the input text
        return ValueTask.FromResult(new string(input.Reverse().ToArray()));
    }
}

ReverseTextExecutor reverse = new();
```

**Key Points:**

- Create a class that inherits from `Executor<TInput, TOutput>`
- Implement `HandleAsync()` to process the input and return the output

### Step 4: Build and Connect the Workflow

Connect the executors using `WorkflowBuilder`:

```csharp
// Build the workflow by connecting executors sequentially
WorkflowBuilder builder = new(uppercase);
builder.AddEdge(uppercase, reverse).WithOutputFrom(reverse);
var workflow = builder.Build();
```

**Key Points:**

- `WorkflowBuilder` constructor takes the starting executor
- `AddEdge()` creates a directed connection from uppercase to reverse
- `WithOutputFrom()` specifies which executors produce workflow outputs
- `Build()` creates the immutable workflow

### Step 5: Execute the Workflow

Run the workflow and observe the results:

```csharp
// Execute the workflow with input data
await using Run run = await InProcessExecution.RunAsync(workflow, "Hello, World!");
foreach (WorkflowEvent evt in run.NewEvents)
{
    switch (evt)
    {
        case ExecutorCompletedEvent executorComplete:
            Console.WriteLine($"{executorComplete.ExecutorId}: {executorComplete.Data}");
            break;
    }
}
```

### Step 6: Understanding the Workflow Output

When you run the workflow, you'll see output like:

```text
UppercaseExecutor: HELLO, WORLD!
ReverseTextExecutor: !DLROW ,OLLEH
```

The input "Hello, World!" is first converted to uppercase ("HELLO, WORLD!"), then reversed ("!DLROW ,OLLEH").

## Key Concepts Explained

### Executor Interface

Executors from functions:

- Use `BindExecutor()` to create an executor from a function

Executors implement `Executor<TInput, TOutput>`:

- **TInput**: The type of data this executor accepts
- **TOutput**: The type of data this executor produces
- **HandleAsync**: The method that processes the input and returns the output

### .NET Workflow Builder Pattern

The `WorkflowBuilder` provides a fluent API for constructing workflows:

- **Constructor**: Takes the starting executor
- **AddEdge()**: Creates directed connections between executors
- **WithOutputFrom()**: Specifies which executors produce workflow outputs
- **Build()**: Creates the final immutable workflow

### .NET Event Types

During execution, you can observe these event types:

- `ExecutorCompletedEvent` - When an executor finishes processing

## Complete .NET Example

For the complete, ready-to-run implementation, see the [01_ExecutorsAndEdges sample](https://github.com/microsoft/agent-framework/blob/main/dotnet/samples/GettingStarted/Workflows/_Foundational/01_ExecutorsAndEdges/Program.cs) in the Agent Framework repository.

This sample includes:

- Full implementation with all using statements and class structure
- Additional comments explaining the workflow concepts
- Complete project setup and configuration

::: zone-end

::: zone pivot="programming-language-python"

## Overview

In this tutorial, you'll create a workflow with two executors:

1. **Upper Case Executor** - Converts input text to uppercase
2. **Reverse Text Executor** - Reverses the text and outputs the final result

The workflow demonstrates core concepts like:

- Two ways to define a unit of work (an executor node):
  1. A custom class that subclasses `Executor` with an async method marked by `@handler`
  2. A standalone async function decorated with `@executor`
- Connecting executors with `WorkflowBuilder`
- Passing data between steps with `ctx.send_message()`
- Yielding final output with `ctx.yield_output()`
- Streaming events for real-time observability

### Concepts Covered

- [Executors](../../user-guide/workflows/core-concepts/executors.md)
- [Direct Edges](../../user-guide/workflows/core-concepts/edges.md#direct-edges)
- [Workflow Builder](../../user-guide/workflows/core-concepts/workflows.md)
- [Events](../../user-guide/workflows/core-concepts/events.md)

## Prerequisites

- Python 3.10 or later
- Agent Framework Core Python package installed: `pip install agent-framework-core --pre`
- No external AI services required for this basic example

## Step-by-Step Implementation

The following sections show how to build the sequential workflow step by step.

### Step 1: Import Required Modules

First, import the necessary modules from Agent Framework:

```python
import asyncio
from typing_extensions import Never
from agent_framework import WorkflowBuilder, WorkflowContext, WorkflowOutputEvent, executor
```

### Step 2: Create the First Executor

Create an executor that converts text to uppercase by implementing an executor with a handler method:

```python
class UpperCase(Executor):
    def __init__(self, id: str):
        super().__init__(id=id)

    @handler
    async def to_upper_case(self, text: str, ctx: WorkflowContext[str]) -> None:
        """Convert the input to uppercase and forward it to the next node.

        Note: The WorkflowContext is parameterized with the type this handler will
        emit. Here WorkflowContext[str] means downstream nodes should expect str.
        """
        result = text.upper()

        # Send the result to the next executor in the workflow.
        await ctx.send_message(result)
```

**Key Points:**

- Subclassing `Executor` lets you define a named node with lifecycle hooks if needed
- The `@handler` decorator marks the async method that does the work
- The handler signature follows a contract:
  - First parameter is the typed input to this node (here: `text: str`)
  - Second parameter is a `WorkflowContext[T_Out]`, where `T_Out` is the type of data this node will emit via `ctx.send_message()` (here: `str`)
- Within a handler you typically compute a result and forward it to downstream nodes using `ctx.send_message(result)`

### Step 3: Create the Second Executor

For simple steps you can skip subclassing and define an async function with the same signature pattern (typed input + `WorkflowContext`) and decorate it with `@executor`. This creates a fully functional node that can be wired into a flow:

```python
@executor(id="reverse_text_executor")
async def reverse_text(text: str, ctx: WorkflowContext[Never, str]) -> None:
    """Reverse the input and yield the workflow output."""
    result = text[::-1]

    # Yield the final output for this workflow run
    await ctx.yield_output(result)
```

**Key Points:**

- The `@executor` decorator transforms a standalone async function into a workflow node
- The `WorkflowContext` is parameterized with two types:
  - `T_Out = Never`: this node does not send messages to downstream nodes
  - `T_W_Out = str`: this node yields workflow output of type `str`
- Terminal nodes yield outputs using `ctx.yield_output()` to provide workflow results
- The workflow completes when it becomes idle (no more work to do)

### Step 4: Build the Workflow

Connect the executors using `WorkflowBuilder`:

```python
upper_case = UpperCase(id="upper_case_executor")

workflow = (
    WorkflowBuilder()
    .add_edge(upper_case, reverse_text)
    .set_start_executor(upper_case)
    .build()
)
```

**Key Points:**

- `add_edge()` creates directed connections between executors
- `set_start_executor()` defines the entry point
- `build()` finalizes the workflow

### Step 5: Run the Workflow with Streaming

Execute the workflow and observe events in real-time:

```python
async def main():
    # Run the workflow and stream events
    async for event in workflow.run_stream("hello world"):
        print(f"Event: {event}")
        if isinstance(event, WorkflowOutputEvent):
            print(f"Workflow completed with result: {event.data}")

if __name__ == "__main__":
    asyncio.run(main())
```

### Step 6: Understanding the Output

When you run the workflow, you'll see events like:

```text
Event: ExecutorInvokedEvent(executor_id=upper_case_executor)
Event: ExecutorCompletedEvent(executor_id=upper_case_executor)
Event: ExecutorInvokedEvent(executor_id=reverse_text_executor)
Event: ExecutorCompletedEvent(executor_id=reverse_text_executor)
Event: WorkflowOutputEvent(data='DLROW OLLEH', source_executor_id=reverse_text_executor)
Workflow completed with result: DLROW OLLEH
```

## Key Concepts Explained

### Two Ways to Define Executors

1. **Custom class (subclassing `Executor`)**: Best when you need lifecycle hooks or complex state. Define an async method with the `@handler` decorator.
2. **Function-based (`@executor` decorator)**: Best for simple steps. Define a standalone async function with the same signature pattern.

Both approaches use the same handler signature:
- First parameter: the typed input to this node
- Second parameter: a `WorkflowContext[T_Out, T_W_Out]`

### Workflow Context Types

The `WorkflowContext` generic type defines what data flows between executors:

- `WorkflowContext[T_Out]` - Used for nodes that send messages of type `T_Out` to downstream nodes via `ctx.send_message()`
- `WorkflowContext[T_Out, T_W_Out]` - Used for nodes that also yield workflow output of type `T_W_Out` via `ctx.yield_output()`
- `WorkflowContext` without type parameters is equivalent to `WorkflowContext[Never, Never]`, meaning this node neither sends messages to downstream nodes nor yields workflow output

### Event Types

During streaming execution, you'll observe these event types:

- `ExecutorInvokedEvent` - When an executor starts processing
- `ExecutorCompletedEvent` - When an executor finishes processing
- `WorkflowOutputEvent` - Contains the final workflow result

### Python Workflow Builder Pattern

The `WorkflowBuilder` provides a fluent API for constructing workflows:

- **add_edge()**: Creates directed connections between executors
- **set_start_executor()**: Defines the workflow entry point
- **build()**: Finalizes and returns an immutable workflow object

## Complete Example

For the complete, ready-to-run implementation, see the [sample](https://github.com/microsoft/agent-framework/blob/main/python/samples/getting_started/workflows/_start-here/step1_executors_and_edges.py) in the Agent Framework repository.

This sample includes:

- Full implementation with all imports and documentation
- Additional comments explaining the workflow concepts
- Sample output showing the expected results

::: zone-end

## Next Steps

> [!div class="nextstepaction"]
> [Learn about creating a simple concurrent workflow](simple-concurrent-workflow.md)
