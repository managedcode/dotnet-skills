# Semantic Kernel Anti-Patterns

## Plugin Anti-Patterns

### Vague Function Descriptions

The LLM relies on descriptions to decide when to call functions. Vague descriptions lead to incorrect or missed function calls.

```csharp
// BAD: Vague description
[KernelFunction]
[Description("Does something with orders")]
public async Task<string> ProcessOrder(string orderId) { ... }

// GOOD: Specific, actionable description
[KernelFunction]
[Description("Retrieves the current status and details of an order by its order ID")]
public async Task<string> GetOrderStatus(
    [Description("The unique order identifier, e.g., 'ORD-12345'")] string orderId) { ... }
```

### Missing Parameter Descriptions

Without parameter descriptions, the LLM guesses parameter formats and values.

```csharp
// BAD: No parameter descriptions
[KernelFunction]
[Description("Searches for products")]
public async Task<string> SearchProducts(string query, int limit, string category) { ... }

// GOOD: Clear parameter descriptions with examples and constraints
[KernelFunction]
[Description("Searches the product catalog")]
public async Task<string> SearchProducts(
    [Description("Search keywords, e.g., 'wireless headphones'")] string query,
    [Description("Maximum results to return (1-50, default 10)")] int limit = 10,
    [Description("Product category filter: electronics, clothing, home, or 'all'")] string category = "all") { ... }
```

### Stateful Plugin Classes

Mutable state in plugins causes race conditions and unpredictable behavior.

```csharp
// BAD: Mutable state
public class CartPlugin
{
    private List<CartItem> _items = new(); // Shared across all calls!

    [KernelFunction]
    public string AddItem(string productId, int quantity)
    {
        _items.Add(new CartItem(productId, quantity));
        return "Added to cart";
    }
}

// GOOD: Inject state management
public class CartPlugin
{
    private readonly ICartService _cartService;

    public CartPlugin(ICartService cartService) => _cartService = cartService;

    [KernelFunction]
    [Description("Adds a product to the user's shopping cart")]
    public async Task<string> AddItem(
        Kernel kernel,
        [Description("Product ID")] string productId,
        [Description("Quantity (1-99)")] int quantity)
    {
        var userId = kernel.Data["userId"]?.ToString();
        if (userId is null) return "Error: User not authenticated";

        await _cartService.AddItemAsync(userId, productId, quantity);
        return $"Added {quantity} x {productId} to your cart";
    }
}
```

### Functions That Return Raw Objects

The LLM cannot interpret complex objects; always return formatted strings.

```csharp
// BAD: Returns complex object
[KernelFunction]
public async Task<Order> GetOrder(string orderId)
{
    return await _orderService.GetAsync(orderId);
}

// GOOD: Returns formatted, interpretable string
[KernelFunction]
[Description("Gets order details including status, items, and total")]
public async Task<string> GetOrder(
    [Description("Order ID")] string orderId)
{
    var order = await _orderService.GetAsync(orderId);
    if (order is null) return $"Order {orderId} not found";

    var items = string.Join("\n", order.Items.Select(i => $"  - {i.Name} x{i.Quantity}: ${i.Price:F2}"));
    return $"""
        Order: {order.Id}
        Status: {order.Status}
        Date: {order.CreatedAt:yyyy-MM-dd}
        Items:
        {items}
        Total: ${order.Total:F2}
        """;
}
```

### No Input Validation

The LLM may hallucinate parameter values. Always validate inputs.

```csharp
// BAD: No validation
[KernelFunction]
public async Task<string> TransferFunds(string fromAccount, string toAccount, decimal amount)
{
    await _bankService.TransferAsync(fromAccount, toAccount, amount);
    return "Transfer complete";
}

// GOOD: Comprehensive validation
[KernelFunction]
[Description("Transfers funds between accounts")]
public async Task<string> TransferFunds(
    [Description("Source account number")] string fromAccount,
    [Description("Destination account number")] string toAccount,
    [Description("Amount to transfer (positive number)")] decimal amount)
{
    // Validate account format
    if (!AccountNumber.TryParse(fromAccount, out _))
        return $"Invalid source account format: {fromAccount}";

    if (!AccountNumber.TryParse(toAccount, out _))
        return $"Invalid destination account format: {toAccount}";

    if (fromAccount == toAccount)
        return "Source and destination accounts must be different";

    // Validate amount
    if (amount <= 0)
        return "Transfer amount must be positive";

    if (amount > 10000)
        return "Transfer amount exceeds single-transaction limit of $10,000";

    try
    {
        var result = await _bankService.TransferAsync(fromAccount, toAccount, amount);
        return $"Transferred ${amount:F2} from {fromAccount} to {toAccount}. Reference: {result.ReferenceId}";
    }
    catch (InsufficientFundsException)
    {
        return "Transfer failed: Insufficient funds in source account";
    }
}
```

## Kernel Anti-Patterns

### Shared Kernel Across Agents

Sharing a kernel instance causes plugin leakage between agents.

```csharp
// BAD: Shared kernel
var kernel = Kernel.CreateBuilder()
    .AddAzureOpenAIChatCompletion(...)
    .Build();

kernel.Plugins.AddFromObject(new AdminPlugin()); // All agents get admin!

var supportAgent = new ChatCompletionAgent { Kernel = kernel };
var salesAgent = new ChatCompletionAgent { Kernel = kernel };

// GOOD: Isolated kernels per agent
Kernel CreateAgentKernel(AgentRole role)
{
    var kernel = Kernel.CreateBuilder()
        .AddAzureOpenAIChatCompletion(...)
        .Build();

    switch (role)
    {
        case AgentRole.Support:
            kernel.Plugins.AddFromObject(new SupportPlugin());
            break;
        case AgentRole.Sales:
            kernel.Plugins.AddFromObject(new SalesPlugin());
            break;
    }

    return kernel;
}
```

### Not Disposing Kernels

Kernels hold resources that should be cleaned up, especially in request-scoped scenarios.

```csharp
// BAD: Kernel never disposed
public class ChatController
{
    public async Task<string> Chat(string message)
    {
        var kernel = Kernel.CreateBuilder()
            .AddAzureOpenAIChatCompletion(...)
            .Build();

        return (await kernel.InvokePromptAsync(message)).ToString();
    }
}

// GOOD: Use DI with proper scoping
public class ChatController
{
    private readonly Kernel _kernel;

    public ChatController(Kernel kernel) => _kernel = kernel;

    public async Task<string> Chat(string message)
    {
        return (await _kernel.InvokePromptAsync(message)).ToString();
    }
}

// Registration
services.AddKernel()
    .AddAzureOpenAIChatCompletion(...);
```

### Hardcoded API Keys

Never hardcode credentials in code.

```csharp
// BAD: Hardcoded credentials
builder.AddAzureOpenAIChatCompletion(
    "gpt-4",
    "https://myresource.openai.azure.com",
    "sk-abc123xyz..."); // Exposed in source control!

// GOOD: Configuration-based
builder.AddAzureOpenAIChatCompletion(
    config["AzureOpenAI:DeploymentName"]!,
    config["AzureOpenAI:Endpoint"]!,
    new DefaultAzureCredential()); // Or use Key Vault
```

## Function Calling Anti-Patterns

### Using Deprecated Planners

The old Planner APIs have been removed. Use FunctionChoiceBehavior instead.

```csharp
// BAD: Deprecated planner (removed in SK 1.x)
var planner = new StepwisePlanner(kernel);
var plan = await planner.CreatePlanAsync("...");

// GOOD: Modern function calling
var settings = new OpenAIPromptExecutionSettings
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

await kernel.InvokePromptAsync("...", new KernelArguments(settings));
```

### Not Handling Function Call Loops

Without limits, function calling can loop indefinitely.

```csharp
// BAD: Unbounded function calling
var settings = new OpenAIPromptExecutionSettings
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

// GOOD: Set maximum iterations
var settings = new OpenAIPromptExecutionSettings
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(
        options: new FunctionChoiceBehaviorOptions
        {
            AllowConcurrentInvocation = false,
            AllowParallelCalls = true
        })
};

// Also handle in chat loop
var maxIterations = 10;
var iteration = 0;
while (iteration++ < maxIterations)
{
    var response = await chatService.GetChatMessageContentAsync(history, settings, kernel);
    if (!response.Items.OfType<FunctionCallContent>().Any())
        break;
    // Process function calls...
}
```

### Exposing Dangerous Functions Without Safeguards

Some functions need additional authorization checks.

```csharp
// BAD: Dangerous function without safeguards
[KernelFunction]
[Description("Deletes a user account")]
public async Task<string> DeleteUser(string userId)
{
    await _userService.DeleteAsync(userId);
    return "User deleted";
}

// GOOD: Safeguards and confirmation
[KernelFunction]
[Description("Initiates user account deletion - requires confirmation")]
public async Task<string> RequestUserDeletion(
    Kernel kernel,
    [Description("User ID to delete")] string userId)
{
    var currentUser = kernel.Data["currentUserId"]?.ToString();

    // Can only delete own account via this function
    if (userId != currentUser)
        return "You can only request deletion of your own account";

    // Create pending deletion, don't actually delete
    var token = await _userService.CreateDeletionRequestAsync(userId);
    return $"Deletion request created. Confirm within 24 hours using token: {token}";
}
```

## Chat and Conversation Anti-Patterns

### Unbounded Chat History

Chat history grows unbounded, causing token limits and cost issues.

```csharp
// BAD: Never truncates history
public class ChatService
{
    private readonly ChatHistory _history = new();

    public async Task<string> Chat(string message)
    {
        _history.AddUserMessage(message);
        var response = await _chatService.GetChatMessageContentAsync(_history);
        _history.AddAssistantMessage(response.Content!);
        return response.Content!;
    }
}

// GOOD: Managed history with truncation
public class ChatService
{
    private const int MaxHistoryMessages = 20;
    private readonly ChatHistory _history = new();

    public async Task<string> Chat(string message)
    {
        _history.AddUserMessage(message);

        // Truncate old messages (keep system message)
        while (_history.Count > MaxHistoryMessages + 1)
        {
            var firstNonSystem = _history.Skip(1).First();
            _history.Remove(firstNonSystem);
        }

        var response = await _chatService.GetChatMessageContentAsync(_history);
        _history.AddAssistantMessage(response.Content!);
        return response.Content!;
    }
}
```

### Ignoring System Messages

System messages set behavior expectations. Missing them causes inconsistent responses.

```csharp
// BAD: No system message
var history = new ChatHistory();
history.AddUserMessage("Help me with my order");

// GOOD: Clear system context
var history = new ChatHistory();
history.AddSystemMessage("""
    You are a helpful customer service agent for Contoso Electronics.
    You can help with: order status, returns, product questions.
    You cannot: process payments, access financial data, make promises about pricing.
    Always be polite and concise.
    """);
history.AddUserMessage("Help me with my order");
```

### Not Handling Streaming Errors

Streaming responses can fail mid-stream.

```csharp
// BAD: No error handling for streaming
await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(history))
{
    Console.Write(chunk.Content);
}

// GOOD: Proper error handling
var fullResponse = new StringBuilder();
try
{
    await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(history))
    {
        fullResponse.Append(chunk.Content);
        await outputStream.WriteAsync(chunk.Content);
    }
    history.AddAssistantMessage(fullResponse.ToString());
}
catch (HttpOperationException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
{
    await outputStream.WriteAsync("\n[Rate limited - please try again shortly]");
    throw;
}
catch (Exception ex)
{
    _logger.LogError(ex, "Streaming failed after: {PartialResponse}", fullResponse.ToString());
    throw;
}
```

## Testing Anti-Patterns

### Testing Against Live LLM

Unit tests should not depend on external LLM services.

```csharp
// BAD: Tests hit live API
[Fact]
public async Task Chat_ReturnsResponse()
{
    var kernel = Kernel.CreateBuilder()
        .AddOpenAIChatCompletion("gpt-4", Environment.GetEnvironmentVariable("OPENAI_KEY")!)
        .Build();

    var response = await kernel.InvokePromptAsync("Say hello");
    Assert.NotEmpty(response.ToString());
}

// GOOD: Mock the chat service
[Fact]
public async Task Plugin_ReturnsExpectedFormat()
{
    var mockWeather = new Mock<IWeatherService>();
    mockWeather.Setup(w => w.GetCurrentAsync("Seattle"))
        .ReturnsAsync(new WeatherData { Temp = 55, Condition = "Cloudy" });

    var plugin = new WeatherPlugin(mockWeather.Object);

    var result = await plugin.GetWeather("Seattle", "fahrenheit");

    Assert.Contains("55", result);
    Assert.Contains("Cloudy", result);
}
```

### Testing Only Happy Paths

Test error conditions and edge cases.

```csharp
// Test error handling
[Theory]
[InlineData("")]
[InlineData(null)]
[InlineData("invalid-id-format")]
public async Task GetOrder_InvalidId_ReturnsError(string orderId)
{
    var plugin = new OrderPlugin(_mockOrderService.Object);

    var result = await plugin.GetOrder(orderId);

    Assert.StartsWith("Error:", result);
}

[Fact]
public async Task GetOrder_ServiceThrows_ReturnsGracefulError()
{
    _mockOrderService.Setup(s => s.GetAsync(It.IsAny<string>()))
        .ThrowsAsync(new TimeoutException());

    var plugin = new OrderPlugin(_mockOrderService.Object);

    var result = await plugin.GetOrder("ORD-123");

    Assert.Contains("temporarily unavailable", result.ToLower());
}
```

## Observability Anti-Patterns

### No Logging

Without logging, debugging AI behavior is impossible.

```csharp
// BAD: No visibility
await kernel.InvokePromptAsync(prompt);

// GOOD: Comprehensive logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Debug);
    logging.AddFilter("Microsoft.SemanticKernel", LogLevel.Debug);
});

// Log function invocations
kernel.FunctionInvocationFilters.Add(new LoggingFilter(_logger));

public class LoggingFilter : IFunctionInvocationFilter
{
    private readonly ILogger _logger;

    public LoggingFilter(ILogger logger) => _logger = logger;

    public async Task OnFunctionInvocationAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        _logger.LogInformation(
            "Invoking {Plugin}.{Function} with args: {Args}",
            context.Function.PluginName,
            context.Function.Name,
            JsonSerializer.Serialize(context.Arguments));

        var sw = Stopwatch.StartNew();
        await next(context);
        sw.Stop();

        _logger.LogInformation(
            "Completed {Plugin}.{Function} in {Elapsed}ms. Result: {Result}",
            context.Function.PluginName,
            context.Function.Name,
            sw.ElapsedMilliseconds,
            context.Result?.ToString()?.Truncate(200));
    }
}
```

### Not Tracking Token Usage

Token usage impacts cost and can hit limits unexpectedly.

```csharp
// GOOD: Track token usage
var response = await chatService.GetChatMessageContentAsync(history, settings, kernel);

if (response.Metadata?.TryGetValue("Usage", out var usage) == true)
{
    var usageData = usage as ChatCompletionUsage;
    _telemetry.TrackMetric("PromptTokens", usageData?.PromptTokens ?? 0);
    _telemetry.TrackMetric("CompletionTokens", usageData?.CompletionTokens ?? 0);
    _telemetry.TrackMetric("TotalTokens", usageData?.TotalTokens ?? 0);
}
```
