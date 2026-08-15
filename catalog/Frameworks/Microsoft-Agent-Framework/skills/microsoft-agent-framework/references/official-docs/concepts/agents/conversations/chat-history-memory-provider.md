---
title: Chat History Memory Provider for Agent Framework
description: Learn how to use the Chat History Memory Provider to add semantic memory capabilities to your Agent Framework agents by storing and retrieving chat history from a vector store.
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: article
ms.author: westey
ms.date: 04/03/2026
ms.service: agent-framework
---

# Chat History Memory Provider

::: zone pivot="programming-language-csharp"

The `ChatHistoryMemoryProvider` is an AI Context Provider that stores all chat history in a vector store and retrieves related messages to augment the current conversation. This enables agents to recall relevant context from prior interactions using semantic similarity search.

## How it works

The provider operates in two phases:

1. **Storage**: After each agent invocation, new request and response messages are stored in the vector store with embeddings generated from their content.

2. **Retrieval**: Before each invocation (or on-demand via function calling), the provider searches the vector store for messages semantically similar to the current user input and injects them as context.

Stored messages are scoped using configurable identifiers (application, agent, user, session) allowing fine-grained control over what history is stored and searchable.

## Prerequisites

- A vector store implementation from 📦 [Microsoft.Extensions.VectorData.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.VectorData.Abstractions) (for example, 📦 [`InMemoryVectorStore`](https://www.nuget.org/packages/Microsoft.SemanticKernel.Connectors.InMemory), 📦 [Azure AI Search](https://www.nuget.org/packages/Microsoft.SemanticKernel.Connectors.AzureAISearch), or [other supported stores](../../../integrations/index.md#vector-store-abstraction-implementations))
- An embedding model configured on your vector store
- Azure OpenAI or OpenAI deployment for the chat model
- .NET 8.0 or later

> [!TIP]
> See the [Vector Stores integration](../../../integrations/index.md#vector-stores) documentation for more information on the VectorData abstraction and available implementations.

## Usage

The following example demonstrates creating an agent with the `ChatHistoryMemoryProvider` using an in-memory vector store.

Note the usage of only userid for the search scope. This allows the agent to recall information from prior conversations with the same user to inform new responses.

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-4o-mini";
var embeddingDeploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME")
    ?? "text-embedding-3-large";

// Create a vector store with an embedding generator.
// For production, replace InMemoryVectorStore with a persistent store.
VectorStore vectorStore = new InMemoryVectorStore(new InMemoryVectorStoreOptions()
{
    EmbeddingGenerator = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
        .GetEmbeddingClient(embeddingDeploymentName)
        .AsIEmbeddingGenerator()
});

// Create the agent with ChatHistoryMemoryProvider
AIAgent agent = new AzureOpenAIClient(
    new Uri(endpoint),
    new DefaultAzureCredential())
    .GetChatClient(deploymentName)
    .AsAIAgent(new ChatClientAgentOptions
    {
        ChatOptions = new() { Instructions = "You are a helpful assistant." },
        Name = "MemoryAgent",
        AIContextProviders = [new ChatHistoryMemoryProvider(
            vectorStore,
            collectionName: "chathistory",
            vectorDimensions: 3072,
            session => new ChatHistoryMemoryProvider.State(
                // Configure where messages are stored
                storageScope: new() { UserId = "user-123", SessionId = Guid.NewGuid().ToString() },
                // Configure where to search (can be broader than storage scope)
                searchScope: new() { UserId = "user-123" }))]
    });

// Start a session and interact with the agent
AgentSession session = await agent.CreateSessionAsync();
Console.WriteLine(await agent.RunAsync("I prefer window seats on flights.", session));

// Start a new session - the agent can recall the user's preference
AgentSession session2 = await agent.CreateSessionAsync();
Console.WriteLine(await agent.RunAsync("Book me a flight to Seattle.", session2));
```

> [!TIP]
> Use different `storageScope` and `searchScope` configurations to control memory isolation. For example, store per-session but search across all sessions for a user.

## Configuration options

The `ChatHistoryMemoryProviderOptions` class provides configuration for the provider behavior.

### Search behavior

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `SearchTime` | `SearchBehavior` | `BeforeAIInvoke` | Controls when memory search is executed. |

The `SearchBehavior` enum has two values:

- **`BeforeAIInvoke`**: Automatically searches for relevant memories before each AI invocation and injects them as context messages. This is the default behavior.
- **`OnDemandFunctionCalling`**: Exposes a function tool that the AI model can invoke to search memories on demand. Use this when you want the model to decide when to recall memories.

### Search result options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `MaxResults` | `int?` | `3` | Maximum number of chat history results to retrieve per search. |
| `ContextPrompt` | `string?` | `"## Memories\nConsider the following memories..."` | The prompt text prefixed to search results before injection. |

### On-demand function tool options

These options only apply when `SearchTime` is set to `OnDemandFunctionCalling`:

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `FunctionToolName` | `string?` | `"Search"` | The name of the search function tool exposed to the model. |
| `FunctionToolDescription` | `string?` | `"Allows searching for related previous chat history..."` | The description of the search function tool. |

### Message filtering

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `SearchInputMessageFilter` | `Func<IEnumerable<ChatMessage>, IEnumerable<ChatMessage>>?` | External messages only | Filter applied to request messages when constructing search queries. |
| `StorageInputRequestMessageFilter` | `Func<IEnumerable<ChatMessage>, IEnumerable<ChatMessage>>?` | External messages only | Filter applied to request messages before storage. |
| `StorageInputResponseMessageFilter` | `Func<IEnumerable<ChatMessage>, IEnumerable<ChatMessage>>?` | No filter | Filter applied to response messages before storage. |

### Logging and telemetry

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `EnableSensitiveTelemetryData` | `bool` | `false` | When `true`, sensitive data (user IDs, message content) appears in logs unchanged. |
| `Redactor` | `Redactor?` | Redactor that replaces text with `"<redacted>"` | Custom redactor for sensitive values when logging. Ignored if `EnableSensitiveTelemetryData` is `true`. |

### State management

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `StateKey` | `string?` | Provider type name | Key used to store provider state in the `AgentSession.StateBag`. Override when using multiple `ChatHistoryMemoryProvider` instances in the same session. |

## Scope configuration

The `ChatHistoryMemoryProviderScope` class controls how messages are organized and filtered in the vector store.

| Property | Type | Description |
|----------|------|-------------|
| `ApplicationId` | `string?` | Scope messages to a specific application. If not set, spans all applications. |
| `AgentId` | `string?` | Scope messages to a specific agent. If not set, spans all agents. |
| `UserId` | `string?` | Scope messages to a specific user. If not set, spans all users. |
| `SessionId` | `string?` | Scope messages to a specific session. |

### Storage vs search scope

The `ChatHistoryMemoryProvider.State` class accepts two scopes:

- **`storageScope`**: Defines how new messages are tagged when stored. All scope properties are written as metadata.
- **`searchScope`**: Defines the filter criteria when searching. Set this broader than storage scope to search across multiple sessions or agents.

Example: Store per-session, search across all sessions for a user:

```csharp
new ChatHistoryMemoryProvider.State(
    storageScope: new() { UserId = "user-123", SessionId = "session-456" },
    searchScope: new() { UserId = "user-123" })
```

## Security considerations

> [!WARNING]
> Review these security considerations before deploying the `ChatHistoryMemoryProvider` in production.

- **Indirect prompt injection**: Messages retrieved from the vector store are injected into the LLM context. If the vector store is compromised, adversarial content could influence LLM behavior. Data from the store is accepted as-is without validation.

- **PII and sensitive data**: Conversation messages (including user inputs and LLM responses) are stored as vectors. These messages may contain PII or sensitive information. Ensure your vector store has appropriate access controls and encryption at rest.

- **On-demand search tool**: When using `OnDemandFunctionCalling`, the AI model controls when and what to search for. The search query is AI-generated and should be treated as untrusted input by the vector store implementation.

- **Trace logging**: When `LogLevel.Trace` is enabled, full search queries and results may be logged. This data may contain PII. Use the `Redactor` option or disable sensitive telemetry in production.

::: zone-end

::: zone pivot="programming-language-python"

This provider is not yet available for Python. See the C# tab for usage examples.

::: zone-end

::: zone pivot="programming-language-go"

> [!NOTE]
> Go support for this feature is coming soon. See the [Agent Framework Go repository](https://github.com/microsoft/agent-framework-go) for the latest status.

::: zone-end
## Next steps

> [!div class="nextstepaction"]
> [Context Providers overview](context-providers.md)
