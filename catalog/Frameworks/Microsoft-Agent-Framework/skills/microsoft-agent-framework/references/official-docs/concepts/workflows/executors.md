---
title: Microsoft Agent Framework Workflows - Executors
description: In-depth look at Executors in Microsoft Agent Framework Workflows.
zone_pivot_groups: programming-languages
author: TaoChenOSU
ms.topic: article
ms.author: taochen
ms.date: 07/01/2026
ms.service: agent-framework
---

<!--
  Language parity table – keep in sync when adding/removing sections.

    | Section                                            | C# | Python | Go | Notes            |
    |----------------------------------------------------|:--:|:------:|:--:|------------------|
    | Basic Executor Structure                           | ✅ |   ✅   | ✅ |                  |
    | Resettable Executors (TIP)                         | ✅ |   ❌   | ✅ | C#/Go reset hooks; Python uses fresh instances |
    | Multiple Input Types                               | ✅ |   ✅   | ✅ |                  |
    | Function-Based Executors                           | ✅ |   ✅   | ✅ |                  |
    | Explicit Type Parameters                           | ❌ |   ✅   | ❌ | Python-specific  |
    | The WorkflowContext Object                         | ✅ |   ✅   | ✅ |                  |
    | Designating Terminal and Intermediate Outputs      | ❌ |   ✅   | ❌ | Python-specific  |
    | Agent Executors                                    | ❌ |   ❌   | ✅ | Go-specific      |
    | Executor Lifecycle                                 | ❌ |   ❌   | ✅ | Go-specific      |
-->

# Executors

Executors are the fundamental building blocks that process messages in a workflow. They are autonomous processing units that receive typed messages, perform operations, and can produce output messages or events.

## Overview

Each executor has a unique identifier and can handle specific message types. Executors can be:

- **Custom logic components** — process data, call APIs, or transform messages
- **AI agents** — use LLMs to generate responses (see [Agents in Workflows](../../workflows/agents-in-workflows.md))

::: zone pivot="programming-language-csharp"

> [!IMPORTANT]
> The recommended way to define executor message handlers in C# is to use the `[MessageHandler]` attribute on methods within a `partial` class that derives from `Executor`. This uses compile-time source generation for handler registration, providing better performance, compile-time validation, and Native AOT compatibility.

## Basic Executor Structure

Executors derive from the `Executor` base class and use the `[MessageHandler]` attribute to declare handler methods. The class must be marked `partial` to enable source generation.

```csharp
using Microsoft.Agents.AI.Workflows;

internal sealed partial class UppercaseExecutor() : Executor("UppercaseExecutor")
{
    [MessageHandler]
    private ValueTask<string> HandleAsync(string message, IWorkflowContext context)
    {
        string result = message.ToUpperInvariant();
        return ValueTask.FromResult(result); // Return value is automatically sent to connected executors
    }
}
```

You can also send messages manually without returning a value:

```csharp
internal sealed partial class UppercaseExecutor() : Executor("UppercaseExecutor")
{
    [MessageHandler]
    private async ValueTask HandleAsync(string message, IWorkflowContext context)
    {
        string result = message.ToUpperInvariant();
        await context.SendMessageAsync(result); // Manually send messages to connected executors
    }
}
```

> [!TIP]
> Executors can hold mutable state. If a stateful executor is shared across workflow runs, it must implement `IResettableExecutor` to clear stale state between runs. See [Resettable Executors](./advanced/resettable-executors.md) for details.

## Multiple Input Types

Handle multiple input types by defining multiple `[MessageHandler]` methods:

```csharp
internal sealed partial class SampleExecutor() : Executor("SampleExecutor")
{
    [MessageHandler]
    private ValueTask<string> HandleStringAsync(string message, IWorkflowContext context)
    {
        return ValueTask.FromResult(message.ToUpperInvariant());
    }

    [MessageHandler]
    private ValueTask<int> HandleIntAsync(int message, IWorkflowContext context)
    {
        return ValueTask.FromResult(message * 2);
    }
}
```

## Function-Based Executors

Create an executor from a function using the `BindExecutor` extension method:

```csharp
Func<string, string> uppercaseFunc = s => s.ToUpperInvariant();
var uppercase = uppercaseFunc.BindExecutor("UppercaseExecutor");
```

## The IWorkflowContext Object

The `IWorkflowContext` provides methods for interacting with the workflow during execution:

- **`SendMessageAsync`** — send messages to connected executors
- **`YieldOutputAsync`** — produce workflow outputs returned/streamed to the caller

```csharp
internal sealed partial class OutputExecutor() : Executor("OutputExecutor")
{
    [MessageHandler]
    private async ValueTask HandleAsync(string message, IWorkflowContext context)
    {
        await context.YieldOutputAsync("Hello, World!");
    }
}
```

If a handler neither sends messages nor yields outputs, it can simply perform side effects:

```csharp
internal sealed partial class LogExecutor() : Executor("LogExecutor")
{
    [MessageHandler]
    private void Handle(string message, IWorkflowContext context)
    {
        Console.WriteLine("Doing some work...");
    }
}
```

::: zone-end

::: zone pivot="programming-language-python"

## Basic Executor Structure

Executors inherit from the `Executor` base class. Each executor uses methods decorated with the `@handler` decorator. Handlers must have proper type annotations to specify the message types they process.

```python
from agent_framework import (
    Executor,
    WorkflowContext,
    handler,
)

class UpperCase(Executor):

    @handler
    async def to_upper_case(self, text: str, ctx: WorkflowContext[str]) -> None:
        """Convert the input to uppercase and forward it to the next node."""
        await ctx.send_message(text.upper())
```

## Function-Based Executors

Create an executor from a function using the `@executor` decorator:

```python
from agent_framework import (
    WorkflowContext,
    executor,
)

@executor(id="upper_case_executor")
async def upper_case(text: str, ctx: WorkflowContext[str]) -> None:
    """Convert the input to uppercase and forward it to the next node."""
    await ctx.send_message(text.upper())
```

## Multiple Input Types

Handle multiple input types by defining multiple handlers:

```python
class SampleExecutor(Executor):

    @handler
    async def to_upper_case(self, text: str, ctx: WorkflowContext[str]) -> None:
        await ctx.send_message(text.upper())

    @handler
    async def double_integer(self, number: int, ctx: WorkflowContext[int]) -> None:
        await ctx.send_message(number * 2)
```

## Explicit Type Parameters

As an alternative to type annotations, you can specify types explicitly via decorator parameters:

> [!IMPORTANT]
> When using explicit type parameters, you must specify **all** types via the decorator — you cannot mix explicit parameters with type annotations. The `input` parameter is required; `output` and `workflow_output` are optional.

```python
class ExplicitTypesExecutor(Executor):

    @handler(input=str, output=str)
    async def to_upper_case(self, text, ctx) -> None:
        await ctx.send_message(text.upper())

    @handler(input=str | int, output=str)
    async def handle_mixed(self, message, ctx) -> None:
        await ctx.send_message(str(message).upper())

    @handler(input=str, output=int, workflow_output=bool)
    async def process_with_workflow_output(self, message, ctx) -> None:
        await ctx.send_message(len(message))
        await ctx.yield_output(True)
```

## The WorkflowContext Object

The `WorkflowContext` provides methods for interacting with the workflow during execution:

- **`send_message`** — send messages to connected executors
- **`yield_output`** — produce workflow outputs returned/streamed to the caller

```python
class OutputExecutor(Executor):

    @handler
    async def handle(self, message: str, ctx: WorkflowContext[Never, str]) -> None:
        await ctx.yield_output("Hello, World!")
```

If a handler neither sends messages nor yields outputs, no type parameter is needed:

```python
class LogExecutor(Executor):

    @handler
    async def handle(self, message: str, ctx: WorkflowContext) -> None:
        print("Doing some work...")
```

::: zone-end

::: zone pivot="programming-language-python"

## Designating Terminal and Intermediate Output Executors

Which executors contribute to the workflow's terminal answer and which emit observational progress is a **build-time** decision configured on `WorkflowBuilder`, not a per-emission flag.

- `output_from` — executors whose `ctx.yield_output(...)` calls produce `"output"` events and are returned by `WorkflowRunResult.get_outputs()`.
- `intermediate_output_from` — executors whose `ctx.yield_output(...)` calls produce `"intermediate"` events and are returned by `WorkflowRunResult.get_intermediate_outputs()`.

```python
from agent_framework import WorkflowBuilder

workflow = WorkflowBuilder(
    start_executor=analysis_executor,
    output_from=[summary_executor],
    intermediate_output_from=[analysis_executor],
).build()
```

> [!IMPORTANT]
> `ctx.yield_output(...)` has **no** per-emission flag. The same call is labelled `"output"` or `"intermediate"` solely based on the builder's designation. There is no `ctx.yield_intermediate(...)` API — designation does not vary per yield.

Both lists are optional. If either output-selection list is provided, an executor that appears in neither list can still send messages to downstream executors via `ctx.send_message(...)`, but its `yield_output` calls are hidden. If both lists are omitted, every `yield_output` still emits `"output"` for compatibility.

::: zone-end

::: zone pivot="programming-language-go"

## Basic Executor Structure

Executors are the processing units in a workflow. They receive input, perform work, and produce output.

## Multiple Input Types

Register multiple handlers by configuring routes on an executor:

```go
sample := (&workflow.Executor{
    ID: "SampleExecutor",
    ConfigureProtocol: func(pb *workflow.ProtocolBuilder) (*workflow.ProtocolBuilder, error) {
        pb.RouteBuilder.
            AddHandlerRaw(reflect.TypeFor[string](), reflect.TypeFor[string](), func(_ *workflow.Context, msg any) (any, error) {
                return strings.ToUpper(msg.(string)), nil
            }).
            AddHandlerRaw(reflect.TypeFor[int](), reflect.TypeFor[int](), func(_ *workflow.Context, msg any) (any, error) {
                return msg.(int) * 2, nil
            })
        return pb, nil
    },
}).Bind()
```

## Function-Based Executors

The simplest way to create an executor is with `workflow.NewExecutor(...).Bind()`:

```go
uppercase := workflow.NewExecutor("UppercaseExecutor", func(input string) string {
    return strings.ToUpper(input)
}).Bind()
```

Function executors automatically register the input type and can auto-send and auto-yield returned values.

## The workflow.Context Object

Handlers can accept `*workflow.Context` to interact with the workflow during execution:

```go
output := workflow.NewExecutor("OutputExecutor", func(ctx *workflow.Context, message string) error {
    return ctx.YieldOutput("Hello, World!")
}).Bind()
```

The context also exposes APIs such as `SendMessage`, `AddEvent`, `PostRequest`, `ReadState`, and `QueueStateUpdate`.

## Agent Executors

Agents can be used as workflow executors via `agentworkflow.New`:

```go
agentExecutor := agentworkflow.New(myAgent, agentworkflow.Config{
    EmitUpdateEvents: true,
})
```

## Executor Lifecycle

Executors support lifecycle hooks through fields on `workflow.Executor`:

| Hook | Purpose |
|---|---|
| `ConfigureProtocol` | Set up message routing and declared send/yield types |
| `InitializeFunc` | Setup when an executor instance is created for a run |
| `ResetFunc` | Reset executor-local state before reuse |
| `OnCheckpointFunc` | Save state at checkpoint |
| `OnCheckpointRestoredFunc` | Restore state from checkpoint |
| `OnMessageDeliveryStartingFunc` | Run before a superstep delivers messages |
| `OnMessageDeliveryFinishedFunc` | Run after a superstep finishes message delivery |

```go
stateful := workflow.NewExecutor("StatefulExecutor", handleMessage).Extend(&workflow.Executor{
    InitializeFunc: func(ctx *workflow.Context) error {
        return nil
    },
    ResetFunc: func() error {
        return nil
    },
    OnCheckpointFunc: func(ctx *workflow.Context) error {
        return ctx.QueueStateUpdate("StatefulExecutorState", "", currentState)
    },
    OnCheckpointRestoredFunc: func(ctx *workflow.Context) error {
        restored, err := ctx.ReadState("StatefulExecutorState", "")
        if err != nil {
            return err
        }
        currentState = restored
        return nil
    },
}).Bind()
```

::: zone-end
## Next steps

> [!div class="nextstepaction"]
> [Edges](./edges.md)
