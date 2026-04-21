---
title: Agent Background Responses
description: Learn how to handle long-running operations with background responses in Agent Framework
zone_pivot_groups: programming-languages
author: sergeymenshykh
ms.topic: reference
ms.author: semenshi
ms.date: 03/17/2026
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
- Long-running tasks that also invoke function tools during background processing

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
> Currently, only agents that use the OpenAI Responses API support background responses: [OpenAI Responses Agent](agent-types/openai-responses-agent.md) and [Azure OpenAI Responses Agent](agent-types/azure-openai-responses-agent.md).

Some agents may not allow explicit control over background responses. These agents can decide autonomously whether to initiate a background response based on the complexity of the operation, regardless of the `AllowBackgroundResponses` setting.

## Non-Streaming Background Responses

For non-streaming scenarios, when you initially run an agent, it may or may not return a continuation token. If no continuation token is returned, it means the operation has completed. If a continuation token is returned, it indicates that the agent has initiated a background response that is still processing and will require polling to retrieve the final result:

```csharp
AIAgent agent = new AzureOpenAIClient(
    new Uri(endpoint),
    new DefaultAzureCredential())
    .GetResponsesClient(deploymentName)
    .AsAIAgent();

AgentRunOptions options = new() { AllowBackgroundResponses = true };

AgentSession session = await agent.CreateSessionAsync();

// Get initial response - may return with or without a continuation token
AgentResponse response = await agent.RunAsync("Write a very long novel about otters in space.", session, options);

// Continue to poll until the final response is received
while (response.ContinuationToken is { } token)
{
    // Wait before polling again.
    await Task.Delay(TimeSpan.FromSeconds(2));

    options.ContinuationToken = token;
    response = await agent.RunAsync(session, options);
}

Console.WriteLine(response.Text);
```

### Key Points:

- The initial call may complete immediately (no continuation token) or start a background operation (with continuation token)
- If no continuation token is returned, the operation is complete and the response contains the final result
- If a continuation token is returned, the agent has started a background process that requires polling
- Use the continuation token from the previous response in subsequent polling calls
- When `ContinuationToken` is `null`, the operation is complete
- Use `AgentSession` (via `CreateSessionAsync()`) to hold conversation context instead of `AgentThread`

## Streaming Background Responses

In streaming scenarios, background responses work much like regular streaming responses - the agent streams all updates back to consumers in real-time. However, the key difference is that if the original stream gets interrupted, agents support stream resumption through continuation tokens. Each update includes a continuation token that captures the current state, allowing the stream to be resumed from exactly where it left off by passing this token to subsequent streaming API calls:

```csharp
AIAgent agent = new AzureOpenAIClient(
    new Uri(endpoint),
    new DefaultAzureCredential())
    .GetResponsesClient(deploymentName)
    .AsAIAgent();

AgentRunOptions options = new() { AllowBackgroundResponses = true };

AgentSession session = await agent.CreateSessionAsync();

AgentResponseUpdate? lastReceivedUpdate = null;

await foreach (AgentResponseUpdate update in agent.RunStreamingAsync("Write a very long novel about otters in space.", session, options))
{
    Console.Write(update.Text);

    lastReceivedUpdate = update;

    // Simulate connection loss after first piece of content received
    if (update.Text.Length > 0)
    {
        break;
    }
}

// Resume from interruption point captured by the continuation token
options.ContinuationToken = lastReceivedUpdate?.ContinuationToken;
await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(session, options))
{
    Console.Write(update.Text);
}
```

### Key Points:

- Each `AgentResponseUpdate` contains a continuation token that can be used for resumption
- Store the continuation token from the last received update before interruption
- Use the stored continuation token to resume the stream from the interruption point

## Background Responses with Tools and State Persistence

Background responses also support function calling during background operations. Functions can be invoked by the agent while it processes in the background. Combined with session serialization, you can persist the agent state between polling cycles and restore it in a new process or after a restart.

> [!TIP]
> For a complete working example, see the [Background Responses with Tools and Persistence sample](https://github.com/microsoft/agent-framework/blob/main/dotnet/samples/02-agents/Agents/Agent_Step10_BackgroundResponsesWithToolsAndPersistence/Program.cs).

```csharp
AIAgent agent = new AzureOpenAIClient(
    new Uri(endpoint),
    new DefaultAzureCredential())
    .GetResponsesClient(deploymentName)
    .AsAIAgent(
        name: "SpaceNovelWriter",
        instructions: "You are a space novel writer. Always research relevant facts before writing.",
        tools: [AIFunctionFactory.Create(ResearchSpaceFactsAsync), AIFunctionFactory.Create(GenerateCharacterProfilesAsync)]);

AgentRunOptions options = new() { AllowBackgroundResponses = true };
AgentSession session = await agent.CreateSessionAsync();

AgentResponse response = await agent.RunAsync("Write a very long novel about astronauts exploring an uncharted galaxy.", session, options);

while (response.ContinuationToken is not null)
{
    // Persist session and continuation token to durable storage
    await PersistAgentState(agent, session, response.ContinuationToken);

    await Task.Delay(TimeSpan.FromSeconds(10));

    // Restore state (e.g. after process restart)
    var (restoredSession, continuationToken) = await RestoreAgentState(agent);

    options.ContinuationToken = continuationToken;
    response = await agent.RunAsync(restoredSession, options);
}

Console.WriteLine(response.Text);
```

### Key Points for Tools and Persistence:

- Tools registered via `AIFunctionFactory.Create(...)` are called normally during background operations
- Use `agent.SerializeSessionAsync(session)` to persist the session to a `JsonElement`
- Use `agent.DeserializeSessionAsync(serializedSession)` to restore a session from storage
- Use `AgentAbstractionsJsonUtilities.DefaultOptions` when serializing `ResponseContinuationToken` directly
- Persisting state enables recovery from process restarts and server-side recycling between polling cycles

::: zone-end

::: zone pivot="programming-language-python"

> [!NOTE]
> Background responses support in Python is coming soon. This feature is currently available in the .NET implementation of Agent Framework.

::: zone-end

## Best Practices

When working with background responses, consider the following best practices:

- **Implement appropriate polling intervals** to avoid overwhelming the service
- **Use exponential backoff** for polling intervals if the operation is taking longer than expected
- **Always check for `null` continuation tokens** to determine when processing is complete
- **Consider storing continuation tokens and session state persistently** for operations that may span user sessions or process restarts
- **Use `DefaultAzureCredential` carefully in production**: it is convenient for development but uses credential fallback chains; prefer `ManagedIdentityCredential` or a specific credential in production to avoid latency and security risks

## Limitations and Considerations

- Background responses are dependent on the underlying AI service supporting long-running operations
- Currently only agents using the OpenAI Responses API (`GetResponsesClient`) support background responses
- Network interruptions or client restarts may require special handling to persist continuation tokens
- Function tools registered with `AIFunctionFactory` are supported during background operations

## Next steps

> [!div class="nextstepaction"]
> [Using MCP Tools](../model-context-protocol/using-mcp-tools.md)