# Middleware

## Middleware Exists At Three Different Layers

| Layer | What It Intercepts | Use It For | Do Not Use It For |
| --- | --- | --- | --- |
| Agent run middleware | Whole agent runs and their outputs | audit, input normalization, cross-run policy, response shaping | core business flow that should live in workflows or tools |
| Function-calling middleware | Tool calls inside the agent loop | approvals, argument checks, result filtering, side-effect controls | generic model-call telemetry that belongs lower |
| `IChatClient` middleware | Raw model requests for `ChatClientAgent`-style agents | logging, retries, tracing, transport policy, model-call stamping | hosted-agent paths that do not use `IChatClient` |

The important point is scope. Put the rule at the lowest layer that still sees the thing you need to govern.

## Registration Patterns

Agent middleware is attached through the agent builder:

```csharp
var guardedAgent = originalAgent
    .AsBuilder()
    .Use(runFunc: CustomRunMiddleware, runStreamingFunc: CustomRunStreamingMiddleware)
    .Use(CustomFunctionCallingMiddleware)
    .Build();
```

`IChatClient` middleware is attached to the chat client:

```csharp
var guardedChatClient = chatClient
    .AsBuilder()
    .Use(getResponseFunc: CustomChatClientMiddleware, getStreamingResponseFunc: CustomStreamingChatMiddleware)
    .Build();
```

Then the guarded client is wrapped in `ChatClientAgent`.

## Layer Selection Rules

Use agent run middleware when the policy cares about:

- inbound messages
- thread use
- high-level run options
- the final aggregated response

Use function middleware when the policy cares about:

- which tool is being invoked
- which arguments are being sent
- whether the tool call should be blocked or approved
- how the raw tool result is normalized

Use `IChatClient` middleware when the policy cares about:

- model request and response telemetry
- transport, retries, and headers
- prompt stamping or correlation IDs
- low-level model call behavior

## Streaming Caveats

The official docs call out an easy footgun:

- if you provide only non-streaming agent middleware, streaming runs can be forced through non-streaming execution
- that changes the runtime behavior and can hide streaming-specific issues

So the default rule is:

- provide both `runFunc` and `runStreamingFunc`
- or use the shared overload only for pre-run inspection that does not need to rewrite output

## Function Middleware Is The Right Place For Tool Governance

Function-calling middleware should own:

- approval checks
- argument validation
- allow/deny policy
- result filtering
- logging of side effects

This is where you stop dangerous calls before they execute, rather than trying to clean up the consequences after the agent already used the result.

### Approval Pattern

If the backend does not provide first-class approval semantics, implement approval with:

1. function middleware that detects risky tools
2. workflow request and response if human approval is a real state transition
3. explicit denial or placeholder result when approval is absent

Use workflow request/response for approval when:

- the process must pause and wait
- the approval itself needs auditability
- the approval result affects future execution branches

## `Terminate` Is Dangerous

The docs explicitly warn that terminating the function loop can leave the thread inconsistent.

Use `FunctionInvocationContext.Terminate = true` only when:

- you understand exactly how the current loop iteration will end
- you do not leave function-call content without matching result content
- you have tests proving the thread can still be reused safely

If the goal is human approval or escalation, request/response workflows are usually safer than hard loop termination.

## Practical Middleware Compositions

### Safe baseline for `ChatClientAgent`

1. `IChatClient` middleware for tracing, retries, and correlation IDs.
2. Agent run middleware for input normalization and high-level audit.
3. Function middleware for tool approval and result filtering.

### Enterprise baseline

1. request-scoped correlation and telemetry
2. PII or sensitive-data checks before model calls
3. risky-tool approval middleware
4. response filtering before external emission
5. OpenTelemetry spans around the whole run

## Anti-Patterns

- Putting domain business logic in middleware because it is "easy to inject".
- Mutating every message on the way through without documenting the contract.
- Assuming `IChatClient` middleware covers hosted-agent services that bypass `IChatClient`.
- Using middleware to fake workflow state transitions.
- Terminating function loops without understanding thread consistency.

## Testing Checklist

- Non-streaming and streaming both execute through the intended middleware paths.
- Risky tools are blocked or paused exactly once.
- Middleware ordering is explicit and documented.
- Chat-client middleware does not leak transport-specific assumptions into provider-agnostic logic.
- Tool result filtering is deterministic and observable.

## Source Pages

- `references/official-docs/user-guide/agents/agent-middleware.md`
- `references/official-docs/tutorials/agents/middleware.md`
- `references/official-docs/tutorials/agents/function-tools-approvals.md`
