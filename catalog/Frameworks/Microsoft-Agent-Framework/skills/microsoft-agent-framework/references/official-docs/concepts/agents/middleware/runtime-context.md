---
title: "Runtime Context"
description: "Learn how to use runtime context in middleware."
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: reference
ms.author: edvan
ms.date: 05/27/2026
ms.service: agent-framework
---


# Runtime Context

Runtime context provides middleware with access to information about the current execution environment and request. This enables patterns such as per-session configuration, user-specific behavior, and dynamic middleware behavior based on runtime conditions.

:::zone pivot="programming-language-csharp"

In C#, runtime context flows through three main surfaces:

- `AgentRunOptions.AdditionalProperties` for per-run key-value metadata that middleware and tools can read.
- `FunctionInvocationContext` for inspecting and modifying tool call arguments inside function invocation middleware.
- `AgentSession.StateBag` for shared state that persists across runs within a conversation.

Use the narrowest surface that fits. Per-run metadata belongs in `AdditionalProperties`, persistent conversation state belongs in the session's `StateBag`, and tool-argument manipulation belongs in function invocation middleware.

> [!TIP]
> See the [Agent vs Run Scope](./agent-vs-run-scope.md) page for information on how middleware scope affects access to runtime context.

### Choose the right runtime surface

| Use case | API surface | Accessed from |
|---|---|---|
| Share conversation state or data across runs | `AgentSession.StateBag` | `session.StateBag` in run middleware, `AIAgent.CurrentRunContext?.Session` in tools |
| Pass per-run metadata to middleware or tools | `AgentRunOptions.AdditionalProperties` | `options.AdditionalProperties` in run middleware, `AIAgent.CurrentRunContext?.RunOptions` in tools |
| Inspect or modify tool call arguments in middleware | `FunctionInvocationContext` | Function invocation middleware callback |

### Pass per-run values via `AgentRunOptions`

Use `AdditionalProperties` on `AgentRunOptions` to attach per-run key-value data. Function invocation middleware can forward these values into tool arguments.

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

[Description("Send an email to the specified address.")]
static string SendEmail(
    [Description("Recipient email address.")] string address,
    [Description("User ID of the sender.")] string userId,
    [Description("Tenant name.")] string tenant = "default")
{
    return $"Queued email for {address} from {userId} ({tenant})";
}

// Function invocation middleware that injects per-run values into tool arguments
async ValueTask<object?> InjectRunContext(
    AIAgent agent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
{
    var runOptions = AIAgent.CurrentRunContext?.RunOptions;
    if (runOptions?.AdditionalProperties is { } props)
    {
        if (props.TryGetValue("user_id", out var userId))
        {
            context.Arguments["userId"] = userId;
        }

        if (props.TryGetValue("tenant", out var tenant))
        {
            context.Arguments["tenant"] = tenant;
        }
    }

    return await next(context, cancellationToken);
}

AIAgent baseAgent = new AIProjectClient(
    new Uri("<your-foundry-project-endpoint>"),
    new DefaultAzureCredential())
        .AsAIAgent(
            model: "gpt-4o-mini",
            instructions: "Send email updates.",
            tools: [AIFunctionFactory.Create(SendEmail)]);

var agent = baseAgent
    .AsBuilder()
        .Use(InjectRunContext)
    .Build();

var response = await agent.RunAsync(
    "Email the launch update to finance@example.com",
    options: new AgentRunOptions
    {
        AdditionalProperties = new AdditionalPropertiesDictionary
        {
            ["user_id"] = "user-123",
            ["tenant"] = "contoso",
        }
    });

Console.WriteLine(response);
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

The middleware reads per-run valuesfrom `AgentRunOptions.AdditionalProperties` via the ambient `AIAgent.CurrentRunContext` and injects them into the tool's `FunctionInvocationContext.Arguments` before the tool executes.

### Function invocation middleware receives context

Function invocation middleware uses `FunctionInvocationContext` to inspect or modify tool arguments, intercept results, or skip tool execution entirely.

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

async ValueTask<object?> EnrichToolContext(
    AIAgent agent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
{
    if (!context.Arguments.ContainsKey("tenant"))
    {
        context.Arguments["tenant"] = "contoso";
    }

    if (!context.Arguments.ContainsKey("requestSource"))
    {
        context.Arguments["requestSource"] = "middleware";
    }

    return await next(context, cancellationToken);
}

AIAgent baseAgent = new AIProjectClient(
    new Uri("<your-foundry-project-endpoint>"),
    new DefaultAzureCredential())
        .AsAIAgent(
            model: "gpt-4o-mini",
            instructions: "Send email updates.",
            tools: [AIFunctionFactory.Create(SendEmail)]);

var agent = baseAgent
    .AsBuilder()
        .Use(EnrichToolContext)
    .Build();
```

The middleware receives the function invocation context and calls `next` to continue the pipeline. Mutate `context.Arguments` before calling `next`, and the tool sees the updated values.

### Use `AgentSession.StateBag` for shared runtime state

```csharp
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

[Description("Store the specified topic in session state.")]
static string RememberTopic(
    [Description("Topic to remember.")] string topic)
{
    var session = AIAgent.CurrentRunContext?.Session;
    if (session is null)
    {
        return "No session available.";
    }

    session.StateBag.SetValue("topic", topic);
    return $"Stored '{topic}' in session state.";
}

AIAgent agent = new AIProjectClient(
    new Uri("<your-foundry-project-endpoint>"),
    new DefaultAzureCredential())
        .AsAIAgent(
            model: "gpt-4o-mini",
            instructions: "Remember important topics.",
            tools: [AIFunctionFactory.Create(RememberTopic)]);

var session = await agent.CreateSessionAsync();
await agent.RunAsync("Remember that the budget review is on Friday.", session: session);
Console.WriteLine(session.StateBag.GetValue<string>("topic"));
```

Pass the session explicitly with `session:` and access it from tools via `AIAgent.CurrentRunContext?.Session`. The `StateBag` provides type-safe, thread-safe storage that persists across runs within the same session.

### Share session state across middleware and tools

Run middleware can read and write the session's `StateBag`, and any changes are visible to function invocation middleware and tools executing in the same request.

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

// Run middleware that stamps the session with request metadata
async Task<AgentResponse> StampRequestMetadata(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
{
    if (session is not null && options?.AdditionalProperties is { } props)
    {
        if (props.TryGetValue("request_id", out var requestId))
        {
            session.StateBag.SetValue("requestId", requestId?.ToString());
        }
    }

    return await innerAgent.RunAsync(messages, session, options, cancellationToken);
}

AIAgent baseAgent = new AIProjectClient(
    new Uri("<your-foundry-project-endpoint>"),
    new DefaultAzureCredential())
        .AsAIAgent(
            model: "gpt-4o-mini",
            instructions: "You are a helpful assistant.");

var agent = baseAgent
    .AsBuilder()
        .Use(runFunc: StampRequestMetadata, runStreamingFunc: null)
    .Build();

var session = await agent.CreateSessionAsync();
await agent.RunAsync(
    "Hello!",
    session: session,
    options: new AgentRunOptions
    {
        AdditionalProperties = new AdditionalPropertiesDictionary
        {
            ["request_id"] = "req-abc-123",
        }
    });

Console.WriteLine(session.StateBag.GetValue<string>("requestId"));
```

Run middleware receives the session directly as a parameter. Use `StateBag.SetValue` and `GetValue` for type-safe access. Any values stored during the run middleware phase are available to tools and function invocation middleware via `AIAgent.CurrentRunContext?.Session`.

:::zone-end

:::zone pivot="programming-language-python"

Python runtime context is split across three public surfaces:

- `session=` for conversation state and history.
- `function_invocation_kwargs=` for values that only tools or function middleware should see.
- `client_kwargs=` for chat-client-specific data or client middleware configuration.

Use the smallest surface that fits the data. This keeps tool inputs explicit and avoids leaking client-only metadata into tool execution.

> [!TIP]
> Treat `function_invocation_kwargs` as the replacement for the old pattern of passing arbitrary public `**kwargs` to `agent.run()` or `get_response()`.

### Choose the right runtime bucket

| Use case | API surface | Accessed from |
|---|---|---|
| Share conversation state, service session IDs, or history | `session=` | `ctx.session`, `AgentContext.session` |
| Pass runtime values only tools or function middleware need | `function_invocation_kwargs=` | `FunctionInvocationContext.kwargs` |
| Pass client-specific runtime values or client middleware configuration | `client_kwargs=` | custom `get_response(..., client_kwargs=...)` implementations |

### Pass tool-only runtime values

```python
from typing import Annotated

from agent_framework import FunctionInvocationContext, tool
from agent_framework.openai import OpenAIChatClient


@tool(approval_mode="never_require")
def send_email(
    address: Annotated[str, "Recipient email address."],
    ctx: FunctionInvocationContext,
) -> str:
    user_id = ctx.kwargs["user_id"]
    tenant = ctx.kwargs.get("tenant", "default")
    return f"Queued email for {address} from {user_id} ({tenant})"


agent = OpenAIChatClient().as_agent(
    name="Notifier",
    instructions="Send email updates.",
    tools=[send_email],
)

response = await agent.run(
    "Email the launch update to finance@example.com",
    function_invocation_kwargs={
        "user_id": "user-123",
        "tenant": "contoso",
    },
)

print(response.text)
```

Use `ctx.kwargs` inside the tool instead of declaring blanket `**kwargs` on the tool callable. Unexpected runtime keyword arguments are rejected; new tools should consume runtime data through `FunctionInvocationContext`.

Any parameter annotated as `FunctionInvocationContext` is treated as the injected runtime context parameter, regardless of its name, and it is not exposed in the JSON schema shown to the model. If you provide an explicit schema/input model, a plain unannotated parameter named `ctx` is also recognized as the injected context parameter.

If the value is long-lived tool state or a dependency rather than per-invocation data, keep it on a tool class instance instead of passing it through `function_invocation_kwargs`. For that pattern, see [Create a class with multiple function tools](../../../agents/tools/function-tools.md#create-a-class-with-multiple-function-tools).

### Function middleware receives the same context

Function middleware uses the same `FunctionInvocationContext` object that tools receive. That means middleware can inspect `context.arguments`, `context.kwargs`, `context.session`, and `context.result`.

```python
from collections.abc import Awaitable, Callable

from agent_framework import FunctionInvocationContext
from agent_framework.openai import OpenAIChatClient


async def enrich_tool_runtime_context(
    context: FunctionInvocationContext,
    call_next: Callable[[], Awaitable[None]],
) -> None:
    context.kwargs.setdefault("tenant", "contoso")
    context.kwargs.setdefault("request_source", "middleware")
    await call_next()


agent = OpenAIChatClient().as_agent(
    name="Notifier",
    instructions="Send email updates.",
    tools=[send_email],
    middleware=[enrich_tool_runtime_context],
)
```

The middleware contract uses `call_next()` with no arguments. Mutate `context.kwargs` before calling it, and the selected tool sees those values through its injected `FunctionInvocationContext`.

### Use `session=` for shared runtime state

```python
from typing import Annotated

from agent_framework import FunctionInvocationContext, tool
from agent_framework.openai import OpenAIChatClient


@tool(approval_mode="never_require")
def remember_topic(
    topic: Annotated[str, "Topic to remember."],
    ctx: FunctionInvocationContext,
) -> str:
    if ctx.session is None:
        return "No session available."

    ctx.session.state["topic"] = topic
    return f"Stored {topic!r} in session state."


agent = OpenAIChatClient().as_agent(
    name="MemoryAgent",
    instructions="Remember important topics.",
    tools=[remember_topic],
)

session = agent.create_session()
await agent.run("Remember that the budget review is on Friday.", session=session)
print(session.state["topic"])
```

Pass the session explicitly with `session=` and read it from `ctx.session`. Session access no longer needs to travel through runtime kwargs.

### Share session state with delegated agents

When an agent is exposed as a tool via `as_tool()`, runtime function kwargs already flow through `ctx.kwargs`. Add `propagate_session=True` only when the sub-agent should share the caller's `AgentSession`.

```python
from agent_framework import FunctionInvocationContext, tool
from agent_framework.openai import OpenAIChatClient


@tool(description="Store findings for later steps.")
def store_findings(findings: str, ctx: FunctionInvocationContext) -> None:
    if ctx.session is not None:
        ctx.session.state["findings"] = findings


client = OpenAIChatClient()

research_agent = client.as_agent(
    name="ResearchAgent",
    instructions="Research the topic and store findings.",
    tools=[store_findings],
)

research_tool = research_agent.as_tool(
    name="research",
    description="Research a topic and store findings.",
    arg_name="query",
    propagate_session=True,
)
```

With `propagate_session=True`, the delegated agent sees the same `ctx.session` state as the caller. Leave it `False` to isolate the child agent in its own session.

### Custom chat clients and agents

If you implement custom public `run()` or `get_response()` methods, add the explicit runtime buckets to the signature.

```python
from collections.abc import Mapping, Sequence
from typing import Any

from agent_framework import ChatOptions, Message


async def get_response(
    self,
    messages: Sequence[Message],
    *,
    options: ChatOptions[Any] | None = None,
    function_invocation_kwargs: Mapping[str, Any] | None = None,
    client_kwargs: Mapping[str, Any] | None = None,
    **kwargs: Any,
):
    ...
```

Use `function_invocation_kwargs` for tool-invocation flows and `client_kwargs` for client-specific behavior. Passing client-specific values directly through public `**kwargs` is only a compatibility path and should be treated as deprecated. Likewise, defining new tools with `**kwargs` is migration-only compatibility — consume runtime data through the injected context object instead.

:::zone-end

:::zone pivot="programming-language-go"

Go passes runtime context through `context.Context` and typed `agent.Option` values. Middleware can inspect options with `agent.GetOption` and add per-run options before calling `next`.

```go
runtimeContext := agent.MiddlewareFunc(func(next agent.RunFunc, ctx context.Context, messages []*message.Message, options ...agent.Option) iter.Seq2[*agent.ResponseUpdate, error] {
    session, _ := agent.GetOption(options, agent.WithSession)
    if session != nil {
        options = append(options, agent.WithInstructions("Use the active session context."))
    }
    return next(ctx, messages, options...)
})

session, err := a.CreateSession(ctx)
resp, err := a.RunText(ctx, "Hello", agent.WithSession(session)).Collect()
```

:::zone-end
## Next steps

> [!div class="nextstepaction"]
> [Providers](../../../integrations/by-component/model-providers/index.md)
