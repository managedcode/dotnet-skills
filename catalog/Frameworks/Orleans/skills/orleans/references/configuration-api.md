# Configuration, Deployment, and Observability API

Detailed configuration and operational patterns from official Orleans documentation.

## Silo Configuration

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.UseOrleans((context, silo) =>
{
    if (context.HostingEnvironment.IsDevelopment())
    {
        silo.UseLocalhostClustering();
        silo.AddMemoryGrainStorage("Default");
        silo.UseInMemoryReminderService();
    }
    else
    {
        // Production clustering
        silo.UseAzureStorageClustering(options =>
            options.ConfigureTableServiceClient(
                new DefaultAzureCredential(),
                new Uri("https://mystorageaccount.table.core.windows.net")));

        // Production persistence
        silo.AddRedisGrainStorage("Default", options =>
            options.ConfigurationOptions = ConfigurationOptions.Parse(redisConn));

        // Production reminders
        silo.UseRedisReminderService(options =>
            options.ConfigurationOptions = ConfigurationOptions.Parse(redisConn));

        silo.Configure<ClusterOptions>(options =>
        {
            options.ClusterId = "prod-cluster";
            options.ServiceId = "my-service";
        });
    }
});
```

## Client Configuration

### Co-hosted Client (Recommended)

Client runs in same process as silo. Get `IClusterClient` from DI:

```csharp
var grain = serviceProvider.GetRequiredService<IClusterClient>()
    .GetGrain<IMyGrain>("key");
```

### External Client

```csharp
builder.UseOrleansClient(client =>
{
    client.UseAzureStorageClustering(options =>
        options.ConfigureTableServiceClient(connectionString));

    client.Configure<ClusterOptions>(options =>
    {
        options.ClusterId = "prod-cluster";
        options.ServiceId = "my-service";
    });
});
```

### Connection Retry

```csharp
public class RetryFilter : IClientConnectionRetryFilter
{
    private int _attempt;
    public async Task<bool> ShouldRetryConnectionAttempt(
        Exception exception, CancellationToken ct)
    {
        if (_attempt++ > 5) return false;
        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, _attempt)), ct);
        return true;
    }
}
```

## .NET Aspire Integration

```csharp
// AppHost
var storage = builder.AddAzureStorage("storage");
var clustering = storage.AddTables("clustering");
var grainStorage = storage.AddBlobs("grain-state");
var redis = builder.AddRedis("redis");

var orleans = builder.AddOrleans("my-cluster")
    .WithClustering(clustering)
    .WithGrainStorage("Default", grainStorage)
    .WithReminders(redis)
    .WithMemoryStreams("StreamProvider");

builder.AddProject<Projects.Silo>("silo")
    .WithReference(orleans);

builder.AddProject<Projects.WebFrontend>("web")
    .WithReference(orleans.AsClient());  // client-only
```

## Clustering Providers

| Provider | Package | Use Case |
|---|---|---|
| Azure Table | `Microsoft.Orleans.Clustering.AzureStorage` | Azure-hosted |
| Redis | `Microsoft.Orleans.Clustering.Redis` | Redis-available environments |
| Cosmos DB | `Microsoft.Orleans.Clustering.Cosmos` | Cosmos-first architectures |
| ADO.NET | `Microsoft.Orleans.Clustering.AdoNet` | SQL Server / PostgreSQL |
| Consul | `Microsoft.Orleans.Clustering.Consul` | Consul-based infrastructure |
| Kubernetes | via sidecar | K8s native |
| Localhost | built-in | Dev only |

## GC Configuration

Critical for Orleans performance. Configure in project file:

```xml
<PropertyGroup>
  <ServerGarbageCollection>true</ServerGarbageCollection>
  <ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>
</PropertyGroup>
```

Or via `runtimeconfig.json`:

```json
{
  "runtimeOptions": {
    "configProperties": {
      "System.GC.Server": true,
      "System.GC.Concurrent": true
    }
  }
}
```

## Observability

### Metrics (System.Diagnostics.Metrics)

Meter name: `"Microsoft.Orleans"`

```csharp
// OpenTelemetry export
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("Microsoft.Orleans");
        metrics.AddOtlpExporter();
    });
```

Monitor from CLI:
```bash
dotnet counters monitor -n <Process> --counters Microsoft.Orleans
```

Meter categories: Networking, Messaging, Gateway, Runtime, Catalog (activations), Directory, Consistent Ring, Watchdog, Client, Grains, App Requests, Reminders, Storage, Streams, Transactions.

### Distributed Tracing

```csharp
// Enable
siloBuilder.AddActivityPropagation();
clientBuilder.AddActivityPropagation();

// Or via options
services.Configure<ActivityPropagationGrainCallFilterOptions>(o =>
    o.EnableDistributedTracing = true);

// Export
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("Microsoft.Orleans.Runtime");
        tracing.AddSource("Microsoft.Orleans.Application");
        tracing.AddOtlpExporter();
    });
```

Activity sources: `"Microsoft.Orleans.Runtime"`, `"Microsoft.Orleans.Application"`.

### Dashboard

```csharp
siloBuilder.UseDashboard(options =>
{
    options.Port = 8080;
    options.Host = "*";
});
```

Secure with ASP.NET Core authorization middleware.

## Deployment Targets

### Azure Container Apps

```csharp
siloBuilder.UseAzureStorageClustering(options =>
    options.ConfigureTableServiceClient(new DefaultAzureCredential(), tableUri));

// ACA provides automatic scaling, zero-downtime deployments
```

### Kubernetes

```csharp
siloBuilder.UseKubernetesHosting(); // auto-configures from K8s environment

// Requires proper RBAC, service account, and headless service
// Configure liveness and readiness probes
```

### Azure App Service

```csharp
// Use Azure Storage for clustering (no multicast)
// Configure sticky sessions for client gateway affinity
// Use deployment slots for zero-downtime upgrades
```

### Graceful Shutdown

```csharp
// Automatic with UseConsoleLifetime() or ASP.NET Core host
// Manual: await host.StopAsync(cancellationToken)

// Configure drain period
services.Configure<SiloMessagingOptions>(options =>
{
    options.ShutdownGracePeriod = TimeSpan.FromSeconds(30);
});
```

## Heterogeneous Silos

Different silos can support different grain types. All must reference grain interfaces.

```csharp
services.Configure<GrainClassOptions>(options =>
{
    options.ExcludedGrainTypes.Add("MyHeavyGrain");
});

services.Configure<TypeManagementOptions>(options =>
{
    options.TypeMapRefreshInterval = TimeSpan.FromMinutes(1);
});
```

Limitations: stateless grains and `[ImplicitStreamSubscription]` not supported in heterogeneous mode.

## Silo Metadata (Orleans 9+)

Label silos for placement filtering:

```csharp
siloBuilder.Configure<SiloMetadataOptions>(options =>
{
    options.Metadata["zone"] = "us-east-1a";
    options.Metadata["tier"] = "compute";
});
```

Used with placement filtering for zone-aware and hardware-affinity placement.

## Silo Lifecycle

Ordered startup/shutdown via observable lifecycle. Components participate via `ILifecycleParticipant<ISiloLifecycle>`.

### Lifecycle Stages

| Stage | Value | Purpose |
|---|---|---|
| `First` | `int.MinValue` | Earliest stage |
| `RuntimeInitialize` | 2000 | Threading init |
| `RuntimeServices` | 4000 | Networking, agents |
| `RuntimeStorageServices` | 6000 | Storage init |
| `RuntimeGrainServices` | 8000 | Grain type management, membership, directory |
| `ApplicationServices` | 10000 | Application layer |
| `BecomeActive` | `Active - 1` | Join cluster |
| `Active` | 20000 | Ready for workload |
| `Last` | `int.MaxValue` | Latest stage |

## Grain Directory

Maps grain identity → activation location (silo). Ensures at most one activation.

| Implementation | Package | Notes |
|---|---|---|
| Distributed In-Cluster (default) | built-in | Eventually consistent DHT |
| Strongly-Consistent (Orleans 10 preview) | built-in | Versioned range locks, prevents duplicates |
| ADO.NET | `Microsoft.Orleans.GrainDirectory.AdoNet` | SQL Server, PostgreSQL, MySQL, Oracle |
| Azure Table | `Microsoft.Orleans.GrainDirectory.AzureStorage` | Azure-hosted |
| Redis | `Microsoft.Orleans.GrainDirectory.Redis` | Redis-available |

```csharp
// Per-grain-type directory
[GrainDirectory(GrainDirectoryName = "my-directory")]
public class MyGrain : Grain, IMyGrain { }

siloBuilder.AddRedisGrainDirectory("my-directory", options => { });
```

## TLS Configuration

Package: `Microsoft.Orleans.Connections.Security`

```csharp
// Silo — certificate from file
var cert = X509CertificateLoader.LoadPkcs12FromFile("cert.pfx", "password");
siloBuilder.UseTls(cert, options =>
{
    options.OnAuthenticateAsClient = (conn, ssl) =>
        ssl.TargetHost = "my-service";
});

// Client
clientBuilder.UseTls(cert, options =>
    options.AllowAnyRemoteCertificate()); // dev only

// Mutual TLS — client sends certificate for silo verification
```

## Dashboard (Orleans 10.0)

Packages: `Microsoft.Orleans.Dashboard`, `Microsoft.Orleans.Dashboard.Abstractions`

```csharp
siloBuilder.AddDashboard();
app.MapOrleansDashboard(); // default /
app.MapOrleansDashboard(routePrefix: "/dashboard");

// Authorization
app.MapOrleansDashboard().RequireAuthorization("AdminPolicy");
```

Features: cluster overview, grain monitoring, method profiling, reminder management, live log streaming, grain state inspection.

Options: `HideTrace` (bool), `CounterUpdateIntervalMs` (int, default 1000), `HistoryLength` (int, default 100).

Exclude grains from profiling: `[NoProfiling]` attribute.

## Startup Tasks

Preferred: use .NET `BackgroundService` or `IHostedService`. Register after `UseOrleans()`.

```csharp
// BackgroundService approach
public class GrainInitializer : BackgroundService
{
    private readonly IGrainFactory _grainFactory;
    public GrainInitializer(IGrainFactory grainFactory) => _grainFactory = grainFactory;
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var grain = _grainFactory.GetGrain<IInitGrain>(0);
        await grain.Initialize();
    }
}

// Legacy startup task
siloBuilder.AddStartupTask(async (IServiceProvider sp, CancellationToken ct) =>
{
    var grain = sp.GetRequiredService<IGrainFactory>().GetGrain<IInitGrain>(0);
    await grain.Initialize();
});
```

Warning: exceptions from startup tasks stop the silo (fail-fast).

## ADO.NET Configuration

NuGet packages: `Microsoft.Orleans.Clustering.AdoNet`, `Microsoft.Orleans.Persistence.AdoNet`, `Microsoft.Orleans.Reminders.AdoNet`

```csharp
siloBuilder.UseAdoNetClustering(options =>
{
    options.Invariant = "Microsoft.Data.SqlClient"; // Orleans 10.0
    options.ConnectionString = connectionString;
});
siloBuilder.AddAdoNetGrainStorage("Default", options =>
{
    options.Invariant = "Microsoft.Data.SqlClient";
    options.ConnectionString = connectionString;
});
siloBuilder.UseAdoNetReminderService(options =>
{
    options.Invariant = "Microsoft.Data.SqlClient";
    options.ConnectionString = connectionString;
});
```

Supported databases and invariants:

| Database | Invariant |
|---|---|
| SQL Server | `Microsoft.Data.SqlClient` (10.0) / `System.Data.SqlClient` (7-9) |
| PostgreSQL | `Npgsql` |
| MySQL/MariaDB | `MySql.Data.MySqlClient` |
| Oracle | `Oracle.DataAccess.Client` |

**Prerequisite**: run SQL setup scripts from `dotnet/orleans` repo before use.

## Azure App Service Deployment

Requires VNet integration and private ports for silo-to-silo communication.

```csharp
var endpointAddress = IPAddress.Parse(builder.Configuration["WEBSITE_PRIVATE_IP"]!);
var strPorts = builder.Configuration["WEBSITE_PRIVATE_PORTS"]!.Split(',');
var (siloPort, gatewayPort) = (int.Parse(strPorts[0]), int.Parse(strPorts[1]));
siloBuilder.ConfigureEndpoints(endpointAddress, siloPort, gatewayPort,
    listenOnAnyHostAddress: true);
```

Enable private ports: `az webapp config set --generic-configurations '{"vnetPrivatePortsCount": "2"}'`

## Kubernetes Deployment

Package: `Microsoft.Orleans.Hosting.Kubernetes`

```csharp
siloBuilder.UseKubernetesHosting();
// Auto-configures SiloName, AdvertisedIPAddress, endpoints from K8s env
// Still need separate clustering provider (Redis, Azure Table, etc.)
```

Required pod labels: `orleans/serviceId`, `orleans/clusterId`. Required env: `POD_NAME`, `POD_NAMESPACE`, `POD_IP`.

Key YAML: `terminationGracePeriodSeconds: 180`, `DOTNET_SHUTDOWNTIMEOUTSECONDS: "120"`, `maxUnavailable: 0`, `maxSurge: 1`, `minReadySeconds: 60`.

RBAC: `get`, `watch`, `list`, `delete`, `patch` on `pods`.

## Handling Failures

- Method calls return exceptions; Orleans propagates across silos
- Getting a grain reference always succeeds locally (lazy activation)
- Orleans auto-reactivates failed grains on next call on another silo
- At-most-once message delivery by default (no automatic retries)

Recovery strategies:
1. **Retry** — suitable when no half-done state changes
2. **Reload state** — `ReadStateAsync()` to refresh from storage
3. **Transactions** — for multi-grain atomicity
4. **Process Manager / Saga** — for complex multi-grain orchestration

## Cluster Management

Fully distributed peer-to-peer membership protocol.

```csharp
siloBuilder.Configure<ClusterMembershipOptions>(options =>
{
    options.NumProbedSilos = 10;           // default (Orleans 9+)
    options.NumVotesForDeathDeclaration = 2;
    options.DeathVoteExpirationTimeout = TimeSpan.FromSeconds(180);
    options.ProbeTimeout = TimeSpan.FromSeconds(10);
    options.NumMissedProbesLimit = 3;
});
```

Typical failure detection: ~15 seconds (Orleans 9+). Properties: handles any number of failures, self-monitoring with health scoring, indirect probing, table unavailability never causes false death declarations.

## Messaging Delivery Guarantees

Default: **at-most-once** (no automatic retries). Message delivered once or not at all, never twice.

With retries: **at-least-once** (may arrive multiple times, no dedup). Every message has automatic configurable timeout.

## Migration Guide (7.x → 10.x)

| Change | Migration |
|---|---|
| `AddGrainCallFilter` removed | Use `AddIncomingGrainCallFilter` |
| `RegisterTimer` obsoleted | Use `RegisterGrainTimer` with `GrainTimerCreationOptions` |
| ADO.NET invariant | Use `Microsoft.Data.SqlClient` instead of `System.Data.SqlClient` |
| `CancelRequestOnTimeout` default → `false` | Set `true` explicitly if needed |
| Default placement → `ResourceOptimized` (9.2) | Explicitly set if different behavior needed |

**Rolling upgrades 7.x → 10.0 NOT recommended** due to protocol changes. Deploy new cluster, migrate state, switch traffic.

## Testing

### InProcessTestCluster (Orleans 9+, recommended)

```csharp
var builder = new InProcessTestClusterBuilder();
builder.ConfigureSilo((options, siloBuilder) =>
{
    siloBuilder.AddMemoryGrainStorage("Default");
});
var cluster = builder.Build();
await cluster.DeployAsync();

var grain = cluster.Client.GetGrain<IMyGrain>(0);
var result = await grain.DoWork();

// Dynamic silo management
var newSilo = await cluster.StartSiloAsync();
await cluster.StopSiloAsync(newSilo);
```

### TestCluster (legacy, still supported)

```csharp
var builder = new TestClusterBuilder();
builder.AddSiloBuilderConfigurator<TestSiloConfig>();
var cluster = builder.Build();
cluster.Deploy();
```

### xUnit Sharing

```csharp
[CollectionDefinition("Orleans")]
public class ClusterCollection : ICollectionFixture<ClusterFixture> { }

[Collection("Orleans")]
public class MyTests
{
    private readonly TestCluster _cluster;
    public MyTests(ClusterFixture fixture) => _cluster = fixture.Cluster;
}
```

## Best Practices Summary

**Good fit**: millions of loosely coupled entities, small single-threaded, interactive workloads.

**Bad fit**: shared memory between entities, few large multithreaded entities, global coordination, long-running batch jobs.

**Rules**:
- Avoid chatty inter-grain communication
- Avoid bottleneck grains — use staged aggregation
- Never block threads
- Use `[StatelessWorker]` for stateless operations
- Initial `ReadStateAsync` happens automatically before `OnActivateAsync`
- Call `WriteStateAsync()` after state changes
- Use Polly for retry logic

## NuGet Packages Map

### Core

| Package | Purpose |
|---|---|
| `Microsoft.Orleans.Server` | Silo hosting (includes Client) |
| `Microsoft.Orleans.Client` | Standalone client |
| `Microsoft.Orleans.Sdk` | Grain development metapackage |

### Dashboard

| Package | Purpose |
|---|---|
| `Microsoft.Orleans.Dashboard` | Built-in dashboard (10.0) |
| `Microsoft.Orleans.Dashboard.Abstractions` | Dashboard abstractions |

### Clustering

| Package | Backend |
|---|---|
| `Microsoft.Orleans.Clustering.AzureStorage` | Azure Table |
| `Microsoft.Orleans.Clustering.AdoNet` | SQL Server, PostgreSQL, MySQL, Oracle |
| `Microsoft.Orleans.Clustering.Redis` | Redis |
| `Microsoft.Orleans.Clustering.Cosmos` | Cosmos DB |
| `Microsoft.Orleans.Clustering.Consul` | Consul |

### Persistence

| Package | Backend |
|---|---|
| `Microsoft.Orleans.Persistence.AzureStorage` | Azure Table/Blob |
| `Microsoft.Orleans.Persistence.AdoNet` | SQL |
| `Microsoft.Orleans.Persistence.Redis` | Redis |
| `Microsoft.Orleans.Persistence.Cosmos` | Cosmos DB |
| `Microsoft.Orleans.Persistence.DynamoDB` | DynamoDB |

### Reminders

| Package | Backend |
|---|---|
| `Microsoft.Orleans.Reminders.AzureStorage` | Azure Table |
| `Microsoft.Orleans.Reminders.AdoNet` | SQL |
| `Microsoft.Orleans.Reminders.Redis` | Redis |
| `Microsoft.Orleans.Reminders.Cosmos` | Cosmos DB |

### Grain Directory

| Package | Backend |
|---|---|
| `Microsoft.Orleans.GrainDirectory.AzureStorage` | Azure Table |
| `Microsoft.Orleans.GrainDirectory.AdoNet` | SQL |
| `Microsoft.Orleans.GrainDirectory.Redis` | Redis |

### Streaming

| Package | Backend |
|---|---|
| `Microsoft.Orleans.Streaming.EventHubs` | Azure Event Hubs |
| `Microsoft.Orleans.Streaming.AzureStorage` | Azure Queue |

### Serializers

| Package | Format |
|---|---|
| `Microsoft.Orleans.Serialization.SystemTextJson` | System.Text.Json |
| `Microsoft.Orleans.Serialization.NewtonsoftJson` | Newtonsoft.Json |
| `Microsoft.Orleans.Serialization.MessagePack` | MessagePack |
| `Microsoft.Orleans.Serialization.Protobuf` | Protobuf |

### Other

| Package | Purpose |
|---|---|
| `Microsoft.Orleans.Transactions` | ACID transactions |
| `Microsoft.Orleans.EventSourcing` | JournaledGrain |
| `Microsoft.Orleans.Connections.Security` | TLS |
| `Microsoft.Orleans.Hosting.Kubernetes` | K8s hosting |
| `Microsoft.Orleans.Analyzers` | Code analyzers |
| `Microsoft.Orleans.TestingHost` | TestCluster |

## Local Development Configuration

```csharp
// Silo — single-node, in-memory everything
await Host.CreateDefaultBuilder(args)
    .UseOrleans(silo => silo.UseLocalhostClustering())
    .RunConsoleAsync();

// Client — connect to local cluster
using IHost host = Host.CreateDefaultBuilder(args)
    .UseOrleansClient(client => client.UseLocalhostClustering())
    .UseConsoleLifetime()
    .Build();
await host.StartAsync();
```

Packages: `Microsoft.Orleans.Server` for silo, `Microsoft.Orleans.Client` for client.

## Server Configuration

```csharp
builder.UseOrleans((context, silo) =>
{
    silo.Configure<ClusterOptions>(options =>
    {
        options.ClusterId = "my-cluster";
        options.ServiceId = "my-service";
    });

    silo.Configure<EndpointOptions>(options =>
    {
        options.SiloPort = 11111;
        options.GatewayPort = 30000;
        options.AdvertisedIPAddress = IPAddress.Loopback;
    });

    // Clustering, persistence, reminders, streams...
});
```

## Client Configuration

```csharp
builder.UseOrleansClient(client =>
{
    client.Configure<ClusterOptions>(options =>
    {
        options.ClusterId = "my-cluster";
        options.ServiceId = "my-service";
    });
    // Clustering provider must match silo
    client.UseAzureStorageClustering(options =>
        options.ConfigureTableServiceClient(connectionString));
});
```

Prefer `TokenCredential` with `DefaultAzureCredential` over connection strings for Azure providers.

## Typical Configurations

### Aspire + Redis (Recommended for Orleans 8+)

```csharp
// AppHost
var redis = builder.AddRedis("redis");
var orleans = builder.AddOrleans("cluster")
    .WithClustering(redis)
    .WithGrainStorage("Default", redis)
    .WithReminders(redis);

// Silo
builder.AddKeyedRedisClient("redis");
builder.UseOrleans();
```

### Azure Storage Production

```csharp
silo.UseAzureStorageClustering(options =>
    options.ConfigureTableServiceClient(
        new DefaultAzureCredential(),
        new Uri("https://account.table.core.windows.net")));
silo.AddAzureBlobGrainStorage("Default", options =>
    options.ConfigureBlobServiceClient(
        new DefaultAzureCredential(),
        new Uri("https://account.blob.core.windows.net")));
```

### SQL Server Production

```csharp
silo.UseAdoNetClustering(options =>
{
    options.Invariant = "Microsoft.Data.SqlClient"; // Orleans 10.0
    options.ConnectionString = connectionString;
});
```

### Unreliable Test Cluster (No External Deps)

```csharp
// Silo
silo.UseDevelopmentClustering(primarySiloEndpoint);

// Client
client.UseStaticClustering(gateways);
```

## Service Fabric Deployment

Orleans integrates with Azure Service Fabric for deployment, service discovery, and failover.

```csharp
// Use Service Fabric's membership system
siloBuilder.UseServiceFabricClustering(serviceContext);
```

Key considerations:
- Service Fabric manages silo lifecycle through reliable services
- Use Service Fabric's naming service for cluster membership
- Co-locate silos with Service Fabric partitions for locality
- Configure endpoints through Service Fabric service manifests

## Consul Deployment

Package: `Microsoft.Orleans.Clustering.Consul`

```csharp
// Silo
silo.UseConsulSiloClustering(options =>
    options.ConfigureConsulClient(new Uri("http://localhost:8500")));

// Client
client.UseConsulClientClustering(options =>
    options.ConfigureConsulClient(new Uri("http://localhost:8500")));
```

Uses Consul Key/Value store with Check-And-Set (CAS) operations. Keys prefixed with `orleans/`. Each silo registers silo details + last alive timestamp.

Limitations: only basic membership protocol (no atomic multi-key updates), KV not replicated between Consul data centers.

## Troubleshooting Deployments

### Common `SiloUnavailableException` Causes

- Silo crashed/terminated and evicted from cluster
- Network partition between silos
- Silo shutting down during request
- No silos available for client connection

### Configuration Issues

- Mismatched clustering provider between silos and clients
- Local/dev config used in cloud environments
- Missing/incorrect connection strings
- `ClusterId` / `ServiceId` mismatch between silo and client

### Container/K8s Issues

- Insufficient resource requests/limits
- Clustering provider connectivity failure
- SiloPort (11111) / GatewayPort (30000) not correctly exposed
- Missing liveness/readiness probes

### Debugging

```csharp
builder.Logging.SetMinimumLevel(LogLevel.Information);
// For Orleans internals:
builder.Logging.AddFilter("Orleans", LogLevel.Debug);
```

## Observability Details

### Silo Error Code Monitoring

Orleans silos emit structured error codes with categories:
- `Runtime` — activation, deactivation, messaging errors
- `Catalog` — grain directory, activation catalog
- `Networking` — connection, socket errors
- `Membership` — cluster membership, failure detection
- `Storage` — persistence provider errors

Monitor via standard `Microsoft.Extensions.Logging` — error codes appear in log messages.

### Client Error Code Monitoring

Client-side error categories:
- `Gateway` — connection to silo gateways
- `Messaging` — request/response failures, timeouts
- `Runtime` — client lifecycle errors

### OpenTelemetry Full Setup

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("Microsoft.Orleans");
        metrics.AddOtlpExporter();
    })
    .WithTracing(tracing =>
    {
        tracing.AddSource("Microsoft.Orleans.Runtime");
        tracing.AddSource("Microsoft.Orleans.Application");
        tracing.AddOtlpExporter();
    });
```

### Metrics Categories

| Category | What It Tracks |
|---|---|
| Networking | Connections, bytes sent/received |
| Messaging | Messages sent/received, queue lengths |
| Gateway | Client connections, active gateways |
| Runtime | Thread pool, memory, CPU |
| Catalog | Activations, activation creation/destruction |
| Directory | Directory lookups, registrations |
| Grains | Per-grain-type activation counts |
| App Requests | Grain method call latency and throughput |
| Reminders | Active reminders, ticks |
| Storage | Read/write latency, failures |
| Streams | Events processed, subscription counts |
| Transactions | Commit/abort rates |

## Key Options Classes

| Class | Purpose |
|---|---|
| `ClusterOptions` | ClusterId, ServiceId |
| `ClusterMembershipOptions` | Probing, death votes, failure detection |
| `SiloMessagingOptions` | ResponseTimeout, ShutdownGracePeriod |
| `ClientMessagingOptions` | ResponseTimeout, CancelRequestOnTimeout |
| `GrainCollectionOptions` | CollectionAge, memory shedding |
| `GrainClassOptions` | ExcludedGrainTypes |
| `GrainVersioningOptions` | Compatibility and version selector strategies |
| `TypeManagementOptions` | TypeMapRefreshInterval |
| `SiloMetadataOptions` | Metadata labels for placement filtering |
| `ResourceOptimizedPlacementOptions` | Placement weights (CPU, memory, etc.) |
| `EndpointOptions` | Silo/gateway ports |
| `LoadSheddingOptions` | CPU threshold for load shedding |
| `SchedulingOptions` | Scheduler behavior |
| `NetworkingOptions` | Socket/connection timeouts |
| `StatisticsOptions` | Statistics output |
| `ActivityPropagationGrainCallFilterOptions` | Distributed tracing |
| `DashboardOptions` | Dashboard port, trace hiding, update interval |
| `GrainProfilerOptions` | Profiling behavior |
