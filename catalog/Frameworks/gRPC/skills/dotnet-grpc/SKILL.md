---
name: dotnet-grpc
version: "1.0.0"
category: "Web"
description: "Build or review gRPC services and clients in .NET with correct contract-first design, streaming behavior, transport assumptions, and backend service integration."
compatibility: "Requires ASP.NET Core gRPC or gRPC client projects."
---

# gRPC for .NET

## Trigger On

- building backend-to-backend RPC services or clients
- adding protobuf contracts, streaming calls, or interceptors
- deciding between gRPC, HTTP APIs, and SignalR
- optimizing gRPC performance and connection management
- implementing service-to-service communication in microservices

## Documentation

- [gRPC on .NET Overview](https://learn.microsoft.com/en-us/aspnet/core/grpc/?view=aspnetcore-10.0)
- [Performance Best Practices with gRPC](https://learn.microsoft.com/en-us/aspnet/core/grpc/performance?view=aspnetcore-10.0)
- [gRPC Client Factory](https://learn.microsoft.com/en-us/aspnet/core/grpc/clientfactory?view=aspnetcore-10.0)
- [gRPC Interceptors](https://learn.microsoft.com/en-us/aspnet/core/grpc/interceptors?view=aspnetcore-10.0)
- [Call gRPC Services with .NET Client](https://learn.microsoft.com/en-us/aspnet/core/grpc/client?view=aspnetcore-10.0)

### References

- [patterns.md](references/patterns.md) - Detailed proto patterns, streaming implementations, interceptors, health checks, and load balancing
- [anti-patterns.md](references/anti-patterns.md) - Common gRPC mistakes with explanations and corrections

## Workflow

1. Use gRPC where low-latency backend communication, strong contracts, or streaming are the real drivers.
2. Treat `.proto` files as source of truth and keep generated code ownership clear.
3. Choose unary, server streaming, client streaming, or bidirectional streaming based on the interaction model, not by default.
4. Do not use gRPC for broad browser-facing APIs unless the limitations and gRPC-Web tradeoffs are explicitly acceptable.
5. Handle deadlines, cancellation, auth, and retry behavior explicitly on both server and client paths.
6. Validate contract changes carefully because gRPC drift breaks callers fast.

## Service Patterns

### Basic Unary Service
```csharp
// greeter.proto
syntax = "proto3";

option csharp_namespace = "GrpcService";

package greet;

service Greeter {
  rpc SayHello (HelloRequest) returns (HelloReply);
}

message HelloRequest {
  string name = 1;
}

message HelloReply {
  string message = 1;
}
```

```csharp
// GreeterService.cs
public class GreeterService : Greeter.GreeterBase
{
    private readonly ILogger<GreeterService> _logger;

    public GreeterService(ILogger<GreeterService> logger)
    {
        _logger = logger;
    }

    public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Greeting {Name}", request.Name);
        return Task.FromResult(new HelloReply
        {
            Message = $"Hello {request.Name}"
        });
    }
}
```

### Server Streaming
```csharp
// In .proto file
service DataStream {
  rpc StreamData (DataRequest) returns (stream DataChunk);
}

// Service implementation
public override async Task StreamData(
    DataRequest request,
    IServerStreamWriter<DataChunk> responseStream,
    ServerCallContext context)
{
    for (int i = 0; i < request.Count; i++)
    {
        // Check for cancellation to avoid wasted work
        if (context.CancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Stream cancelled by client");
            break;
        }

        await responseStream.WriteAsync(new DataChunk
        {
            Index = i,
            Data = await GetDataAsync(i)
        });

        // Respect backpressure
        await Task.Delay(10, context.CancellationToken);
    }
}
```

### Bidirectional Streaming
```csharp
// In .proto file
service Chat {
  rpc ChatStream (stream ChatMessage) returns (stream ChatMessage);
}

// Service implementation
public override async Task ChatStream(
    IAsyncStreamReader<ChatMessage> requestStream,
    IServerStreamWriter<ChatMessage> responseStream,
    ServerCallContext context)
{
    await foreach (var message in requestStream.ReadAllAsync(context.CancellationToken))
    {
        _logger.LogInformation("Received: {Message}", message.Text);

        // Echo back with transformation
        await responseStream.WriteAsync(new ChatMessage
        {
            Text = $"Echo: {message.Text}",
            Timestamp = Timestamp.FromDateTime(DateTime.UtcNow)
        });
    }
}
```

## Client Patterns

### Channel Reuse with Client Factory (Recommended)
```csharp
// Program.cs - Register gRPC client with factory
builder.Services.AddGrpcClient<Greeter.GreeterClient>(options =>
{
    options.Address = new Uri("https://localhost:5001");
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new SocketsHttpHandler
    {
        PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
        KeepAlivePingDelay = TimeSpan.FromSeconds(60),
        KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
        EnableMultipleHttp2Connections = true
    };
    return handler;
})
.AddInterceptor<LoggingInterceptor>();

// Usage in service
public class MyService
{
    private readonly Greeter.GreeterClient _client;

    public MyService(Greeter.GreeterClient client)
    {
        _client = client;
    }

    public async Task<string> GreetAsync(string name, CancellationToken ct)
    {
        // Always set deadlines
        var deadline = DateTime.UtcNow.AddSeconds(5);
        var response = await _client.SayHelloAsync(
            new HelloRequest { Name = name },
            deadline: deadline,
            cancellationToken: ct);
        return response.Message;
    }
}
```

### Manual Channel Creation with Connection Options
```csharp
// Reuse channels - expensive to create
var channel = GrpcChannel.ForAddress("https://localhost:5001", new GrpcChannelOptions
{
    HttpHandler = new SocketsHttpHandler
    {
        EnableMultipleHttp2Connections = true,
        PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
        KeepAlivePingDelay = TimeSpan.FromSeconds(60),
        KeepAlivePingTimeout = TimeSpan.FromSeconds(30)
    },
    MaxRetryAttempts = 3,
    ServiceConfig = new ServiceConfig
    {
        MethodConfigs =
        {
            new MethodConfig
            {
                Names = { MethodName.Default },
                RetryPolicy = new RetryPolicy
                {
                    MaxAttempts = 3,
                    InitialBackoff = TimeSpan.FromSeconds(1),
                    MaxBackoff = TimeSpan.FromSeconds(5),
                    BackoffMultiplier = 1.5,
                    RetryableStatusCodes = { StatusCode.Unavailable }
                }
            }
        }
    }
});

// Create multiple clients from same channel
var greeterClient = new Greeter.GreeterClient(channel);
var orderClient = new Orders.OrdersClient(channel);
```

### Consuming Server Streaming
```csharp
public async Task ProcessStreamAsync(CancellationToken ct)
{
    using var call = _client.StreamData(new DataRequest { Count = 100 });

    try
    {
        await foreach (var chunk in call.ResponseStream.ReadAllAsync(ct))
        {
            await ProcessChunkAsync(chunk);
        }
    }
    catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
    {
        _logger.LogInformation("Stream cancelled");
    }
}
```

### Bidirectional Streaming Client
```csharp
public async Task ChatAsync(CancellationToken ct)
{
    using var call = _client.ChatStream();

    // Read responses in background
    var readTask = Task.Run(async () =>
    {
        await foreach (var response in call.ResponseStream.ReadAllAsync(ct))
        {
            Console.WriteLine($"Received: {response.Text}");
        }
    }, ct);

    // Send messages
    foreach (var message in GetMessages())
    {
        if (ct.IsCancellationRequested) break;

        await call.RequestStream.WriteAsync(new ChatMessage { Text = message });
    }

    // Signal completion and wait for responses
    await call.RequestStream.CompleteAsync();
    await readTask;
}
```

## Interceptor Patterns

### Logging Interceptor
```csharp
public class LoggingInterceptor : Interceptor
{
    private readonly ILogger<LoggingInterceptor> _logger;

    public LoggingInterceptor(ILogger<LoggingInterceptor> logger)
    {
        _logger = logger;
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var sw = Stopwatch.StartNew();
        var call = continuation(request, context);

        return new AsyncUnaryCall<TResponse>(
            HandleResponse(call.ResponseAsync, context.Method.FullName, sw),
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    private async Task<TResponse> HandleResponse<TResponse>(
        Task<TResponse> responseTask, string method, Stopwatch sw)
    {
        try
        {
            var response = await responseTask;
            _logger.LogInformation("{Method} completed in {Elapsed}ms",
                method, sw.ElapsedMilliseconds);
            return response;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "{Method} failed with {Status} in {Elapsed}ms",
                method, ex.StatusCode, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
```

### Server Exception Interceptor
```csharp
public class ExceptionInterceptor : Interceptor
{
    private readonly ILogger<ExceptionInterceptor> _logger;

    public ExceptionInterceptor(ILogger<ExceptionInterceptor> logger)
    {
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (RpcException)
        {
            throw; // Let gRPC exceptions pass through
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in {Method}", context.Method);
            throw new RpcException(new Status(StatusCode.Internal, "An error occurred"));
        }
    }
}
```

## Server Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaxReceiveMessageSize = 4 * 1024 * 1024; // 4 MB
    options.MaxSendMessageSize = 4 * 1024 * 1024;
    options.Interceptors.Add<ExceptionInterceptor>();
});

// Configure Kestrel for HTTP/2
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.Http2.MaxStreamsPerConnection = 100;
    options.Limits.Http2.InitialConnectionWindowSize = 1024 * 1024; // 1 MB
    options.Limits.Http2.InitialStreamWindowSize = 512 * 1024; // 512 KB
});

var app = builder.Build();

app.MapGrpcService<GreeterService>();
app.MapGet("/", () => "gRPC endpoint");

app.Run();
```

## Anti-Patterns to Avoid

| Anti-Pattern | Why It's Bad | Better Approach |
|--------------|--------------|-----------------|
| Creating new channel per call | Connection overhead kills performance | Reuse channels, use client factory |
| Missing deadlines | Calls can hang indefinitely | Always set deadline on client calls |
| Ignoring cancellation in streams | Wastes server resources | Check `CancellationToken` periodically |
| Using gRPC for browser clients | Limited browser support | Use gRPC-Web with Envoy or REST |
| Large messages (>1MB) | Memory pressure, LOH allocations | Stream in chunks or use HTTP for files |
| Sync blocking (`Task.Result`) | Thread pool starvation | Use async/await consistently |
| Swallowing exceptions in interceptors | Hides failures from clients | Rethrow or convert to `RpcException` |
| Not aligning client/server deadlines | Mismatched timeout behavior | Coordinate deadline budgets |
| Blocking `AsyncUnaryCall` with `BlockingUnaryCall` interceptor | Interceptors are method-specific | Implement both interceptor methods |
| Missing retry configuration | Single failures cause request failure | Configure retry policy on channel |

## Best Practices

### Channel and Connection Management
1. **Reuse channels** across the application lifetime
2. **Enable multiple HTTP/2 connections** with `EnableMultipleHttp2Connections = true`
3. **Configure keep-alive pings** to maintain connections through idle periods
4. **Use client factory** (`AddGrpcClient`) for centralized channel management
5. **Set `PooledConnectionIdleTimeout`** to prevent premature connection closure

### Deadlines and Cancellation
1. **Always set deadlines** on client calls to prevent indefinite hangs
2. **Propagate cancellation** through the call chain
3. **Check cancellation** in long-running streaming handlers
4. **Coordinate deadline budgets** between client and server

### Performance
1. **Avoid large messages** (>85KB to stay off Large Object Heap)
2. **Use streaming** for large data transfers instead of single messages
3. **Enable server GC** for high-throughput client applications
4. **Complete streams gracefully** to allow connection reuse
5. **Dispose streaming calls** when done to release resources

### Error Handling
1. **Use appropriate status codes** (not just `Internal` for everything)
2. **Let `RpcException` propagate** through interceptors
3. **Convert domain exceptions** to gRPC status codes at service boundaries
4. **Include meaningful error details** in development mode only

### Contract Design
1. **Use custom objects** in proto to enable backward-compatible evolution
2. **Reserve field numbers** you remove instead of reusing
3. **Version service names** for breaking changes (`GreeterV2`)
4. **Keep proto files** as the single source of truth

### Observability
1. **Add logging interceptors** for request/response timing
2. **Track error rates** by status code
3. **Monitor connection pool** health and reuse rates
4. **Integrate with distributed tracing** (OpenTelemetry)

## Deliver

- stable protobuf contracts and generated code flow
- service and client code that match the RPC shape
- tests or smoke checks for serialization and call behavior
- proper deadline and cancellation handling

## Validate

- gRPC is chosen for the right problem
- streaming semantics and deadlines are explicit
- browser constraints are acknowledged when relevant
- channels are reused appropriately
- error handling converts exceptions to proper status codes
- interceptors are ordered correctly (logging before auth before validation)
