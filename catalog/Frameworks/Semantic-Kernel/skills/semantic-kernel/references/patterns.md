# Semantic Kernel Patterns

## Plugin Patterns

### Single Responsibility Plugin

Each plugin should focus on one domain or capability.

```csharp
// Good: focused plugin
public class InventoryPlugin
{
    private readonly IInventoryService _inventory;

    public InventoryPlugin(IInventoryService inventory) => _inventory = inventory;

    [KernelFunction]
    [Description("Checks if a product is in stock")]
    public async Task<string> CheckStock(
        [Description("Product SKU")] string sku)
    {
        var quantity = await _inventory.GetQuantityAsync(sku);
        return quantity > 0
            ? $"Product {sku} is in stock ({quantity} available)"
            : $"Product {sku} is out of stock";
    }

    [KernelFunction]
    [Description("Reserves inventory for an order")]
    public async Task<string> ReserveStock(
        [Description("Product SKU")] string sku,
        [Description("Quantity to reserve")] int quantity)
    {
        var result = await _inventory.ReserveAsync(sku, quantity);
        return result.Success
            ? $"Reserved {quantity} units of {sku}"
            : $"Failed to reserve: {result.Reason}";
    }
}
```

### Stateless Plugin Design

Keep plugins stateless; inject dependencies for external state.

```csharp
// Good: stateless with injected dependencies
public class CustomerPlugin
{
    private readonly ICustomerRepository _customers;
    private readonly ILogger<CustomerPlugin> _logger;

    public CustomerPlugin(ICustomerRepository customers, ILogger<CustomerPlugin> logger)
    {
        _customers = customers;
        _logger = logger;
    }

    [KernelFunction]
    [Description("Looks up customer information by email")]
    public async Task<string> LookupCustomer(
        [Description("Customer email address")] string email)
    {
        _logger.LogInformation("Looking up customer: {Email}", email);
        var customer = await _customers.FindByEmailAsync(email);
        return customer is not null
            ? $"Customer: {customer.Name}, ID: {customer.Id}, Status: {customer.Status}"
            : "Customer not found";
    }
}
```

### Contextual Plugin with Kernel Arguments

Pass runtime context through KernelArguments.

```csharp
public class AuthorizedPlugin
{
    [KernelFunction]
    [Description("Gets user-specific data")]
    public async Task<string> GetUserData(
        Kernel kernel,
        [Description("Data type to retrieve")] string dataType)
    {
        // Access context from kernel arguments
        if (!kernel.Data.TryGetValue("userId", out var userId))
            return "Error: User context not available";

        var data = await _dataService.GetAsync(userId.ToString()!, dataType);
        return data ?? "No data found";
    }
}

// Usage
var args = new KernelArguments
{
    ["userId"] = currentUser.Id
};
await kernel.InvokePromptAsync("Get my recent orders", args);
```

## Function Calling Patterns

### Conditional Function Exposure

Expose different functions based on context.

```csharp
public class ConditionalPluginBuilder
{
    public void ConfigureKernel(Kernel kernel, UserContext context)
    {
        // Always available
        kernel.Plugins.AddFromObject(new InfoPlugin(), "Info");

        // Role-based exposure
        if (context.Roles.Contains("Admin"))
        {
            kernel.Plugins.AddFromObject(new AdminPlugin(), "Admin");
        }

        if (context.Roles.Contains("Support"))
        {
            kernel.Plugins.AddFromObject(new SupportPlugin(), "Support");
        }

        // Feature-flag based
        if (_featureFlags.IsEnabled("BetaFeatures"))
        {
            kernel.Plugins.AddFromObject(new BetaPlugin(), "Beta");
        }
    }
}
```

### Required Function Forcing

Force specific function calls when needed.

```csharp
// Force weather lookup for weather-related queries
public async Task<string> HandleWeatherQuery(Kernel kernel, string query)
{
    var settings = new OpenAIPromptExecutionSettings
    {
        FunctionChoiceBehavior = FunctionChoiceBehavior.Required(
            [kernel.Plugins["Weather"]["GetCurrentWeather"]])
    };

    var result = await kernel.InvokePromptAsync(query, new KernelArguments(settings));
    return result.ToString();
}
```

### Function Result Post-Processing

Process function results before returning to conversation.

```csharp
public class ProcessingPlugin
{
    [KernelFunction]
    [Description("Searches products and formats results")]
    public async Task<string> SearchProducts(
        [Description("Search query")] string query,
        [Description("Maximum results (1-20)")] int maxResults = 5)
    {
        var products = await _productService.SearchAsync(query, maxResults);

        if (!products.Any())
            return "No products found matching your search.";

        var sb = new StringBuilder();
        sb.AppendLine($"Found {products.Count} products:");
        foreach (var p in products)
        {
            sb.AppendLine($"- {p.Name} (${p.Price:F2}) - {p.ShortDescription}");
        }
        return sb.ToString();
    }
}
```

## Multi-Agent Patterns

### Kernel Factory for Agent Isolation

Create isolated kernels per agent to prevent plugin leakage.

```csharp
public class AgentKernelFactory
{
    private readonly IConfiguration _config;
    private readonly IServiceProvider _services;

    public AgentKernelFactory(IConfiguration config, IServiceProvider services)
    {
        _config = config;
        _services = services;
    }

    public Kernel CreateForRole(AgentRole role)
    {
        var builder = Kernel.CreateBuilder();

        builder.AddAzureOpenAIChatCompletion(
            _config["AzureOpenAI:DeploymentName"]!,
            _config["AzureOpenAI:Endpoint"]!,
            _config["AzureOpenAI:ApiKey"]!);

        var kernel = builder.Build();

        // Role-specific plugins
        switch (role)
        {
            case AgentRole.CustomerSupport:
                kernel.Plugins.AddFromObject(
                    _services.GetRequiredService<CustomerPlugin>(), "Customer");
                kernel.Plugins.AddFromObject(
                    _services.GetRequiredService<TicketPlugin>(), "Tickets");
                break;

            case AgentRole.Sales:
                kernel.Plugins.AddFromObject(
                    _services.GetRequiredService<ProductPlugin>(), "Products");
                kernel.Plugins.AddFromObject(
                    _services.GetRequiredService<PricingPlugin>(), "Pricing");
                break;

            case AgentRole.TechnicalSupport:
                kernel.Plugins.AddFromObject(
                    _services.GetRequiredService<DiagnosticsPlugin>(), "Diagnostics");
                kernel.Plugins.AddFromObject(
                    _services.GetRequiredService<KnowledgeBasePlugin>(), "KB");
                break;
        }

        return kernel;
    }
}
```

### Agent Handoff Pattern

Coordinate between specialized agents.

```csharp
public class AgentRouter
{
    private readonly Dictionary<string, ChatCompletionAgent> _agents;
    private readonly Kernel _routingKernel;

    public async Task<AgentResponse> RouteQuery(string userMessage)
    {
        // Determine best agent
        var classification = await ClassifyIntent(userMessage);

        if (!_agents.TryGetValue(classification.AgentType, out var agent))
        {
            agent = _agents["general"];
        }

        // Execute with selected agent
        var history = new ChatHistory();
        history.AddUserMessage(userMessage);

        var response = await agent.InvokeAsync(history);

        return new AgentResponse
        {
            Agent = classification.AgentType,
            Content = response.Content,
            Confidence = classification.Confidence
        };
    }

    private async Task<IntentClassification> ClassifyIntent(string message)
    {
        var result = await _routingKernel.InvokePromptAsync(
            $"Classify this message into one of: support, sales, technical, general. Message: {message}");
        // Parse and return classification
        return IntentClassification.Parse(result.ToString());
    }
}
```

### Shared Memory with Isolated Execution

Share memory across agents while keeping function execution isolated.

```csharp
public class MultiAgentOrchestrator
{
    private readonly ISemanticTextMemory _sharedMemory;
    private readonly AgentKernelFactory _kernelFactory;

    public async Task<string> ProcessWithContext(
        string query,
        AgentRole role,
        string conversationId)
    {
        // Retrieve relevant context from shared memory
        var memories = await _sharedMemory.SearchAsync(
            collection: conversationId,
            query: query,
            limit: 5);

        // Create isolated kernel for this agent
        var kernel = _kernelFactory.CreateForRole(role);

        // Build context-aware prompt
        var contextBuilder = new StringBuilder();
        contextBuilder.AppendLine("Relevant context:");
        foreach (var memory in memories)
        {
            contextBuilder.AppendLine($"- {memory.Metadata.Text}");
        }
        contextBuilder.AppendLine($"\nUser query: {query}");

        // Execute with isolated plugins but shared context
        var response = await kernel.InvokePromptAsync(contextBuilder.ToString());

        // Store response in shared memory for other agents
        await _sharedMemory.SaveInformationAsync(
            collection: conversationId,
            text: response.ToString(),
            id: Guid.NewGuid().ToString());

        return response.ToString();
    }
}
```

### Agent Group Chat Pattern

Coordinate multiple agents in a group conversation.

```csharp
public class AgentGroupChat
{
    private readonly List<ChatCompletionAgent> _agents;
    private readonly ChatHistory _sharedHistory;
    private readonly ITerminationStrategy _termination;

    public async IAsyncEnumerable<AgentMessage> RunAsync(string initialPrompt)
    {
        _sharedHistory.AddUserMessage(initialPrompt);

        var turnCount = 0;
        while (!await _termination.ShouldTerminateAsync(_sharedHistory, turnCount))
        {
            foreach (var agent in _agents)
            {
                var response = await agent.InvokeAsync(_sharedHistory);

                _sharedHistory.AddMessage(new ChatMessageContent(
                    AuthorRole.Assistant,
                    response.Content)
                {
                    AuthorName = agent.Name
                });

                yield return new AgentMessage
                {
                    AgentName = agent.Name,
                    Content = response.Content
                };

                if (await _termination.ShouldTerminateAsync(_sharedHistory, turnCount))
                    break;
            }
            turnCount++;
        }
    }
}
```

## Prompt Template Patterns

### Parameterized Templates

Use Handlebars or Prompty templates for reusable prompts.

```csharp
// prompts/analyze-sentiment.prompty
/*
---
name: AnalyzeSentiment
description: Analyzes text sentiment
authors:
  - Team
model:
  api: chat
  parameters:
    temperature: 0.3
---
system:
You are a sentiment analysis assistant. Analyze the sentiment of the given text.
Respond with: positive, negative, or neutral, followed by a confidence score.

user:
Text to analyze: {{$text}}
*/

// Usage
var function = kernel.CreateFunctionFromPromptyFile("prompts/analyze-sentiment.prompty");
var result = await kernel.InvokeAsync(function, new() { ["text"] = customerFeedback });
```

### Template Composition

Compose complex prompts from smaller templates.

```csharp
public class PromptComposer
{
    private readonly Kernel _kernel;

    public async Task<string> ComposeAnalysis(AnalysisRequest request)
    {
        // Step 1: Summarize
        var summary = await _kernel.InvokeAsync(
            _kernel.Plugins["Prompts"]["Summarize"],
            new() { ["content"] = request.Content });

        // Step 2: Extract entities
        var entities = await _kernel.InvokeAsync(
            _kernel.Plugins["Prompts"]["ExtractEntities"],
            new() { ["content"] = request.Content });

        // Step 3: Final analysis combining results
        var analysis = await _kernel.InvokeAsync(
            _kernel.Plugins["Prompts"]["FinalAnalysis"],
            new()
            {
                ["summary"] = summary.ToString(),
                ["entities"] = entities.ToString(),
                ["originalContent"] = request.Content
            });

        return analysis.ToString();
    }
}
```

## Memory and RAG Patterns

### Scoped Memory Collections

Organize memory by scope for better retrieval.

```csharp
public class ScopedMemoryService
{
    private readonly ISemanticTextMemory _memory;

    public async Task SaveAsync(string scope, string content, Dictionary<string, string> metadata)
    {
        var collection = $"{scope}-knowledge";
        await _memory.SaveInformationAsync(
            collection: collection,
            text: content,
            id: Guid.NewGuid().ToString(),
            additionalMetadata: string.Join(";", metadata.Select(kv => $"{kv.Key}={kv.Value}")));
    }

    public async Task<IEnumerable<string>> SearchAsync(string scope, string query, int limit = 5)
    {
        var collection = $"{scope}-knowledge";
        var results = await _memory.SearchAsync(collection, query, limit);
        return results.Select(r => r.Metadata.Text);
    }
}

// Usage
await memoryService.SaveAsync("product", productDocs, new() { ["category"] = "electronics" });
var context = await memoryService.SearchAsync("product", "wireless headphones");
```

### RAG-Enhanced Function

Combine retrieval with function execution.

```csharp
public class RagEnhancedPlugin
{
    private readonly ISemanticTextMemory _memory;
    private readonly string _collection;

    [KernelFunction]
    [Description("Answers questions using the knowledge base")]
    public async Task<string> AnswerFromKnowledge(
        Kernel kernel,
        [Description("The user's question")] string question)
    {
        // Retrieve relevant documents
        var results = await _memory.SearchAsync(_collection, question, limit: 3);
        var context = string.Join("\n\n", results.Select(r => r.Metadata.Text));

        if (string.IsNullOrEmpty(context))
            return "I don't have information about that in my knowledge base.";

        // Generate answer with context
        var prompt = $"""
            Answer the question based only on the following context:

            {context}

            Question: {question}

            If the context doesn't contain enough information, say so.
            """;

        var answer = await kernel.InvokePromptAsync(prompt);
        return answer.ToString();
    }
}
```
