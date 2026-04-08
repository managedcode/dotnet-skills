# Azure Functions Patterns

## Isolated Worker Model Patterns

### Dependency Injection with Scoped Services

```csharp
// Program.cs - Register services with appropriate lifetimes
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        // Singleton - shared across all function invocations
        services.AddSingleton<ICacheService, RedisCacheService>();

        // Scoped - one instance per function invocation
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Transient - new instance each time requested
        services.AddTransient<IEmailBuilder, EmailBuilder>();

        // Options pattern for configuration
        services.Configure<StorageOptions>(
            hostContext.Configuration.GetSection("Storage"));
    })
    .Build();
```

### Request/Response Pipeline with Middleware

```csharp
// Full middleware pipeline setup
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(builder =>
    {
        // Order matters - executed in registration order
        builder.UseMiddleware<ExceptionHandlingMiddleware>();
        builder.UseMiddleware<RequestLoggingMiddleware>();
        builder.UseMiddleware<AuthenticationMiddleware>();
        builder.UseMiddleware<CorrelationIdMiddleware>();
    })
    .Build();

// Exception handling middleware
public class ExceptionHandlingMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Validation error in {Function}", context.FunctionDefinition.Name);
            await WriteErrorResponse(context, StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in {Function}", context.FunctionDefinition.Name);
            await WriteErrorResponse(context, StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    private static async Task WriteErrorResponse(FunctionContext context, int statusCode, string message)
    {
        var httpReqData = await context.GetHttpRequestDataAsync();
        if (httpReqData != null)
        {
            var response = httpReqData.CreateResponse((HttpStatusCode)statusCode);
            await response.WriteAsJsonAsync(new { error = message });
            context.GetInvocationResult().Value = response;
        }
    }
}
```

### Output Bindings with Multiple Outputs

```csharp
public class MultiOutputFunctions
{
    [Function("ProcessOrder")]
    public async Task<MultiOutput> ProcessOrder(
        [QueueTrigger("orders", Connection = "StorageConnection")] Order order,
        FunctionContext context)
    {
        var logger = context.GetLogger<MultiOutputFunctions>();
        logger.LogInformation("Processing order {OrderId}", order.Id);

        var confirmation = new OrderConfirmation
        {
            OrderId = order.Id,
            ProcessedAt = DateTime.UtcNow
        };

        return new MultiOutput
        {
            Confirmation = confirmation,
            NotificationMessage = $"Order {order.Id} processed"
        };
    }
}

public class MultiOutput
{
    [QueueOutput("confirmations", Connection = "StorageConnection")]
    public OrderConfirmation Confirmation { get; set; }

    [QueueOutput("notifications", Connection = "StorageConnection")]
    public string NotificationMessage { get; set; }
}
```

### Typed Configuration with Validation

```csharp
// Program.cs
services.AddOptions<ServiceBusOptions>()
    .Bind(configuration.GetSection("ServiceBus"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Options class with validation
public class ServiceBusOptions
{
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    [Required]
    public string QueueName { get; set; } = string.Empty;

    [Range(1, 100)]
    public int MaxConcurrentCalls { get; set; } = 10;
}

// Function using validated options
public class ServiceBusFunctions(IOptions<ServiceBusOptions> options, ILogger<ServiceBusFunctions> logger)
{
    [Function("ProcessServiceBusMessage")]
    public async Task ProcessMessage(
        [ServiceBusTrigger("%ServiceBus:QueueName%", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message)
    {
        logger.LogInformation("Processing message with max concurrency: {Max}",
            options.Value.MaxConcurrentCalls);
    }
}
```

---

## Durable Functions Patterns

### Sub-Orchestrations for Modularity

```csharp
[Function(nameof(MainOrchestrator))]
public static async Task<OrderResult> MainOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    var order = context.GetInput<Order>();

    // Validate order in sub-orchestration
    var isValid = await context.CallSubOrchestratorAsync<bool>(
        nameof(ValidationOrchestrator),
        order);

    if (!isValid)
    {
        return new OrderResult { Status = "ValidationFailed" };
    }

    // Process payment in sub-orchestration with retry
    var paymentResult = await context.CallSubOrchestratorAsync<PaymentResult>(
        nameof(PaymentOrchestrator),
        order.Payment,
        new TaskOptions { Retry = new RetryPolicy(3, TimeSpan.FromSeconds(5)) });

    // Ship order in sub-orchestration
    var shipmentResult = await context.CallSubOrchestratorAsync<ShipmentResult>(
        nameof(ShippingOrchestrator),
        new ShippingRequest { OrderId = order.Id, Address = order.ShippingAddress });

    return new OrderResult
    {
        Status = "Completed",
        PaymentId = paymentResult.TransactionId,
        TrackingNumber = shipmentResult.TrackingNumber
    };
}

[Function(nameof(ValidationOrchestrator))]
public static async Task<bool> ValidationOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    var order = context.GetInput<Order>();

    // Parallel validation checks
    var inventoryTask = context.CallActivityAsync<bool>(nameof(CheckInventory), order.Items);
    var fraudTask = context.CallActivityAsync<bool>(nameof(CheckFraud), order.Customer);
    var creditTask = context.CallActivityAsync<bool>(nameof(CheckCredit), order.Payment);

    var results = await Task.WhenAll(inventoryTask, fraudTask, creditTask);
    return results.All(r => r);
}
```

### Eternal Orchestrations for Long-Running Processes

```csharp
[Function(nameof(MonitorOrchestrator))]
public static async Task MonitorOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    var config = context.GetInput<MonitorConfig>();

    // Check status
    var status = await context.CallActivityAsync<ServiceStatus>(
        nameof(CheckServiceStatus),
        config.ServiceEndpoint);

    if (status.IsHealthy)
    {
        // Wait before next check
        await context.CreateTimer(
            context.CurrentUtcDateTime.AddMinutes(config.CheckIntervalMinutes),
            CancellationToken.None);
    }
    else
    {
        // Alert and wait shorter interval
        await context.CallActivityAsync(nameof(SendAlert), new Alert
        {
            Service = config.ServiceName,
            Status = status,
            Timestamp = context.CurrentUtcDateTime
        });

        await context.CreateTimer(
            context.CurrentUtcDateTime.AddMinutes(1),
            CancellationToken.None);
    }

    // Continue as new to prevent history growth
    context.ContinueAsNew(config);
}
```

### Aggregator Pattern with Entity Functions

```csharp
// Entity definition
[Function(nameof(CounterEntity))]
public static Task CounterEntity(
    [EntityTrigger] TaskEntityDispatcher dispatcher)
{
    return dispatcher.DispatchAsync<Counter>();
}

public class Counter
{
    public int Value { get; set; }

    public void Add(int amount) => Value += amount;
    public void Reset() => Value = 0;
    public int Get() => Value;
}

// Using entity from orchestrator
[Function(nameof(AggregatorOrchestrator))]
public static async Task<int> AggregatorOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    var entityId = new EntityInstanceId(nameof(CounterEntity), "global-counter");

    // Signal entity (fire and forget)
    await context.Entities.SignalEntityAsync(entityId, "Add", 5);

    // Call entity and get result
    var currentValue = await context.Entities.CallEntityAsync<int>(entityId, "Get");

    return currentValue;
}
```

### Saga Pattern with Compensation

```csharp
[Function(nameof(SagaOrchestrator))]
public static async Task<SagaResult> SagaOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    var request = context.GetInput<BookingRequest>();
    var compensations = new Stack<Func<Task>>();

    try
    {
        // Step 1: Reserve flight
        var flightReservation = await context.CallActivityAsync<FlightReservation>(
            nameof(ReserveFlight), request.Flight);
        compensations.Push(() => context.CallActivityAsync(nameof(CancelFlight), flightReservation.Id));

        // Step 2: Reserve hotel
        var hotelReservation = await context.CallActivityAsync<HotelReservation>(
            nameof(ReserveHotel), request.Hotel);
        compensations.Push(() => context.CallActivityAsync(nameof(CancelHotel), hotelReservation.Id));

        // Step 3: Reserve car
        var carReservation = await context.CallActivityAsync<CarReservation>(
            nameof(ReserveCar), request.Car);
        compensations.Push(() => context.CallActivityAsync(nameof(CancelCar), carReservation.Id));

        // Step 4: Charge payment
        var payment = await context.CallActivityAsync<PaymentConfirmation>(
            nameof(ProcessPayment), new PaymentRequest
            {
                Amount = flightReservation.Price + hotelReservation.Price + carReservation.Price,
                CustomerId = request.CustomerId
            });

        return new SagaResult
        {
            Success = true,
            FlightConfirmation = flightReservation.ConfirmationNumber,
            HotelConfirmation = hotelReservation.ConfirmationNumber,
            CarConfirmation = carReservation.ConfirmationNumber
        };
    }
    catch (Exception ex)
    {
        // Compensate in reverse order
        while (compensations.Count > 0)
        {
            var compensation = compensations.Pop();
            try
            {
                await compensation();
            }
            catch (Exception compEx)
            {
                // Log compensation failure but continue
                context.SetCustomStatus($"Compensation failed: {compEx.Message}");
            }
        }

        return new SagaResult
        {
            Success = false,
            FailureReason = ex.Message
        };
    }
}
```

### Retry Policies and Error Handling

```csharp
[Function(nameof(ResilientOrchestrator))]
public static async Task<ProcessingResult> ResilientOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    var input = context.GetInput<ProcessingInput>();

    // Retry policy for transient failures
    var retryPolicy = new RetryPolicy(
        maxNumberOfAttempts: 5,
        firstRetryInterval: TimeSpan.FromSeconds(1),
        backoffCoefficient: 2.0,
        maxRetryInterval: TimeSpan.FromMinutes(1));

    try
    {
        // Call with retry
        var result = await context.CallActivityAsync<string>(
            nameof(UnreliableActivity),
            input.Data,
            new TaskOptions { Retry = retryPolicy });

        return new ProcessingResult { Success = true, Data = result };
    }
    catch (TaskFailedException ex) when (ex.FailureDetails?.ErrorType == "TransientException")
    {
        // Handle specific transient failure after retries exhausted
        await context.CallActivityAsync(nameof(NotifyOperations), new OperationsAlert
        {
            Message = "Activity failed after retries",
            Exception = ex.Message
        });

        return new ProcessingResult { Success = false, Error = "Service temporarily unavailable" };
    }
    catch (TaskFailedException ex)
    {
        // Handle permanent failure
        return new ProcessingResult { Success = false, Error = ex.Message };
    }
}

[Function(nameof(UnreliableActivity))]
public static async Task<string> UnreliableActivity(
    [ActivityTrigger] string input,
    FunctionContext context)
{
    var logger = context.GetLogger(nameof(UnreliableActivity));

    // Simulate transient failure
    if (Random.Shared.NextDouble() < 0.3)
    {
        throw new TransientException("Temporary service unavailable");
    }

    return await ProcessData(input);
}
```

### Timer-Based Scheduling in Orchestrations

```csharp
[Function(nameof(ScheduledOrchestrator))]
public static async Task ScheduledOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    var schedule = context.GetInput<ProcessingSchedule>();

    foreach (var scheduledTime in schedule.Times)
    {
        // Wait until scheduled time
        var fireAt = scheduledTime;
        if (fireAt > context.CurrentUtcDateTime)
        {
            await context.CreateTimer(fireAt, CancellationToken.None);
        }

        // Execute scheduled work
        await context.CallActivityAsync(nameof(ScheduledWork), new WorkItem
        {
            ScheduledFor = scheduledTime,
            ActualStart = context.CurrentUtcDateTime
        });
    }
}

[Function(nameof(BatchProcessingOrchestrator))]
public static async Task BatchProcessingOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    var config = context.GetInput<BatchConfig>();
    var processedCount = 0;

    while (processedCount < config.TotalItems)
    {
        // Process batch
        var batchSize = Math.Min(config.BatchSize, config.TotalItems - processedCount);
        await context.CallActivityAsync(nameof(ProcessBatch), new BatchRequest
        {
            StartIndex = processedCount,
            Count = batchSize
        });

        processedCount += batchSize;

        // Delay between batches to avoid throttling
        if (processedCount < config.TotalItems)
        {
            await context.CreateTimer(
                context.CurrentUtcDateTime.AddSeconds(config.DelayBetweenBatchesSeconds),
                CancellationToken.None);
        }
    }
}
```

---

## Advanced Binding Patterns

### Blob Input/Output with Metadata

```csharp
public class BlobFunctions(ILogger<BlobFunctions> logger)
{
    [Function("ProcessBlob")]
    [BlobOutput("processed/{name}", Connection = "StorageConnection")]
    public async Task<byte[]> ProcessBlob(
        [BlobTrigger("uploads/{name}", Connection = "StorageConnection")]
        BlobClient blobClient,
        string name,
        FunctionContext context)
    {
        logger.LogInformation("Processing blob: {Name}", name);

        // Read blob content
        var downloadResult = await blobClient.DownloadContentAsync();
        var content = downloadResult.Value.Content.ToArray();

        // Get blob properties
        var properties = await blobClient.GetPropertiesAsync();
        logger.LogInformation("Blob size: {Size}, Content-Type: {ContentType}",
            properties.Value.ContentLength,
            properties.Value.ContentType);

        // Process and return for output binding
        return TransformContent(content);
    }
}
```

### Cosmos DB with Partition Key Routing

```csharp
public class CosmosDbFunctions(ILogger<CosmosDbFunctions> logger)
{
    [Function("ProcessCosmosDocument")]
    [CosmosDBOutput(
        databaseName: "MyDatabase",
        containerName: "ProcessedItems",
        Connection = "CosmosDBConnection",
        PartitionKey = "/category")]
    public ProcessedItem ProcessDocument(
        [CosmosDBTrigger(
            databaseName: "MyDatabase",
            containerName: "Items",
            Connection = "CosmosDBConnection",
            LeaseContainerName = "leases",
            CreateLeaseContainerIfNotExists = true)]
        IReadOnlyList<MyDocument> documents)
    {
        logger.LogInformation("Processing {Count} documents", documents.Count);

        var processed = documents.Select(doc => new ProcessedItem
        {
            Id = Guid.NewGuid().ToString(),
            OriginalId = doc.Id,
            Category = doc.Category, // Partition key
            ProcessedAt = DateTime.UtcNow,
            Data = TransformData(doc)
        });

        // Return for output binding
        return processed.First();
    }
}
```

### Event Grid with CloudEvents

```csharp
public class EventGridFunctions(ILogger<EventGridFunctions> logger)
{
    [Function("HandleCloudEvent")]
    public async Task HandleCloudEvent(
        [EventGridTrigger] CloudEvent cloudEvent)
    {
        logger.LogInformation("Received event: Type={Type}, Source={Source}",
            cloudEvent.Type,
            cloudEvent.Source);

        // Deserialize event data
        var data = cloudEvent.Data?.ToObjectFromJson<MyEventData>();

        if (data != null)
        {
            await ProcessEventData(data);
        }
    }

    [Function("PublishEvent")]
    [EventGridOutput(TopicEndpointUri = "EventGridTopicUri", TopicKeySetting = "EventGridTopicKey")]
    public EventGridEvent PublishEvent(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
    {
        return new EventGridEvent(
            subject: "myapp/items/created",
            eventType: "ItemCreated",
            dataVersion: "1.0",
            data: new { ItemId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow });
    }
}
```
