# Orleans Testing Patterns

Use this reference when the task is about testing Orleans grains together with real hosting, HTTP/SignalR surfaces, Aspire resources, or browser flows.

These patterns are grounded in working test harnesses used in `AIBase` and `WA.Storied.Agents`: one shared AppHost fixture for the distributed topology, an optional `WebApplicationFactory` layer for direct Host DI/grain access, and separate browser contexts per UI test.

## Choose The Harness First

| Need | Best Harness | Why |
|---|---|---|
| Grain runtime behavior only | `InProcessTestCluster` | Fastest Orleans-focused harness without HTTP/UI layers |
| Real API, SignalR, SSE, or AppHost wiring | Shared Aspire/AppHost fixture | Exercises the actual distributed topology and resource graph |
| Direct Host DI services or `IGrainFactory` from the hosted app | Shared Aspire/AppHost fixture + `WebApplicationFactory<TEntryPoint>` | Keeps real infra while letting tests resolve services from the Host container |
| Browser automation against a co-hosted Orleans app | Shared Aspire/AppHost fixture + Playwright | Reuses one browser process and one AppHost boot, but isolates browser state per test |

## Runtime-Only Orleans Tests

When the test is purely about grain scheduling, persistence, reentrancy, or state transitions, stay close to Orleans and use `InProcessTestCluster`:

```csharp
var builder = new InProcessTestClusterBuilder();
builder.ConfigureSilo(siloBuilder =>
{
    siloBuilder.AddMemoryGrainStorageAsDefault();
});

await using var cluster = await builder.BuildAndStartAsync();
var grain = cluster.GrainFactory.GetGrain<IOrderGrain>(Guid.NewGuid());

await grain.SubmitAsync(new SubmitOrder("PO-42"));
var state = await grain.GetStateAsync();
```

Use this harness when HTTP, SignalR, and AppHost resources are irrelevant to the assertion.

## Shared AppHost Fixture For Real Topology Tests

Use Aspire when the assertion depends on the actual Orleans host plus the real surrounding resources:

```csharp
public sealed class AspireTestFixture : IAsyncDisposable
{
    private DistributedApplication? _app;

    public DistributedApplication App =>
        _app ?? throw new InvalidOperationException("App not initialized.");

    public string HostUrl { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.My_AppHost>();

        builder.Services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Warning);
            logging.AddFilter("Aspire.Hosting.Dcp", LogLevel.Warning);
            logging.AddFilter("Aspire.Hosting.Backchannel", LogLevel.Critical);
        });

        _app = await builder.BuildAsync();
        await _app.StartAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("host", cts.Token);

        HostUrl = _app.CreateHttpClient("host", "http").BaseAddress?.ToString()
            ?? throw new InvalidOperationException("Host URL was not resolved.");
    }

    public HttpClient CreateApiClient() => App.CreateHttpClient("host", "http");
}
```

This is the right shape for:

- HTTP endpoint tests
- SignalR/SSE/OpenResponses tests
- admin or operator UI tests
- Orleans flows that require the real host pipeline, auth, or middleware

## Mix AppHost Infra With `WebApplicationFactory`

This is the production-style pattern for co-hosted Orleans apps: boot infrastructure in Aspire once, then create a `WebApplicationFactory` over the Host/API assembly for direct DI and grain access.

```csharp
public sealed class TestApplication
    : WebApplicationFactory<HostEntryPointMarker>, IAsyncDisposable
{
    private static readonly AspireTestFixture SharedFixture = new();
    private readonly Dictionary<string, string?> _overrides = new(StringComparer.OrdinalIgnoreCase);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(_overrides));
    }

    public async Task InitializeAsync()
    {
        await SharedFixture.InitializeAsync();

        var tables = await SharedFixture.App.GetConnectionStringAsync("tables");
        var blobs = await SharedFixture.App.GetConnectionStringAsync("blobs");

        _overrides["ConnectionStrings:Tables"] = tables;
        _overrides["ConnectionStrings:Blobs"] = blobs;

        Environment.SetEnvironmentVariable("ConnectionStrings__Tables", tables);
        Environment.SetEnvironmentVariable("ConnectionStrings__Blobs", blobs);

        CreateClient();
    }

    public AsyncServiceScope CreateScope() => Services.CreateAsyncScope();

    public HttpClient CreateApiClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.Timeout = TimeSpan.FromMinutes(5);
        return client;
    }
}
```

Use this when the test needs:

- `IGrainFactory`
- repositories or managers from DI
- direct access to runtime services
- real HTTP clients and hub connections against the same host
- whichever async fixture contract the test framework expects: xUnit, TUnit, or a repo-local wrapper

## TUnit Per-Session Sharing

For TUnit-based Orleans suites, keep the AppHost boot outside individual tests:

```csharp
[ClassDataSource<TestApplication>(Shared = SharedType.PerTestSession)]
public sealed class OrderGrainIntegrationTests(TestApplication app)
{
    [Test]
    public async Task Order_grain_persists_state_through_real_host()
    {
        await using var scope = app.CreateScope();
        var grainFactory = scope.ServiceProvider.GetRequiredService<IGrainFactory>();

        var grain = grainFactory.GetGrain<IOrderGrain>(Guid.NewGuid());
        await grain.SubmitAsync(new SubmitOrder("PO-42"));

        var state = await grain.GetStateAsync();
        await Assert.That(state.Number).IsEqualTo("PO-42");
    }
}
```

The same pattern works for API-only tests with `ClassDataSource<AspireTestFixture>(Shared = SharedType.PerTestSession)`.

## SignalR And Browser Flows

For SignalR or UI flows, keep connection and browser helpers on the shared fixture:

```csharp
public HubConnection CreateAgentHubConnection(string token)
{
    var hubUrl = new Uri(new Uri(HostUrl), "/agenthub");

    return new HubConnectionBuilder()
        .WithUrl(hubUrl, options =>
        {
            options.AccessTokenProvider = () => Task.FromResult(token)!;
            options.Headers["Authorization"] = $"Bearer {token}";
            options.Transports = HttpTransportType.LongPolling;
        })
        .WithAutomaticReconnect()
        .Build();
}
```

For Playwright:

- initialize Playwright once in the shared fixture
- create a new browser context per test
- set `BaseURL`, viewport, and `IgnoreHTTPSErrors` in the helper, not inline in every test

## Failure Diagnostics

When a host-backed Orleans test fails, emit server-side logs before rethrowing:

```csharp
var logStart = DateTimeOffset.UtcNow;

try
{
    var response = await app.CreateApiClient().GetAsync("/health");
    response.EnsureSuccessStatusCode();
}
catch
{
    Console.WriteLine(app.GetErrorLogDump(logStart));
    throw;
}
```

Useful practices:

- capture error/critical logs from the Host into a test log collector
- print the log dump on HTTP 500 or startup failures
- save Playwright screenshots and HTML on UI failures
- keep AppHost resource logs available when resource-health waits fail

## Anti-Patterns

- Creating `DistributedApplicationTestingBuilder.CreateAsync<...>()` inside each test method
- Booting a second Orleans client inside a co-hosted Host test when the app already uses `UseOrleans`
- Copy-pasting local connection strings into tests instead of resolving them from `SharedFixture.App`
- Using in-memory substitutes for persistence-sensitive or streaming-sensitive integration tests
- Sharing a Playwright page or browser context across tests instead of sharing only the browser process
