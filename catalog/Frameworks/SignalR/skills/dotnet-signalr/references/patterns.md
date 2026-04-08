# SignalR Hub Patterns

This reference provides detailed patterns for implementing SignalR hubs, streaming, groups, and connection management.

## Hub Design Patterns

### Hub per Feature

Separate hubs by domain feature rather than creating one monolithic hub:

```csharp
// Good: Feature-focused hubs
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<CollaborationHub>("/hubs/collaboration");
app.MapHub<PresenceHub>("/hubs/presence");

// Bad: One hub for everything
app.MapHub<ApplicationHub>("/hubs/app"); // Too broad
```

### Hub Method Delegation Pattern

Keep hub methods thin and delegate to services:

```csharp
public class OrderHub : Hub<IOrderClient>
{
    private readonly IOrderService _orderService;
    private readonly IValidator<PlaceOrderRequest> _validator;
    private readonly ILogger<OrderHub> _logger;

    public OrderHub(
        IOrderService orderService,
        IValidator<PlaceOrderRequest> validator,
        ILogger<OrderHub> logger)
    {
        _orderService = orderService;
        _validator = validator;
        _logger = logger;
    }

    public async Task<OrderResult> PlaceOrder(PlaceOrderRequest request)
    {
        // 1. Validate
        var validation = await _validator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return OrderResult.Failed(validation.Errors);
        }

        // 2. Delegate to service
        var order = await _orderService.PlaceOrderAsync(
            request,
            Context.User!.GetUserId());

        // 3. Broadcast (orchestration only)
        await Clients.Group($"order-watchers-{order.CustomerId}")
            .OrderPlaced(order.ToDto());

        return OrderResult.Success(order.Id);
    }
}
```

### Connection Context Pattern

Use connection items for per-connection state:

```csharp
public class GameHub : Hub<IGameClient>
{
    public async Task JoinGame(string gameId)
    {
        // Store per-connection state
        Context.Items["GameId"] = gameId;
        Context.Items["JoinedAt"] = DateTime.UtcNow;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"game-{gameId}");
        await Clients.Group($"game-{gameId}").PlayerJoined(Context.User!.Identity!.Name!);
    }

    public async Task MakeMove(MoveRequest move)
    {
        // Retrieve per-connection state
        var gameId = Context.Items["GameId"] as string
            ?? throw new HubException("Not in a game");

        // Process move...
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue("GameId", out var gameIdObj) && gameIdObj is string gameId)
        {
            await Clients.Group($"game-{gameId}").PlayerLeft(Context.User!.Identity!.Name!);
        }
        await base.OnDisconnectedAsync(exception);
    }
}
```

## Streaming Patterns

### Server-to-Client Streaming

Use `IAsyncEnumerable<T>` for server-to-client streams:

```csharp
public class DataHub : Hub<IDataClient>
{
    private readonly IDataService _dataService;

    public DataHub(IDataService dataService) => _dataService = dataService;

    // Client calls: connection.stream("StreamData", query)
    public async IAsyncEnumerable<DataItem> StreamData(
        DataQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in _dataService.GetDataStreamAsync(query, cancellationToken))
        {
            // Check cancellation between yields
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    // Alternative with ChannelReader for more control
    public ChannelReader<StockQuote> StreamStockQuotes(
        string[] symbols,
        CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<StockQuote>();

        _ = WriteQuotesToChannelAsync(channel.Writer, symbols, cancellationToken);

        return channel.Reader;
    }

    private async Task WriteQuotesToChannelAsync(
        ChannelWriter<StockQuote> writer,
        string[] symbols,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                foreach (var symbol in symbols)
                {
                    var quote = await GetQuoteAsync(symbol);
                    await writer.WriteAsync(quote, cancellationToken);
                }
                await Task.Delay(1000, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when client disconnects
        }
        finally
        {
            writer.Complete();
        }
    }
}
```

### Client-to-Server Streaming

Accept `ChannelReader<T>` or `IAsyncEnumerable<T>` for client uploads:

```csharp
public class UploadHub : Hub<IUploadClient>
{
    public async Task UploadChunks(
        string fileName,
        ChannelReader<byte[]> stream)
    {
        var totalBytes = 0L;

        await foreach (var chunk in stream.ReadAllAsync(Context.ConnectionAborted))
        {
            // Process each chunk
            await ProcessChunkAsync(fileName, chunk);
            totalBytes += chunk.Length;

            // Report progress back to client
            await Clients.Caller.UploadProgress(totalBytes);
        }

        await Clients.Caller.UploadComplete(fileName, totalBytes);
    }

    // IAsyncEnumerable variant
    public async Task StreamMessages(IAsyncEnumerable<ChatMessage> stream)
    {
        await foreach (var message in stream)
        {
            // Validate each message
            if (string.IsNullOrWhiteSpace(message.Content))
                continue;

            // Broadcast to group
            await Clients.Group(message.RoomId).ReceiveMessage(message);
        }
    }
}
```

### Bidirectional Streaming

Combine both patterns for full-duplex streams:

```csharp
public class CollaborationHub : Hub<ICollaborationClient>
{
    public async IAsyncEnumerable<DocumentChange> Collaborate(
        string documentId,
        IAsyncEnumerable<DocumentEdit> edits,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Join the document group to receive others' changes
        await Groups.AddToGroupAsync(Context.ConnectionId, $"doc-{documentId}");

        // Start a background task to process incoming edits
        var editProcessor = ProcessEditsAsync(documentId, edits, cancellationToken);

        // Stream out all changes from other users
        var changeChannel = GetChangeChannel(documentId);

        await foreach (var change in changeChannel.ReadAllAsync(cancellationToken))
        {
            // Skip changes from this connection
            if (change.ConnectionId != Context.ConnectionId)
            {
                yield return change;
            }
        }

        await editProcessor;
    }
}
```

## Group Patterns

### Hierarchical Groups

Model group membership hierarchically:

```csharp
public class OrganizationHub : Hub<IOrgClient>
{
    public async Task JoinOrganization(string orgId)
    {
        // Hierarchical group structure
        await Groups.AddToGroupAsync(Context.ConnectionId, $"org-{orgId}");
    }

    public async Task JoinTeam(string orgId, string teamId)
    {
        // More specific group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"org-{orgId}-team-{teamId}");
    }

    public async Task JoinProject(string orgId, string teamId, string projectId)
    {
        // Most specific group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"org-{orgId}-team-{teamId}-project-{projectId}");
    }

    public async Task NotifyOrganization(string orgId, Notification notification)
    {
        // Reaches everyone in the org
        await Clients.Group($"org-{orgId}").ReceiveNotification(notification);
    }

    public async Task NotifyTeam(string orgId, string teamId, Notification notification)
    {
        // Reaches only team members
        await Clients.Group($"org-{orgId}-team-{teamId}").ReceiveNotification(notification);
    }
}
```

### Group Membership Tracking

Track group membership for presence features:

```csharp
public class PresenceHub : Hub<IPresenceClient>
{
    private readonly IGroupMembershipService _membership;

    public PresenceHub(IGroupMembershipService membership) => _membership = membership;

    public async Task JoinRoom(string roomId)
    {
        var userId = Context.User!.GetUserId();
        var connectionId = Context.ConnectionId;

        // Track in persistent store
        await _membership.AddMemberAsync(roomId, userId, connectionId);

        // Add to SignalR group
        await Groups.AddToGroupAsync(connectionId, roomId);

        // Get current members and notify
        var members = await _membership.GetMembersAsync(roomId);
        await Clients.Caller.RoomMembers(members);
        await Clients.OthersInGroup(roomId).UserJoined(userId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User!.GetUserId();
        var connectionId = Context.ConnectionId;

        // Find and clean up all rooms this connection was in
        var rooms = await _membership.GetRoomsForConnectionAsync(connectionId);
        foreach (var roomId in rooms)
        {
            await _membership.RemoveMemberAsync(roomId, connectionId);

            // Check if user has other connections in this room
            var hasOtherConnections = await _membership.UserHasConnectionsInRoomAsync(roomId, userId);
            if (!hasOtherConnections)
            {
                await Clients.Group(roomId).UserLeft(userId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}
```

### Excluding Connections from Group Sends

Use exclusion lists for targeted broadcasts:

```csharp
public class BroadcastHub : Hub<IBroadcastClient>
{
    public async Task SendToGroupExcept(
        string groupName,
        string message,
        string[] excludeConnectionIds)
    {
        await Clients
            .GroupExcept(groupName, excludeConnectionIds)
            .ReceiveMessage(message);
    }

    public async Task SendToAllExceptCaller(string message)
    {
        // Built-in exclusion of caller
        await Clients.Others.ReceiveMessage(message);
    }

    public async Task SendToGroupExceptCaller(string groupName, string message)
    {
        // Exclude caller from group send
        await Clients.OthersInGroup(groupName).ReceiveMessage(message);
    }
}
```

## User-Based Targeting

### Multi-Connection Users

Handle users with multiple connections:

```csharp
public class UserHub : Hub<IUserClient>
{
    public async Task SendToUser(string userId, Message message)
    {
        // Reaches all connections for this user
        await Clients.User(userId).ReceiveMessage(message);
    }

    public async Task SendToUsers(string[] userIds, Message message)
    {
        await Clients.Users(userIds).ReceiveMessage(message);
    }
}
```

### Custom User ID Provider

Map connections to custom user identifiers:

```csharp
public class TenantUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        var tenantId = connection.User?.FindFirst("tenant_id")?.Value;
        var userId = connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (tenantId != null && userId != null)
        {
            // Namespace user IDs by tenant for multi-tenant apps
            return $"{tenantId}:{userId}";
        }

        return userId;
    }
}

// Registration
builder.Services.AddSingleton<IUserIdProvider, TenantUserIdProvider>();
```

## Presence Patterns

### Connection Counting

Track user presence across connections:

```csharp
public class PresenceTracker
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _userConnections = new();

    public bool AddConnection(string userId, string connectionId)
    {
        var connections = _userConnections.GetOrAdd(userId, _ => new HashSet<string>());
        lock (connections)
        {
            var wasEmpty = connections.Count == 0;
            connections.Add(connectionId);
            return wasEmpty; // True if this is the first connection
        }
    }

    public bool RemoveConnection(string userId, string connectionId)
    {
        if (_userConnections.TryGetValue(userId, out var connections))
        {
            lock (connections)
            {
                connections.Remove(connectionId);
                if (connections.Count == 0)
                {
                    _userConnections.TryRemove(userId, out _);
                    return true; // User is now offline
                }
            }
        }
        return false;
    }

    public string[] GetOnlineUsers() => _userConnections.Keys.ToArray();
}
```

### Presence with Redis (Scale-Out)

Distributed presence for multi-server deployments:

```csharp
public class RedisPresenceService : IPresenceService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _serverInstance;

    public RedisPresenceService(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _serverInstance = $"{Environment.MachineName}-{Process.GetCurrentProcess().Id}";
    }

    public async Task<bool> UserConnectedAsync(string userId, string connectionId)
    {
        var db = _redis.GetDatabase();
        var key = $"presence:{userId}";

        var wasEmpty = !(await db.KeyExistsAsync(key));

        await db.HashSetAsync(key, connectionId, _serverInstance);
        await db.KeyExpireAsync(key, TimeSpan.FromMinutes(30));

        return wasEmpty;
    }

    public async Task<bool> UserDisconnectedAsync(string userId, string connectionId)
    {
        var db = _redis.GetDatabase();
        var key = $"presence:{userId}";

        await db.HashDeleteAsync(key, connectionId);
        var remaining = await db.HashLengthAsync(key);

        return remaining == 0;
    }

    public async Task<string[]> GetOnlineUsersAsync()
    {
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var keys = server.Keys(pattern: "presence:*");

        return keys.Select(k => k.ToString().Replace("presence:", "")).ToArray();
    }
}
```

## Request-Response Pattern

### Hub Methods with Return Values

Return results directly from hub methods:

```csharp
public class QueryHub : Hub<IQueryClient>
{
    private readonly IQueryService _queryService;

    public QueryHub(IQueryService queryService) => _queryService = queryService;

    public async Task<SearchResult> Search(SearchRequest request)
    {
        // Validate
        if (string.IsNullOrEmpty(request.Query))
        {
            throw new HubException("Query is required");
        }

        // Execute and return
        return await _queryService.SearchAsync(request, Context.ConnectionAborted);
    }

    public async Task<PagedResult<Item>> GetItems(int page, int pageSize)
    {
        if (pageSize > 100)
        {
            throw new HubException("Page size cannot exceed 100");
        }

        return await _queryService.GetItemsAsync(page, pageSize);
    }
}
```

### Error Handling

Use `HubException` for client-visible errors:

```csharp
public class RobustHub : Hub<IRobustClient>
{
    public async Task<OperationResult> PerformOperation(OperationRequest request)
    {
        try
        {
            // Business logic...
            return OperationResult.Success();
        }
        catch (ValidationException ex)
        {
            // Client sees this message
            throw new HubException($"Validation failed: {ex.Message}");
        }
        catch (NotFoundException ex)
        {
            throw new HubException($"Not found: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in PerformOperation");
            // Generic message for unexpected errors
            throw new HubException("An unexpected error occurred");
        }
    }
}
```

## Acknowledgment Pattern

### Message Acknowledgments

Track message delivery acknowledgments:

```csharp
public interface IReliableClient
{
    Task ReceiveMessage(string messageId, Message message);
    Task MessageAcknowledged(string messageId);
}

public class ReliableHub : Hub<IReliableClient>
{
    private readonly IPendingMessageStore _pendingStore;

    public ReliableHub(IPendingMessageStore pendingStore) => _pendingStore = pendingStore;

    public async Task SendReliable(string recipientId, Message message)
    {
        var messageId = Guid.NewGuid().ToString();

        // Store pending message
        await _pendingStore.StorePendingAsync(messageId, recipientId, message);

        // Attempt delivery
        await Clients.User(recipientId).ReceiveMessage(messageId, message);

        // Notify sender
        await Clients.Caller.MessageAcknowledged(messageId);
    }

    public async Task AcknowledgeMessage(string messageId)
    {
        // Mark as delivered
        await _pendingStore.MarkDeliveredAsync(messageId);
    }

    // Background job retries unacknowledged messages
}
```

## Batching Pattern

### Message Batching for High Throughput

Batch messages to reduce overhead:

```csharp
public class BatchingHub : Hub<IBatchingClient>
{
    private readonly IBatchService _batchService;

    public BatchingHub(IBatchService batchService) => _batchService = batchService;

    public async Task StartBatchedUpdates(string subscriptionId)
    {
        var channel = Channel.CreateBounded<Update>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        // Register this connection for updates
        _batchService.RegisterSubscription(subscriptionId, Context.ConnectionId, channel.Writer);

        // Background batching loop
        _ = Task.Run(async () =>
        {
            var batch = new List<Update>();
            var batchTimeout = TimeSpan.FromMilliseconds(100);

            while (!Context.ConnectionAborted.IsCancellationRequested)
            {
                batch.Clear();

                // Collect items for up to 100ms or 50 items
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(Context.ConnectionAborted);
                cts.CancelAfter(batchTimeout);

                try
                {
                    while (batch.Count < 50 && await channel.Reader.WaitToReadAsync(cts.Token))
                    {
                        if (channel.Reader.TryRead(out var item))
                        {
                            batch.Add(item);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Timeout or disconnect - send what we have
                }

                if (batch.Count > 0)
                {
                    await Clients.Caller.ReceiveBatch(batch.ToArray());
                }
            }
        });
    }
}
```

## Throttling Pattern

### Rate Limiting Hub Methods

Implement per-connection rate limiting:

```csharp
public class ThrottledHub : Hub<IThrottledClient>
{
    private readonly IRateLimiter _rateLimiter;

    public ThrottledHub(IRateLimiter rateLimiter) => _rateLimiter = rateLimiter;

    public async Task SendMessage(string message)
    {
        var key = $"hub:{Context.ConnectionId}:sendMessage";

        if (!await _rateLimiter.TryAcquireAsync(key, maxRequests: 10, window: TimeSpan.FromSeconds(1)))
        {
            throw new HubException("Rate limit exceeded. Please slow down.");
        }

        await Clients.All.ReceiveMessage(Context.User!.Identity!.Name!, message);
    }
}

// Using System.Threading.RateLimiting
public class SlidingWindowRateLimiter : IRateLimiter
{
    private readonly ConcurrentDictionary<string, RateLimiter> _limiters = new();

    public async ValueTask<bool> TryAcquireAsync(string key, int maxRequests, TimeSpan window)
    {
        var limiter = _limiters.GetOrAdd(key, _ =>
            new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
            {
                PermitLimit = maxRequests,
                Window = window,
                SegmentsPerWindow = 4,
                AutoReplenishment = true
            }));

        using var lease = await limiter.AcquireAsync();
        return lease.IsAcquired;
    }
}
```
