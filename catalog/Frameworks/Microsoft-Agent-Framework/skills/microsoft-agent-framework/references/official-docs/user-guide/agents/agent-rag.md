---
title: Agent Retrieval Augmented Generation (RAG)
description: Learn how to use Retrieval Augmented Generation (RAG) with Agent Framework
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: reference
ms.author: westey
ms.date: 11/11/2025
ms.service: agent-framework
---

# Agent Retrieval Augmented Generation (RAG)

Microsoft Agent Framework supports adding Retrieval Augmented Generation (RAG) capabilities to agents easily by adding AI Context Providers to the agent.

::: zone pivot="programming-language-csharp"

## Using TextSearchProvider

The `TextSearchProvider` class is an out-of-the-box implementation of a RAG context provider.

It can easily be attached to a `ChatClientAgent` using the `AIContextProviderFactory` option to provide RAG capabilities to the agent.

The factory is an async function that receives a context object and a cancellation token.

```csharp
// Create the AI agent with the TextSearchProvider as the AI context provider.
AIAgent agent = azureOpenAIClient
    .GetChatClient(deploymentName)
    .AsAIAgent(new ChatClientAgentOptions
    {
        ChatOptions = new() { Instructions = "You are a helpful support specialist for Contoso Outdoors. Answer questions using the provided context and cite the source document when available." },
        AIContextProviderFactory = (ctx, ct) => new ValueTask<AIContextProvider>(
            new TextSearchProvider(SearchAdapter, ctx.SerializedState, ctx.JsonSerializerOptions, textSearchOptions))
    });
```

The `TextSearchProvider` requires a function that provides the search results given a query. This can be implemented using any search technology, e.g. Azure AI Search, or a web search engine.

Here is an example of a mock search function that returns pre-defined results based on the query.
`SourceName` and `SourceLink` are optional, but if provided will be used by the agent to cite the source of the information when answering the user's question.

```csharp
static Task<IEnumerable<TextSearchProvider.TextSearchResult>> SearchAdapter(string query, CancellationToken cancellationToken)
{
    // The mock search inspects the user's question and returns pre-defined snippets
    // that resemble documents stored in an external knowledge source.
    List<TextSearchProvider.TextSearchResult> results = new();

    if (query.Contains("return", StringComparison.OrdinalIgnoreCase) || query.Contains("refund", StringComparison.OrdinalIgnoreCase))
    {
        results.Add(new()
        {
            SourceName = "Contoso Outdoors Return Policy",
            SourceLink = "https://contoso.com/policies/returns",
            Text = "Customers may return any item within 30 days of delivery. Items should be unused and include original packaging. Refunds are issued to the original payment method within 5 business days of inspection."
        });
    }

    return Task.FromResult<IEnumerable<TextSearchProvider.TextSearchResult>>(results);
}
```

### TextSearchProvider Options

The `TextSearchProvider` can be customized via the `TextSearchProviderOptions` class. Here is an example of creating options to run the search prior to every model invocation and keep a short rolling window of conversation context.

```csharp
TextSearchProviderOptions textSearchOptions = new()
{
    // Run the search prior to every model invocation and keep a short rolling window of conversation context.
    SearchTime = TextSearchProviderOptions.TextSearchBehavior.BeforeAIInvoke,
    RecentMessageMemoryLimit = 6,
};
```

The `TextSearchProvider` class supports the following options via the `TextSearchProviderOptions` class.

| Option | Type | Description | Default |
|--------|------|-------------|---------|
| SearchTime | `TextSearchProviderOptions.TextSearchBehavior` | Indicates when the search should be executed. There are two options, each time the agent is invoked, or on-demand via function calling. | `TextSearchProviderOptions.TextSearchBehavior.BeforeAIInvoke` |
| FunctionToolName | `string` | The name of the exposed search tool when operating in on-demand mode. | "Search" |
| FunctionToolDescription | `string` | The description of the exposed search tool when operating in on-demand mode. | "Allows searching for additional information to help answer the user question." |
| ContextPrompt | `string` | The context prompt prefixed to results when operating in `BeforeAIInvoke` mode. | "## Additional Context\nConsider the following information from source documents when responding to the user:" |
| CitationsPrompt | `string` | The instruction appended after results to request citations when operating in `BeforeAIInvoke` mode. | "Include citations to the source document with document name and link if document name and link is available." |
| ContextFormatter | `Func<IList<TextSearchProvider.TextSearchResult>, string>` | Optional delegate to fully customize formatting of the result list when operating in `BeforeAIInvoke` mode. If provided, `ContextPrompt` and `CitationsPrompt` are ignored. | `null` |
| RecentMessageMemoryLimit | `int` | The number of recent conversation messages (both user and assistant) to keep in memory and include when constructing the search input for `BeforeAIInvoke` searches. | `0` (disabled) |
| RecentMessageRolesIncluded | `List<ChatRole>` | The list of `ChatRole` types to filter recent messages to when deciding which recent messages to include when constructing the search input. | `ChatRole.User` |

::: zone-end
::: zone pivot="programming-language-python"

## Using Semantic Kernel VectorStore with Agent Framework

Agent Framework supports using Semantic Kernel's VectorStore collections to provide RAG capabilities to agents. This is achieved through the bridge functionality that converts Semantic Kernel search functions into Agent Framework tools.

> [!IMPORTANT]
> This feature requires `semantic-kernel` version 1.38 or higher.

### Creating a Search Tool from VectorStore

The `create_search_function` method from a Semantic Kernel VectorStore collection returns a `KernelFunction` that can be converted to an Agent Framework tool using `.as_agent_framework_tool()`.
Use [the vector store connectors documentation](/semantic-kernel/concepts/vector-store-connectors) to learn how to set up different vector store collections.

```python
from semantic_kernel.connectors.ai.open_ai import OpenAITextEmbedding
from semantic_kernel.connectors.azure_ai_search import AzureAISearchCollection
from semantic_kernel.functions import KernelParameterMetadata
from agent_framework.openai import OpenAIResponsesClient

# Define your data model
class SupportArticle:
    article_id: str
    title: str
    content: str
    category: str
    # ... other fields

# Create an Azure AI Search collection
collection = AzureAISearchCollection[str, SupportArticle](
    record_type=SupportArticle,
    embedding_generator=OpenAITextEmbedding()
)

async with collection:
    await collection.ensure_collection_exists()
    # Load your knowledge base articles into the collection
    # await collection.upsert(articles)

    # Create a search function from the collection
    search_function = collection.create_search_function(
        function_name="search_knowledge_base",
        description="Search the knowledge base for support articles and product information.",
        search_type="keyword_hybrid",
        parameters=[
            KernelParameterMetadata(
                name="query",
                description="The search query to find relevant information.",
                type="str",
                is_required=True,
                type_object=str,
            ),
            KernelParameterMetadata(
                name="top",
                description="Number of results to return.",
                type="int",
                default_value=3,
                type_object=int,
            ),
        ],
        string_mapper=lambda x: f"[{x.record.category}] {x.record.title}: {x.record.content}",
    )

    # Convert the search function to an Agent Framework tool
    search_tool = search_function.as_agent_framework_tool()

    # Create an agent with the search tool
    agent = OpenAIResponsesClient(model_id="gpt-4o").as_agent(
        instructions="You are a helpful support specialist. Use the search tool to find relevant information before answering questions. Always cite your sources.",
        tools=search_tool
    )

    # Use the agent with RAG capabilities
    response = await agent.run("How do I return a product?")
    print(response.text)
```

### Customizing Search Behavior

You can customize the search function with various options:

```python
# Create a search function with filtering and custom formatting
search_function = collection.create_search_function(
    function_name="search_support_articles",
    description="Search for support articles in specific categories.",
    search_type="keyword_hybrid",
    # Apply filters to restrict search scope
    filter=lambda x: x.is_published == True,
    parameters=[
        KernelParameterMetadata(
            name="query",
            description="What to search for in the knowledge base.",
            type="str",
            is_required=True,
            type_object=str,
        ),
        KernelParameterMetadata(
            name="category",
            description="Filter by category: returns, shipping, products, or billing.",
            type="str",
            type_object=str,
        ),
        KernelParameterMetadata(
            name="top",
            description="Maximum number of results to return.",
            type="int",
            default_value=5,
            type_object=int,
        ),
    ],
    # Customize how results are formatted for the agent
    string_mapper=lambda x: f"Article: {x.record.title}\nCategory: {x.record.category}\nContent: {x.record.content}\nSource: {x.record.article_id}",
)
```

For the full details on the parameters available for `create_search_function`, see the [Semantic Kernel documentation](/semantic-kernel/concepts/vector-store-connectors/).

### Using Multiple Search Functions

You can provide multiple search tools to an agent for different knowledge domains:

```python
# Create search functions for different knowledge bases
product_search = product_collection.create_search_function(
    function_name="search_products",
    description="Search for product information and specifications.",
    search_type="semantic_hybrid",
    string_mapper=lambda x: f"{x.record.name}: {x.record.description}",
).as_agent_framework_tool()

policy_search = policy_collection.create_search_function(
    function_name="search_policies",
    description="Search for company policies and procedures.",
    search_type="keyword_hybrid",
    string_mapper=lambda x: f"Policy: {x.record.title}\n{x.record.content}",
).as_agent_framework_tool()

# Create an agent with multiple search tools
agent = chat_client.as_agent(
    instructions="You are a support agent. Use the appropriate search tool to find information before answering. Cite your sources.",
    tools=[product_search, policy_search]
)
```

You can also create multiple search functions from the same collection with different descriptions and parameters to provide specialized search capabilities:

```python
# Create multiple search functions from the same collection
# Generic search for broad queries
general_search = support_collection.create_search_function(
    function_name="search_all_articles",
    description="Search all support articles for general information.",
    search_type="semantic_hybrid",
    parameters=[
        KernelParameterMetadata(
            name="query",
            description="The search query.",
            type="str",
            is_required=True,
            type_object=str,
        ),
    ],
    string_mapper=lambda x: f"{x.record.title}: {x.record.content}",
).as_agent_framework_tool()

# Detailed lookup for specific article IDs
detail_lookup = support_collection.create_search_function(
    function_name="get_article_details",
    description="Get detailed information for a specific article by its ID.",
    search_type="keyword",
    top=1,
    parameters=[
        KernelParameterMetadata(
            name="article_id",
            description="The specific article ID to retrieve.",
            type="str",
            is_required=True,
            type_object=str,
        ),
    ],
    string_mapper=lambda x: f"Title: {x.record.title}\nFull Content: {x.record.content}\nLast Updated: {x.record.updated_date}",
).as_agent_framework_tool()

# Create an agent with both search functions
agent = chat_client.as_agent(
    instructions="You are a support agent. Use search_all_articles for general queries and get_article_details when you need full details about a specific article.",
    tools=[general_search, detail_lookup]
)
```

This approach allows the agent to choose the most appropriate search strategy based on the user's query.

### Supported VectorStore Connectors

This pattern works with any Semantic Kernel VectorStore connector, including:

- Azure AI Search (`AzureAISearchCollection`)
- Qdrant (`QdrantCollection`)
- Pinecone (`PineconeCollection`)
- Redis (`RedisCollection`)
- Weaviate (`WeaviateCollection`)
- In-Memory (`InMemoryVectorStoreCollection`)
- And more

Each connector provides the same `create_search_function` method that can be bridged to Agent Framework tools, allowing you to choose the vector database that best fits your needs. See [the full list here](/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors).

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Agent Middleware](./agent-middleware.md)
