# gRPC Anti-Patterns Reference

This document catalogs common mistakes when building gRPC services and clients in .NET, with explanations and corrections.

## Connection and Channel Anti-Patterns

### Creating a New Channel Per Call

```csharp
// WRONG: Creating channel per call
public async Task<string> GetGreetingAsync(string name)
{
    using var channel = GrpcChannel.ForAddress("https://localhost:5001");
    var client = new Greeter.GreeterClient(channel);
    var reply = await client.SayHelloAsync(new HelloRequest { Name = name });
    return reply.Message;
}
```

**Why it's bad:**
- HTTP/2 connection establishment is expensive (TLS handshake, SETTINGS exchange)
- Prevents connection pooling and multiplexing
- Kills performance under load
- Causes connection churn on the server

**Correct approach:**
```csharp
// CORRECT: Use client factory for channel reuse
builder.Services.AddGrpcClient<Greeter.GreeterClient>(options =>
{
    options.Address = new Uri("https://localhost:5001");
});

// Or manage singleton channel manually
public class GrpcClientService
{
    private static readonly GrpcChannel _channel =
        GrpcChannel.ForAddress("https://localhost:5001", new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30)
            }
        });

    private readonly Greeter.GreeterClient _client = new(_channel);

    public Task<HelloReply> SayHelloAsync(HelloRequest request) =>
        _client.SayHelloAsync(request).ResponseAsync;
}
```

### Disposing Channel After Every Call

```csharp
// WRONG: Disposing channel immediately
public async Task DoWorkAsync()
{
    var channel = GrpcChannel.ForAddress("https://localhost:5001");
    try
    {
        var client = new Greeter.GreeterClient(channel);
        await client.SayHelloAsync(new HelloRequest { Name = "World" });
    }
    finally
    {
        await channel.ShutdownAsync(); // Kills connection reuse
    }
}
```

**Why it's bad:**
- ShutdownAsync closes all HTTP/2 connections
- Subsequent calls must re-establish connections
- Negates HTTP/2 multiplexing benefits

**Correct approach:**
```csharp
// CORRECT: Channel lives for application lifetime
public class ChannelManager : IAsyncDisposable
{
    private readonly GrpcChannel _channel;

    public ChannelManager(string address)
    {
        _channel = GrpcChannel.ForAddress(address);
    }

    public GrpcChannel Channel => _channel;

    public async ValueTask DisposeAsync()
    {
        await _channel.ShutdownAsync();
    }
}

// Register as singleton
builder.Services.AddSingleton(sp =>
    new ChannelManager("https://localhost:5001"));
```

## Deadline and Timeout Anti-Patterns

### Missing Deadlines on Client Calls

```csharp
// WRONG: No deadline set
var response = await client.ProcessOrderAsync(new OrderRequest { OrderId = orderId });
```

**Why it's bad:**
- Calls can hang indefinitely if server is slow or network fails
- No way to enforce timeout at the gRPC level
- Can cause thread/connection exhaustion under load

**Correct approach:**
```csharp
// CORRECT: Always set a deadline
var deadline = DateTime.UtcNow.AddSeconds(10);
var response = await client.ProcessOrderAsync(
    new OrderRequest { OrderId = orderId },
    deadline: deadline);

// Or use CallOptions for more control
var options = new CallOptions(deadline: DateTime.UtcNow.AddSeconds(10));
var response = await client.ProcessOrderAsync(new OrderRequest { OrderId = orderId }, options);
```

### Using CancellationToken Instead of Deadline

```csharp
// WRONG: CancellationToken alone doesn't signal the server
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var response = await client.ProcessOrderAsync(request, cancellationToken: cts.Token);
```

**Why it's bad:**
- CancellationToken only cancels client-side
- Server continues processing, wasting resources
- Deadline is transmitted to server, CancellationToken is not

**Correct approach:**
```csharp
// CORRECT: Use deadline AND cancellation token
var deadline = DateTime.UtcNow.AddSeconds(10);
var response = await client.ProcessOrderAsync(
    request,
    deadline: deadline,
    cancellationToken: cancellationToken);
```

### Mismatched Client/Server Deadlines

```csharp
// WRONG: Server deadline longer than client
// Client sets 5 second deadline
var deadline = DateTime.UtcNow.AddSeconds(5);
var response = await client.ProcessAsync(request, deadline: deadline);

// Server handler takes up to 30 seconds
public override async Task<Response> Process(Request request, ServerCallContext context)
{
    await LongRunningOperation(TimeSpan.FromSeconds(30)); // Client already timed out
    return new Response();
}
```

**Why it's bad:**
- Server wastes resources after client has given up
- Results are computed but never delivered
- No coordinated timeout behavior

**Correct approach:**
```csharp
// CORRECT: Server respects incoming deadline
public override async Task<Response> Process(Request request, ServerCallContext context)
{
    // Check remaining time before expensive operations
    var remaining = context.Deadline - DateTime.UtcNow;
    if (remaining < TimeSpan.FromSeconds(1))
    {
        throw new RpcException(new Status(StatusCode.DeadlineExceeded, "Insufficient time"));
    }

    using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
    cts.CancelAfter(remaining - TimeSpan.FromMilliseconds(100)); // Buffer for response

    await ProcessWithTimeoutAsync(request, cts.Token);
    return new Response();
}
```

## Streaming Anti-Patterns

### Ignoring Cancellation in Streaming Handlers

```csharp
// WRONG: Not checking cancellation
public override async Task StreamData(
    DataRequest request,
    IServerStreamWriter<DataChunk> responseStream,
    ServerCallContext context)
{
    var items = await GetAllItemsAsync(); // Could be millions
    foreach (var item in items)
    {
        await responseStream.WriteAsync(item); // Continues even if client disconnects
    }
}
```

**Why it's bad:**
- Server continues processing after client disconnects
- Wastes CPU, memory, and network resources
- Can cause resource exhaustion under load

**Correct approach:**
```csharp
// CORRECT: Check cancellation regularly
public override async Task StreamData(
    DataRequest request,
    IServerStreamWriter<DataChunk> responseStream,
    ServerCallContext context)
{
    await foreach (var item in GetItemsAsync(context.CancellationToken))
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        await responseStream.WriteAsync(item, context.CancellationToken);
    }
}
```

### Not Completing Client Streams

```csharp
// WRONG: Forgetting to complete the request stream
public async Task UploadDataAsync(IEnumerable<DataChunk> chunks)
{
    using var call = _client.UploadData();

    foreach (var chunk in chunks)
    {
        await call.RequestStream.WriteAsync(chunk);
    }

    // Missing: await call.RequestStream.CompleteAsync();
    var response = await call.ResponseAsync; // Hangs forever
}
```

**Why it's bad:**
- Server waits indefinitely for more messages
- Call never completes
- Resources are held until timeout

**Correct approach:**
```csharp
// CORRECT: Always complete client streams
public async Task UploadDataAsync(IEnumerable<DataChunk> chunks)
{
    using var call = _client.UploadData();

    foreach (var chunk in chunks)
    {
        await call.RequestStream.WriteAsync(chunk);
    }

    await call.RequestStream.CompleteAsync(); // Signal end of stream
    var response = await call.ResponseAsync;
}
```

### Blocking on Bidirectional Streams

```csharp
// WRONG: Sequential read/write in bidirectional stream
public async Task ChatAsync()
{
    using var call = _client.Chat();

    while (true)
    {
        var message = await GetNextMessageAsync();
        await call.RequestStream.WriteAsync(message);

        // Blocks until response arrives - can't send another message until then
        var response = await call.ResponseStream.MoveNext();
    }
}
```

**Why it's bad:**
- Serializes what should be concurrent operations
- Loses bidirectional streaming benefits
- Can deadlock if server expects multiple requests before responding

**Correct approach:**
```csharp
// CORRECT: Concurrent read and write
public async Task ChatAsync(CancellationToken ct)
{
    using var call = _client.Chat();

    var readTask = Task.Run(async () =>
    {
        await foreach (var response in call.ResponseStream.ReadAllAsync(ct))
        {
            ProcessResponse(response);
        }
    }, ct);

    var writeTask = Task.Run(async () =>
    {
        await foreach (var message in GetMessagesAsync(ct))
        {
            await call.RequestStream.WriteAsync(message);
        }
        await call.RequestStream.CompleteAsync();
    }, ct);

    await Task.WhenAll(readTask, writeTask);
}
```

## Message Size Anti-Patterns

### Sending Large Messages

```csharp
// WRONG: Sending multi-MB payloads
message FileUploadRequest {
  bytes content = 1; // Could be hundreds of MB
  string filename = 2;
}

public async Task UploadFileAsync(byte[] fileContent)
{
    await _client.UploadFileAsync(new FileUploadRequest
    {
        Content = ByteString.CopyFrom(fileContent), // LOH allocation
        Filename = "large-file.zip"
    });
}
```

**Why it's bad:**
- Large messages cause Large Object Heap allocations
- Memory fragmentation and GC pressure
- Can exceed default message size limits (4MB)
- Single-request timeout affects entire transfer

**Correct approach:**
```csharp
// CORRECT: Use streaming for large data
service FileService {
  rpc UploadFile (stream FileChunk) returns (UploadResponse);
}

message FileChunk {
  oneof data {
    FileMetadata metadata = 1;
    bytes chunk = 2;
  }
}

public async Task UploadFileAsync(string filePath, CancellationToken ct)
{
    using var call = _client.UploadFile();

    // Send metadata first
    await call.RequestStream.WriteAsync(new FileChunk
    {
        Metadata = new FileMetadata { Filename = Path.GetFileName(filePath) }
    });

    // Stream file in chunks
    await using var stream = File.OpenRead(filePath);
    var buffer = new byte[64 * 1024]; // 64KB chunks
    int bytesRead;

    while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
    {
        await call.RequestStream.WriteAsync(new FileChunk
        {
            Chunk = ByteString.CopyFrom(buffer, 0, bytesRead)
        });
    }

    await call.RequestStream.CompleteAsync();
    var response = await call.ResponseAsync;
}
```

## Error Handling Anti-Patterns

### Swallowing Exceptions

```csharp
// WRONG: Swallowing exceptions returns OK status
public override async Task<OrderResponse> CreateOrder(OrderRequest request, ServerCallContext context)
{
    try
    {
        await _repository.CreateOrderAsync(request);
        return new OrderResponse { Success = true };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Order creation failed");
        return new OrderResponse { Success = false }; // Client sees OK status
    }
}
```

**Why it's bad:**
- Client receives OK status despite failure
- Error details lost in response message
- Client retry logic doesn't trigger
- Breaks gRPC error handling conventions

**Correct approach:**
```csharp
// CORRECT: Convert exceptions to appropriate status codes
public override async Task<OrderResponse> CreateOrder(OrderRequest request, ServerCallContext context)
{
    try
    {
        await _repository.CreateOrderAsync(request);
        return new OrderResponse { Success = true };
    }
    catch (ValidationException ex)
    {
        throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
    }
    catch (DuplicateOrderException ex)
    {
        throw new RpcException(new Status(StatusCode.AlreadyExists, ex.Message));
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Order creation failed");
        throw new RpcException(new Status(StatusCode.Internal, "An error occurred"));
    }
}
```

### Using Wrong Status Codes

```csharp
// WRONG: Using Internal for everything
catch (KeyNotFoundException)
{
    throw new RpcException(new Status(StatusCode.Internal, "Not found")); // Should be NotFound
}
catch (UnauthorizedAccessException)
{
    throw new RpcException(new Status(StatusCode.Internal, "Access denied")); // Should be PermissionDenied
}
catch (ArgumentException)
{
    throw new RpcException(new Status(StatusCode.Internal, "Bad argument")); // Should be InvalidArgument
}
```

**Why it's bad:**
- Clients can't distinguish error types
- Retry policies may retry non-retryable errors
- Metrics and monitoring lose granularity

**Correct approach:**
```csharp
// CORRECT: Map to appropriate gRPC status codes
public static class ExceptionMapping
{
    public static RpcException ToRpcException(Exception ex) => ex switch
    {
        ArgumentException e => new RpcException(new Status(StatusCode.InvalidArgument, e.Message)),
        KeyNotFoundException e => new RpcException(new Status(StatusCode.NotFound, e.Message)),
        UnauthorizedAccessException e => new RpcException(new Status(StatusCode.PermissionDenied, e.Message)),
        InvalidOperationException e => new RpcException(new Status(StatusCode.FailedPrecondition, e.Message)),
        NotImplementedException e => new RpcException(new Status(StatusCode.Unimplemented, e.Message)),
        OperationCanceledException e => new RpcException(new Status(StatusCode.Cancelled, e.Message)),
        TimeoutException e => new RpcException(new Status(StatusCode.DeadlineExceeded, e.Message)),
        _ => new RpcException(new Status(StatusCode.Internal, "An internal error occurred"))
    };
}
```

### Leaking Sensitive Information in Error Details

```csharp
// WRONG: Including stack trace and internal details
catch (Exception ex)
{
    throw new RpcException(new Status(
        StatusCode.Internal,
        $"Failed: {ex.Message}\n{ex.StackTrace}\nConnection: {_connectionString}"));
}
```

**Why it's bad:**
- Exposes internal implementation details
- May leak sensitive data (connection strings, paths)
- Security vulnerability

**Correct approach:**
```csharp
// CORRECT: Log details server-side, return safe message
catch (Exception ex)
{
    var errorId = Guid.NewGuid();
    _logger.LogError(ex, "Error {ErrorId}: {Message}", errorId, ex.Message);

    var message = _environment.IsDevelopment()
        ? ex.Message
        : $"An error occurred. Reference: {errorId}";

    throw new RpcException(new Status(StatusCode.Internal, message));
}
```

## Interceptor Anti-Patterns

### Blocking in Interceptors

```csharp
// WRONG: Synchronous blocking in async interceptor
public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
    TRequest request,
    ClientInterceptorContext<TRequest, TResponse> context,
    AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
{
    // Blocking call - ties up thread pool
    var token = _tokenService.GetTokenAsync().Result; // NEVER do this

    var headers = context.Options.Headers ?? new Metadata();
    headers.Add("authorization", $"Bearer {token}");

    return continuation(request, context);
}
```

**Why it's bad:**
- Blocks thread pool threads
- Can cause thread pool starvation
- Degrades application throughput

**Correct approach:**
```csharp
// CORRECT: Handle async properly in interceptor
public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
    TRequest request,
    ClientInterceptorContext<TRequest, TResponse> context,
    AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
{
    // Get token synchronously if possible, or wrap the response
    var call = continuation(request, context);

    return new AsyncUnaryCall<TResponse>(
        AddAuthAndCallAsync(request, context, call.ResponseAsync),
        call.ResponseHeadersAsync,
        call.GetStatus,
        call.GetTrailers,
        call.Dispose);
}

private async Task<TResponse> AddAuthAndCallAsync<TRequest, TResponse>(
    TRequest request,
    ClientInterceptorContext<TRequest, TResponse> context,
    Task<TResponse> responseTask)
{
    // Can await safely here
    var token = await _tokenService.GetTokenAsync();
    // Note: Headers are already sent at this point, so this pattern
    // requires restructuring to modify context before continuation
    return await responseTask;
}
```

### Incomplete Interceptor Implementation

```csharp
// WRONG: Only implementing UnaryServerHandler
public class AuthInterceptor : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        await ValidateAuthAsync(context);
        return await continuation(request, context);
    }

    // Missing: ServerStreamingServerHandler, ClientStreamingServerHandler,
    // DuplexStreamingServerHandler - streaming calls bypass auth!
}
```

**Why it's bad:**
- Streaming calls bypass the interceptor logic
- Security holes in streaming endpoints
- Inconsistent behavior

**Correct approach:**
```csharp
// CORRECT: Implement all relevant interceptor methods
public class AuthInterceptor : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        await ValidateAuthAsync(context);
        return await continuation(request, context);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await ValidateAuthAsync(context);
        await continuation(request, responseStream, context);
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await ValidateAuthAsync(context);
        return await continuation(requestStream, context);
    }

    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await ValidateAuthAsync(context);
        await continuation(requestStream, responseStream, context);
    }
}
```

## Proto Design Anti-Patterns

### Using Primitives for Optional Fields

```protobuf
// WRONG: Primitives have default values, not null
message SearchRequest {
  int32 page_size = 1;    // 0 means unset OR 0?
  bool include_deleted = 2; // false means unset OR false?
}
```

**Why it's bad:**
- Can't distinguish "not set" from "set to default value"
- Forces awkward conventions (e.g., -1 means unset)
- Proto3 removed "required" and made all fields optional with defaults

**Correct approach:**
```protobuf
// CORRECT: Use wrapper types for optional primitives
import "google/protobuf/wrappers.proto";

message SearchRequest {
  google.protobuf.Int32Value page_size = 1;    // null means unset
  google.protobuf.BoolValue include_deleted = 2; // null means unset
}

// Or use explicit presence with optional keyword (proto3 optional)
message SearchRequest {
  optional int32 page_size = 1;
  optional bool include_deleted = 2;
}
```

### Reusing Field Numbers

```protobuf
// VERSION 1
message User {
  string id = 1;
  string email = 2;
  string phone = 3; // Later removed
}

// VERSION 2 - WRONG
message User {
  string id = 1;
  string email = 2;
  string address = 3; // Reused field 3 - breaks wire compatibility
}
```

**Why it's bad:**
- Old clients deserialize address bytes as phone string
- Silent data corruption
- Breaks backward compatibility

**Correct approach:**
```protobuf
// VERSION 2 - CORRECT
message User {
  string id = 1;
  string email = 2;
  reserved 3;
  reserved "phone";
  string address = 4; // New field number
}
```

### Flat Request Messages

```protobuf
// WRONG: Flat request makes evolution difficult
service OrderService {
  rpc CreateOrder (CreateOrderRequest) returns (CreateOrderResponse);
}

message CreateOrderRequest {
  string customer_id = 1;
  string product_id = 2;
  int32 quantity = 3;
  string shipping_street = 4;
  string shipping_city = 5;
  string shipping_country = 6;
  // Adding billing address requires many new fields
}
```

**Why it's bad:**
- Hard to add related groups of fields
- No reuse across messages
- Field explosion over time

**Correct approach:**
```protobuf
// CORRECT: Nested messages for structured data
message CreateOrderRequest {
  string customer_id = 1;
  OrderDetails order = 2;
  Address shipping_address = 3;
  Address billing_address = 4; // Easy to add
}

message OrderDetails {
  repeated OrderItem items = 1;
}

message OrderItem {
  string product_id = 1;
  int32 quantity = 2;
}

message Address {
  string street = 1;
  string city = 2;
  string country = 3;
  string postal_code = 4;
}
```

## Configuration Anti-Patterns

### Not Configuring HTTP/2 Limits

```csharp
// WRONG: Using defaults that may not match your workload
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddGrpc(); // Default limits
```

**Why it's bad:**
- Default stream limits may be too low for your traffic
- Window sizes may cause unnecessary backpressure
- Keep-alive not configured for long-lived connections

**Correct approach:**
```csharp
// CORRECT: Configure HTTP/2 settings appropriately
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.Http2.MaxStreamsPerConnection = 250;
    options.Limits.Http2.InitialConnectionWindowSize = 1024 * 1024; // 1MB
    options.Limits.Http2.InitialStreamWindowSize = 768 * 1024; // 768KB
    options.Limits.Http2.KeepAlivePingDelay = TimeSpan.FromSeconds(30);
    options.Limits.Http2.KeepAlivePingTimeout = TimeSpan.FromSeconds(10);
});
```

### Ignoring Keep-Alive Configuration

```csharp
// WRONG: No keep-alive configuration
var channel = GrpcChannel.ForAddress("https://server:5001");
```

**Why it's bad:**
- Connections may be silently closed by proxies/load balancers
- First call after idle period fails
- No detection of dead connections

**Correct approach:**
```csharp
// CORRECT: Configure keep-alive
var channel = GrpcChannel.ForAddress("https://server:5001", new GrpcChannelOptions
{
    HttpHandler = new SocketsHttpHandler
    {
        KeepAlivePingDelay = TimeSpan.FromSeconds(60),
        KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
        PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
        EnableMultipleHttp2Connections = true
    }
});
```

## Testing Anti-Patterns

### Testing with Real Network Calls

```csharp
// WRONG: Integration test depends on real server
[Fact]
public async Task CreateOrder_WithValidData_ReturnsSuccess()
{
    var channel = GrpcChannel.ForAddress("https://localhost:5001");
    var client = new Orders.OrdersClient(channel);

    var response = await client.CreateOrderAsync(new CreateOrderRequest
    {
        CustomerId = "test-customer"
    });

    Assert.True(response.Success);
}
```

**Why it's bad:**
- Tests depend on external server
- Flaky due to network issues
- Slow test execution
- Can't test edge cases easily

**Correct approach:**
```csharp
// CORRECT: Use TestServer or mocks
[Fact]
public async Task CreateOrder_WithValidData_ReturnsSuccess()
{
    await using var factory = new WebApplicationFactory<Program>();
    using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
    {
        HttpHandler = factory.Server.CreateHandler()
    });

    var client = new Orders.OrdersClient(channel);

    var response = await client.CreateOrderAsync(new CreateOrderRequest
    {
        CustomerId = "test-customer"
    });

    Assert.True(response.Success);
}

// Or use Moq with generated client interface
[Fact]
public async Task ProcessOrder_CallsService()
{
    var mockClient = new Mock<IOrdersClient>();
    mockClient
        .Setup(x => x.CreateOrderAsync(It.IsAny<CreateOrderRequest>(), null, null, default))
        .Returns(new AsyncUnaryCall<CreateOrderResponse>(
            Task.FromResult(new CreateOrderResponse { Success = true }),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { }));

    var service = new OrderProcessor(mockClient.Object);
    await service.ProcessAsync();

    mockClient.Verify(x => x.CreateOrderAsync(
        It.Is<CreateOrderRequest>(r => r.CustomerId == "expected"),
        null, null, default), Times.Once);
}
```
