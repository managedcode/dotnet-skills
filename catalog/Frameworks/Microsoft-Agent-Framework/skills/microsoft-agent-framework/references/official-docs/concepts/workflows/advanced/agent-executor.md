---
title: Agent Executor
description: Deep dive into the AgentExecutor, the built-in executor that adapts AI agents for use in workflows.
zone_pivot_groups: programming-languages
author: TaoChenOSU
ms.topic: article
ms.author: taochen
ms.date: 07/01/2026
ms.service: agent-framework
---

<!--
  Language parity table – keep in sync when adding/removing sections.

    | Section                        | C# | Python | Go | Notes                                     |
    |--------------------------------|:--:|:------:|:--:|-------------------------------------------|
    | Overview                       | ✅ |   ✅   | ✅ |                                           |
    | How It Works                   | ✅ |   ✅   | ✅ |                                           |
    | Implicit vs Explicit Creation  | ✅ |   ✅   | ❌ | Go documents explicit `agentworkflow.New` |
    | Input Types                    | ✅ |   ✅   | ✅ | Different supported types per language     |
    | Output and Chaining            | ✅ |   ✅   | ✅ |                                           |
    | Streaming Behavior             | ✅ |   ✅   | ✅ |                                           |
    | Shared Sessions                | ✅ |   ✅   | ❌ |                                           |
    | Configuration Options          | ✅ |   ❌   | ✅ | C# uses AIAgentHostOptions; Go uses agentworkflow.Config |
    | Context Modes                  | ❌ |   ✅   | ❌ | Python-only                                |
    | Checkpointing                  | ✅ |   ✅   | ✅ |                                           |
-->

# Agent Executor

When you add an AI agent to a workflow, it needs to be wrapped in an executor so the workflow engine can route messages to it, manage its session state, and handle its output. The **Agent Executor** is the built-in executor that handles this adaptation.

## Overview

The Agent Executor bridges the gap between the agent abstraction and the workflow execution model. It:

- Receives typed messages from the workflow graph and forwards them to the underlying agent.
- Manages the agent's session and conversation state between runs.
- Adapts its behavior based on the workflow execution mode (streaming or non-streaming).
- Yields output events (`AgentResponse` or `AgentResponseUpdate`) to the workflow caller for observation.
- Sends messages to connected downstream executors for continued processing within the graph.
- Supports checkpointing for long-running workflows.

::: zone pivot="programming-language-csharp"

## How It Works

In C#, the workflow engine internally creates an `AIAgentHostExecutor` for each `AIAgent` added to a workflow. This specialized executor extends `ChatProtocolExecutor` and uses a **turn token** pattern:

1. **Message caching** — as messages arrive from other executors, the agent executor collects them. If `ForwardIncomingMessages` is enabled (the default), the incoming messages are also forwarded to downstream executors.
2. **Turn token trigger** — the agent processes its cached messages only after receiving a `TurnToken`.
3. **Agent invocation** — the executor calls `RunAsync` (non-streaming) or `RunStreamingAsync` (streaming) on the underlying agent.
4. **Output yielding** — if streaming events are enabled, each incremental `AgentResponseUpdate` is yielded as a workflow output. If `EmitAgentResponseEvents` is enabled, the aggregated `AgentResponse` is also yielded as a workflow output.
5. **Downstream messaging** — the agent's response messages are sent to connected downstream executors.
6. **Turn token pass-through** — after completing its turn, the executor sends a new `TurnToken` downstream so that the next agent in the chain can begin processing.

> [!TIP]
> Some scenarios may require a more specialized agent executor; for example, [handoff orchestrations](../../../workflows/orchestrations/handoff.md) use a dedicated `HandoffAgentExecutor` with custom routing logic.

## Implicit vs Explicit Creation

When you pass an `AIAgent` to `WorkflowBuilder`, the framework automatically wraps it in an `AIAgentBinding`, which creates the underlying `AIAgentHostExecutor`. You do not need to instantiate the agent executor directly.

```csharp
AIAgent writerAgent = /* create your agent */;
AIAgent reviewerAgent = /* create your agent */;

// Agents are automatically wrapped — no manual executor creation required
var workflow = new WorkflowBuilder(writerAgent)
    .AddEdge(writerAgent, reviewerAgent)
    .Build();
```

You can also use the helper methods on `AgentWorkflowBuilder` for common patterns:

```csharp
// Build a sequential pipeline of agents
var workflow = AgentWorkflowBuilder.BuildSequential(writerAgent, reviewerAgent);
```

### Custom Configuration

To customize how the agent executor behaves, use `BindAsExecutor` with `AIAgentHostOptions`:

```csharp
var options = new AIAgentHostOptions
{
    EmitAgentUpdateEvents = true,
    EmitAgentResponseEvents = true,
    ReassignOtherAgentsAsUsers = true,
    ForwardIncomingMessages = true,
};

ExecutorBinding writerBinding = writerAgent.BindAsExecutor(options);
var workflow = new WorkflowBuilder(writerBinding)
    .AddEdge(writerBinding, reviewerAgent)
    .Build();
```

## Input Types

The agent executor in C# accepts multiple input types: `string`, `ChatMessage`, and `IEnumerable<ChatMessage>`. String inputs are automatically converted to `ChatMessage` instances with the `User` role. All incoming messages are accumulated until a `TurnToken` is received, at which point the executor processes the batch. When `ReassignOtherAgentsAsUsers` is enabled (the default), messages from other agents are reassigned to the `User` role so the underlying model treats them as user inputs, while messages from the current agent retain the `Assistant` role.

## Output and Chaining

After the agent completes its turn, the executor:

1. Sends the agent's response messages to all connected downstream executors.
2. Forwards a new `TurnToken` so the next agent in the chain can begin processing.

This makes chaining agents straightforward — simply connect them with edges:

```csharp
var workflow = new WorkflowBuilder(frenchTranslator)
    .AddEdge(frenchTranslator, spanishTranslator)
    .AddEdge(spanishTranslator, englishTranslator)
    .Build();
```

## Streaming Behavior

Streaming behavior is controlled by the `EmitAgentUpdateEvents` option on `AIAgentHostOptions`, or dynamically via the `TurnToken`:

- **When enabled** — the executor calls `RunStreamingAsync` on the agent and yields each `AgentResponseUpdate` as a workflow output event. This provides real-time token-by-token updates.
- **When disabled** — the executor calls `RunAsync` and produces a single complete response.

```csharp
// Enable streaming events at the configuration level
var options = new AIAgentHostOptions
{
    EmitAgentUpdateEvents = true,
};

// Or enable streaming dynamically via TurnToken
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
```

## Shared Sessions

Each agent executor maintains its own session by default. To share a session between agents, configure the agents with a common session provider before adding them to the workflow.

## Configuration Options

`AIAgentHostOptions` controls the agent executor's behavior:

| Option | Default | Description |
|--------|---------|-------------|
| `EmitAgentUpdateEvents` | `null` | Emit streaming update events during execution. `TurnToken` takes precedence if set. If both are `null`, streaming is disabled. |
| `EmitAgentResponseEvents` | `false` | Emit the aggregated agent response as a workflow output event. |
| `InterceptUserInputRequests` | `false` | Intercept `UserInputRequestContent` and route it as a workflow message for handling. |
| `InterceptUnterminatedFunctionCalls` | `false` | Intercept `FunctionCallContent` without a corresponding result and route it as a workflow message. |
| `ReassignOtherAgentsAsUsers` | `true` | Reassign messages from other agents to the `User` role so the model treats them as user inputs. |
| `ForwardIncomingMessages` | `true` | Forward incoming messages to downstream executors before the agent's generated messages. |

## Checkpointing

The agent executor supports checkpointing for long-running workflows. When a checkpoint is taken, the executor serializes:

- The agent's session state (via `SerializeSessionAsync`).
- The current turn's event emission configuration (only present while requests are pending and the executor has not yet yielded its incoming `TurnToken`).
- Any pending user input requests and function call requests.

On restore, the executor deserializes the session and pending request state, allowing the workflow to resume from where it left off.

::: zone-end

::: zone pivot="programming-language-python"

## How It Works

The `AgentExecutor` class wraps an agent that implements the `SupportsAgentRun` protocol. When the executor receives a message:

1. **Message normalization** — the input is normalized into a list of `Message` objects and added to the executor's internal cache. The executor accepts multiple input types — `str`, `Message`, `list[str | Message]`, `AgentExecutorRequest`, and `AgentExecutorResponse` — each routed to a dedicated handler that normalizes the input before caching.
2. **Agent invocation** — the executor calls `agent.run()` with the cached messages, automatically selecting streaming or non-streaming mode based on the workflow execution mode.
3. **Output emission** — in streaming mode, each `AgentResponseUpdate` is yielded as a workflow output event. In non-streaming mode, a single `AgentResponse` is yielded.
4. **Downstream dispatch** — after the agent completes, the executor sends an `AgentExecutorResponse` to all connected downstream executors. This response includes the full conversation history, enabling seamless chaining.
5. **Cache reset** — the executor's internal message cache is cleared after the agent is invoked, ensuring that each agent invocation processes only new messages received since the last invocation.

> [!TIP]
> Some scenarios may require a more specialized agent executor; for example, [handoff orchestrations](../../../workflows/orchestrations/handoff.md) use a dedicated executor with custom routing logic.

## Implicit vs Explicit Creation

The `WorkflowBuilder` automatically wraps agents in `AgentExecutor` instances when you pass an agent directly. For most workflows, implicit creation is sufficient:

```python
from agent_framework import WorkflowBuilder

writer_agent = client.as_agent(name="Writer", instructions="...")
reviewer_agent = client.as_agent(name="Reviewer", instructions="...")

# Agents are automatically wrapped — no manual AgentExecutor creation required
workflow = (
    WorkflowBuilder(start_executor=writer_agent)
    .add_edge(writer_agent, reviewer_agent)
    .build()
)
```

### Explicit Creation

Create an `AgentExecutor` explicitly when you need to:

- Share a session between multiple agents.
- Provide a custom executor ID for routing and targeted runtime kwargs.
- Reference the same executor instance in multiple edges.

```python
from agent_framework import AgentExecutor

writer_executor = AgentExecutor(writer_agent, id="my-writer")
reviewer_executor = AgentExecutor(reviewer_agent, id="my-reviewer")

workflow = (
    WorkflowBuilder(start_executor=writer_executor)
    .add_edge(writer_executor, reviewer_executor)
    .build()
)
```

**Constructor parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `agent` | `SupportsAgentRun` | The agent to wrap. |
| `session` | `AgentSession \| None` | Session to use for agent runs. If `None`, a new session is created from the agent. |
| `id` | `str \| None` | Unique executor ID. Defaults to the agent's name if available. |
| `context_mode` | `"full" \| "last_agent" \| "custom" \| None` | Controls how conversation context is handled when receiving an `AgentExecutorResponse` from an upstream agent. Defaults to `"full"`, which provides the upstream agent's full conversation (input + response). See [Context Modes](#context-modes). |
| `context_filter` | `Callable[[list[Message]], list[Message]] \| None` | Custom filter function for selecting which messages to include. Required when `context_mode` is `"custom"`. |

> [!TIP]
> The executor ID is also the key used when you target `workflow.run(function_invocation_kwargs=...)` or `client_kwargs=` at individual agents. If you omit `id`, the workflow uses the wrapped agent's name.

## Input Types

The `AgentExecutor` defines multiple handler methods, each accepting a different input type. The workflow engine automatically dispatches the correct handler based on the message type. All input types trigger the agent to run immediately, except for `AgentExecutorRequest` where the `should_respond` flag controls whether the agent runs or simply caches the messages:

| Input Type | Handler | Triggers Agent | Description |
|------------|---------|:--------------:|-------------|
| `AgentExecutorRequest` | `run` | Conditional | The canonical input type. Contains a list of messages and a `should_respond` flag that controls whether the agent runs. |
| `str` | `from_str` | Always | Accepts a raw string prompt. |
| `Message` | `from_message` | Always | Accepts a single `Message` object. |
| `list[str \| Message]` | `from_messages` | Always | Accepts a list of strings or `Message` objects as conversation context. |
| `AgentExecutorResponse` | `from_response` | Always | Accepts a prior agent executor's response, enabling direct chaining. |

### Using AgentExecutorRequest

`AgentExecutorRequest` is the canonical input type and provides the most control:

```python
from agent_framework import AgentExecutorRequest, Message

# Create a request with messages
request = AgentExecutorRequest(
    messages=[Message(role="user", contents=["Hello, world!"])],
    should_respond=True,
)

# Run the workflow
result = await workflow.run(request)
```

The `should_respond` flag controls whether the agent processes the messages immediately or simply caches them for later:

- `True` (default) — the agent runs and produces a response.
- `False` — the messages are added to the cache but the agent does not run. This is useful for preloading conversation context before triggering a response.

## Output and Chaining

After the agent completes, the executor sends an `AgentExecutorResponse` downstream. This dataclass contains:

| Field | Type | Description |
|-------|------|-------------|
| `executor_id` | `str` | The ID of the executor that produced the response. |
| `agent_response` | `AgentResponse` | The underlying agent response (unaltered from the client). |
| `full_conversation` | `list[Message]` | The full conversation context (prior inputs + agent outputs) for chaining. |

When chaining agent executors, the downstream executor receives the `AgentExecutorResponse` via the `from_response` handler. By default, it uses the `full_conversation` field to preserve the complete conversation history, preventing downstream agents from losing prior context. You can change this behavior with [context modes](#context-modes):

```python
spam_detector = AgentExecutor(create_spam_detector_agent())
email_assistant = AgentExecutor(create_email_assistant_agent())

# The email_assistant receives the spam_detector's full conversation context
workflow = (
    WorkflowBuilder(start_executor=spam_detector)
    .add_edge(spam_detector, email_assistant)
    .build()
)
```

## Streaming Behavior

The `AgentExecutor` automatically adapts to the workflow execution mode:

- **`stream=True`** — calls `agent.run(stream=True)` and yields each `AgentResponseUpdate` as a workflow output event. After streaming completes, the updates are aggregated into a full `AgentResponse` for downstream dispatch.
- **`stream=False`** (default) — calls `agent.run(stream=False)` and yields a single `AgentResponse` as a workflow output event.

```python
# Streaming mode — receive incremental updates
events = workflow.run("Write a story about a cat.", stream=True)
async for event in events:
    if event.type == "output" and isinstance(event.data, AgentResponseUpdate):
        print(event.data.text, end="", flush=True)

# Non-streaming mode — receive complete response
result = await workflow.run("Write a story about a cat.")

# Retrieve terminal AgentResponse objects from the result
outputs = result.get_outputs()
for output in outputs:
    if isinstance(output, AgentResponse):
        print(output.text)

# Retrieve intermediate outputs (progress / observational emissions)
intermediate_outputs = result.get_intermediate_outputs()
for item in intermediate_outputs:
    print(f"Intermediate: {item}")
```

## Context Modes

When agents are chained together, the `context_mode` parameter on `AgentExecutor` controls what conversation context the agent consumes when it receives an `AgentExecutorResponse` from an upstream agent via the `from_response` handler.

### Available modes

| Mode | Behavior |
|------|----------|
| `"full"` (default) | The agent consumes the upstream agent's full conversation — both the input messages provided to the upstream agent and its response messages. |
| `"last_agent"` | The agent consumes only the upstream agent's response messages, excluding the input that was provided to the upstream agent. |
| `"custom"` | A user-provided `context_filter` function determines which messages the agent consumes. Requires the `context_filter` parameter. |

### Using `last_agent` mode

Use `"last_agent"` when each agent should focus solely on transforming the previous agent's output without being influenced by earlier conversation turns. This is useful for translation pipelines, progressive refinement, and similar sequential transformations:

```python
from agent_framework import AgentExecutor, WorkflowBuilder

# Each agent consumes only the previous agent's response messages
french_executor = AgentExecutor(french_agent, context_mode="last_agent")
spanish_executor = AgentExecutor(spanish_agent, context_mode="last_agent")

workflow = (
    WorkflowBuilder(start_executor=writer_agent)
    .add_edge(writer_agent, french_executor)
    .add_edge(french_executor, spanish_executor)
    .build()
)
```

With `context_mode="last_agent"`, the French translator consumes only the writer's response messages (excluding the original user prompt that was input to the writer), and the Spanish translator consumes only the French translator's response messages.

### Using `custom` mode

For fine-grained control over what context an agent consumes, use `context_mode="custom"` with a `context_filter` function. The filter receives the full conversation as a `list[Message]` and returns the filtered subset:

```python
from agent_framework import AgentExecutor, Message

def keep_user_and_last_agent(messages: list[Message]) -> list[Message]:
    """Keep only user messages and the last agent's response."""
    user_msgs = [m for m in messages if m.role == "user"]
    agent_msgs = [m for m in messages if m.role == "assistant"]
    return user_msgs + agent_msgs[-1:] if agent_msgs else user_msgs

executor = AgentExecutor(
    my_agent,
    context_mode="custom",
    context_filter=keep_user_and_last_agent,
)
```

### Context modes in SequentialBuilder

The `SequentialBuilder` orchestration provides a convenient `chain_only_agent_responses` parameter that configures all agent participants to use `context_mode="last_agent"`, so each agent consumes only the previous agent's response messages:

```python
from agent_framework.orchestrations import SequentialBuilder

workflow = SequentialBuilder(
    participants=[writer, translator, reviewer],
    chain_only_agent_responses=True,
).build()
```

For a complete example, see [sequential_chain_only_agent_responses.py](https://github.com/microsoft/agent-framework/blob/main/python/samples/03-workflows/orchestrations/sequential_chain_only_agent_responses.py) in the Agent Framework repository.

## Shared Sessions

By default, each `AgentExecutor` creates its own session. To share a session between multiple agents (for example, to maintain a common conversation thread), create a session explicitly and pass it to each executor:

```python
from agent_framework import AgentExecutor

# Create a shared session from one agent
shared_session = writer_agent.create_session()

# Both executors share the same session
writer_executor = AgentExecutor(writer_agent, session=shared_session)
reviewer_executor = AgentExecutor(reviewer_agent, session=shared_session)
```

> [!NOTE]
> Not all agents support shared sessions. Typically, only agents of the same provider type can share a session.

## Checkpointing

The `AgentExecutor` supports checkpointing for saving and restoring state in long-running workflows. When a checkpoint is taken, the executor serializes:

- The internal message cache.
- The full conversation history.
- The agent session state.
- Any pending user input requests and responses.

On restore, the executor deserializes this state, allowing the workflow to resume from where it left off.

> [!WARNING]
> Checkpointing with agents that use server-side sessions (such as `FoundryAgent`) has limitations. Server-side session state is not captured in checkpoints and can be modified by subsequent runs. Consider implementing a custom executor if you need reliable checkpointing with server-side sessions.

::: zone-end

::: zone pivot="programming-language-go"

## How It Works

Go hosts agents as workflow executors with `workflow/agentworkflow`. The hosted executor uses the following **turn token** pattern:

1. **Message buffering** — as messages arrive from other executors, the hosted agent collects them. If message forwarding is enabled (the default), incoming messages are also forwarded to downstream executors.
2. **Turn token trigger** — the hosted agent processes its cached messages only after receiving a `workflow.TurnToken`.
3. **Agent invocation** — the executor calls the underlying agent through `Run` and chooses streaming behavior from `agentworkflow.Config` or the `TurnToken`.
4. **Output yielding** — if update events are enabled, each `*agent.ResponseUpdate` is yielded as a workflow output. If response events are enabled, the aggregated `*agent.Response` is yielded as a workflow output.
5. **Downstream messaging** — the agent's response messages are sent to connected downstream executors.
6. **Turn token pass-through** — after the turn completes, the executor sends a new `workflow.TurnToken` downstream so the next hosted agent can begin processing.

## Custom Configuration

Customize how the hosted agent executor behaves by creating the binding with `agentworkflow.New` and a `agentworkflow.Config` value:

```go
hostedAgent := agentworkflow.New(myAgent, agentworkflow.Config{
    EmitUpdateEvents: true,
    DisableForwardIncomingMessages: true,
})

wf, err := workflow.NewBuilder(hostedAgent).
    WithOutputFrom(hostedAgent).
    Build()
if err != nil {
    return err
}
```

> [!TIP]
> See the [agents in workflows sample](https://github.com/microsoft/agent-framework-go/blob/main/examples/03-workflows/01-start-here/02_agents_in_workflows/main.go) for a complete runnable example.

## Input Types

The hosted agent executor accepts `string`, `*message.Message`, `[]*message.Message`, and `iter.Seq[*message.Message]` inputs. String inputs are converted to `message.Message` instances with the `User` role. Message inputs are buffered until the executor receives a `workflow.TurnToken`, which triggers the hosted agent to run on the accumulated batch.

```go
run, err := inproc.Default.RunStreaming(ctx, wf, nil)
if err != nil {
    return err
}
defer run.Close(ctx)

if err := run.SendMessage(ctx, "Summarize this deployment plan."); err != nil {
    return err
}
if err := run.SendMessage(ctx, message.NewText("Include risk notes.")); err != nil {
    return err
}
if err := run.SendMessage(ctx, []*message.Message{message.NewText("Keep it concise.")}); err != nil {
    return err
}

emitEvents := true
if err := run.SendMessage(ctx, workflow.TurnToken{EmitEvents: &emitEvents}); err != nil {
    return err
}
```

## Output and Chaining

After the hosted agent completes its turn, it sends the agent's response messages and a new turn token to connected downstream executors. This makes chaining agents straightforward:

```go
french := agentworkflow.New(frenchAgent, agentworkflow.Config{})
spanish := agentworkflow.New(spanishAgent, agentworkflow.Config{})
english := agentworkflow.New(englishAgent, agentworkflow.Config{})

wf, err := workflow.NewBuilder(french).
    AddEdge(french, spanish).
    AddEdge(spanish, english).
    Build()
if err != nil {
    return err
}
```

## Streaming Behavior

Set `EmitUpdateEvents` on `agentworkflow.Config`, or send a `workflow.TurnToken` with `EmitEvents` set, to emit agent response updates through workflow output events.

```go
hostedAgent := agentworkflow.New(myAgent, agentworkflow.Config{
    EmitUpdateEvents: true,
})

wf, err := workflow.NewBuilder(hostedAgent).
    WithOutputFrom(hostedAgent).
    Build()
if err != nil {
    return err
}

run, err := inproc.Default.RunStreaming(ctx, wf, message.NewText("Write a status update."))
if err != nil {
    return err
}
defer run.Close(ctx)

emitEvents := true
if err := run.SendMessage(ctx, workflow.TurnToken{EmitEvents: &emitEvents}); err != nil {
    return err
}

for evt, err := range run.WatchStream(ctx) {
    if err != nil {
        return err
    }
    if output, ok := evt.(workflow.OutputEvent); ok {
        if update, ok := output.Output.(*agent.ResponseUpdate); ok {
            fmt.Print(update.String())
        }
    }
}
```

## Configuration Options

`agentworkflow.Config` controls the hosted agent executor's behavior:

| Option | Default | Description |
|--------|---------|-------------|
| `EmitUpdateEvents` | `false` | Emit streaming `*agent.ResponseUpdate` values during execution. `workflow.TurnToken.EmitEvents` takes precedence when set. |
| `EmitResponseEvents` | `false` | Emit the aggregated `*agent.Response` as a workflow output event. |
| `InterceptUserInputRequests` | `false` | Intercept `ToolApprovalRequestContent` and route it as a workflow message for handling. |
| `InterceptUnterminatedFunctionCalls` | `false` | Intercept unresolved `FunctionCallContent` values and route them as workflow messages. |
| `DisableReassignOtherAgentsAsUsers` | `false` | Preserve incoming assistant roles from other agents instead of reassigning them to the user role. |
| `DisableForwardIncomingMessages` | `false` | Stop forwarding incoming messages to downstream executors before the hosted agent's generated messages. |

```go
hostedAgent := agentworkflow.New(myAgent, agentworkflow.Config{
    EmitUpdateEvents:                  true,
    EmitResponseEvents:                true,
    InterceptUserInputRequests:        true,
    InterceptUnterminatedFunctionCalls: true,
    DisableReassignOtherAgentsAsUsers: false,
    DisableForwardIncomingMessages:    false,
})
```

## Checkpointing

Hosted agents participate in workflow checkpointing. `agentworkflow.New` registers checkpoint and restore hooks on the executor. When a checkpoint is taken, the host stores:

- The hosted agent's `agent.Session` JSON state.
- The current turn's event-emission setting.
- Pending tool approval and function call request state.

On restore, the host recreates the agent session and restores pending request handlers before the workflow continues. Enable checkpointing through the workflow execution environment, for example with `inproc.Default.WithCheckpointing(...)`; no `agentworkflow.Config` option is required.

```go
checkpointManager := checkpoint.NewInMemoryManager()
environment := inproc.Default.WithCheckpointing(checkpointManager)

var checkpoints []workflow.CheckpointInfo
run, err := environment.RunStreaming(ctx, wf, message.NewText("Start the review."))
if err != nil {
    return err
}
defer run.Close(ctx)

emitEvents := true
if err := run.SendMessage(ctx, workflow.TurnToken{EmitEvents: &emitEvents}); err != nil {
    return err
}

for evt, err := range run.WatchUntilHalt(ctx) {
    if err != nil {
        return err
    }
    if completed, ok := evt.(workflow.SuperStepCompletedEvent); ok && completed.CompletionInfo != nil {
        if completed.CompletionInfo.CheckpointInfo != nil {
            checkpoints = append(checkpoints, *completed.CompletionInfo.CheckpointInfo)
        }
    }
}

if len(checkpoints) == 0 {
    return fmt.Errorf("no checkpoints were created")
}

resumedRun, err := environment.ResumeStreaming(ctx, wf, checkpoints[len(checkpoints)-1])
if err != nil {
    return err
}
defer resumedRun.Close(ctx)
```

> [!NOTE]
> Provider-backed sessions can still have provider-specific durability limits. Checkpointing captures the `agent.Session` state available to the Go host, not external service state that the provider does not serialize into the session.

::: zone-end
## Next steps

> [!div class="nextstepaction"]
> [Agents in Workflows](../../../workflows/agents-in-workflows.md)
