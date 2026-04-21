---
name: semantic-kernel
description: "Build AI-enabled .NET applications with Semantic Kernel using services, plugins, prompts, and function-calling patterns that remain testable and maintainable."
compatibility: "Requires Semantic Kernel 1.x packages (.NET 8+)."
---

# Semantic Kernel for .NET

## Trigger On

- adding AI-driven prompts, plugins, or orchestration to a .NET app
- reviewing kernel construction, service registration, or plugin usage
- building function-calling patterns with LLMs
- migrating older Semantic Kernel code to current APIs

## Documentation

- [Semantic Kernel Overview](https://learn.microsoft.com/en-us/semantic-kernel/overview/)
- [Plugins and Functions](https://learn.microsoft.com/en-us/semantic-kernel/concepts/plugins/)
- [Agent Functions](https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-functions)
- [GitHub Repository](https://github.com/microsoft/semantic-kernel)
- [Microsoft Agent Framework](https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/)

### References

- [patterns.md](references/patterns.md) - Plugin patterns, function calling patterns, multi-agent patterns, prompt templates, and RAG patterns
- [anti-patterns.md](references/anti-patterns.md) - Common Semantic Kernel mistakes and how to avoid them

## Core Concepts

| Concept | Description |
|---------|-------------|
| **Kernel** | Central orchestrator for AI services and plugins |
| **Plugin** | Collection of functions exposed to the LLM |
| **Function** | Native C# method or prompt template |
| **Chat Completion** | LLM service for generating responses |
| **Memory** | Vector storage for semantic search |

## Workflow

1. **Build the Kernel** with required services
2. **Create Plugins** with well-described functions
3. **Configure Function Calling** for automatic tool use
4. **Handle Responses** and manage conversation state
5. **Test and Observe** AI behavior with logging

## Kernel Setup

### Basic Configuration
```csharp
var builder = Kernel.CreateBuilder();

builder.AddAzureOpenAIChatCompletion(
    deploymentName: "gpt-4",
    endpoint: config["AzureOpenAI:Endpoint"]!,
    apiKey: config["AzureOpenAI:ApiKey"]!);

// Or OpenAI
builder.AddOpenAIChatCompletion(
    modelId: "gpt-4",
    apiKey: config["OpenAI:ApiKey"]!);

var kernel = builder.Build();
```

### With Dependency Injection
```csharp
builder.Services.AddKernel()
    .AddAzureOpenAIChatCompletion(
        deploymentName: "gpt-4",
        endpoint: config["AzureOpenAI:Endpoint"]!,
        apiKey: config["AzureOpenAI:ApiKey"]!);

// Register plugins
builder.Services.AddSingleton<WeatherPlugin>();
builder.Services.AddSingleton<OrderPlugin>();

// In your service
public class AiService(Kernel kernel)
{
    public async Task<string> ChatAsync(string message)
    {
        var response = await kernel.InvokePromptAsync(message);
        return response.ToString();
    }
}
```

## Plugin Patterns

### Creating a Plugin
```csharp
public class WeatherPlugin
{
    [KernelFunction]
    [Description("Gets the current weather for a specified city")]
    public async Task<string> GetWeather(
        [Description("The city name, e.g., 'Seattle'")] string city,
        [Description("Temperature unit: 'celsius' or 'fahrenheit'")] string unit = "celsius")
    {
        // Call actual weather API
        var weather = await _weatherService.GetCurrentAsync(city);
        return $"Weather in {city}: {weather.Temperature}° {unit}, {weather.Condition}";
    }

    [KernelFunction]
    [Description("Gets the weather forecast for the next N days")]
    public async Task<string> GetForecast(
        [Description("The city name")] string city,
        [Description("Number of days (1-7)")] int days = 3)
    {
        var forecast = await _weatherService.GetForecastAsync(city, days);
        return FormatForecast(forecast);
    }
}
```

### Plugin Best Practices

| Practice | Why It Matters |
|----------|----------------|
| Clear `[Description]` | LLM uses this to decide when to call |
| Specific parameter names | Helps LLM map user intent |
| Idempotent functions | Safe to retry on failures |
| Return meaningful strings | LLM needs to understand results |
| Validate inputs | LLM may hallucinate parameters |

## Function Calling

### Automatic Function Calling
```csharp
var settings = new OpenAIPromptExecutionSettings
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

kernel.Plugins.AddFromObject(new WeatherPlugin(), "Weather");
kernel.Plugins.AddFromObject(new OrderPlugin(), "Orders");

var result = await kernel.InvokePromptAsync(
    "What's the weather in Seattle and do I have any pending orders?",
    new KernelArguments(settings));
```

### Manual Function Selection
```csharp
var settings = new OpenAIPromptExecutionSettings
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Required(
        [kernel.Plugins["Weather"]["GetWeather"]])
};
```

## Chat Completion Patterns

### Multi-Turn Conversation
```csharp
var chatService = kernel.GetRequiredService<IChatCompletionService>();
var history = new ChatHistory();

history.AddSystemMessage("You are a helpful assistant.");
history.AddUserMessage(userMessage);

var response = await chatService.GetChatMessageContentAsync(
    history,
    executionSettings: new OpenAIPromptExecutionSettings
    {
        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
    },
    kernel: kernel);

history.AddAssistantMessage(response.Content!);
```

### Streaming Response
```csharp
await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(
    history, executionSettings, kernel))
{
    Console.Write(chunk.Content);
}
```

## Multi-Agent Plugin Isolation

```csharp
// WRONG - agents share plugins
var sharedKernel = Kernel.CreateBuilder().Build();
sharedKernel.Plugins.AddFromObject(new AllPlugins());

var agent1 = new ChatCompletionAgent { Kernel = sharedKernel };
var agent2 = new ChatCompletionAgent { Kernel = sharedKernel };
// Both agents have same plugins!

// CORRECT - isolated kernels
var kernel1 = CreateKernelForAgent1();
kernel1.Plugins.AddFromObject(new WeatherPlugin());

var kernel2 = CreateKernelForAgent2();
kernel2.Plugins.AddFromObject(new OrderPlugin());

var agent1 = new ChatCompletionAgent { Kernel = kernel1 };
var agent2 = new ChatCompletionAgent { Kernel = kernel2 };
```

## Anti-Patterns to Avoid

| Anti-Pattern | Why It's Bad | Better Approach |
|--------------|--------------|-----------------|
| Vague `[Description]` | LLM won't call at right time | Be specific and actionable |
| Sharing kernel across agents | Plugin leakage | Clone or create new kernels |
| No input validation | Hallucinated parameters | Validate and return errors |
| Using deprecated Planners | Removed in favor of function calling | Use `FunctionChoiceBehavior` |
| Ignoring logging | Can't debug AI decisions | Enable Semantic Kernel logging |

## Error Handling

```csharp
[KernelFunction]
[Description("Places an order for a product")]
public async Task<string> PlaceOrder(
    [Description("Product ID")] string productId,
    [Description("Quantity (1-100)")] int quantity)
{
    // Validate inputs
    if (string.IsNullOrEmpty(productId))
        return "Error: Product ID is required";

    if (quantity < 1 || quantity > 100)
        return "Error: Quantity must be between 1 and 100";

    try
    {
        var order = await _orderService.CreateAsync(productId, quantity);
        return $"Order {order.Id} placed successfully for {quantity} units";
    }
    catch (ProductNotFoundException)
    {
        return $"Error: Product '{productId}' not found";
    }
}
```

## Testing Plugins

```csharp
[Fact]
public async Task GetWeather_ReturnsFormattedWeather()
{
    var mockWeatherService = new Mock<IWeatherService>();
    mockWeatherService.Setup(w => w.GetCurrentAsync("Seattle"))
        .ReturnsAsync(new Weather { Temperature = 20, Condition = "Sunny" });

    var plugin = new WeatherPlugin(mockWeatherService.Object);

    var result = await plugin.GetWeather("Seattle", "celsius");

    Assert.Contains("20°", result);
    Assert.Contains("Sunny", result);
}
```

## Microsoft Agent Framework

For complex multi-agent scenarios, consider `microsoft-agent-framework`:
- Multi-agent orchestration
- Agent-to-agent communication
- Enterprise patterns

## Deliver

- kernel setup with clear service and plugin composition
- AI features that fit naturally into the existing .NET app
- observable and testable function-calling behavior
- proper plugin isolation for multi-agent scenarios

## Validate

- plugins have clear, specific descriptions
- function calling works as expected
- AI flows are logged and debuggable
- input validation prevents hallucination issues
- kernel instances are properly scoped
- deprecated APIs are not used
