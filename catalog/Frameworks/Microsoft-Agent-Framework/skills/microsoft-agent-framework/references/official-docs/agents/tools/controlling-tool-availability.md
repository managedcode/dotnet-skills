---
title: Controlling tool availability
description: How to progressively expose tools, gate tool calls, and enforce ordering within an agent run (Python).
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 06/23/2026
ms.service: agent-framework
---

# Controlling tool availability

::: zone pivot="programming-language-csharp"

> [!NOTE]
> The progressive tool exposure API (`FunctionInvocationContext.add_tools` / `remove_tools`) is currently Python-only.

::: zone-end

::: zone pivot="programming-language-python"

This page covers three complementary techniques for controlling which tools a model can call and in what order, all within a single agent run, without requiring a workflow:

- **Progressive tool exposure** — add or remove tools at runtime from inside a tool or function middleware, so the model only sees tools it is ready to use.
- **Middleware gating** — use function middleware to validate call arguments and return corrective feedback without executing the underlying function.
- **Forced first call** — use `tool_choice` to require the model to call a specific tool before any others.

> [!NOTE]
> Pairwise ordering constraints such as "always call `get_record` before `update_record`" do not require a workflow. The techniques on this page handle that pattern inside a single run. Workflows are for genuine multi-step orchestration across runs or parallel branches.

## Progressive tool exposure

Progressive tool exposure lets you start a run with a small set of tools and add or remove tools in response to earlier tool results, all within the same run. The model only sees the updated set on the **next iteration** of the function-calling loop; tool calls already requested in the in-flight batch still execute before the change takes effect.

The API is experimental and lives on `FunctionInvocationContext`:

| Member | Description |
|--------|-------------|
| `ctx.tools` | The live, mutable `list` of tools for the current run. `None` when the function is invoked outside a function-calling loop. |
| `ctx.add_tools(tools)` | Add one or more tools. Callables are wrapped as `FunctionTool`. Re-adding the same object is a no-op; a different object with a duplicate name raises `ValueError`. All-or-nothing: if any tool in the batch would raise, none are added. |
| `ctx.remove_tools(tools)` | Remove by name, tool object, or callable. Names not present in the list are silently ignored. |

Both helpers emit `ExperimentalWarning` the first time they are called in a process (feature id `PROGRESSIVE_TOOLS`). Calling either helper outside a function-calling loop raises `RuntimeError`.

> [!IMPORTANT]
> The tool list resets to the original set on every new `agent.run()` call, so all gates re-arm automatically for each turn.

> [!NOTE]
> Progressive tool exposure applies to the standard function-calling loop only. It is not available for CodeAct providers (`agent-framework-monty`, `agent-framework-hyperlight`), where the model sees a single code-execution surface rather than individual tool schemas. Calling `add_tools` or `remove_tools` from inside a CodeAct sandbox raises `RuntimeError`. To change the tool set for a CodeAct agent, use the provider's own `add_tools` / `remove_tool` / `clear_tools` methods between runs.

### Loader-tool pattern

Register a small set of "loader" tools up front and let the model pull in additional tools on demand. This keeps the initial schema small, which improves tool-selection accuracy and reduces cost.

```python
import asyncio
import warnings
from typing import Annotated

from agent_framework import Agent, FunctionInvocationContext, tool
from agent_framework.openai import OpenAIChatClient
from pydantic import Field

warnings.filterwarnings("ignore", category=FutureWarning)  # suppress ExperimentalWarning for brevity


@tool(approval_mode="never_require")
def factorial(n: Annotated[int, Field(description="A non-negative integer.")]) -> str:
    """Compute the factorial of n."""
    if n < 0:
        return "Error: n must be a non-negative integer."
    result = 1
    for value in range(2, n + 1):
        result *= value
    return f"{n}! = {result}"


@tool(approval_mode="never_require")
def fibonacci(n: Annotated[int, Field(description="The 0-based index in the Fibonacci sequence.")]) -> str:
    """Compute the n-th Fibonacci number."""
    if n < 0:
        return "Error: n must be a non-negative integer."
    a, b = 0, 1
    for _ in range(n):
        a, b = b, a + b
    return f"fib({n}) = {a}"


# The ctx parameter is injected by the framework and is NOT visible to the model.
@tool(approval_mode="never_require")
def load_math_tools(ctx: FunctionInvocationContext) -> str:
    """Load additional math tools (factorial, fibonacci) so they can be used."""
    ctx.add_tools([factorial, fibonacci])
    return "Loaded math tools: factorial, fibonacci. You can now call them."


async def main() -> None:
    agent = Agent(
        client=OpenAIChatClient(),
        name="MathAgent",
        instructions=(
            "You are a math assistant. "
            "If you need math capabilities that are not yet available, call load_math_tools first."
        ),
        tools=[load_math_tools],  # agent starts with only the loader
    )
    print(await agent.run("What is 5 factorial?"))


asyncio.run(main())
```

The full runnable sample is at [`python/samples/02-agents/tools/dynamic_tool_exposure.py`](https://github.com/microsoft/agent-framework/blob/main/python/samples/02-agents/tools/dynamic_tool_exposure.py).

### Gating pattern

Register only the read tool initially. The read tool adds the write tool after a successful fetch, so the model cannot call the write tool before the read tool has run.

```python
from agent_framework import Agent, FunctionInvocationContext, tool
from agent_framework.openai import OpenAIChatClient

_last_fetched_id: str | None = None


@tool(approval_mode="never_require")
def get_record(record_id: str, ctx: FunctionInvocationContext) -> str:
    """Fetch a record. Unlocks update_record for the same record."""
    global _last_fetched_id
    _last_fetched_id = record_id
    ctx.add_tools(update_record)  # gate: expose the write tool now
    return f"Record {record_id}: title='Example record', status='open'"


@tool(approval_mode="never_require")
def update_record(record_id: str, status: str) -> str:
    """Update the status of a record."""
    return f"Updated record {record_id} to status '{status}'."


agent = Agent(
    client=OpenAIChatClient(),
    name="RecordAgent",
    instructions="You help manage records. Fetch a record before updating it.",
    tools=[get_record],  # update_record is hidden until get_record runs
)
```

Because `ctx.tools` resets to `[get_record]` at the start of every run, the gate re-arms automatically for each conversation turn.

## Middleware gating

Function middleware can inspect the arguments of a pending tool call and reject it before the underlying function executes by setting `context.result` without calling `call_next()`. The string assigned to `context.result` is returned to the model as the function result, giving it corrective feedback.

This is useful for argument-level checks that need information not available at schema-definition time, for example verifying that an update targets the same item that was fetched earlier in the run.

```python
from collections.abc import Awaitable, Callable

from agent_framework import FunctionInvocationContext

_last_fetched_id: str | None = None


async def enforce_read_before_write(
    context: FunctionInvocationContext,
    call_next: Callable[[], Awaitable[None]],
) -> None:
    """Reject update_record calls that target a different record than the one fetched."""
    if context.function.name == "update_record":
        requested_id = context.arguments.get("record_id") if hasattr(context.arguments, "get") else None
        if requested_id != _last_fetched_id:
            # Set result without calling call_next — the function never executes.
            context.result = (
                f"Error: you must fetch record '{requested_id}' before updating it. "
                f"Last fetched record was '{_last_fetched_id}'."
            )
            return
    await call_next()
```

Add the middleware to the agent:

```python
agent = Agent(
    client=OpenAIChatClient(),
    name="RecordAgent",
    instructions="Fetch a record before updating it.",
    tools=[get_record, update_record],
    middleware=[enforce_read_before_write],
)
```

For more on function middleware, see [Defining Middleware](../../concepts/agents/middleware/defining-middleware.md) and [Result Overrides](../../concepts/agents/middleware/result-overrides.md).

## Forcing a tool call with `tool_choice`

To require the model to call a specific tool as its first action, pass `tool_choice` with mode `"required"` and a `required_function_name`. The framework automatically resets `tool_choice` to `None` after the first iteration so the model is free on subsequent iterations.

```python
result = await agent.run(
    "Update record REC-42 to status 'in-progress'.",
    options={"tool_choice": {"mode": "required", "required_function_name": "get_record"}},
)
```

The `tool_choice` field accepts a `ToolMode` dict, or the shorthand strings `"auto"`, `"required"`, or `"none"`:

```python
from agent_framework import ToolMode

tool_choice: ToolMode = {"mode": "required", "required_function_name": "get_record"}
```

## Semantics and caveats

| Behavior | Detail |
|----------|--------|
| **Next-iteration effect** | `add_tools` / `remove_tools` mutations are visible to the model on the next loop iteration. Tool calls already dispatched in the current batch complete regardless. |
| **In-flight batch** | If the model requests several tools in one batch, all execute before the updated tool list is sent back. |
| **Duplicate names** | Re-adding the exact same object is a no-op. Adding a different object whose name matches an existing tool raises `ValueError`. The entire batch is validated before any addition, so a duplicate midway through a list leaves the live list unchanged. |
| **Outside-loop error** | Calling `add_tools` or `remove_tools` when `ctx.tools is None` raises `RuntimeError`. This happens when the function is invoked directly (for example via `FunctionTool.invoke`) rather than through the agent loop. |
| **Experimental status** | Both helpers emit `ExperimentalWarning` on first call per process. Suppress with `warnings.filterwarnings("ignore", category=FutureWarning)` if desired. |
| **Per-run scope** | The live tool list is a fresh copy created from `normalize_tools` at the start of each `agent.run()` call. The caller's original `tools` container is never mutated. |
| **CodeAct exclusion** | Not available for `agent-framework-monty` or `agent-framework-hyperlight` CodeAct providers. |

::: zone-end

::: zone pivot="programming-language-go"

> [!NOTE]
> The runtime tool availability APIs covered on this page are currently Python-only. For Go tool patterns, see [Function Tools](./function-tools.md) and [Using function tools with human in the loop approvals](./tool-approval.md).

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Function Tools](./function-tools.md)

> [!div class="nextstepaction"]
> [Defining Middleware](../../concepts/agents/middleware/defining-middleware.md)
