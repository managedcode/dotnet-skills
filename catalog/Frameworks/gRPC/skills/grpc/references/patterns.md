# gRPC Patterns Reference

This document provides detailed patterns for gRPC services in .NET, covering proto design, streaming implementations, and interceptor patterns.

## Proto File Patterns

### Service Definition Best Practices

```protobuf
syntax = "proto3";

package mycompany.orders.v1;

option csharp_namespace = "MyCompany.Orders.V1";

import "google/protobuf/timestamp.proto";
import "google/protobuf/wrappers.proto";
import "google/protobuf/empty.proto";

// Version services explicitly for breaking changes
service OrderService {
  // Unary RPC for simple request-response
  rpc CreateOrder (CreateOrderRequest) returns (CreateOrderResponse);

  // Server streaming for large result sets
  rpc ListOrders (ListOrdersRequest) returns (stream OrderItem);

  // Client streaming for batch uploads
  rpc UploadOrders (stream CreateOrderRequest) returns (UploadOrdersResponse);

  // Bidirectional streaming for real-time sync
  rpc SyncOrders (stream OrderSyncMessage) returns (stream OrderSyncMessage);
}
```

### Message Design Patterns

```protobuf
// Use wrapper types for optional primitives
message OrderFilter {
  google.protobuf.StringValue customer_id = 1;
  google.protobuf.Int32Value min_quantity = 2;
  google.protobuf.BoolValue is_active = 3;
}

// Wrap request fields in objects for extensibility
message CreateOrderRequest {
  OrderDetails order = 1;
  RequestMetadata metadata = 2;
}

message OrderDetails {
  string customer_id = 1;
  repeated OrderLineItem items = 2;
  ShippingAddress address = 3;
}

// Use oneof for mutually exclusive fields
message PaymentMethod {
  oneof method {
    CreditCard credit_card = 1;
    BankTransfer bank_transfer = 2;
    DigitalWallet digital_wallet = 3;
  }
}

// Reserve removed field numbers to prevent reuse
message Order {
  string id = 1;
  string customer_id = 2;
  // Field 3 was deprecated_field
  reserved 3;
  reserved "deprecated_field";
  OrderStatus status = 4;
}

// Use enums with explicit zero value as unknown/default
enum OrderStatus {
  ORDER_STATUS_UNSPECIFIED = 0;
  ORDER_STATUS_PENDING = 1;
  ORDER_STATUS_CONFIRMED = 2;
  ORDER_STATUS_SHIPPED = 3;
  ORDER_STATUS_DELIVERED = 4;
  ORDER_STATUS_CANCELLED = 5;
}
```

### Pagination Pattern

```protobuf
message ListOrdersRequest {
  int32 page_size = 1;
  string page_token = 2;
  OrderFilter filter = 3;
}

message ListOrdersResponse {
  repeated Order orders = 1;
  string next_page_token = 2;
  int32 total_count = 3;
}
```

## Streaming Patterns

### Server Streaming with Backpressure

```csharp
public override async Task StreamOrders(
    ListOrdersRequest request,
    IServerStreamWriter<Order> responseStream,
    ServerCallContext context)
{
    var batchSize = 100;
    var lastId = string.Empty;

    while (!context.CancellationToken.IsCancellationRequested)
    {
        var orders = await _repository.GetOrdersBatchAsync(
            lastId,
            batchSize,
            context.CancellationToken);

        if (orders.Count == 0)
            break;

        foreach (var order in orders)
        {
            // WriteAsync handles backpressure automatically
            await responseStream.WriteAsync(order, context.CancellationToken);
            lastId = order.Id;
        }

        // Optional: yield control to prevent starvation
        await Task.Yield();
    }
}
```

### Client Streaming with Batching

```csharp
public override async Task<UploadOrdersResponse> UploadOrders(
    IAsyncStreamReader<CreateOrderRequest> requestStream,
    ServerCallContext context)
{
    var batch = new List<CreateOrderRequest>();
    var batchSize = 100;
    var totalProcessed = 0;
    var errors = new List<string>();

    await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
    {
        batch.Add(request);

        if (batch.Count >= batchSize)
        {
            var result = await ProcessBatchAsync(batch, context.CancellationToken);
            totalProcessed += result.SuccessCount;
            errors.AddRange(result.Errors);
            batch.Clear();
        }
    }

    // Process remaining items
    if (batch.Count > 0)
    {
        var result = await ProcessBatchAsync(batch, context.CancellationToken);
        totalProcessed += result.SuccessCount;
        errors.AddRange(result.Errors);
    }

    return new UploadOrdersResponse
    {
        ProcessedCount = totalProcessed,
        ErrorCount = errors.Count,
        Errors = { errors.Take(10) }
    };
}
```

### Bidirectional Streaming with Concurrent Processing

```csharp
public override async Task SyncOrders(
    IAsyncStreamReader<OrderSyncMessage> requestStream,
    IServerStreamWriter<OrderSyncMessage> responseStream,
    ServerCallContext context)
{
    var pendingResponses = Channel.CreateUnbounded<OrderSyncMessage>();

    // Writer task - sends responses as they become available
    var writerTask = Task.Run(async () =>
    {
        await foreach (var response in pendingResponses.Reader.ReadAllAsync(context.CancellationToken))
        {
            await responseStream.WriteAsync(response, context.CancellationToken);
        }
    }, context.CancellationToken);

    // Reader task - processes incoming messages concurrently
    try
    {
        await foreach (var message in requestStream.ReadAllAsync(context.CancellationToken))
        {
            // Process each message asynchronously
            _ = ProcessAndQueueResponseAsync(message, pendingResponses.Writer, context.CancellationToken);
        }
    }
    finally
    {
        pendingResponses.Writer.Complete();
        await writerTask;
    }
}

private async Task ProcessAndQueueResponseAsync(
    OrderSyncMessage message,
    ChannelWriter<OrderSyncMessage> writer,
    CancellationToken ct)
{
    try
    {
        var response = await ProcessSyncMessageAsync(message, ct);
        await writer.WriteAsync(response, ct);
    }
    catch (Exception ex)
    {
        await writer.WriteAsync(new OrderSyncMessage
        {
            CorrelationId = message.CorrelationId,
            Error = ex.Message
        }, ct);
    }
}
```

### Streaming with Heartbeats

```csharp
public override async Task Subscribe(
    SubscribeRequest request,
    IServerStreamWriter<Event> responseStream,
    ServerCallContext context)
{
    var subscription = await _eventBus.SubscribeAsync(request.Topic, context.CancellationToken);
    var heartbeatInterval = TimeSpan.FromSeconds(30);
    var lastActivity = DateTime.UtcNow;

    using var heartbeatTimer = new PeriodicTimer(heartbeatInterval);

    // Start heartbeat task
    var heartbeatTask = Task.Run(async () =>
    {
        while (await heartbeatTimer.WaitForNextTickAsync(context.CancellationToken))
        {
            if (DateTime.UtcNow - lastActivity > heartbeatInterval)
            {
                await responseStream.WriteAsync(new Event { IsHeartbeat = true }, context.CancellationToken);
            }
        }
    }, context.CancellationToken);

    try
    {
        await foreach (var evt in subscription.ReadAllAsync(context.CancellationToken))
        {
            await responseStream.WriteAsync(evt, context.CancellationToken);
            lastActivity = DateTime.UtcNow;
        }
    }
    finally
    {
        await heartbeatTask;
    }
}
```

## Interceptor Patterns

### Authentication Interceptor (Server)

```csharp
public class AuthenticationInterceptor : Interceptor
{
    private readonly ITokenValidator _tokenValidator;
    private readonly ILogger<AuthenticationInterceptor> _logger;

    // Methods that don't require authentication
    private static readonly HashSet<string> AnonymousMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "/grpc.health.v1.Health/Check",
        "/mycompany.auth.v1.AuthService/Login"
    };

    public AuthenticationInterceptor(
        ITokenValidator tokenValidator,
        ILogger<AuthenticationInterceptor> logger)
    {
        _tokenValidator = tokenValidator;
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        await ValidateAuthenticationAsync(context);
        return await continuation(request, context);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await ValidateAuthenticationAsync(context);
        await continuation(request, responseStream, context);
    }

    private async Task ValidateAuthenticationAsync(ServerCallContext context)
    {
        if (AnonymousMethods.Contains(context.Method))
            return;

        var authHeader = context.RequestHeaders.GetValue("authorization");
        if (string.IsNullOrEmpty(authHeader))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Missing authorization header"));
        }

        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid authorization scheme"));
        }

        var token = authHeader.Substring(7);
        var principal = await _tokenValidator.ValidateTokenAsync(token, context.CancellationToken);

        if (principal == null)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid or expired token"));
        }

        // Store principal for downstream use
        context.UserState["ClaimsPrincipal"] = principal;
    }
}
```

### Authentication Interceptor (Client)

```csharp
public class ClientAuthInterceptor : Interceptor
{
    private readonly ITokenProvider _tokenProvider;

    public ClientAuthInterceptor(ITokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var newContext = AddAuthHeader(context);
        return continuation(request, newContext);
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var newContext = AddAuthHeader(context);
        return continuation(request, newContext);
    }

    private ClientInterceptorContext<TRequest, TResponse> AddAuthHeader<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context)
        where TRequest : class
        where TResponse : class
    {
        var token = _tokenProvider.GetToken();
        if (string.IsNullOrEmpty(token))
            return context;

        var headers = context.Options.Headers ?? new Metadata();
        headers.Add("authorization", $"Bearer {token}");

        return new ClientInterceptorContext<TRequest, TResponse>(
            context.Method,
            context.Host,
            context.Options.WithHeaders(headers));
    }
}
```

### Retry Interceptor with Circuit Breaker

```csharp
public class RetryInterceptor : Interceptor
{
    private readonly ILogger<RetryInterceptor> _logger;
    private readonly RetryPolicy _policy;

    private static readonly HashSet<StatusCode> RetryableStatusCodes = new()
    {
        StatusCode.Unavailable,
        StatusCode.Aborted,
        StatusCode.DeadlineExceeded,
        StatusCode.ResourceExhausted
    };

    public RetryInterceptor(ILogger<RetryInterceptor> logger, RetryPolicy policy)
    {
        _logger = logger;
        _policy = policy;
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var call = continuation(request, context);

        return new AsyncUnaryCall<TResponse>(
            RetryAsync(call.ResponseAsync, request, context, continuation),
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    private async Task<TResponse> RetryAsync<TRequest, TResponse>(
        Task<TResponse> responseTask,
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        where TRequest : class
        where TResponse : class
    {
        var attempt = 0;
        var delay = _policy.InitialBackoff;

        while (true)
        {
            try
            {
                return await responseTask;
            }
            catch (RpcException ex) when (ShouldRetry(ex, attempt))
            {
                attempt++;
                _logger.LogWarning(
                    "Retry attempt {Attempt} for {Method} after {Status}",
                    attempt, context.Method.FullName, ex.StatusCode);

                await Task.Delay(delay);
                delay = TimeSpan.FromTicks(Math.Min(
                    (long)(delay.Ticks * _policy.BackoffMultiplier),
                    _policy.MaxBackoff.Ticks));

                var call = continuation(request, context);
                responseTask = call.ResponseAsync;
            }
        }
    }

    private bool ShouldRetry(RpcException ex, int attempt)
    {
        return attempt < _policy.MaxAttempts
            && RetryableStatusCodes.Contains(ex.StatusCode);
    }
}

public record RetryPolicy(
    int MaxAttempts = 3,
    TimeSpan InitialBackoff = default,
    TimeSpan MaxBackoff = default,
    double BackoffMultiplier = 2.0)
{
    public TimeSpan InitialBackoff { get; init; } = InitialBackoff == default
        ? TimeSpan.FromMilliseconds(100)
        : InitialBackoff;

    public TimeSpan MaxBackoff { get; init; } = MaxBackoff == default
        ? TimeSpan.FromSeconds(5)
        : MaxBackoff;
}
```

### Validation Interceptor

```csharp
public class ValidationInterceptor : Interceptor
{
    private readonly IServiceProvider _serviceProvider;

    public ValidationInterceptor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        await ValidateRequestAsync(request, context.CancellationToken);
        return await continuation(request, context);
    }

    private async Task ValidateRequestAsync<TRequest>(TRequest request, CancellationToken ct)
    {
        var validator = _serviceProvider.GetService<IValidator<TRequest>>();
        if (validator == null)
            return;

        var result = await validator.ValidateAsync(request, ct);
        if (!result.IsValid)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.ErrorMessage));
            throw new RpcException(new Status(StatusCode.InvalidArgument, errors));
        }
    }
}
```

### Metrics Interceptor

```csharp
public class MetricsInterceptor : Interceptor
{
    private readonly IMeterFactory _meterFactory;
    private readonly Histogram<double> _requestDuration;
    private readonly Counter<long> _requestCount;

    public MetricsInterceptor(IMeterFactory meterFactory)
    {
        _meterFactory = meterFactory;
        var meter = _meterFactory.Create("GrpcServer");

        _requestDuration = meter.CreateHistogram<double>(
            "grpc.server.request.duration",
            unit: "ms",
            description: "Duration of gRPC requests");

        _requestCount = meter.CreateCounter<long>(
            "grpc.server.request.count",
            description: "Number of gRPC requests");
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var sw = Stopwatch.StartNew();
        var status = StatusCode.OK;

        try
        {
            return await continuation(request, context);
        }
        catch (RpcException ex)
        {
            status = ex.StatusCode;
            throw;
        }
        finally
        {
            sw.Stop();
            RecordMetrics(context.Method, status, sw.Elapsed.TotalMilliseconds);
        }
    }

    private void RecordMetrics(string method, StatusCode status, double duration)
    {
        var tags = new TagList
        {
            { "grpc.method", method },
            { "grpc.status_code", status.ToString() }
        };

        _requestDuration.Record(duration, tags);
        _requestCount.Add(1, tags);
    }
}
```

## Deadline Propagation

```csharp
public class DeadlinePropagationInterceptor : Interceptor
{
    private readonly TimeSpan _bufferTime = TimeSpan.FromMilliseconds(100);

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var newContext = PropagateDeadline(context);
        return continuation(request, newContext);
    }

    private ClientInterceptorContext<TRequest, TResponse> PropagateDeadline<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context)
        where TRequest : class
        where TResponse : class
    {
        // Check if there's an incoming deadline from the current call context
        if (ServerCallContext.Current?.Deadline is { } incomingDeadline)
        {
            var remaining = incomingDeadline - DateTime.UtcNow;

            // Apply buffer to leave time for response processing
            var adjustedRemaining = remaining - _bufferTime;

            if (adjustedRemaining > TimeSpan.Zero)
            {
                var newOptions = context.Options.WithDeadline(DateTime.UtcNow + adjustedRemaining);
                return new ClientInterceptorContext<TRequest, TResponse>(
                    context.Method,
                    context.Host,
                    newOptions);
            }
        }

        return context;
    }
}
```

## Health Check Implementation

```csharp
public class HealthServiceImpl : Health.HealthBase
{
    private readonly IEnumerable<IHealthCheck> _healthChecks;
    private readonly ConcurrentDictionary<string, HealthCheckResponse.Types.ServingStatus> _statusMap = new();

    public HealthServiceImpl(IEnumerable<IHealthCheck> healthChecks)
    {
        _healthChecks = healthChecks;
    }

    public override async Task<HealthCheckResponse> Check(
        HealthCheckRequest request,
        ServerCallContext context)
    {
        var service = request.Service;

        if (string.IsNullOrEmpty(service))
        {
            // Overall health check
            var allHealthy = await CheckAllServicesAsync(context.CancellationToken);
            return new HealthCheckResponse
            {
                Status = allHealthy
                    ? HealthCheckResponse.Types.ServingStatus.Serving
                    : HealthCheckResponse.Types.ServingStatus.NotServing
            };
        }

        if (_statusMap.TryGetValue(service, out var status))
        {
            return new HealthCheckResponse { Status = status };
        }

        throw new RpcException(new Status(StatusCode.NotFound, $"Service '{service}' not found"));
    }

    public override async Task Watch(
        HealthCheckRequest request,
        IServerStreamWriter<HealthCheckResponse> responseStream,
        ServerCallContext context)
    {
        var lastStatus = HealthCheckResponse.Types.ServingStatus.Unknown;

        while (!context.CancellationToken.IsCancellationRequested)
        {
            var currentStatus = await GetServiceStatusAsync(request.Service, context.CancellationToken);

            if (currentStatus != lastStatus)
            {
                await responseStream.WriteAsync(new HealthCheckResponse { Status = currentStatus });
                lastStatus = currentStatus;
            }

            await Task.Delay(TimeSpan.FromSeconds(5), context.CancellationToken);
        }
    }

    private async Task<bool> CheckAllServicesAsync(CancellationToken ct)
    {
        foreach (var check in _healthChecks)
        {
            var result = await check.CheckHealthAsync(new HealthCheckContext(), ct);
            if (result.Status != HealthStatus.Healthy)
                return false;
        }
        return true;
    }

    private async Task<HealthCheckResponse.Types.ServingStatus> GetServiceStatusAsync(
        string service,
        CancellationToken ct)
    {
        // Implementation depends on service-specific health checks
        return HealthCheckResponse.Types.ServingStatus.Serving;
    }
}
```

## Load Balancing Client Configuration

```csharp
// Configure DNS-based load balancing
var channel = GrpcChannel.ForAddress("dns:///my-service.example.com", new GrpcChannelOptions
{
    ServiceConfig = new ServiceConfig
    {
        LoadBalancingConfigs = { new RoundRobinConfig() },
        MethodConfigs =
        {
            new MethodConfig
            {
                Names = { MethodName.Default },
                RetryPolicy = new RetryPolicy
                {
                    MaxAttempts = 3,
                    InitialBackoff = TimeSpan.FromMilliseconds(100),
                    MaxBackoff = TimeSpan.FromSeconds(2),
                    BackoffMultiplier = 1.5,
                    RetryableStatusCodes = { StatusCode.Unavailable }
                }
            }
        }
    },
    Credentials = ChannelCredentials.SecureSsl
});
```

## Compression Configuration

```csharp
// Server-side compression
builder.Services.AddGrpc(options =>
{
    options.ResponseCompressionAlgorithm = "gzip";
    options.ResponseCompressionLevel = CompressionLevel.Optimal;
    options.CompressionProviders = new List<ICompressionProvider>
    {
        new GzipCompressionProvider(CompressionLevel.Optimal)
    };
});

// Client-side compression
var callOptions = new CallOptions(
    headers: new Metadata
    {
        { "grpc-accept-encoding", "gzip" }
    },
    writeOptions: new WriteOptions(WriteFlags.NoCompress));
```
