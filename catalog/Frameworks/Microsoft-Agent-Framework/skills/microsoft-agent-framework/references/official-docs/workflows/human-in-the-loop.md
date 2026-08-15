---
title: Microsoft Agent Framework Workflows - Human-in-the-loop (HITL)
description: In-depth look at Human-in-the-loop interactions in Microsoft Agent Framework Workflows.
zone_pivot_groups: programming-languages
author: TaoChenOSU
ms.topic: tutorial
ms.author: taochen
ms.date: 07/16/2026
ms.service: agent-framework
---

<!--
  Language parity table – keep in sync when adding/removing sections.

    | Section                              | C# | Python | Go | Notes                                           |
    |--------------------------------------|:--:|:------:|:--:|:------------------------------------------------|
    | Overview                             | ✅ |   ✅   | ✅ |                                                  |
    | Enable Request and Response Handling | ✅ |   ✅   | ✅ | C# and Go use RequestPort; Python uses ctx.request_info |
    | Handling Requests and Responses      | ✅ |   ✅   | ✅ |                                                  |
    | HITL with Agent Orchestrations       | ✅ |   ✅   | ✅ | No zone pivots; links to orchestration docs     |
    | Checkpoints and Requests             | ✅ |   ✅   | ✅ |                                                  |
    | Next Steps                           | ✅ |   ✅   | ✅ |                                                  |
-->

# Microsoft Agent Framework Workflows - Human-in-the-loop (HITL)

This page provides an overview of **Human-in-the-loop (HITL)** interactions in the Microsoft Agent Framework Workflow system. HITL is achieved through the **request and response** handling mechanism in workflows, which allows executors to send requests to external systems (such as human operators) and wait for their responses before proceeding with the workflow execution.

## Overview

Executors in a workflow can send requests to outside of the workflow and wait for responses. This is useful for scenarios where an executor needs to interact with external systems, such as human-in-the-loop interactions, or any other asynchronous operations.

::: zone pivot="programming-language-csharp"

Let's build a workflow that asks a human operator to guess a number and uses an executor to judge whether the guess is correct.

## Enable Request and Response Handling in a Workflow

Requests and responses are handled via a special type called `RequestPort`.

A `RequestPort` is a communication channel that allows executors to send requests and receive responses. When an executor sends a message to a `RequestPort`, the request port emits a `RequestInfoEvent` that contains the details of the request. External systems can listen for these events, process the requests, and send responses back to the workflow. The framework automatically routes the responses back to the appropriate executor based on the original request.

```csharp
// Create a request port that receives requests of type NumberSignal and responses of type int.
var numberRequestPort = RequestPort.Create<NumberSignal, int>("GuessNumber");
```

Add the input port to a workflow.

```csharp
JudgeExecutor judgeExecutor = new(42);
var workflow = new WorkflowBuilder(numberRequestPort)
    .AddEdge(numberRequestPort, judgeExecutor)
    .AddEdge(judgeExecutor, numberRequestPort)
    .WithOutputFrom(judgeExecutor)
    .Build();
```

The definition of `JudgeExecutor` needs a target number and be able to judge whether the guess is correct. If it is not correct, it will send another request to ask for a new guess through the `RequestPort`.

```csharp
internal enum NumberSignal
{
    Init,
    Above,
    Below,
}

internal sealed class JudgeExecutor() : Executor<int>("Judge")
{
    private readonly int _targetNumber;
    private int _tries;

    public JudgeExecutor(int targetNumber) : this()
    {
        this._targetNumber = targetNumber;
    }

    public override async ValueTask HandleAsync(int message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        this._tries++;
        if (message == this._targetNumber)
        {
            await context.YieldOutputAsync($"{this._targetNumber} found in {this._tries} tries!", cancellationToken);
        }
        else if (message < this._targetNumber)
        {
            await context.SendMessageAsync(NumberSignal.Below, cancellationToken: cancellationToken);
        }
        else
        {
            await context.SendMessageAsync(NumberSignal.Above, cancellationToken: cancellationToken);
        }
    }
}
```

::: zone-end

::: zone pivot="programming-language-python"

In Python, executors send requests using `ctx.request_info()` and handle responses with the `@response_handler` decorator.

Let's build a workflow that asks a human operator to guess a number and uses an executor to judge whether the guess is correct.

## Enable Request and Response Handling in a Workflow

```python
from dataclasses import dataclass

from agent_framework import (
    Executor,
    WorkflowBuilder,
    WorkflowContext,
    handler,
    response_handler,
)


@dataclass
class NumberSignal:
    hint: str  # "init", "above", or "below"


class JudgeExecutor(Executor):
    def __init__(self, target_number: int):
        super().__init__(id="judge")
        self._target_number = target_number
        self._tries = 0

    @handler
    async def handle_guess(self, guess: int, ctx: WorkflowContext[int, str]) -> None:
        self._tries += 1
        if guess == self._target_number:
            await ctx.yield_output(f"{self._target_number} found in {self._tries} tries!")
        elif guess < self._target_number:
            await ctx.request_info(request_data=NumberSignal(hint="below"), response_type=int)
        else:
            await ctx.request_info(request_data=NumberSignal(hint="above"), response_type=int)

    @response_handler
    async def on_human_response(
        self,
        original_request: NumberSignal,
        response: int,
        ctx: WorkflowContext[int, str],
    ) -> None:
        await self.handle_guess(response, ctx)


judge = JudgeExecutor(target_number=42)
workflow = WorkflowBuilder(start_executor=judge).build()
```

The `@response_handler` decorator automatically registers the method to handle responses for the specified request and response types. The framework matches incoming responses to the correct handler based on the type annotations of the `original_request` and `response` parameters.

::: zone-end

::: zone pivot="programming-language-go"

Workflows support human-in-the-loop patterns through `RequestPort`, which pauses execution and waits for external input.

```go
approvalPort := workflow.RequestPort{
    ID:       "ApprovalPort",
    Request:  reflect.TypeFor[string](),
    Response: reflect.TypeFor[bool](),
}

approval := approvalPort.Bind()
finalize := workflow.NewExecutor("FinalizeExecutor", func(approved bool) string {
    if approved {
        return "Request approved by the human reviewer"
    }
    return "Request rejected by the human reviewer"
}).Bind()

wf, err := workflow.NewBuilder(approval).
    AddEdge(approval, finalize).
    WithOutputFrom(finalize).
    Build()
```

A `RequestPort` defines a typed request/response channel between the workflow and the outside world. When an executor reaches a request port, the workflow pauses and emits an external request event. The workflow resumes when an external response is provided.

::: zone-end

## Handling Requests and Responses

::: zone pivot="programming-language-csharp"

An `RequestPort` emits a `RequestInfoEvent` when it receives a request. You can subscribe to these events to handle incoming requests from the workflow. When you receive a response from an external system, send it back to the workflow using the response mechanism. The framework automatically routes the response to the executor that sent the original request.

```csharp
await using StreamingRun handle = await InProcessExecution.RunStreamingAsync(workflow, NumberSignal.Init);
await foreach (WorkflowEvent evt in handle.WatchStreamAsync())
{
    switch (evt)
    {
        case RequestInfoEvent requestInputEvt:
            // Handle `RequestInfoEvent` from the workflow
            int guess = ...; // Get the guess from the human operator or any external system
            await handle.SendResponseAsync(requestInputEvt.Request.CreateResponse(guess));
            break;

        case WorkflowOutputEvent outputEvt:
            // The workflow has yielded output
            Console.WriteLine($"Workflow completed with result: {outputEvt.Data}");
            return;
    }
}
```

> [!TIP]
> See the [full sample](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/HumanInTheLoop/HumanInTheLoopBasic) for the complete runnable project.

::: zone-end

::: zone pivot="programming-language-python"

Executors can send requests directly without needing a separate component. When an executor calls `ctx.request_info()`, the workflow emits a `WorkflowEvent` with `type == "request_info"`. You can subscribe to these events to handle incoming requests from the workflow. When you receive a response from an external system, send it back to the workflow using the response mechanism. The framework automatically routes the response to the executor's `@response_handler` method.

```python
from collections.abc import AsyncIterable

from agent_framework import WorkflowEvent


async def process_event_stream(stream: AsyncIterable[WorkflowEvent]) -> dict[str, int] | None:
    """Process events from the workflow stream to capture requests."""
    requests: list[tuple[str, NumberSignal]] = []
    async for event in stream:
        if event.type == "request_info":
            requests.append((event.request_id, event.data))

    # Handle any pending human feedback requests.
    if requests:
        responses: dict[str, int] = {}
        for request_id, request in requests:
            guess = ...  # Get the guess from the human operator or any external system.
            responses[request_id] = guess
        return responses

    return None

# Initiate the first run of the workflow with an initial guess.
# Runs are not isolated; state is preserved across multiple calls to run.
stream = workflow.run(25, stream=True)

pending_responses = await process_event_stream(stream)
while pending_responses is not None:
    # Run the workflow until there is no more human feedback to provide,
    # in which case this workflow completes.
    stream = workflow.run(stream=True, responses=pending_responses)
    pending_responses = await process_event_stream(stream)
```

> [!TIP]
> See this [full sample](https://github.com/microsoft/agent-framework/blob/main/python/samples/03-workflows/human-in-the-loop/guessing_game_with_human_input.py) for a complete runnable file.

::: zone-end

::: zone pivot="programming-language-go"

Listen for `workflow.RequestInfoEvent`, create a response from the request, and resume the run with that response:

```go
run, err := inproc.Default.Run(ctx, wf, "Approve deployment to production?")
if err != nil {
    return err
}

var request *workflow.ExternalRequest
for evt := range run.NewEvents() {
    if requestEvent, ok := evt.(workflow.RequestInfoEvent); ok {
        request = requestEvent.Request
        break
    }
}

response, err := request.CreateResponse(true)
if err != nil {
    return err
}

if _, err := run.Resume(ctx, response); err != nil {
    return err
}

for evt := range run.NewEvents() {
    if output, ok := evt.(workflow.OutputEvent); ok {
        fmt.Println(output.Output)
    }
}
```

> [!TIP]
> See the [human-in-the-loop sample](https://github.com/microsoft/agent-framework-go/blob/main/examples/03-workflows/human-in-the-loop/human_in_the_loop_basic/main.go) for a complete runnable file.

::: zone-end
## Human-in-the-Loop with Agent Orchestrations

The `RequestPort` pattern described above works with custom executors and `WorkflowBuilder`. When using **agent orchestrations** (such as sequential, concurrent, or group chat workflows), **tool approval** is achieved through the human-in-the-loop request/response mechanism.

Agents can use tools that require human approval before execution. When the agent attempts to call an approval-required tool, the workflow pauses and emits a `RequestInfoEvent` just like the `RequestPort` pattern, but the event payload contains a `ToolApprovalRequestContent` (C# and Go) or a `Content` with `type == "function_approval_request"` (Python) instead of a custom request type.

For interactive scenarios where an agent needs to gather more information from the user and iterate before proceeding; rather than only approving or rejecting a tool call; use the **[handoff orchestration](./orchestrations/handoff.md)**. Handoff is interactive by default: when an agent responds without handing off to another agent, control returns to the user for the next input, which enables multi-turn back-and-forth within the orchestration. Sequential, concurrent, and group chat orchestrations do not pause for free-form user input on their own; pair them with a `RequestPort` in a custom `WorkflowBuilder` workflow when you need that control between steps.

> [!TIP]
> For complete examples with code, see:
> - [Sequential orchestration with HITL](./orchestrations/sequential.md#sequential-orchestration-with-human-in-the-loop)
> - [GroupChatToolApproval sample (C#)](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/Agents/GroupChatToolApproval)
> - [Sequential tool approval sample (Python)](https://github.com/microsoft/agent-framework/blob/main/python/samples/03-workflows/tool-approval/sequential_builder_tool_approval.py)
> - [Sequential request info sample (Python)](https://github.com/microsoft/agent-framework/blob/main/python/samples/03-workflows/human-in-the-loop/sequential_request_info.py)

## Checkpoints and Requests

To learn more about checkpoints, see [Checkpoints](./checkpoints.md).

When a checkpoint is created, pending requests are also saved as part of the checkpoint state. When you restore from a checkpoint, any pending requests will be re-emitted as `RequestInfoEvent` objects, allowing you to capture and respond to them. You can also resume from a checkpoint and provide responses in the same call by passing both `checkpoint_id` and `responses` to `workflow.run(...)`.

After restoring, listen for the re-emitted request events and respond through the same response mechanism shown earlier for your language.

## Next Steps

- [Learn about sequential orchestration with HITL](./orchestrations/sequential.md#sequential-orchestration-with-human-in-the-loop).
- [Learn how to manage state](../concepts/workflows/state.md) in workflows.
- [Learn how to create checkpoints and resume from them](./checkpoints.md).
- [Learn how to monitor workflows](./observability.md).
- [Learn how to visualize workflows](./visualization.md).
