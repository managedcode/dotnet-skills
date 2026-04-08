# Grains, State, and Runtime Primitives

Use this reference when the main question is inside grain design rather than hosting or deployment.

## Core Grain Modeling

| Need | Official Source | What It Covers |
|---|---|---|
| Start with the grain programming model | [Develop grains](https://learn.microsoft.com/dotnet/orleans/grains/) | Grain classes, interfaces, and the core programming surface |
| Understand grain references | [Grain references](https://learn.microsoft.com/dotnet/orleans/grains/grain-references) | How grains are addressed and invoked |
| Pick the right identity shape | [Grain identity](https://learn.microsoft.com/dotnet/orleans/grains/grain-identity) | Keys, namespaces, and identity semantics |
| Understand default placement | [Grain placement](https://learn.microsoft.com/dotnet/orleans/grains/grain-placement) | Runtime placement model and locality tradeoffs |
| Filter or constrain placement | [Grain placement filtering](https://learn.microsoft.com/dotnet/orleans/grains/grain-placement-filtering) | Placement filters and targeting rules |
| Add extension points to grains | [Grain extensions](https://learn.microsoft.com/dotnet/orleans/grains/grain-extensions) | Grain extension patterns |
| Generate serializers and proxies correctly | [Code generation](https://learn.microsoft.com/dotnet/orleans/grains/code-generation) | Codegen expectations and generated artifacts |

## Timers, Reminders, and Execution Flow

| Need | Official Source | What It Covers |
|---|---|---|
| Choose timers vs reminders | [Timers and reminders](https://learn.microsoft.com/dotnet/orleans/grains/timers-and-reminders) | Activation-local timers versus durable reminders |
| Push updates back to clients | [Observers](https://learn.microsoft.com/dotnet/orleans/grains/observers) | Grain observers and callback patterns |
| Cancel grain work safely | [Cancellation tokens](https://learn.microsoft.com/dotnet/orleans/grains/cancellation-tokens) | Cancellation behavior across grain calls |
| Reason about reentrancy and ordering | [Request scheduling](https://learn.microsoft.com/dotnet/orleans/grains/request-scheduling) | Scheduler rules, interleaving, and request ordering |
| Flow ambient metadata | [Request context](https://learn.microsoft.com/dotnet/orleans/grains/request-context) | Request-scoped metadata across calls |
| Hook into activation stages | [Grain lifecycle](https://learn.microsoft.com/dotnet/orleans/grains/grain-lifecycle) | Lifecycle stages and activation events |
| Offload stateless fan-out | [Stateless worker grains](https://learn.microsoft.com/dotnet/orleans/grains/stateless-worker-grains) | Stateless scaling patterns |
| Use external tasks safely | [External tasks and grains](https://learn.microsoft.com/dotnet/orleans/grains/external-tasks-and-grains) | Mixing Orleans scheduling with external async work |
| Add interceptors or filters | [Interceptors](https://learn.microsoft.com/dotnet/orleans/grains/interceptors) | Cross-cutting interception points |
| Create runtime helper services | [GrainServices](https://learn.microsoft.com/dotnet/orleans/grains/grainservices) | Cluster-local services for shared runtime behavior |
| Use fire-and-forget deliberately | [One-way requests](https://learn.microsoft.com/dotnet/orleans/grains/oneway) | One-way call semantics and limits |

## Persistence and State

| Need | Official Source | What It Covers |
|---|---|---|
| Persist grain state | [Grain persistence](https://learn.microsoft.com/dotnet/orleans/grains/grain-persistence/) | Persistent state model and provider wiring |
| Use Azure Cosmos DB storage | [Azure Cosmos DB persistence](https://learn.microsoft.com/dotnet/orleans/grains/grain-persistence/azure-cosmos-db) | Cosmos-backed state provider setup |
| Use relational storage | [Relational storage (ADO.NET)](https://learn.microsoft.com/dotnet/orleans/grains/grain-persistence/relational-storage) | SQL-backed provider options |
| Use Azure Storage | [Azure storage persistence](https://learn.microsoft.com/dotnet/orleans/grains/grain-persistence/azure-storage) | Azure Table/Blob-backed state provider guidance |
| Use DynamoDB | [Amazon DynamoDB storage](https://learn.microsoft.com/dotnet/orleans/grains/grain-persistence/dynamodb-storage) | DynamoDB-backed persistence options |

## Event Sourcing

| Need | Official Source | What It Covers |
|---|---|---|
| Decide whether to use event sourcing | [Event sourcing overview](https://learn.microsoft.com/dotnet/orleans/grains/event-sourcing/) | Journaled grain model and tradeoffs |
| Start with `JournaledGrain` | [JournaledGrain basics](https://learn.microsoft.com/dotnet/orleans/grains/event-sourcing/journaledgrain-basics) | Core journaled grain API and state evolution |
| Diagnose journaled grains | [JournaledGrain diagnostics](https://learn.microsoft.com/dotnet/orleans/grains/event-sourcing/journaledgrain-diagnostics) | Troubleshooting and diagnostics for journaled grains |
| Choose confirmation mode | [Immediate vs delayed confirmation](https://learn.microsoft.com/dotnet/orleans/grains/event-sourcing/immediate-vs-delayed-confirmation) | Consistency and confirmation tradeoffs |
| Publish event notifications | [Notifications](https://learn.microsoft.com/dotnet/orleans/grains/event-sourcing/notifications) | Observer/notification patterns for journaled grains |
| Configure event sourcing | [Event sourcing configuration](https://learn.microsoft.com/dotnet/orleans/grains/event-sourcing/event-sourcing-configuration) | Provider and configuration model |
| Review built-in providers | [Built-in log-consistency providers](https://learn.microsoft.com/dotnet/orleans/grains/event-sourcing/log-consistency-providers) | Available log consistency implementations |
| Understand replicated instances | [Replicated instances](https://learn.microsoft.com/dotnet/orleans/grains/event-sourcing/replicated-instances) | Multi-instance replication behavior |

## Transactions and Versioning

| Need | Official Source | What It Covers |
|---|---|---|
| Use transactional state | [Transactions](https://learn.microsoft.com/dotnet/orleans/grains/transactions) | Orleans ACID transaction model |
| Plan contract evolution | [Grain versioning overview](https://learn.microsoft.com/dotnet/orleans/grains/grain-versioning/grain-versioning) | Interface and implementation versioning |
| Preserve compatibility | [Backward compatibility guidelines](https://learn.microsoft.com/dotnet/orleans/grains/grain-versioning/backward-compatibility-guidelines) | Safe versioning rules |
| Mark compatible implementations | [Compatible grains](https://learn.microsoft.com/dotnet/orleans/grains/grain-versioning/compatible-grains) | Compatibility declarations |
| Control version selection | [Version selector strategy](https://learn.microsoft.com/dotnet/orleans/grains/grain-versioning/version-selector-strategy) | Version routing rules |
| Roll out new grain versions | [Deploying new versions of grains](https://learn.microsoft.com/dotnet/orleans/grains/grain-versioning/deploying-new-versions-of-grains) | Deployment workflow for upgrades |

## Usage Guidance

- Start here when the dominant question is grain boundaries, runtime primitives, or state semantics.
- Jump to [hosting.md](hosting.md) when the problem is cluster wiring, clients, observability, or deployment.
- Jump to [implementation.md](implementation.md) when you need runtime-internals or testing details.
