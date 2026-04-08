# TUnit Integration Testing Patterns

Use this reference when the repo uses TUnit for integration, API, SignalR, Orleans, or Playwright-driven UI suites rather than only pure unit tests.

The patterns below are grounded in working suites from `AIBase` and `WA.Storied.Agents`: shared per-session fixtures, Aspire-backed distributed application boot, optional `WebApplicationFactory` layering for DI/grain access, and concrete artifact capture on failures.

## Pick The Right Fixture Level

| Need | Recommended Pattern |
|---|---|
| Plain logic verification | normal TUnit test class with no shared infra |
| Real HTTP/API/resource graph | `ClassDataSource<AspireTestFixture>(Shared = SharedType.PerTestSession)` |
| Direct DI services, managers, or grains | `ClassDataSource<AIBaseTestApplication>(Shared = SharedType.PerTestSession)` or equivalent `WebApplicationFactory` wrapper |
| Browser automation | shared Aspire/AppHost fixture plus Playwright helpers or `TUnit.Playwright` |

## Shared AppHost Fixture

For real distributed tests, keep one AppHost boot per test session:

```csharp
[ClassDataSource<AspireTestFixture>(Shared = SharedType.PerTestSession)]
public sealed class HealthTests(AspireTestFixture fixture)
{
    [Test]
    public async Task Health_endpoint_returns_ok()
    {
        using var client = fixture.CreateApiClient();
        var response = await client.GetAsync("/health");

        await Assert.That(response.IsSuccessStatusCode).IsTrue();
    }
}
```

This is the right shape for:

- API health and contract tests
- SignalR or SSE flows
- Playwright-backed UI tests
- AppHost resource graph and startup validation

## Mix TUnit With `WebApplicationFactory`

When the test needs runtime services or Orleans grains from the Host container, keep the TUnit fixture but expose a `WebApplicationFactory` wrapper:

```csharp
[ClassDataSource<TestApplication>(Shared = SharedType.PerTestSession)]
public sealed class OrderRuntimeTests(TestApplication app)
{
    [Test]
    public async Task Order_runtime_can_resolve_grain_from_host_scope()
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

This pattern is especially useful for:

- Orleans grain integration tests
- service-manager and repository tests
- host-level dependency graph validation
- co-hosted SignalR/API/runtime tests

## TUnit Hooks For Deterministic Context

Use hooks for per-test context that must be reset cleanly:

```csharp
public abstract class IntegrationTestBase(TestApplication app)
{
    protected TestApplication Application { get; } = app;
    private IDisposable? _scope;

    [Before(Test)]
    public void SetContext()
    {
        _scope = TestRequestContextScope.Push("test-user");
    }

    [After(Test)]
    public void ClearContext()
    {
        _scope?.Dispose();
        _scope = null;
    }
}
```

Keep hooks small, explicit, and local to the test concern.

## Focused Commands

For TUnit on Microsoft.Testing.Platform, keep the repo's command shape and pass framework switches after `--`:

```bash
# Full project
dotnet test --project Tests/MyProject.Tests/MyProject.Tests.csproj

# One class
dotnet test --project Tests/MyProject.Tests/MyProject.Tests.csproj -- --treenode-filter "/*/*/ChatControllerTests/*"

# One category
dotnet test --project Tests/MyProject.Tests/MyProject.Tests.csproj -- --treenode-filter "/*/*/*/*[Category=Integration]"

# Coverage
dotnet test --project Tests/MyProject.Tests/MyProject.Tests.csproj -- --coverage --coverage-output coverage.cobertura.xml --coverage-output-format cobertura
```

Do not use VSTest-style `--filter` for TUnit suites. Do not put TUnit switches before `--`.

## Playwright In TUnit Suites

Shared fixture boot:

```csharp
public async Task InitializePlaywrightAsync()
{
    var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
    if (exitCode != 0)
    {
        throw new InvalidOperationException($"Playwright install failed: {exitCode}");
    }

    Playwright ??= await Microsoft.Playwright.Playwright.CreateAsync();
    Browser ??= await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
}
```

Per-test usage:

```csharp
await fixture.InitializePlaywrightAsync();
await using var context = await fixture.CreateBrowserContextAsync();
var page = await context.NewPageAsync();
```

Reuse the browser process, but never reuse a mutable page or browser context across tests.

## Capture Results And Failure Evidence

Good TUnit integration suites capture more than just the assertion failure:

- host-side error log dump on HTTP 500 or startup failures
- coverage output such as `coverage.cobertura.xml`
- screenshots and HTML for Playwright failures
- narrowed console output from the fixture rather than unfiltered infrastructure noise

Example failure-artifact pattern:

```csharp
private static async Task CaptureArtifactsAsync(IPage page)
{
    Directory.CreateDirectory("artifacts");
    var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);

    await page.ScreenshotAsync(new PageScreenshotOptions
    {
        Path = Path.Combine("artifacts", $"failure-{timestamp}.png"),
        FullPage = true
    });

    await File.WriteAllTextAsync(
        Path.Combine("artifacts", $"failure-{timestamp}.html"),
        await page.ContentAsync());
}
```

Example server-log dump pattern:

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

## Practical Rules

- Prefer `ClassDataSource<...>(Shared = SharedType.PerTestSession)` for expensive distributed fixtures.
- Keep fixture code responsible for startup, teardown, and shared helpers only; assertions belong in the test classes.
- Resolve real connection strings and endpoints from the Aspire fixture instead of copying appsettings into the test project.
- Use coverage and filter switches that match Microsoft.Testing.Platform, not VSTest conventions.
- Capture enough diagnostics on the first failing run so the next step is a fix, not a blind rerun.
