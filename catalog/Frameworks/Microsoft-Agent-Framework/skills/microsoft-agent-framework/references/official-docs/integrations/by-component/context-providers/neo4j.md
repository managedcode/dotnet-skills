---
title: Neo4j
description: Use Neo4j context providers for GraphRAG over existing knowledge graphs and persistent agent memory.
zone_pivot_groups: programming-languages
author: retroryan
ms.topic: article
ms.author: westey
ms.date: 07/28/2026
ms.service: agent-framework
---

<!--
  Language parity table - keep in sync when adding/removing sections.

  | Section              | C# | Python | Go | Notes                  |
  |----------------------|:--:|:------:|:--:|------------------------|
  | Pattern comparison   | ✅ |   ✅   | ✅ | Shared                 |
  | GraphRAG             | ✅ |   ✅   | ❌ |                        |
  | Persistent memory    | ✅ |   ✅   | ❌ | Separate implementations |
  | Go availability      | ✅ |   ✅   | ✅ | Go zones are status only |
-->

# Neo4j

Neo4j supports two distinct Agent Framework context-provider patterns. They share a graph database but use separate packages and data flows.

| Pattern | Behavior |
|---|---|
| GraphRAG | Searches an existing indexed knowledge graph with vector, full-text, or hybrid retrieval and can traverse related entities with Cypher. |
| Persistent memory | Extracts entities, facts, preferences, and reasoning from conversations and builds a knowledge graph that can be recalled across sessions. |

## GraphRAG from an existing knowledge graph

The Neo4j GraphRAG Context Provider adds Retrieval Augmented Generation (RAG) capabilities to Agent Framework agents using a Neo4j knowledge graph. It supports vector, fulltext, and hybrid search modes, with optional graph traversal to enrich results with related entities via custom Cypher queries.

For other managed retrieval services, see [Azure AI Search](azure-ai-search.md) and [Microsoft Foundry](microsoft-foundry.md).

For knowledge graph scenarios where relationships between entities matter, this provider retrieves relevant subgraphs rather than isolated text chunks, giving agents richer context for generating responses.

### Why use Neo4j for GraphRAG?

- **Graph enhanced retrieval**: Standard vector search returns isolated chunks; graph traversal follows connections to surface related entities, giving agents richer context.
- **Flexible search modes**: Combine vector similarity, keyword/BM25, and graph traversal in a single query.
- **Custom retrieval queries**: Cypher queries let you control exactly which relationships to traverse and what context to return.

::: zone pivot="programming-language-csharp"

### Prerequisites

- A Neo4j instance (self-hosted or [Neo4j AuraDB](https://neo4j.com/cloud/aura/)) with a vector or fulltext index configured
- An Azure AI Foundry project with a deployed chat model and an embedding model (e.g. `text-embedding-3-small`)
- Environment variables set: `NEO4J_URI`, `NEO4J_USERNAME`, `NEO4J_PASSWORD`, `AZURE_AI_SERVICES_ENDPOINT`, `AZURE_AI_EMBEDDING_NAME`
- Azure CLI credentials configured (`az login`)
- .NET 8.0 or later

### Installation

```bash
dotnet add package Neo4j.AgentFramework.GraphRAG
```

### Usage

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.OpenAI;
using Microsoft.Extensions.AI;
using Neo4j.AgentFramework.GraphRAG;
using Neo4j.Driver;

// Read connection details from environment variables
var neo4jSettings = new Neo4jSettings();
var azureEndpoint = Environment.GetEnvironmentVariable("AZURE_AI_SERVICES_ENDPOINT")!;

// Create embedding generator
var credential = new DefaultAzureCredential();
var azureClient = new AzureOpenAIClient(new Uri(azureEndpoint), credential);

IEmbeddingGenerator<string, Embedding<float>> embedder = azureClient
    .GetEmbeddingClient("text-embedding-3-small")
    .AsIEmbeddingGenerator();

// Create Neo4j driver
await using var driver = GraphDatabase.Driver(
    neo4jSettings.Uri, AuthTokens.Basic(neo4jSettings.Username, neo4jSettings.Password!));

// Create the Neo4j context provider
await using var provider = new Neo4jContextProvider(driver, new Neo4jContextProviderOptions
{
    IndexName = "chunkEmbeddings",
    IndexType = IndexType.Vector,
    EmbeddingGenerator = embedder,
    TopK = 5,
    RetrievalQuery = """
        MATCH (node)-[:FROM_DOCUMENT]->(doc:Document)
        OPTIONAL MATCH (doc)<-[:FILED]-(company:Company)
        RETURN node.text AS text, score, doc.title AS title, company.name AS company
        ORDER BY score DESC
        """,
});

// Create an agent with the provider
AIAgent agent = azureClient
    .GetChatClient("gpt-4o")
    .AsIChatClient()
    .AsBuilder()
    .UseAIContextProviders(provider)
    .BuildAIAgent(new ChatClientAgentOptions
    {
        ChatOptions = new ChatOptions
        {
            Instructions = "You are a financial analyst assistant.",
        },
    });

var session = await agent.CreateSessionAsync();
Console.WriteLine(await agent.RunAsync("What risks does Acme Corp face?", session));
```

### Key features

- **Index-driven**: Works with any Neo4j vector or fulltext index
- **Graph traversal**: Custom Cypher queries enrich search results with related entities
- **Search modes**: Vector (semantic similarity), fulltext (keyword/BM25), or hybrid (both combined)

### Resources

- [Neo4j Context Provider repository](https://github.com/neo4j-labs/neo4j-maf-provider)
- [NuGet package page](https://www.nuget.org/packages/Neo4j.AgentFramework.GraphRAG)
- [Workshop: Neo4j Context Providers for Agent Framework](https://github.com/neo4j-partners/maf-context-providers-lab)

::: zone-end

::: zone pivot="programming-language-python"

### Prerequisites

- A Neo4j instance (self-hosted or [Neo4j AuraDB](https://neo4j.com/cloud/aura/)) with a vector or fulltext index configured
- An Azure AI Foundry project with a deployed chat model and an embedding model (e.g. `text-embedding-ada-002`)
- Environment variables set: `NEO4J_URI`, `NEO4J_USERNAME`, `NEO4J_PASSWORD`, `FOUNDRY_PROJECT_ENDPOINT`, `FOUNDRY_MODEL`, `AZURE_AI_EMBEDDING_NAME`
- Azure CLI credentials configured (`az login`)
- Python 3.10 or later

### Installation

```bash
pip install agent-framework-neo4j
```

### Usage

```python
import os

from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient
from agent_framework_neo4j import Neo4jContextProvider, Neo4jSettings, AzureAISettings, AzureAIEmbedder
from azure.identity import DefaultAzureCredential
from azure.identity.aio import AzureCliCredential

# Reads NEO4J_URI, NEO4J_USERNAME, NEO4J_PASSWORD from environment variables
neo4j_settings = Neo4jSettings()

# Reads FOUNDRY_PROJECT_ENDPOINT, AZURE_AI_EMBEDDING_NAME from environment variables
azure_settings = AzureAISettings()

sync_credential = DefaultAzureCredential()
embedder = AzureAIEmbedder(
    endpoint=azure_settings.inference_endpoint,
    credential=sync_credential,
    model=azure_settings.embedding_model,
)

neo4j_provider = Neo4jContextProvider(
    uri=neo4j_settings.uri,
    username=neo4j_settings.username,
    password=neo4j_settings.get_password(),
    index_name=neo4j_settings.vector_index_name,
    index_type="vector",
    embedder=embedder,
    top_k=5,
    retrieval_query="""
        MATCH (node)-[:FROM_DOCUMENT]->(doc:Document)
        OPTIONAL MATCH (doc)<-[:FILED]-(company:Company)
        RETURN node.text AS text, score, doc.title AS title, company.name AS company
        ORDER BY score DESC
    """,
)

async with (
    neo4j_provider,
    AzureCliCredential() as credential,
    Agent(
        client=FoundryChatClient(
            credential=credential,
            project_endpoint=azure_settings.project_endpoint,
            model=os.environ["FOUNDRY_MODEL"],
        ),
        instructions="You are a financial analyst assistant.",
        context_providers=[neo4j_provider],
    ) as agent,
):
    session = agent.create_session()
    response = await agent.run("What risks does Acme Corp face?", session=session)
```

### Key features

- **Index-driven**: Works with any Neo4j vector or fulltext index
- **Graph traversal**: Custom Cypher queries enrich search results with related entities
- **Search modes**: Vector (semantic similarity), fulltext (keyword/BM25), or hybrid (both combined)

### Resources

- [Neo4j Context Provider repository](https://github.com/neo4j-labs/neo4j-maf-provider)
- [PyPI package page](https://pypi.org/project/agent-framework-neo4j/)
- [Workshop: Neo4j Context Providers for Agent Framework](https://github.com/neo4j-partners/maf-context-providers-lab)

::: zone-end

::: zone pivot="programming-language-go"

> [!NOTE]
> Go support for this feature is coming soon. See the [Agent Framework Go repository](https://github.com/microsoft/agent-framework-go) for the latest status.

::: zone-end

## Persistent agent memory

The Neo4j memory integrations store and recall agent interactions, automatically extracting entities and building a knowledge graph over time.

The providers manage:

- **Short-term memory**: Conversation history and recent context.
- **Long-term memory**: Entities, preferences, and facts extracted from interactions.
- **Reasoning memory**: Past reasoning traces and tool usage patterns.

### Why use Neo4j for agent memory?

- **Knowledge graph persistence**: Memories are stored as connected entities, not flat records, so the agent can reason about relationships between remembered information.
- **Automatic entity extraction**: Conversations are parsed into structured entities and relationships without a manually defined schema.
- **Cross-session recall**: Preferences, facts, and reasoning traces persist across sessions and surface through context providers.

::: zone pivot="programming-language-csharp"

> [!NOTE]
> The .NET package (`AgentMemory`) is an independent, community-maintained .NET port of the Neo4j Labs memory provider. It isn't an official Neo4j Labs package. See the [AgentMemory (.NET) repository](https://github.com/joslat/agent-memory-dotnet) for source and details.

### Prerequisites

- A Neo4j instance (self-hosted or [Neo4j AuraDB](https://neo4j.com/cloud/aura/)).
- An Azure OpenAI or Microsoft Foundry deployment with a chat model and an embedding model.
- Environment variables set: `NEO4J_URI`, `NEO4J_USERNAME`, `NEO4J_PASSWORD`, `AZURE_OPENAI_ENDPOINT`.
- Azure CLI credentials configured (`az login`), or an API key.
- .NET 8.0 or later.

### Installation

```bash
dotnet add package AgentMemory
dotnet add package AgentMemory.AgentFramework
```

### Usage

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AgentMemory;
using AgentMemory.Abstractions.Services;
using AgentMemory.AgentFramework;
using AgentMemory.AgentFramework.Tools;

var builder = Host.CreateApplicationBuilder(args);

// Registers Core + Neo4j infrastructure in one call (reads NEO4J_URI / NEO4J_USERNAME /
// NEO4J_PASSWORD, falling back to local-dev defaults). Passing configureLlm opts in to
// LLM-backed entity/fact/preference extraction, using the IChatClient registered below.
builder.Services.AddNeo4jAgentMemory(
    configureMemory: _ => { },
    configureNeo4j: neo4j =>
    {
        neo4j.Uri = Environment.GetEnvironmentVariable("NEO4J_URI") ?? "bolt://localhost:7687";
        neo4j.Username = Environment.GetEnvironmentVariable("NEO4J_USERNAME") ?? "neo4j";
        neo4j.Password = Environment.GetEnvironmentVariable("NEO4J_PASSWORD") ?? "password";
    },
    configureLlm: _ => { });

// Any Microsoft.Extensions.AI-compatible chat + embedding client works
var azureClient = new AzureOpenAIClient(
    new Uri(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!), new DefaultAzureCredential());
builder.Services.AddSingleton(azureClient.GetChatClient("gpt-4o-mini").AsIChatClient());
builder.Services.AddSingleton(azureClient.GetEmbeddingClient("text-embedding-3-small").AsIEmbeddingGenerator());

// AutoExtractOnPersist builds the knowledge graph from every conversation turn
builder.Services.AddAgentMemoryFramework(options =>
{
    options.AutoExtractOnPersist = true;
    options.ContextFormat.IncludeEntities = true;
    options.ContextFormat.IncludeFacts = true;
    options.ContextFormat.IncludePreferences = true;
});

using var host = builder.Build();
await using var scope = host.Services.CreateAsyncScope();
var services = scope.ServiceProvider;

// Bootstraps Neo4j schema/indexes on first run (idempotent)
await services.GetRequiredService<ISchemaBootstrapper>().BootstrapAsync();

var memoryProvider = services.GetRequiredService<Neo4jMemoryContextProvider>();
var memoryTools = services.GetRequiredService<MemoryToolFactory>().CreateAIFunctions();

// WithMemoryOwnerScoping wraps the whole invocation — recall, the tool-calling loop, and
// persistence — in the owner scope set by WithMemoryIdentity below, so no manual
// BeginOwnerScope call is needed around RunAsync.
AIAgent agent = services.GetRequiredService<IChatClient>().AsAIAgent(new ChatClientAgentOptions
{
    ChatOptions = new ChatOptions
    {
        Instructions = "You are a helpful assistant with persistent memory.",
        Tools = [.. memoryTools],
    },
    AIContextProviders = [memoryProvider],
}).WithMemoryOwnerScoping(services);

var session = (await agent.CreateSessionAsync())
    .WithMemoryIdentity(userId: "user-123", sessionId: "session-1", applicationId: "my-app");

var response = await agent.RunAsync("Remember that I prefer window seats on flights.", session);
```

### Key features

- **Bidirectional**: `Neo4jMemoryContextProvider` recalls relevant memory before each run and persists new memory after it.
- **Entity extraction**: The configurable extraction pipeline builds a knowledge graph from conversations.
- **Preference learning**: Preferences, facts, and entities can be recalled by a new `AgentSession` for the same user.
- **Memory tools**: `MemoryToolFactory` exposes `AIFunction` instances for explicit search, remember, and recall operations.
- **Dependency-injection first**: `AddNeo4jAgentMemory` and `AddAgentMemoryFramework` integrate with Generic Host and ASP.NET Core applications.
- **Beyond Agent Framework**: The same library also integrates with Semantic Kernel and MCP clients and includes OpenTelemetry observability.

### Resources

- [AgentMemory (.NET) repository](https://github.com/joslat/agent-memory-dotnet)
- [NuGet package page](https://www.nuget.org/packages/AgentMemory)
- [Sample: Retail Assistant with AgentMemory](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/AgentWithMemory/AgentWithMemory_Step06_MemoryUsingAgentMemory)

::: zone-end

::: zone pivot="programming-language-python"

### Prerequisites

- A Neo4j instance (self-hosted or [Neo4j AuraDB](https://neo4j.com/cloud/aura/)).
- A Microsoft Foundry project with a deployed chat model.
- An OpenAI API key or Azure OpenAI deployment for embeddings and entity extraction.
- Environment variables set: `NEO4J_URI`, `NEO4J_PASSWORD`, `FOUNDRY_PROJECT_ENDPOINT`, `FOUNDRY_MODEL`, `OPENAI_API_KEY`.
- Azure CLI credentials configured (`az login`).
- Python 3.10 or later.

### Installation

```bash
pip install neo4j-agent-memory[microsoft-agent]
```

### Usage

```python
import os
from pydantic import SecretStr
from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient
from azure.identity.aio import AzureCliCredential
from neo4j_agent_memory import MemoryClient, MemorySettings
from neo4j_agent_memory.integrations.microsoft_agent import (
    Neo4jMicrosoftMemory,
    create_memory_tools,
)

# Pass Neo4j and embedding configuration directly via constructor arguments.
# MemorySettings also supports loading from environment variables or .env files
# using the NAM_ prefix (e.g. NAM_NEO4J__URI, NAM_EMBEDDING__MODEL).
settings = MemorySettings(
    neo4j={
        "uri": os.environ["NEO4J_URI"],
        "username": os.environ.get("NEO4J_USERNAME", "neo4j"),
        "password": SecretStr(os.environ["NEO4J_PASSWORD"]),
    },
    embedding={
        "provider": "openai",
        "model": "text-embedding-3-small",
    },
)

memory_client = MemoryClient(settings)

async with memory_client:
    memory = Neo4jMicrosoftMemory.from_memory_client(
        memory_client=memory_client,
        session_id="user-123",
    )
    tools = create_memory_tools(memory)

    async with AzureCliCredential() as credential, Agent(
        client=FoundryChatClient(
            credential=credential,
            project_endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
            model=os.environ["FOUNDRY_MODEL"],
        ),
        instructions="You are a helpful assistant with persistent memory.",
        tools=tools,
        context_providers=[memory.context_provider],
    ) as agent:
        session = agent.create_session()
        response = await agent.run("Remember that I prefer window seats on flights.", session=session)
```

### Key features

- **Bidirectional**: Retrieves relevant context before invocation and saves new memories after responses.
- **Entity extraction**: Builds a knowledge graph from conversations with a multi-stage extraction pipeline.
- **Preference learning**: Infers and stores user preferences across sessions.
- **Memory tools**: Lets agents explicitly search memory, remember preferences, and find entity connections.

### Resources

- [Neo4j Agent Memory repository](https://github.com/neo4j-labs/agent-memory)
- [PyPI package page](https://pypi.org/project/neo4j-agent-memory/)
- [Sample: Retail Assistant with Neo4j Agent Memory](https://github.com/neo4j-labs/agent-memory/tree/main/examples/microsoft_agent_retail_assistant)
- [Workshop: Neo4j Context Providers for Agent Framework](https://github.com/neo4j-partners/maf-context-providers-lab)

::: zone-end

::: zone pivot="programming-language-go"

> [!NOTE]
> Neo4j GraphRAG and memory integrations aren't currently documented for Agent Framework Go. See the [Agent Framework Go repository](https://github.com/microsoft/agent-framework-go) for the latest status.

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Browse context provider integrations](index.md)
