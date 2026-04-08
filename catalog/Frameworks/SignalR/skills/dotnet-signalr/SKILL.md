---
name: dotnet-signalr
description: "Implement or review SignalR hubs, streaming, reconnection, transport, and real-time delivery patterns in ASP.NET Core applications."
compatibility: "Requires ASP.NET Core SignalR server or client code."
---

# SignalR

## Trigger On

- building chat, notification, collaboration, or live-update features
- debugging hub lifetime, connection state, or transport issues
- deciding whether SignalR or another transport better fits the scenario
- implementing real-time broadcasting to groups of connected clients
- scaling SignalR across multiple servers

## Documentation

- [ASP.NET Core SignalR Overview](https://learn.microsoft.com/en-us/aspnet/core/signalr/introduction?view=aspnetcore-10.0)
- [SignalR Hubs](https://learn.microsoft.com/en-us/aspnet/core/signalr/hubs?view=aspnetcore-10.0)
- [SignalR API Design Considerations](https://learn.microsoft.com/en-us/aspnet/core/signalr/api-design?view=aspnetcore-10.0)
- [SignalR Production Hosting and Scaling](https://learn.microsoft.com/en-us/aspnet/core/signalr/scale?view=aspnetcore-10.0)
- [SignalR Configuration](https://learn.microsoft.com/en-us/aspnet/core/signalr/configuration?view=aspnetcore-10.0)

### References

- [patterns.md](references/patterns.md) - Detailed hub patterns, streaming, groups, presence, and advanced messaging techniques
- [anti-patterns.md](references/anti-patterns.md) - Common SignalR mistakes and how to avoid them

## Workflow

1. Use SignalR for broadcast-style or connection-oriented real-time features; do not force gRPC into hub-style fan-out scenarios.
2. Model hub contracts intentionally and keep hub methods thin, delegating durable work elsewhere.
3. Plan for reconnection, backpressure, auth, and fan-out costs instead of treating real-time messaging as stateless request/response.
4. Use groups, presence, and connection metadata deliberately so scale-out behavior is understandable.
5. If Native AOT or trimming is in play, validate supported protocols and serialization choices explicitly.
6. Test connection behavior and failure modes, not just happy-path message delivery.

## Hub Patterns

### Strongly-Typed Hub (Recommended)
```csharp
// Define the client interface
public interface IChatClient
{
    Task ReceiveMessage(string user, string message);
    Task UserJoined(string user);
    Task UserLeft(string user);
}

// Implement the strongly-typed hub
public class ChatHub : Hub<IChatClient>
{
    public async Task SendMessage(string user, string message)
    {
        // Compiler checks client method calls
        await Clients.All.ReceiveMessage(user, message);
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Others.UserJoined(Context.User?.Identity?.Name ?? "Anonymous");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Clients.Others.UserLeft(Context.User?.Identity?.Name ?? "Anonymous");
        await base.OnDisconnectedAsync(exception);
    }
}
```

### Using Groups for Targeted Messaging
```csharp
public class NotificationHub : Hub<INotificationClient>
{
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await Clients.Group(groupName).UserJoined(Context.User?.Identity?.Name);
    }

    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task SendToGroup(string groupName, string message)
    {
        await Clients.Group(groupName).ReceiveNotification(message);
    }
}
```

### Hub Method with Custom Object Parameters (API Versioning)
```csharp
// Use custom objects to avoid breaking changes
public class SendMessageRequest
{
    public string Message { get; set; } = string.Empty;
    public string? Recipient { get; set; }  // Added later without breaking clients
    public int? Priority { get; set; }       // Added later without breaking clients
}

public class ChatHub : Hub<IChatClient>
{
    public async Task SendMessage(SendMessageRequest request)
    {
        // Handle both old and new clients
        if (request.Recipient != null)
        {
            await Clients.User(request.Recipient).ReceiveMessage(request.Message);
        }
        else
        {
            await Clients.All.ReceiveMessage(request.Message);
        }
    }
}
```

## Client Patterns

### JavaScript Client with Automatic Reconnection
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chatHub")
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000]) // Retry delays
    .configureLogging(signalR.LogLevel.Information)
    .build();

// Handle reconnection events
connection.onreconnecting(error => {
    console.log("Reconnecting...", error);
    updateUIForReconnecting();
});

connection.onreconnected(connectionId => {
    console.log("Reconnected with ID:", connectionId);
    // Rejoin groups - reconnection does not restore group membership
    rejoinGroups();
    updateUIForConnected();
});

connection.onclose(error => {
    console.log("Connection closed", error);
    updateUIForDisconnected();
});

async function start() {
    try {
        await connection.start();
        console.log("SignalR Connected");
    } catch (err) {
        console.log(err);
        setTimeout(start, 5000);
    }
}

start();
```

### .NET Client with Reconnection
```csharp
var connection = new HubConnectionBuilder()
    .WithUrl("https://localhost:5001/chatHub", options =>
    {
        options.AccessTokenProvider = () => Task.FromResult(GetAccessToken());
    })
    .WithAutomaticReconnect()
    .Build();

connection.Reconnecting += error =>
{
    _logger.LogWarning("Connection lost. Reconnecting: {Error}", error?.Message);
    return Task.CompletedTask;
};

connection.Reconnected += connectionId =>
{
    _logger.LogInformation("Reconnected with ID: {ConnectionId}", connectionId);
    // Rejoin groups after reconnection
    return RejoinGroupsAsync();
};

connection.Closed += async error =>
{
    _logger.LogError("Connection closed: {Error}", error?.Message);
    await Task.Delay(Random.Shared.Next(0, 5) * 1000);
    await connection.StartAsync();
};

await connection.StartAsync();
```

## Server Configuration

### Hub Registration with Authentication
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 64 * 1024; // 64 KB
    options.StreamBufferCapacity = 10;
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
})
.AddMessagePackProtocol(); // Binary protocol for performance

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Read token from query string for WebSocket connections
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapHub<ChatHub>("/hubs/chat");
```

### Sending Messages from Outside a Hub
```csharp
public class NotificationService
{
    private readonly IHubContext<NotificationHub, INotificationClient> _hubContext;

    public NotificationService(IHubContext<NotificationHub, INotificationClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyAllAsync(string message)
    {
        await _hubContext.Clients.All.ReceiveNotification(message);
    }

    public async Task NotifyUserAsync(string userId, string message)
    {
        await _hubContext.Clients.User(userId).ReceiveNotification(message);
    }

    public async Task NotifyGroupAsync(string groupName, string message)
    {
        await _hubContext.Clients.Group(groupName).ReceiveNotification(message);
    }
}
```

## Scaling with Redis Backplane

```csharp
builder.Services.AddSignalR()
    .AddStackExchangeRedis(connectionString, options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal("MyApp");
    });
```

## Anti-Patterns to Avoid

| Anti-Pattern | Why It's Bad | Better Approach |
|--------------|--------------|-----------------|
| Storing state in Hub properties | Hub instances are created per method call | Use `IMemoryCache`, database, or external store |
| Instantiating Hub directly | Bypasses SignalR infrastructure | Use `IHubContext<THub>` for external messaging |
| Not awaiting `SendAsync` calls | Messages may not be sent before hub method completes | Always `await` async hub calls |
| Adding method parameters without versioning | Breaking change for existing clients | Use custom object parameters |
| Ignoring reconnection group loss | Clients lose group membership on reconnect | Re-add to groups in `OnConnectedAsync` or client reconnect handler |
| Large payloads over SignalR | Memory pressure, bandwidth issues | Use REST/gRPC for bulk data, SignalR for notifications |
| Missing backplane in multi-server | Messages only reach clients on same server | Use Redis backplane or Azure SignalR Service |
| Exposing ORM entities directly | May serialize sensitive data | Use DTOs with explicit properties |
| Not validating incoming messages | Security risk after initial auth | Validate every hub method input |

## Best Practices

### Connection Management
1. **Enable automatic reconnection** with exponential backoff delays
2. **Handle group rejoining** explicitly after reconnection (connection ID changes)
3. **Implement heartbeat monitoring** on the client to detect stale connections
4. **Use sticky sessions** when scaling across multiple servers (unless using Azure SignalR Service)

### Performance
1. **Use MessagePack protocol** for smaller message sizes and faster serialization
2. **Throttle high-frequency events** like typing indicators or mouse movements
3. **Batch messages** when possible instead of many small sends
4. **Set appropriate buffer sizes** based on expected message throughput

### Security
1. **Authenticate at connection time** using JWT tokens via query string
2. **Authorize hub methods** using `[Authorize]` attribute
3. **Validate all incoming messages** even after authentication
4. **Use HTTPS** for all SignalR connections

### API Design
1. **Use strongly-typed hubs** to catch client method name typos at compile time
2. **Use custom object parameters** to enable backward-compatible API evolution
3. **Version hub names** (e.g., `ChatHubV2`) for breaking changes
4. **Keep hub methods thin** and delegate business logic to services

### Observability
1. **Log connection events** (connect, disconnect, reconnect)
2. **Track transport type** used by each connection
3. **Monitor message delivery** latency and failure rates
4. **Integrate with Application Insights** or other APM tools

## Deliver

- clear hub contracts and connection behavior
- real-time delivery that matches the product scenario
- validation for reconnection and authorization flows
- appropriate scale-out strategy for multi-server deployments

## Validate

- SignalR is the correct transport for the use case
- hub methods remain orchestration-oriented
- group and auth behavior are explicit and tested
- reconnection and group membership are handled correctly
- backplane is configured for multi-server scenarios
- message validation is implemented in hub methods
