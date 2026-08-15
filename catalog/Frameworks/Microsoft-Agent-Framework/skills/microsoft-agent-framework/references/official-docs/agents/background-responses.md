---
title: Agent Background Responses
description: Learn how to handle long-running operations with background responses in Agent Framework
zone_pivot_groups: programming-languages
author: sergeymenshykh
ms.topic: reference
ms.author: semenshi
ms.date: 05/27/2026
ms.service: agent-framework
---

# Agent Background Responses

The Microsoft Agent Framework supports background responses for handling long-running operations that may take time to complete. This feature enables agents to start processing a request and return a continuation token that can be used to poll for results or resume interrupted streams.

> [!TIP]
> For a complete working example, see the [Background Responses sample](https://github.com/microsoft/agent-framework/blob/main/dotnet/samples/02-agents/Agents/Agent_Step14_BackgroundResponses/Program.cs).

## When to Use Background Responses

Background responses are particularly useful for:
- Complex reasoning tasks that require significant processing time
- Operations that may be interrupted by network issues or client timeouts
- Scenarios where you want to start a long-running task and check back later for results

## How Background Responses Work

Background responses use a **continuation token** mechanism to handle long-running operations. When you send a request to an agent with background responses enabled, one of two things happens:

1. **Immediate completion**: The agent completes the task quickly and returns the final response without a continuation token
2. **Background processing**: The agent starts processing in the background and returns a continuation token instead of the final result

The continuation token contains all necessary information to either poll for completion using the non-streaming agent API or resume an interrupted stream with streaming agent API. When the continuation token is `null`, the operation is complete - this happens when a background response has completed, failed, or cannot proceed further (for example, when user input is required).

::: zone pivot="programming-language-csharp"

## Enabling Background Responses

To enable background responses, set the `AllowBackgroundResponses` property to `true` in the `AgentRunOptions`:

```csharp
AgentRunOptions options = new()
{
    AllowBackgroundResponses = true
};
```

> [!NOTE]
> Currently, only agents that use the OpenAI Responses API support background responses: [OpenAI Responses Agent](../integrations/by-component/model-providers/openai.md) and [Azure OpenAI Responses Agent](../integrations/by-component/model-providers/azure-openai.md).

Some agents may not allow explicit control over background responses. These agents can decide autonomously whether to initiate a background response based on the complexity of the operation, regardless of the `AllowBackgroundResponses` setting.

## Non-Streaming Background Responses

For non-streaming scenarios, when you initially run an agent, it may or may not return a continuation token. If no continuation token is returned, it means the operation has completed. If a continuation token is returned, it indicates that the agent has initiated a background response that is still processing and will require polling to retrieve the final result:

```csharp
AIAgent agent = new AIProjectClient(
    new Uri("<your-foundry-project-endpoint>"),
    new DefaultAzureCredential())
    .AsAIAgent(model: "<deployment-name>", instructions: "You are a helpful assistant.");

AgentRunOptions options = new()
{
    AllowBackgroundResponses = true
};

AgentSession session = await agent.CreateSessionAsync();

// Get initial response - may return with or without a continuation token
AgentResponse response = await agent.RunAsync("Write a very long novel about otters in space.", session, options);

// Continue to poll until the final response is received
while (response.ContinuationToken is not null)
{
    // Wait before polling again.
    await Task.Delay(TimeSpan.FromSeconds(2));

    options.ContinuationToken = response.ContinuationToken;
    response = await agent.RunAsync(session, options);
}

Console.WriteLine(response.Text);
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

### Key Points:

- The initial call may complete immediately (no continuation token) or start a background operation (with continuation token)
- If no continuation token is returned, the operation is complete and the response contains the final result
- If a continuation token is returned, the agent has started a background process that requires polling
- Use the continuation token from the previous response in subsequent polling calls
- When `ContinuationToken` is `null`, the operation is complete

## Streaming Background Responses

In streaming scenarios, background responses work much like regular streaming responses - the agent streams all updates back to consumers in real-time. However, the key difference is that if the original stream gets interrupted, agents support stream resumption through continuation tokens. Each update includes a continuation token that captures the current state, allowing the stream to be resumed from exactly where it left off by passing this token to subsequent streaming API calls:

```csharp
AIAgent agent = new AIProjectClient(
    new Uri("<your-foundry-project-endpoint>"),
    new DefaultAzureCredential())
    .AsAIAgent(model: "<deployment-name>", instructions: "You are a helpful assistant.");

AgentRunOptions options = new()
{
    AllowBackgroundResponses = true
};

AgentSession session = await agent.CreateSessionAsync();

AgentResponseUpdate? latestReceivedUpdate = null;

await foreach (var update in agent.RunStreamingAsync("Write a very long novel about otters in space.", session, options))
{
    Console.Write(update.Text);

    latestReceivedUpdate = update;

    // Simulate an interruption
    break;
}

// Resume from interruption point captured by the continuation token
options.ContinuationToken = latestReceivedUpdate?.ContinuationToken;
await foreach (var update in agent.RunStreamingAsync(session, options))
{
    Console.Write(update.Text);
}
```

### Key Points:

- Each `AgentResponseUpdate` contains a continuation token that can be used for resumption
- Store the continuation token from the last received update before interruption
- Use the stored continuation token to resume the stream from the interruption point

> [!TIP]
> See the [.NET samples](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples) for complete runnable examples.

::: zone-end

::: zone pivot="programming-language-python"

> [!TIP]
> For a complete working example, see the [Background Responses sample](https://github.com/microsoft/agent-framework/blob/main/python/samples/02-agents/background_responses.py).

## Enabling Background Responses

To enable background responses, pass the `background` option when calling `agent.run()`:

```python
session = agent.create_session()
response = await agent.run(
    messages="Your prompt here",
    session=session,
    options={"background": True},
)
```

> [!NOTE]
> Currently, only agents that use the OpenAI Responses API support background responses: [OpenAI Responses Agent](../integrations/by-component/model-providers/openai.md) and [Azure OpenAI Responses Agent](../integrations/by-component/model-providers/azure-openai.md).

## Non-Streaming Background Responses

For non-streaming scenarios, when you initially run an agent with `background=True`, it may return immediately with a `continuation_token`. If `continuation_token` is `None`, the operation has completed. Otherwise, poll by passing the token back in subsequent calls:

```python
import asyncio
from agent_framework import Agent
from agent_framework.openai import OpenAIChatClient

agent = Agent(
    name="researcher",
    instructions="You are a helpful research assistant.",
    client=OpenAIChatClient(model="o3"),
)

session = agent.create_session()

# Start a background run — returns immediately
response = await agent.run(
    messages="Briefly explain the theory of relativity in two sentences.",
    session=session,
    options={"background": True},
)

# Poll until the operation completes
while response.continuation_token is not None:
    await asyncio.sleep(2)
    response = await agent.run(
        session=session,
        options={"continuation_token": response.continuation_token},
    )

# Done — response.text contains the final result
print(response.text)
```

### Key Points

- The initial call may complete immediately (no continuation token) or start a background operation (with continuation token)
- Use the `continuation_token` from the previous response in subsequent polling calls
- When `continuation_token` is `None`, the operation is complete

## Streaming Background Responses

In streaming scenarios, background responses work like regular streaming — the agent streams updates back in real time. The key difference is that each update includes a `continuation_token`, enabling stream resumption if the connection is interrupted:

```python
session = agent.create_session()

# Start a streaming background run
last_token = None
stream = agent.run(
    messages="Briefly list three benefits of exercise.",
    stream=True,
    session=session,
    options={"background": True},
)

# Read chunks — each update carries a continuation_token
async for update in stream:
    last_token = update.continuation_token
    if update.text:
        print(update.text, end="", flush=True)
    # If interrupted (e.g., network issue), break and resume later
```

### Resuming an Interrupted Stream

If the stream is interrupted, use the last `continuation_token` to resume from where it left off:

```python
if last_token is not None:
    stream = agent.run(
        stream=True,
        session=session,
        options={"continuation_token": last_token},
    )
    async for update in stream:
        if update.text:
            print(update.text, end="", flush=True)
```

### Key Points

- Each `AgentResponseUpdate` contains a `continuation_token` for resumption
- Store the token from the last received update before interruption
- Pass the stored token via `options={"continuation_token": token}` to resume

::: zone-end

::: zone pivot="programming-language-go"
## Background responses

Go agents support background responses through the `agent.AllowBackgroundResponses` option. This enables the agent to produce asynchronous responses that can be retrieved later.

```go
resp, err := a.RunText(ctx, "Start a long analysis.",
    agent.WithSession(session),
    agent.AllowBackgroundResponses(true),
).Collect()
```

Background responses require an explicit session via `agent.WithSession(session)` to ensure consistent behavior between initial and follow-up runs.

::: zone-end

## Use background responses with Harness Agent

A Harness Agent remains a standard Agent Framework agent, so provider background responses use the same per-run options documented above. Harness construction doesn't enable provider background responses automatically: set `AllowBackgroundResponses` in .NET or `options={"background": True}` in Python when starting the run, keep the session, and persist continuation tokens when the operation must survive a process restart.

This is separate from [background agents](background-agents.md#use-background-agents-with-harnessed-agent), which delegate work to child agents rather than continuing one provider request.

## Best Practices

When working with background responses, consider the following best practices:

- **Implement appropriate polling intervals** to avoid overwhelming the service
- **Use exponential backoff** for polling intervals if the operation is taking longer than expected
- **Always check for `null` continuation tokens** to determine when processing is complete
- **Consider storing continuation tokens persistently** for operations that may span user sessions

## Limitations and Considerations

- Background responses are dependent on the underlying AI service supporting long-running operations
- Not all agent types may support background responses
- Network interruptions or client restarts may require special handling to persist continuation tokens

## Next steps

> [!div class="nextstepaction"]
> [RAG](rag.md)
