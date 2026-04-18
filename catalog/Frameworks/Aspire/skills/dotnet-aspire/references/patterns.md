# .NET Aspire Patterns Reference

Use this reference when the task is clearly about current Aspire application-host design, CLI-first workflows, `ServiceDefaults`, testing, or upgrade checkpoints.

## Table of Contents

- [CLI-first setup flows](#cli-first-setup-flows)
- [AppHost shapes](#apphost-shapes)
- [Current AppHost modeling patterns](#current-apphost-modeling-patterns)
- [Dependency and configuration flow](#dependency-and-configuration-flow)
- [ServiceDefaults boundaries](#servicedefaults-boundaries)
- [Servicing patch posture](#servicing-patch-posture)
- [Closed-box testing](#closed-box-testing)
- [Upgrade checkpoints](#upgrade-checkpoints)
- [Anti-patterns](#anti-patterns)

## CLI-first setup flows

### Create a new starter application

Use the current Aspire CLI when the user wants a fresh distributed-app baseline:

```bash
aspire new aspire-starter --name MyShop
cd MyShop
aspire run
```

This gives you the modern baseline:

- an AppHost for orchestration
- a ServiceDefaults project for cross-cutting infrastructure
- sample service projects wired into the AppHost
- the Aspire Dashboard for local observability

### Enlist an existing solution

When the repo already has working services and you want to add orchestration instead of recreating the solution:

```bash
aspire init
```

Use `aspire init` when you need one of these:

- an AppHost added to an existing solution
- a file-based AppHost created quickly
- Aspire support layered onto code that already exists

### Add capabilities with the CLI

Use the CLI to add official integrations or starter assets instead of hand-editing packages when the command exists:

```bash
aspire add <integration-or-starter>
```

Use `aspire add` when it improves repeatability, especially for:

- common first-party integrations
- starter resources that should match current Aspire conventions
- reducing hand-written AppHost or project-file drift

## Servicing patch posture

Aspire `13.2.0` is the current baseline release in the 13.2 line, not a new application model. Treat 13.2.x updates as CLI and AppHost servicing work that should preserve the existing topology and only refine the toolchain surface.

When you roll a 13.2.x patch:

1. Keep the Aspire CLI and `Aspire.AppHost.Sdk` on the same patch line.
2. Update adjacent Aspire packages that move with the AppHost, especially hosting and testing packages.
3. Run `aspire update` before hand-editing package versions unless the repo intentionally pins them.
4. Revalidate the AppHost start path, resource graph, dashboard, and any deployment scripts after the patch lands.
5. Re-check the current CLI commands that changed in 13.2, especially `aspire start`/`aspire stop`/`aspire ps`, `aspire describe`, `aspire docs`, `aspire agent`, and `aspire restore`.

Do not re-architect the AppHost just because a servicing release shipped.

## AppHost shapes

Current Aspire supports two valid AppHost styles.

### Project-based AppHost

Use this when the repo already uses the normal solution and project structure:

```xml
<Project Sdk="Aspire.AppHost.Sdk/<version>">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\MyShop.Api\MyShop.Api.csproj" />
    <ProjectReference Include="..\MyShop.Web\MyShop.Web.csproj" />
  </ItemGroup>
</Project>
```

Prefer the SDK-style AppHost in current projects. Do not create new 13-era examples that manually model the older AppHost package layout as if it were the default.

### File-based AppHost

Use this when a lightweight single-file orchestration layer is the better fit:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("db")
    .AddDatabase("appdata");

builder.AddProject<Projects.MyShop_Api>("api")
    .WithReference(postgres)
    .WaitFor(postgres);

builder.Build().Run();
```

File-based AppHosts are useful for experimentation, smaller repos, and incremental adoption. They do not automatically include a ServiceDefaults project, so create one when the services need it.

## Current AppHost modeling patterns

### Minimal multi-service topology

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .AddDatabase("catalog")
    .WithDataVolume();

var cache = builder.AddRedis("cache");

var api = builder.AddProject<Projects.MyShop_Api>("api")
    .WithReference(postgres)
    .WithReference(cache)
    .WaitFor(postgres)
    .WaitFor(cache);

builder.AddProject<Projects.MyShop_Web>("web")
    .WithReference(api);

builder.Build().Run();
```

What matters:

- infrastructure resources are modeled explicitly
- consuming services get their config through `WithReference(...)`
- startup ordering is intentional through `WaitFor(...)`
- the AppHost stays at topology level

### Persistent local resources

If the slow path is repeated container bootstrap rather than code changes, keep state across local AppHost restarts:

```csharp
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent)
    .AddDatabase("catalog");
```

Use persistence deliberately. It is helpful for realistic local development, but it can hide initialization bugs if the team forgets the difference between a cold start and a reused container.

### Publish-mode resource switching

Use publish-mode branching when local development should use containers or emulators while published environments should use managed Azure resources:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.ExecutionContext.IsPublishMode
    ? builder.AddAzureRedis("cache")
    : builder.AddRedis("cache").WithDataVolume();

var database = builder.ExecutionContext.IsPublishMode
    ? builder.AddAzurePostgresFlexibleServer("db").AddDatabase("catalog")
    : builder.AddPostgres("db").AddDatabase("catalog");

builder.AddProject<Projects.MyShop_Api>("api")
    .WithReference(cache)
    .WithReference(database)
    .WaitFor(cache)
    .WaitFor(database);
```

This pattern is better than trying to maintain separate hand-written local and cloud topologies.

## Dependency and configuration flow

Official Aspire guidance treats integrations as two related but independent layers:

- hosting integrations extend `IDistributedApplicationBuilder` and model resources in the AppHost
- client integrations wire client libraries into DI, health checks, resiliency, and telemetry

Use that split intentionally.

### `WithReference` is the default wiring mechanism

`WithReference(...)` is the normal way to pass endpoints, connection strings, credentials, or other configuration between resources.

Use it instead of:

- hardcoded URLs
- copy-pasted connection strings
- manually synchronized environment variables

### `WaitFor` is for startup order, not configuration

`WaitFor(...)` solves a different problem than `WithReference(...)`.

- `WithReference(...)` injects configuration and expresses the dependency edge
- `WaitFor(...)` delays startup until the dependency is ready or healthy

Use both when the consuming service should not even begin until the dependency is available.

### Named endpoints

Use named endpoints when a service exposes more than one surface:

```csharp
var api = builder.AddProject<Projects.MyShop_Api>("api")
    .WithHttpEndpoint(port: 5001, name: "public")
    .WithHttpEndpoint(port: 5002, name: "internal");
```

Named endpoints are useful for:

- separating public versus internal traffic
- gRPC and HTTP on the same service
- routing test-only or admin endpoints distinctly

## ServiceDefaults boundaries

The current ServiceDefaults template exists to centralize cross-cutting infrastructure, not shared business code.

### Keep it focused

Good content for `ServiceDefaults`:

- OpenTelemetry logging, metrics, and tracing
- health checks
- service discovery
- `HttpClient` resilience defaults
- default endpoint mapping for health endpoints

Bad content for `ServiceDefaults`:

- domain models
- repository implementations
- DTOs
- application-specific utilities unrelated to cross-cutting infrastructure

### Typical structure

```csharp
public static class Extensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }
}
```

Current official guidance also keeps health endpoints and tracing filters aligned with these defaults. Do not fork this pattern casually unless the repo truly needs a custom shared-hosting baseline.

## Closed-box testing

Use Aspire testing when the requirement is to exercise the distributed system as a system instead of unit-testing a single component.

Prefer Aspire testing for:

- end-to-end flows across multiple services
- verifying resource startup and wiring
- validating service discovery and real HTTP or messaging paths
- regression tests around the AppHost topology

Do not reach for Aspire testing when:

- a plain xUnit or NUnit test against one class is enough
- the service logic can be verified entirely in-process
- the AppHost topology is irrelevant to the assertion

Testing is especially important for:

- ensuring `WithReference(...)` and `WaitFor(...)` match the intended runtime graph
- catching broken resource names or renamed endpoints
- proving a version upgrade did not silently break orchestration

Load `testing.md` when the repo mixes AppHost lifecycle, `DistributedApplicationTestingBuilder`, `WebApplicationFactory`, SignalR, or Playwright instead of using Aspire as a pure black-box API harness.

## Upgrade checkpoints

When modernizing older Aspire solutions, verify these points explicitly:

1. The team is using the current Aspire CLI, and the upgrade path starts there.
2. The AppHost project uses the current SDK-style layout rather than retaining older manual AppHost package wiring by inertia.
3. The AppHost target framework is aligned with current tooling expectations for Aspire 13-era projects.
4. The repo has removed assumptions about the old workload-based setup when those assumptions no longer apply.
5. ServiceDefaults remains a narrow infrastructure project instead of having accumulated random shared code over time.

Use:

```bash
aspire update
```

Pair the CLI upgrade with a review of:

- AppHost project structure
- project references
- integration package versions
- test coverage for the distributed topology
- local run and dashboard behavior

## Anti-Patterns

- Treating the AppHost like an ordinary application project with business logic, service implementations, or repo-specific glue.
- Using `WithEnvironment(...)` as the first answer when `WithReference(...)` or a normal integration already models the dependency correctly.
- Assuming `WithExternalHttpEndpoints()` belongs on every web project. It should follow runtime needs, especially publish targets such as App Service.
- Modeling a large topology with vague resource names like `service1` and `db2`, then expecting logs and traces to remain understandable.
- Carrying an obsolete 8.x or 9.x setup pattern into new samples or new repos without a compatibility reason.
