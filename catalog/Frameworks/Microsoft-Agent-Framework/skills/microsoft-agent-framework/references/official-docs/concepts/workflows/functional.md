---
title: Microsoft Agent Framework - Functional Workflow API
description: Write workflows as plain Python async functions using the @workflow and @step decorators.
author: moonbox3
ms.topic: tutorial
ms.author: evmattso
ms.date: 04/24/2026
ms.service: agent-framework
zone_pivot_groups: programming-languages
---

::: zone pivot="programming-language-python"

# Functional Workflow API

> [!WARNING]
> The functional workflow API is **experimental** and subject to change or removal in future versions without notice.

The functional workflow API lets you write workflows as plain Python async functions. Instead of defining executor classes, wiring edges, and using `WorkflowBuilder`, you decorate an `async` function with `@workflow` and use native Python control flow — `if`/`else`, `for` loops, `asyncio.gather` — to express your logic.

For a side-by-side comparison with the graph API, see [Workflow APIs](./index.md#workflow-apis) on the Workflows overview.

## `@workflow` decorator

Apply `@workflow` to an `async` function to convert it into a `FunctionalWorkflow` object:

```python
from agent_framework import workflow

@workflow
async def text_pipeline(text: str) -> str:
    upper = await to_upper_case(text)
    return await reverse_text(upper)
```

The `@workflow` decorator supports a parameterized form with optional arguments:

```python
from agent_framework import InMemoryCheckpointStorage, workflow

storage = InMemoryCheckpointStorage()

@workflow(name="my_pipeline", description="Uppercase then reverse", checkpoint_storage=storage)
async def text_pipeline(text: str) -> str:
    ...
```

### `@workflow` parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `name` | `str | None` | Display name for the workflow. Defaults to the function's `__name__`. |
| `description` | `str | None` | Optional human-readable description. |
| `checkpoint_storage` | `CheckpointStorage | None` | Default storage for persisting step results between runs. Can be overridden per call in `run()`. |

### Workflow function signature

The workflow function's **first parameter** receives the input passed to `.run()`. Add a `ctx: RunContext` parameter only when you need HITL, key/value state, or custom events — it is optional otherwise:

```python
# No ctx needed — just a plain pipeline
@workflow
async def simple_pipeline(data: str) -> str:
    result = await process(data)
    return result

# ctx needed for HITL, state, or custom events
@workflow
async def hitl_pipeline(data: str, ctx: RunContext) -> str:
    feedback = await ctx.request_info({"draft": data}, response_type=str)
    return feedback
```

`RunContext` is detected by type annotation first, then by the parameter name `ctx`, so both `ctx: RunContext` and a bare `ctx` parameter work.

## Running a workflow

Call `.run()` on the `FunctionalWorkflow` object returned by `@workflow`:

```python
# Calling the decorated function directly returns the raw return value
raw = await text_pipeline("hello world")   # str — the raw return value

# .run() wraps the result in a WorkflowRunResult with events and state
result = await text_pipeline.run("hello world")
print(result.text)                        # first output as a string
print(result.get_outputs())               # list of terminal outputs
print(result.get_intermediate_outputs())  # list of intermediate outputs
print(result.get_final_state())           # WorkflowRunState.IDLE
```

### `run()` parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `message` | `Any | None` | Input passed to the workflow function as its first argument. |
| `stream` | `bool` | If `True`, returns a `ResponseStream` that yields `WorkflowEvent` objects. Defaults to `False`. |
| `responses` | `dict[str, Any] | None` | HITL responses keyed by `request_id`. Used to resume a suspended workflow. |
| `checkpoint_id` | `str | None` | Checkpoint to restore from. Requires `checkpoint_storage` to be set. |
| `checkpoint_storage` | `CheckpointStorage | None` | Overrides the default storage set on the decorator for this run. |
| `include_status_events` | `bool` | Include status-change events in the non-streaming result. |

Provide one input mode per call: `message`, `responses`, or `checkpoint_id`. The exception is checkpoint resume with external input, where `checkpoint_id` and `responses` can be passed together.

### `WorkflowRunResult`

`run()` (non-streaming) returns a `WorkflowRunResult`. Key methods:

| Method / property | Returns | Description |
|---|---|---|
| `.text` | `str` | First output as a string. Empty string if no string outputs. |
| `.get_outputs()` | `list[Any]` | All terminal outputs emitted by the workflow (events with `type == "output"`). |
| `.get_intermediate_outputs()` | `list[Any]` | All intermediate outputs emitted by the workflow (events with `type == "intermediate"`). |
| `.get_final_state()` | `WorkflowRunState` | Final run state (`IDLE`, `IDLE_WITH_PENDING_REQUESTS`, `FAILED`, …). |
| `.get_request_info_events()` | `list[WorkflowEvent]` | Pending HITL requests when state is `IDLE_WITH_PENDING_REQUESTS`. |

## Streaming

Pass `stream=True` to receive events as they are produced:

```python
from agent_framework import workflow

@workflow
async def data_pipeline(url: str) -> str:
    raw = await fetch_data(url)
    return await transform_data(raw)

# stream=True returns a ResponseStream you iterate with async for
stream = data_pipeline.run("https://example.com/api/data", stream=True)
async for event in stream:
    if event.type == "output":
        print(f"Output: {event.data}")

# After iteration, get_final_response() returns the WorkflowRunResult
result = await stream.get_final_response()
print(f"Final state: {result.get_final_state()}")
```

See [`python/samples/03-workflows/functional/basic_streaming_pipeline.py`](https://github.com/microsoft/agent-framework/tree/main/python/samples/03-workflows/functional/basic_streaming_pipeline.py) for a complete example.

## `@step` decorator

`@step` is an opt-in decorator that adds result caching, event emission, and per-step checkpointing to individual async functions:

```python
from agent_framework import step, workflow

@step
async def fetch_data(url: str) -> dict:
    # expensive — hits a real API
    return await http_get(url)

@workflow
async def pipeline(url: str) -> str:
    raw = await fetch_data(url)
    return process(raw)
```

### What `@step` does inside a workflow

- **Caches results** — the result is stored by `(step_name, call_index)`. On HITL resume or checkpoint restore, a completed step returns its saved result instantly instead of re-executing.
- **Emits events** — `executor_invoked` / `executor_completed` / `executor_failed` are emitted for observability. On a cache hit, `executor_bypassed` is emitted instead.
- **Saves checkpoints** — if the workflow has `checkpoint_storage`, a checkpoint is saved after each step completes.
- **Injects `RunContext`** — if the step function declares a `ctx: RunContext` parameter, the active context is automatically injected.

Outside a running workflow, `@step` is transparent — the function behaves identically to its undecorated version, making it fully testable in isolation.

### When to use `@step`

Use `@step` on functions that are **expensive to re-run**: agent calls, external API requests, or any operation where re-execution on resume would be costly or have side effects. Plain functions (without `@step`) still work inside `@workflow`; they simply re-execute when the workflow resumes.

```python
from agent_framework import InMemoryCheckpointStorage, step, workflow

storage = InMemoryCheckpointStorage()

@step  # cached — won't re-run on resume
async def call_llm(prompt: str) -> str:
    return (await agent.run(prompt)).text

# No @step — cheap, fine to re-run
async def validate(text: str) -> bool:
    return len(text) > 0

@workflow(checkpoint_storage=storage)
async def pipeline(topic: str) -> str:
    draft = await call_llm(f"Write about: {topic}")
    ok = await validate(draft)
    return draft if ok else ""
```

`@step` also accepts a `name` parameter:

```python
@step(name="transform")
async def transform_data(raw: dict) -> str:
    ...
```

See [`python/samples/03-workflows/functional/steps_and_checkpointing.py`](https://github.com/microsoft/agent-framework/tree/main/python/samples/03-workflows/functional/steps_and_checkpointing.py) for a complete example.

## `RunContext`

`RunContext` is the execution context injected into workflow and step functions. You only need it when you use HITL, key/value state, or custom events.

Import it from `agent_framework`:

```python
from agent_framework import RunContext, workflow
```

### `ctx.request_info()` — Human-in-the-loop

`ctx.request_info()` suspends the workflow to wait for external input:

```python
@workflow
async def review_pipeline(topic: str, ctx: RunContext) -> str:
    draft = await write_draft(topic)
    feedback = await ctx.request_info(
        {"draft": draft, "instructions": "Please review this draft"},
        response_type=str,
        request_id="review_request",
    )
    return await revise_draft(draft, feedback)
```

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `request_data` | `Any` | Payload describing what input is needed (dict, Pydantic model, string, …). |
| `response_type` | `type` | Expected Python type of the response. |
| `request_id` | `str | None` | Stable identifier for this request. If omitted, a deterministic `auto::<index>` id is generated from call order. |

**Replay semantics:** On first execution, `request_info()` raises an internal signal (never visible to your code) that suspends the workflow. The caller receives a `WorkflowRunResult` with `get_final_state() == WorkflowRunState.IDLE_WITH_PENDING_REQUESTS`. Resume by calling `.run(responses={request_id: value})` — the workflow re-executes from the top, and `request_info()` returns the provided value immediately.

`@step`-decorated functions that ran before the suspension return their cached results on resume instead of re-executing.

**Handling the response:**

```python
# Phase 1 — run until the workflow pauses
result1 = await review_pipeline.run("AI Safety")
assert result1.get_final_state() == WorkflowRunState.IDLE_WITH_PENDING_REQUESTS

requests = result1.get_request_info_events()
print(requests[0].request_id)  # "review_request"

# Phase 2 — resume with the human's answer
result2 = await review_pipeline.run(
    responses={"review_request": "Add more details about alignment research"}
)
print(result2.text)
```

See [`python/samples/03-workflows/functional/hitl_review.py`](https://github.com/microsoft/agent-framework/tree/main/python/samples/03-workflows/functional/hitl_review.py) for a complete example.

`ctx.request_info()` is also supported inside `@step` functions.

### `ctx.add_event()` — Custom events

Use `ctx.add_event()` to emit application-specific events alongside framework lifecycle events. For full details and examples, see [Emitting custom events](events.md#emitting-custom-events).

### `ctx.get_state()` / `ctx.set_state()` — Key/value state

Use `ctx.get_state()` and `ctx.set_state()` to store values that persist across HITL interruptions and are included in checkpoints. For full details, see [Workflow state](state.md).

State values must be JSON-serializable when checkpoint storage is configured.

### `ctx.is_streaming()`

Returns `True` when the current run was started with `stream=True`. Useful inside step functions that want to adjust their behavior based on streaming mode.

### `get_run_context()`

Retrieves the active `RunContext` from anywhere inside a running workflow — useful in helper functions that don't declare a `ctx` parameter:

```python
from agent_framework import get_run_context

async def helper():
    ctx = get_run_context()
    if ctx is not None:
        ctx.set_state("helper_ran", True)
```

Returns `None` when called outside a running workflow.

## Parallelism with `asyncio.gather`

Use standard Python concurrency for fan-out/fan-in — no framework primitives needed:

```python
import asyncio
from agent_framework import workflow

@workflow
async def research_pipeline(topic: str) -> str:
    web, papers, news = await asyncio.gather(
        research_web(topic),
        research_papers(topic),
        research_news(topic),
    )
    return await synthesize([web, papers, news])
```

`asyncio.gather` also works when the functions are decorated with `@step`.

See [`python/samples/03-workflows/functional/parallel_pipeline.py`](https://github.com/microsoft/agent-framework/tree/main/python/samples/03-workflows/functional/parallel_pipeline.py) for a complete example.

## Calling agents inside workflows

Agent calls work as plain function calls inside `@workflow`:

```python
from agent_framework import Agent, workflow

writer = Agent(name="WriterAgent", instructions="Write a short poem.", client=client)
reviewer = Agent(name="ReviewerAgent", instructions="Review the poem.", client=client)

@workflow
async def poem_workflow(topic: str) -> str:
    poem = (await writer.run(f"Write a poem about: {topic}")).text
    review = (await reviewer.run(f"Review this poem: {poem}")).text
    return f"Poem:\n{poem}\n\nReview: {review}"
```

Add `@step` to agent-calling functions when you want their results cached across HITL resumes or checkpoint restores:

```python
from agent_framework import step

@step
async def write_poem(topic: str) -> str:
    return (await writer.run(f"Write a poem about: {topic}")).text
```

See [`python/samples/03-workflows/functional/agent_integration.py`](https://github.com/microsoft/agent-framework/tree/main/python/samples/03-workflows/functional/agent_integration.py) for a complete example.

## `.as_agent()` — Using a workflow as an agent

Wrap a `FunctionalWorkflow` as an agent-compatible object with `.as_agent()`:

```python
from agent_framework import workflow

@workflow
async def poem_workflow(topic: str) -> str:
    ...

# Wrap as an agent
agent = poem_workflow.as_agent(name="PoemAgent")

# Use with the standard agent interface
response = await agent.run("Write a poem about the ocean")
print(response.text)

# Or use in a larger workflow or orchestration
```

`.as_agent()` returns a `FunctionalWorkflowAgent` that exposes the same `run()` interface as other agent objects, making functional workflows composable with any system that accepts agents.

| Parameter | Type | Description |
|-----------|------|-------------|
| `name` | `str | None` | Display name for the agent. Defaults to the workflow name. |

See [`python/samples/03-workflows/functional/agent_integration.py`](https://github.com/microsoft/agent-framework/tree/main/python/samples/03-workflows/functional/agent_integration.py) for an example.

## Samples

Runnable examples are in the following sample folders:

- [`python/samples/01-get-started/`](https://github.com/microsoft/agent-framework/tree/main/python/samples/01-get-started/) — introductory `@workflow` examples
- [`python/samples/03-workflows/functional/`](https://github.com/microsoft/agent-framework/tree/main/python/samples/03-workflows/functional/) — full-feature functional workflow samples

## Next steps

> [!div class="nextstepaction"]
> [Workflow Builder & Execution](./builder-and-execution.md)

**Related topics:**

- [Executors](./executors.md) — processing units in the graph-based API
- [Human-in-the-loop](../../workflows/human-in-the-loop.md) — HITL in graph-based workflows
- [Checkpoints](../../workflows/checkpoints.md) — checkpoint storage and resume
- [Events](./events.md) — workflow event types
- [Using Workflows as Agents](../../workflows/as-agents.md)

::: zone-end

::: zone pivot="programming-language-csharp"

The functional workflow API is not available for C# at this time.

::: zone-end

::: zone pivot="programming-language-go"

The functional workflow API is not available for Go at this time. Use the graph workflow APIs in [Workflow Builder & Execution](./builder-and-execution.md).

::: zone-end
