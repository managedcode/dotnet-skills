# SignalR Anti-Patterns

This reference documents common SignalR mistakes and how to avoid them.

## Hub Instance Anti-Patterns

### Storing State in Hub Properties

Hub instances are transient and created per invocation:

```csharp
// BAD: State is lost between calls
public class BadHub : Hub
{
    private List<string> _messages = new(); // Created fresh every call
    private int _messageCount = 0;          // Always 0

    public Task SendMessage(string message)
    {
        _messages.Add(message);  // Lost immediately
        _messageCount++;         // Always 1
        return Task.CompletedTask;
    }
}

// GOOD: Use external state management
public class GoodHub : Hub
{
    private readonly IMessageStore _store;
    private readonly IMemoryCache _cache;

    public GoodHub(IMessageStore store, IMemoryCache cache)
    {
        _store = store;
        _cache = cache;
    }

    public async Task SendMessage(string message)
    {
        await _store.AddMessageAsync(message);
        _cache.Set("lastMessage", message);
    }
}

// GOOD: Use Context.Items for per-connection state
public class ConnectionStateHub : Hub
{
    public Task SetConnectionData(string key, object value)
    {
        Context.Items[key] = value;
        return Task.CompletedTask;
    }

    public object? GetConnectionData(string key)
    {
        return Context.Items.TryGetValue(key, out var value) ? value : null;
    }
}
```

### Instantiating Hub Directly

Never create hub instances manually:

```csharp
// BAD: Bypasses SignalR infrastructure
public class BadService
{
    public async Task NotifyUsers()
    {
        var hub = new ChatHub(); // No context, no clients, no groups
        await hub.Clients.All.ReceiveMessage("test"); // NullReferenceException
    }
}

// GOOD: Use IHubContext<THub>
public class GoodService
{
    private readonly IHubContext<ChatHub, IChatClient> _hubContext;

    public GoodService(IHubContext<ChatHub, IChatClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyUsers()
    {
        await _hubContext.Clients.All.ReceiveMessage("test");
    }
}
```

## Async Anti-Patterns

### Not Awaiting SendAsync Calls

Fire-and-forget messaging loses errors and ordering:

```csharp
// BAD: No await means method may complete before message is sent
public class BadHub : Hub<IChatClient>
{
    public Task SendMessage(string message)
    {
        Clients.All.ReceiveMessage(message); // Not awaited!
        return Task.CompletedTask;
    }
}

// BAD: Multiple unawaited calls have undefined ordering
public class AlsoBadHub : Hub<IChatClient>
{
    public Task NotifyAll()
    {
        Clients.All.Message1(); // Which arrives first?
        Clients.All.Message2(); // Unknown!
        return Task.CompletedTask;
    }
}

// GOOD: Always await async calls
public class GoodHub : Hub<IChatClient>
{
    public async Task SendMessage(string message)
    {
        await Clients.All.ReceiveMessage(message);
    }

    public async Task NotifyAll()
    {
        await Clients.All.Message1();
        await Clients.All.Message2(); // Guaranteed order
    }
}
```

### Blocking Async Code

Blocking calls cause thread pool starvation:

```csharp
// BAD: Blocking on async
public class BadHub : Hub
{
    private readonly IDataService _dataService;

    public string GetData()
    {
        // Blocks thread pool thread
        return _dataService.GetDataAsync().Result;
    }

    public void SendBlocking()
    {
        // Potential deadlock
        Clients.All.SendAsync("Method").Wait();
    }
}

// GOOD: Async all the way
public class GoodHub : Hub
{
    private readonly IDataService _dataService;

    public async Task<string> GetData()
    {
        return await _dataService.GetDataAsync();
    }

    public async Task Send()
    {
        await Clients.All.SendAsync("Method");
    }
}
```

## Connection Management Anti-Patterns

### Ignoring Group Loss on Reconnection

Group membership is tied to connection ID, which changes on reconnect:

```csharp
// BAD: Assumes groups persist across reconnection
public class BadHub : Hub
{
    public async Task JoinRoom(string roomId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        // Client reconnects with new connection ID = no longer in group
    }
}

// GOOD: Track groups and rejoin on connect
public class GoodHub : Hub
{
    private readonly IGroupMembershipStore _store;

    public GoodHub(IGroupMembershipStore store) => _store = store;

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User!.GetUserId();
        var groups = await _store.GetUserGroupsAsync(userId);

        foreach (var group in groups)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, group);
        }

        await base.OnConnectedAsync();
    }

    public async Task JoinRoom(string roomId)
    {
        var userId = Context.User!.GetUserId();

        // Persist membership
        await _store.AddUserToGroupAsync(userId, roomId);

        // Add current connection
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
    }
}
```

Client-side handling is also required:

```javascript
// BAD: No reconnection handling
connection.start();

// GOOD: Rejoin groups after reconnection
connection.onreconnected(async (connectionId) => {
    console.log("Reconnected with ID:", connectionId);
    // Rejoin all rooms
    for (const roomId of joinedRooms) {
        await connection.invoke("JoinRoom", roomId);
    }
});
```

### Not Handling Connection Lifecycle

Ignoring connect/disconnect events loses cleanup opportunities:

```csharp
// BAD: No lifecycle handling
public class BadHub : Hub
{
    public Task JoinGame(string gameId)
    {
        // What happens when they disconnect mid-game?
        return Groups.AddToGroupAsync(Context.ConnectionId, gameId);
    }
}

// GOOD: Handle lifecycle events
public class GoodHub : Hub
{
    private readonly IGameService _gameService;

    public GoodHub(IGameService gameService) => _gameService = gameService;

    public async Task JoinGame(string gameId)
    {
        Context.Items["GameId"] = gameId;
        await _gameService.AddPlayerAsync(gameId, Context.User!.GetUserId());
        await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue("GameId", out var gameIdObj) && gameIdObj is string gameId)
        {
            await _gameService.RemovePlayerAsync(gameId, Context.User!.GetUserId());
            await Clients.Group(gameId).PlayerLeft(Context.User!.GetUserId());
        }
        await base.OnDisconnectedAsync(exception);
    }
}
```

## Security Anti-Patterns

### Not Validating Hub Method Inputs

Authentication does not equal authorization or validation:

```csharp
// BAD: No validation after auth
[Authorize]
public class BadHub : Hub
{
    public async Task SendToGroup(string groupId, string message)
    {
        // Is user allowed in this group? Unknown!
        // Is message content safe? Unknown!
        await Clients.Group(groupId).ReceiveMessage(message);
    }

    public async Task GetUserData(string userId)
    {
        // Can caller access this user's data? Not checked!
        var data = await _userService.GetDataAsync(userId);
        await Clients.Caller.ReceiveData(data);
    }
}

// GOOD: Validate every hub method
[Authorize]
public class GoodHub : Hub
{
    private readonly IAuthorizationService _authService;
    private readonly IValidator<SendMessageRequest> _validator;

    public async Task SendToGroup(string groupId, SendMessageRequest request)
    {
        // Check authorization
        var canSend = await _authService.CanSendToGroupAsync(Context.User!, groupId);
        if (!canSend)
        {
            throw new HubException("Not authorized for this group");
        }

        // Validate content
        var validation = await _validator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            throw new HubException($"Invalid message: {validation.Errors.First().ErrorMessage}");
        }

        await Clients.Group(groupId).ReceiveMessage(request.Message);
    }

    public async Task GetUserData(string userId)
    {
        // Verify caller can access this data
        var callerId = Context.User!.GetUserId();
        if (userId != callerId && !Context.User.IsInRole("Admin"))
        {
            throw new HubException("Not authorized to access this user's data");
        }

        var data = await _userService.GetDataAsync(userId);
        await Clients.Caller.ReceiveData(data);
    }
}
```

### Exposing Internal Exceptions

Internal errors leak implementation details:

```csharp
// BAD: Exceptions leak to client
public class BadHub : Hub
{
    public async Task ProcessOrder(OrderRequest request)
    {
        // SqlException details visible to client
        // Stack traces visible to client
        await _orderService.ProcessAsync(request);
    }
}

// GOOD: Wrap exceptions
public class GoodHub : Hub
{
    private readonly ILogger<GoodHub> _logger;

    public async Task ProcessOrder(OrderRequest request)
    {
        try
        {
            await _orderService.ProcessAsync(request);
        }
        catch (ValidationException ex)
        {
            throw new HubException($"Validation error: {ex.Message}");
        }
        catch (BusinessRuleException ex)
        {
            throw new HubException(ex.UserFriendlyMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order");
            throw new HubException("An error occurred processing your order");
        }
    }
}
```

## Scaling Anti-Patterns

### Missing Backplane for Multi-Server

Without a backplane, messages only reach local connections:

```csharp
// BAD: Single-server assumption
builder.Services.AddSignalR();
// Users on Server B never receive messages from Server A

// GOOD: Use backplane for multi-server
builder.Services.AddSignalR()
    .AddStackExchangeRedis(connectionString, options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal("MyApp");
    });

// OR: Use Azure SignalR Service
builder.Services.AddSignalR()
    .AddAzureSignalR(connectionString);
```

### Storing Connection State In-Memory for Scale-Out

In-memory state does not scale:

```csharp
// BAD: In-memory state with scale-out
public class BadPresenceTracker
{
    // Only tracks connections on this server
    private readonly ConcurrentDictionary<string, string> _connections = new();
}

// GOOD: Distributed state
public class GoodPresenceTracker
{
    private readonly IDistributedCache _cache;
    // OR
    private readonly IConnectionMultiplexer _redis;
    // OR
    private readonly IDatabase _database;
}
```

## Performance Anti-Patterns

### Large Payloads Over SignalR

SignalR is optimized for small, frequent messages:

```csharp
// BAD: Large file transfer over SignalR
public class BadHub : Hub
{
    public async Task UploadFile(byte[] fileData) // 50MB payload
    {
        await _fileService.SaveAsync(fileData);
    }

    public async Task<byte[]> DownloadFile(string fileId)
    {
        return await _fileService.GetAsync(fileId); // Returns 50MB
    }
}

// GOOD: Use SignalR for signaling, HTTP for bulk data
public class GoodHub : Hub
{
    public async Task<string> RequestUploadUrl()
    {
        // Return pre-signed URL for direct upload
        return await _blobService.GetUploadUrlAsync();
    }

    public async Task NotifyUploadComplete(string fileId)
    {
        await Clients.Others.FileUploaded(fileId);
    }

    public async Task<string> RequestDownloadUrl(string fileId)
    {
        return await _blobService.GetDownloadUrlAsync(fileId);
    }
}
```

### No Throttling for High-Frequency Events

Unthrottled events overwhelm clients and servers:

```csharp
// BAD: Every keystroke sends a message
public class BadHub : Hub
{
    public async Task UserTyping()
    {
        await Clients.Others.UserIsTyping(Context.User!.Identity!.Name!);
    }
}

// GOOD: Throttle high-frequency events
public class GoodHub : Hub
{
    private readonly IThrottler _throttler;

    public async Task UserTyping()
    {
        var key = $"typing:{Context.ConnectionId}";
        if (await _throttler.ShouldThrottleAsync(key, TimeSpan.FromSeconds(2)))
        {
            return; // Skip if sent within last 2 seconds
        }

        await Clients.Others.UserIsTyping(Context.User!.Identity!.Name!);
    }
}
```

### Not Using Streaming for Large Result Sets

Loading everything into memory wastes resources:

```csharp
// BAD: Load all records into memory
public class BadHub : Hub
{
    public async Task<List<LogEntry>> GetLogs(DateTime from, DateTime to)
    {
        // Loads potentially millions of records
        return await _logService.GetAllLogsAsync(from, to);
    }
}

// GOOD: Stream results
public class GoodHub : Hub
{
    public async IAsyncEnumerable<LogEntry> StreamLogs(
        DateTime from,
        DateTime to,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var log in _logService.StreamLogsAsync(from, to, cancellationToken))
        {
            yield return log;
        }
    }
}
```

## API Design Anti-Patterns

### Using Dynamic Hubs

Dynamic invocations lose compile-time safety:

```csharp
// BAD: Dynamic client invocation
public class BadHub : Hub
{
    public async Task SendMessage(string message)
    {
        // Typos not caught at compile time
        await Clients.All.SendAsync("RecieveMessage", message); // Typo!
    }
}

// GOOD: Strongly-typed hub
public interface IChatClient
{
    Task ReceiveMessage(string message);
}

public class GoodHub : Hub<IChatClient>
{
    public async Task SendMessage(string message)
    {
        // Compile-time checking
        await Clients.All.ReceiveMessage(message);
    }
}
```

### Breaking API Changes

Changing method signatures breaks existing clients:

```csharp
// BAD: Breaking change
public class HubV1 : Hub
{
    // Original
    public Task SendMessage(string message) => ...;

    // Changed to add parameter - breaks existing clients!
    public Task SendMessage(string message, string category) => ...;
}

// GOOD: Use request objects for evolution
public class SendMessageRequest
{
    public string Message { get; set; } = "";
    public string? Category { get; set; }  // Added later, optional
    public int? Priority { get; set; }      // Added later, optional
}

public class GoodHub : Hub<IChatClient>
{
    public async Task SendMessage(SendMessageRequest request)
    {
        // Handles both old and new clients
        var category = request.Category ?? "general";
        await ProcessMessageAsync(request.Message, category);
    }
}
```

### Exposing ORM Entities

Direct entity exposure leaks implementation and sensitive data:

```csharp
// BAD: Exposing EF entities
public class BadHub : Hub
{
    public async Task<User> GetUser(string userId)
    {
        // May include navigation properties, password hashes, etc.
        return await _dbContext.Users.FindAsync(userId);
    }
}

// GOOD: Use DTOs
public class GoodHub : Hub
{
    public async Task<UserDto> GetUser(string userId)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        return new UserDto
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl
            // No password hash, no internal fields
        };
    }
}
```

## Diagnostic Anti-Patterns

### No Logging for Connection Events

Silent connections make debugging impossible:

```csharp
// BAD: No visibility
public class BadHub : Hub
{
    // No logging, no metrics
}

// GOOD: Log important events
public class GoodHub : Hub
{
    private readonly ILogger<GoodHub> _logger;

    public GoodHub(ILogger<GoodHub> logger) => _logger = logger;

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation(
            "Client connected: {ConnectionId}, User: {User}, Transport: {Transport}",
            Context.ConnectionId,
            Context.User?.Identity?.Name ?? "anonymous",
            Context.Features.Get<IHttpTransportFeature>()?.TransportType);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception,
                "Client disconnected with error: {ConnectionId}",
                Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation(
                "Client disconnected: {ConnectionId}",
                Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
```

### Not Monitoring Message Delivery

Assuming messages always arrive:

```csharp
// BAD: Fire and forget without tracking
public class BadHub : Hub
{
    public async Task Broadcast(string message)
    {
        await Clients.All.ReceiveMessage(message);
        // Did everyone get it? Unknown!
    }
}

// GOOD: Track delivery metrics
public class GoodHub : Hub
{
    private readonly IMetrics _metrics;
    private readonly ILogger<GoodHub> _logger;

    public async Task Broadcast(string message)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await Clients.All.ReceiveMessage(message);
            _metrics.RecordBroadcast(stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _metrics.RecordBroadcastFailure();
            _logger.LogError(ex, "Broadcast failed");
            throw;
        }
    }
}
```

## Testing Anti-Patterns

### Not Testing Reconnection Scenarios

Only testing happy paths misses critical failures:

```csharp
// BAD: Only test message sending
[Fact]
public async Task CanSendMessage()
{
    await _connection.InvokeAsync("SendMessage", "hello");
    // What about reconnection? Disconnection?
}

// GOOD: Test connection lifecycle
[Fact]
public async Task ReconnectionRestoresGroupMembership()
{
    await _connection.InvokeAsync("JoinRoom", "test-room");

    // Simulate network failure
    await _connection.StopAsync();
    await _connection.StartAsync();

    // Verify group membership restored
    var isInRoom = await _connection.InvokeAsync<bool>("IsInRoom", "test-room");
    Assert.True(isInRoom);
}

[Fact]
public async Task DisconnectionCleansUpResources()
{
    await _connection.InvokeAsync("JoinGame", "game-1");
    await _connection.StopAsync();

    // Verify cleanup occurred
    var game = await _gameService.GetGameAsync("game-1");
    Assert.DoesNotContain(_userId, game.Players);
}
```

### Not Testing Under Load

Performance issues only appear at scale:

```csharp
// GOOD: Load test SignalR
[Fact]
public async Task HandlesHighMessageVolume()
{
    var connections = new List<HubConnection>();
    var receivedCounts = new ConcurrentDictionary<string, int>();

    // Create many connections
    for (int i = 0; i < 100; i++)
    {
        var connection = CreateConnection();
        var connId = $"conn-{i}";
        receivedCounts[connId] = 0;

        connection.On<string>("ReceiveMessage", msg =>
        {
            receivedCounts.AddOrUpdate(connId, 1, (_, c) => c + 1);
        });

        await connection.StartAsync();
        connections.Add(connection);
    }

    // Send many messages
    var sendTasks = Enumerable.Range(0, 1000)
        .Select(i => connections[i % connections.Count].InvokeAsync("SendMessage", $"msg-{i}"));

    await Task.WhenAll(sendTasks);
    await Task.Delay(5000); // Allow delivery

    // Verify delivery
    foreach (var count in receivedCounts.Values)
    {
        Assert.Equal(1000, count);
    }
}
```
