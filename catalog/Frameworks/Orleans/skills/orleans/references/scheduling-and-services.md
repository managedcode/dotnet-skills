# Scheduling and Runtime Services

Use this reference after deciding whether work is activation-local, recurring, one-time, per-host, or per-silo. Do not select these APIs only because they all “run in the background.” Their ownership and failure models are different.

## Contents

- [Version gate](#version-gate)
- [Grain timers](#grain-timers)
- [Reminders](#reminders)
- [Durable Jobs](#durable-jobs)
- [Stateless worker grains](#stateless-worker-grains)
- [Background services and hosted services](#background-services-and-hosted-services)
- [Startup tasks and silo lifecycle](#startup-tasks-and-silo-lifecycle)
- [Grain services](#grain-services)
- [Failure-oriented tests](#failure-oriented-tests)
- [Official sources](#official-sources)

## Version Gate

Check the target project's Orleans packages before copying an API. This reference targets the current Orleans 10.2 model:

- use `RegisterGrainTimer`, not the obsolete `RegisterTimer` API;
- use standard .NET `BackgroundService` or `IHostedService` for normal host-owned work;
- treat Orleans startup tasks as a lifecycle-specific compatibility surface;
- treat `Microsoft.Orleans.DurableJobs` and `Microsoft.Orleans.DurableJobs.AzureStorage` as experimental because the 10.2.1 packages are versioned `10.2.1-alpha.1`;
- use `IJobRunContext`, not the older `IDurableJobContext` name found in stale examples.

## Grain Timers

Purpose: run periodic or one-shot work within the current grain activation while preserving Orleans turn scheduling.

```csharp
private IGrainTimer? _flushTimer;

public override Task OnActivateAsync(CancellationToken cancellationToken)
{
    _flushTimer = this.RegisterGrainTimer(
        static (grain, ct) => grain.FlushAsync(ct),
        this,
        new GrainTimerCreationOptions
        {
            DueTime = TimeSpan.FromSeconds(5),
            Period = TimeSpan.FromSeconds(30),
            Interleave = false,
            KeepAlive = false
        });

    return base.OnActivateAsync(cancellationToken);
}
```

Use a timer for cache refresh, batching, lease renewal, polling, or periodic flushes that matter only while the activation is alive. A timer:

- stops when the activation deactivates or its silo fails;
- does not make its work durable;
- invokes each callback on a separate grain turn;
- measures `Period` from callback completion, so callbacks do not overlap;
- does not keep an idle activation alive unless `KeepAlive` is `true`;
- does not interleave by default with the modern API;
- can be changed through `IGrainTimer.Change` and canceled by disposal.

Do not persist a timer handle. Recreate activation-local timers during activation. If a future occurrence must survive activation loss, select a reminder or Durable Job.

## Reminders

Purpose: keep a recurring schedule definition for a grain identity and reactivate that grain when a tick is due.

```csharp
public sealed class SubscriptionGrain : Grain, ISubscriptionGrain, IRemindable
{
    public async Task StartRenewalChecksAsync()
    {
        await RegisterOrUpdateReminder(
            "renewal-check",
            dueTime: TimeSpan.FromMinutes(5),
            period: TimeSpan.FromHours(12));
    }

    public Task ReceiveReminder(string reminderName, TickStatus status) =>
        reminderName switch
        {
            "renewal-check" => CheckRenewalAsync(),
            _ => Task.CompletedTask
        };
}
```

Use reminders for recurring low-frequency work such as subscription review, stale-state reconciliation, daily rollups, or periodic maintenance. A reminder:

- belongs to the logical grain, not one activation;
- persists its definition in a reminder provider;
- reactivates the grain when necessary;
- continues across partial/full cluster restarts unless unregistered;
- delivers through normal grain messaging and scheduling;
- is explicitly unregistered rather than disposed.

The reminder service does not persist every scheduled occurrence. If the cluster is unavailable when a tick is due, that tick is missed and only a later tick is delivered. Therefore, make the handler derive required work from durable timestamps/state when catch-up matters. Do not assume “one callback per calendar occurrence.”

Use periods measured in minutes, hours, or days. Use a grain timer for frequent activation-local ticks. Use a Durable Job for one future occurrence.

Configure a persistent reminder provider in production. In-memory reminders are for development/testing and disappear on restart.

## Durable Jobs

Purpose: schedule persistent one-time work to a target grain for execution around a future due time.

Install the experimental packages on the same Orleans line as the rest of the app:

```bash
dotnet add package Microsoft.Orleans.DurableJobs --version 10.2.1-alpha.1
dotnet add package Microsoft.Orleans.DurableJobs.AzureStorage --version 10.2.1-alpha.1
```

Configure volatile in-memory jobs only for development:

```csharp
builder.UseOrleans(siloBuilder =>
    siloBuilder
        .UseLocalhostClustering()
        .UseInMemoryDurableJobs());
```

Configure Azure Blob-backed jobs for durable storage:

```csharp
builder.UseOrleans(siloBuilder =>
    siloBuilder.UseAzureBlobDurableJobs(options =>
    {
        options.BlobServiceClient = blobServiceClient;
        options.ContainerName = "durable-jobs";
    }));
```

Target a grain that implements `IDurableJobHandler`:

```csharp
public sealed class OrderGrain(
    ILocalDurableJobManager jobs) : Grain, IOrderGrain, IDurableJobHandler
{
    public Task<DurableJob> ScheduleExpiryAsync(
        DateTimeOffset dueTime,
        CancellationToken cancellationToken) =>
        jobs.ScheduleJobAsync(
            new ScheduleJobRequest
            {
                Target = this.GetGrainId(),
                JobName = "expire-order",
                DueTime = dueTime,
                Metadata = new Dictionary<string, string>
                {
                    ["OrderId"] = this.GetPrimaryKeyString()
                }
            },
            cancellationToken);

    public Task ExecuteJobAsync(
        IJobRunContext context,
        CancellationToken cancellationToken) =>
        context.Job.Name == "expire-order"
            ? ExpireIfStillPendingAsync(context.Job.Id, cancellationToken)
            : Task.CompletedTask;
}
```

Use Durable Jobs for delayed notifications, expirations, appointment callbacks, and one-time workflow steps. The system partitions jobs into time-based shards, distributes ownership across silos, reassigns work after failure, and applies the configured retry policy.

Design for these semantics:

- execution is at-least-once, so a handler can run again;
- `DueTime` is scheduling intent, not a real-time deadline;
- keep metadata small and store identifiers rather than full domain objects;
- make effects idempotent using the job ID or a domain idempotency key;
- respect the cancellation token;
- cancellation can race with dispatch, so cancellation success/failure must be handled explicitly;
- configure `DurableJobsOptions.MaxConcurrentJobsPerSilo` to protect downstream resources;
- configure `ShouldRetry` to distinguish transient and permanent failures;
- monitor dispatch lag, dequeue count/retries, handler latency, shard ownership, and journal storage failures.

Current 10.2 Durable Jobs store/discover shards through `Orleans.Journaling`; custom job storage must support the journal catalog. Pin all Orleans package versions together and test upgrade/recovery before production adoption.

If alpha dependencies are unacceptable, use a stable external scheduler/workflow engine or model a bounded one-time workflow with durable grain state plus a stable trigger, making the tradeoff explicit.

## Stateless Worker Grains

Purpose: expose a grain-shaped, auto-scaled pool for interchangeable stateless work.

```csharp
[StatelessWorker(maxLocalWorkers: 4)]
public sealed class PayloadDecoderGrain : Grain, IPayloadDecoderGrain
{
    public ValueTask<DecodedPayload> DecodeAsync(byte[] payload) =>
        ValueTask.FromResult(Decode(payload));
}
```

Use stateless workers for decompression, parsing, routing, CPU-bound transforms, local caches, or reduce-style pre-aggregation. Orleans prefers a local idle activation and creates more activations up to the per-silo limit.

Do not interpret “worker” as “job.” There is no durable queue, due time, retry record, or stable activation identity. Multiple activations can hold local state, but those copies are not coordinated. Never put one authoritative mutable value in a stateless worker.

## Background Services and Hosted Services

Purpose: run standard .NET host-owned work for the lifetime of each process.

```csharp
builder.UseOrleans(siloBuilder =>
{
    // Configure the silo before the hosted service.
});
builder.Services.AddHostedService<ExternalFeedPump>();
```

Use `BackgroundService` for a continuous loop such as consuming an external broker/feed and routing events into grains. Use `IHostedService` for simpler bounded startup/shutdown work. Register after `UseOrleans` when the service needs the local Orleans runtime to be started first.

Every application replica starts its own hosted service. Scaling from one to five silos creates five loops. Use one of these explicit models:

- duplicates are acceptable and work is naturally partitioned by the external source;
- the external broker uses consumer groups/leases;
- each hosted service owns only its local silo work;
- a well-known grain serializes one logical cluster-wide operation;
- an external leader/lease selects one process.

Never assume `AddHostedService` creates a cluster singleton.

## Startup Tasks and Silo Lifecycle

Purpose: initialize or validate an Orleans component at a defined host/silo stage.

Prefer standard `BackgroundService` or `IHostedService` for application work. Use `AddStartupTask` only when the task genuinely belongs to Orleans silo lifecycle ordering:

```csharp
siloBuilder.AddStartupTask(
    async (services, cancellationToken) =>
    {
        var validator = services.GetRequiredService<RuntimeDependencyValidator>();
        await validator.ValidateAsync(cancellationToken);
    },
    ServiceLifecycleStage.ApplicationServices);
```

An exception from a startup task is logged and stops silo startup. Use that fail-fast behavior for deterministic configuration/provider validation, not transient remote work that should retry forever and keep the host unavailable.

Implement `ILifecycleParticipant<ISiloLifecycle>` when a provider or runtime component needs explicit ordered start/stop callbacks. Choose the narrowest lifecycle stage and avoid business data migrations on every silo unless the migration system itself provides safe distributed coordination.

## Grain Services

Purpose: provide a remotely accessible service instance on every silo and partition responsibility for grains across those instances.

A `GrainService`:

- starts before the silo finishes initialization and lives until silo shutdown;
- has no stable domain identity;
- runs on every silo;
- can service a subset of grains through `GrainServiceClient<T>` routing;
- can call grains through `IGrainFactory`;
- is used by Orleans infrastructure such as reminders.

Choose a grain service only for cluster/runtime support that must be co-located with silos and partitioned across them. For ordinary domain behavior, use a normal grain. For a per-process external loop, use `BackgroundService`. For one future invocation, use a Durable Job. For a single cluster-wide owner, use a well-known normal grain or another leader mechanism.

Register both the service and its client:

```csharp
builder.UseOrleans(siloBuilder =>
{
    siloBuilder.Services
        .AddGrainService<DataService>()
        .AddSingleton<IDataServiceClient, DataServiceClient>();
});
```

## Failure-Oriented Tests

Test the guarantee selected by the architecture:

- timer: deactivate the grain and prove that the timer is not treated as durable;
- reminder: deactivate/reactivate, restart silos, and prove recurring behavior with a persistent provider;
- reminder catch-up: keep the cluster down across a due time and prove the application handles the missed tick;
- Durable Job: restart the owning silo, force handler failure/retry, and prove idempotent effects;
- cancellation: race cancellation with job dispatch and assert both outcomes are safe;
- stateless worker: saturate workers and prove scaling without relying on activation identity;
- hosted service: start multiple silos and prove duplicate loops are either prevented or safe;
- grain service: add/remove silos and prove responsibility is re-established;
- startup task: inject a deterministic failure and prove the silo remains unavailable with actionable logs.

Use in-memory providers for fast behavioral tests only. Use the real production provider for recovery, persistence, ordering, lease/ownership, and failover claims.

## Official Sources

- [Timers and reminders](https://learn.microsoft.com/dotnet/orleans/grains/timers-and-reminders)
- [Stateless worker grains](https://learn.microsoft.com/dotnet/orleans/grains/stateless-worker-grains)
- [Background services and startup tasks](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/startup-tasks)
- [Silo lifecycle](https://learn.microsoft.com/dotnet/orleans/host/silo-lifecycle)
- [Grain services](https://learn.microsoft.com/dotnet/orleans/grains/grainservices)
- [Durable Jobs package README for Orleans 10.2.1](https://github.com/dotnet/orleans/blob/v10.2.1/src/Orleans.DurableJobs/README.md)
- [Azure Storage Durable Jobs README for Orleans 10.2.1](https://github.com/dotnet/orleans/blob/v10.2.1/src/Azure/Orleans.DurableJobs.AzureStorage/README.md)
- [Durable Jobs public API for Orleans 10.2.1](https://github.com/dotnet/orleans/blob/v10.2.1/src/api/Orleans.DurableJobs/Orleans.DurableJobs.cs)
- [Orleans 10.2.0 release notes](https://github.com/dotnet/orleans/releases/tag/v10.2.0)
