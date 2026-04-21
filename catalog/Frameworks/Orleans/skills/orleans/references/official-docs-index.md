# Official Docs Index

Use this reference when the summarized guidance in the skill is not enough and you need the official Orleans documentation tree through direct links.

This skill keeps a live-link map for Orleans instead of a mirrored local docs snapshot.

## Scope

- Full Orleans Microsoft Learn tree mapped through links, including getting started, grains, streaming, host, deployment, implementation details, resources, quickstarts, and code samples
- Official examples and samples entry points from Microsoft Learn, `dotnet/samples`, and the Orleans repository sample index
- GitHub repository entry points for repo-level docs, releases, and sample navigation

## Primary Entry Points

- [Microsoft Orleans documentation root](https://learn.microsoft.com/dotnet/orleans/)
- [Orleans overview](https://learn.microsoft.com/dotnet/orleans/overview)
- [Benefits](https://learn.microsoft.com/dotnet/orleans/benefits)
- [Migration guide](https://learn.microsoft.com/dotnet/orleans/migration-guide)
- [Best practices](https://learn.microsoft.com/dotnet/orleans/resources/best-practices)
- [Orleans GitHub repository](https://github.com/dotnet/orleans)
- [Latest Orleans release](https://github.com/dotnet/orleans/releases/latest)

## Get Started

- [Overview](https://learn.microsoft.com/dotnet/orleans/overview)
- [Benefits](https://learn.microsoft.com/dotnet/orleans/benefits)
- [Migration guide](https://learn.microsoft.com/dotnet/orleans/migration-guide)

## Quickstarts

- [Build your first Orleans app](https://learn.microsoft.com/dotnet/orleans/quickstarts/build-your-first-orleans-app)
- [Deploy and scale an Orleans app on Azure](https://learn.microsoft.com/dotnet/orleans/quickstarts/deploy-scale-orleans-on-azure)

## Grains

- [Develop grains](https://learn.microsoft.com/dotnet/orleans/grains/)
- [Grain references](https://learn.microsoft.com/dotnet/orleans/grains/grain-references)
- [Grain identity](https://learn.microsoft.com/dotnet/orleans/grains/grain-identity)
- [Grain placement overview](https://learn.microsoft.com/dotnet/orleans/grains/grain-placement)
- [Grain placement filtering](https://learn.microsoft.com/dotnet/orleans/grains/grain-placement-filtering)
- [Grain extensions](https://learn.microsoft.com/dotnet/orleans/grains/grain-extensions)
- [Timers and reminders](https://learn.microsoft.com/dotnet/orleans/grains/timers-and-reminders)
- [Observers](https://learn.microsoft.com/dotnet/orleans/grains/observers)
- [Cancellation tokens](https://learn.microsoft.com/dotnet/orleans/grains/cancellation-tokens)
- [Request scheduling](https://learn.microsoft.com/dotnet/orleans/grains/request-scheduling)
- [Request context](https://learn.microsoft.com/dotnet/orleans/grains/request-context)
- [Code generation](https://learn.microsoft.com/dotnet/orleans/grains/code-generation)

### Persistence

- [Grain persistence](https://learn.microsoft.com/dotnet/orleans/grains/grain-persistence/)
- [Azure Cosmos DB persistence](https://learn.microsoft.com/dotnet/orleans/grains/grain-persistence/azure-cosmos-db)
- [Relational storage (ADO.NET)](https://learn.microsoft.com/dotnet/orleans/grains/grain-persistence/relational-storage)
- [Azure storage persistence](https://learn.microsoft.com/dotnet/orleans/grains/grain-persistence/azure-storage)
- [Amazon DynamoDB storage](https://learn.microsoft.com/dotnet/orleans/grains/grain-persistence/dynamodb-storage)

### Event Sourcing

- [Event sourcing overview](https://learn.microsoft.com/dotnet/orleans/grains/event-sourcing/)
- [JournaledGrain basics](https://learn.microsoft.com/dotnet/orleans/grains/event-sourcing/journaledgrain-basics)
- [JournaledGrain diagnostics](https://learn.microsoft.com/dotnet/orleans/grains/event-sourcing/journaledgrain-diagnostics)
- [Immediate vs delayed confirmation](https://learn.microsoft.com/dotnet/orleans/grains/event-sourcing/immediate-vs-delayed-confirmation)
- [Notifications](https://learn.microsoft.com/dotnet/orleans/grains/event-sourcing/notifications)
- [Event sourcing configuration](https://learn.microsoft.com/dotnet/orleans/grains/event-sourcing/event-sourcing-configuration)
- [Built-in log-consistency providers](https://learn.microsoft.com/dotnet/orleans/grains/event-sourcing/log-consistency-providers)
- [Replicated instances](https://learn.microsoft.com/dotnet/orleans/grains/event-sourcing/replicated-instances)

### Advanced Grain Features

- [External tasks and grains](https://learn.microsoft.com/dotnet/orleans/grains/external-tasks-and-grains)
- [Interceptors](https://learn.microsoft.com/dotnet/orleans/grains/interceptors)
- [GrainServices](https://learn.microsoft.com/dotnet/orleans/grains/grainservices)
- [Stateless worker grains](https://learn.microsoft.com/dotnet/orleans/grains/stateless-worker-grains)
- [Transactions](https://learn.microsoft.com/dotnet/orleans/grains/transactions)
- [One-way requests](https://learn.microsoft.com/dotnet/orleans/grains/oneway)
- [Grain lifecycle](https://learn.microsoft.com/dotnet/orleans/grains/grain-lifecycle)

### Grain Versioning

- [Grain versioning overview](https://learn.microsoft.com/dotnet/orleans/grains/grain-versioning/grain-versioning)
- [Backward compatibility guidelines](https://learn.microsoft.com/dotnet/orleans/grains/grain-versioning/backward-compatibility-guidelines)
- [Compatible grains](https://learn.microsoft.com/dotnet/orleans/grains/grain-versioning/compatible-grains)
- [Version selector strategy](https://learn.microsoft.com/dotnet/orleans/grains/grain-versioning/version-selector-strategy)
- [Deploying new versions of grains](https://learn.microsoft.com/dotnet/orleans/grains/grain-versioning/deploying-new-versions-of-grains)

## Streaming

- [Streaming overview](https://learn.microsoft.com/dotnet/orleans/streaming/)
- [Streams quick start](https://learn.microsoft.com/dotnet/orleans/streaming/streams-quick-start)
- [Why streams?](https://learn.microsoft.com/dotnet/orleans/streaming/streams-why)
- [Broadcast channels](https://learn.microsoft.com/dotnet/orleans/streaming/broadcast-channel)
- [Streams APIs](https://learn.microsoft.com/dotnet/orleans/streaming/streams-programming-apis)
- [Stream providers](https://learn.microsoft.com/dotnet/orleans/streaming/stream-providers)

## Host

- [Clients](https://learn.microsoft.com/dotnet/orleans/host/client)
- [Dashboard](https://learn.microsoft.com/dotnet/orleans/dashboard/)
- [.NET Aspire integration](https://learn.microsoft.com/dotnet/orleans/host/aspire-integration)
- [Silo lifecycle](https://learn.microsoft.com/dotnet/orleans/host/silo-lifecycle)
- [Heterogeneous silos](https://learn.microsoft.com/dotnet/orleans/host/heterogeneous-silos)
- [Grain directory](https://learn.microsoft.com/dotnet/orleans/host/grain-directory)
- [Transport Layer Security (TLS)](https://learn.microsoft.com/dotnet/orleans/host/transport-layer-security)

### Configuration Guide

- [Configuration overview](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/)
- [Local development configuration](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/local-development-configuration)
- [Client configuration](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/client-configuration)
- [Server configuration](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/server-configuration)
- [Typical configurations](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/typical-configurations)
- [List of options classes](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/list-of-options-classes)
- [Silo metadata](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/silo-metadata)
- [Activation collection](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/activation-collection)
- [Configure .NET garbage collection](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/configuring-garbage-collection)
- [Configure ADO.NET providers](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/configuring-ado-dot-net-providers)
- [ADO.NET database configuration](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/adonet-configuration)
- [Serialization overview](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/serialization)
- [Serialization of immutable types](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/serialization-immutability)
- [Configure serialization](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/serialization-configuration)
- [Customize serialization](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/serialization-customization)
- [Startup tasks](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/startup-tasks)
- [Graceful shutdown](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/shutting-down-orleans)

### Observability

- [Observability overview](https://learn.microsoft.com/dotnet/orleans/host/monitoring/)
- [Silo error code monitoring](https://learn.microsoft.com/dotnet/orleans/host/monitoring/silo-error-code-monitoring)
- [Client error code monitoring](https://learn.microsoft.com/dotnet/orleans/host/monitoring/client-error-code-monitoring)

## Deployment

- [Running the app](https://learn.microsoft.com/dotnet/orleans/deployment/)
- [Azure App Service](https://learn.microsoft.com/dotnet/orleans/deployment/deploy-to-azure-app-service)
- [Azure Container Apps](https://learn.microsoft.com/dotnet/orleans/deployment/deploy-to-azure-container-apps)
- [Kubernetes](https://learn.microsoft.com/dotnet/orleans/deployment/kubernetes)
- [Service Fabric](https://learn.microsoft.com/dotnet/orleans/deployment/service-fabric)
- [Handle failures](https://learn.microsoft.com/dotnet/orleans/deployment/handling-failures)
- [Troubleshooting Azure Cloud Services (Legacy)](https://learn.microsoft.com/dotnet/orleans/deployment/troubleshooting-azure-cloud-services-deployments)
- [Consul deployments](https://learn.microsoft.com/dotnet/orleans/deployment/consul-deployment)
- [Troubleshoot deployments](https://learn.microsoft.com/dotnet/orleans/deployment/troubleshooting-deployments)

## Code Samples And Tutorials

- [Code samples overview](https://learn.microsoft.com/dotnet/orleans/tutorials-and-samples/)
- [Hello World tutorial](https://learn.microsoft.com/dotnet/orleans/tutorials-and-samples/overview-helloworld)
- [Orleans basics tutorial](https://learn.microsoft.com/dotnet/orleans/tutorials-and-samples/tutorial-1)
- [Adventure game sample](https://learn.microsoft.com/dotnet/orleans/tutorials-and-samples/adventure)
- [Unit testing](https://learn.microsoft.com/dotnet/orleans/implementation/testing)
- [Custom grain storage sample](https://learn.microsoft.com/dotnet/orleans/tutorials-and-samples/custom-grain-storage)

## Implementation Details

- [Implementation overview](https://learn.microsoft.com/dotnet/orleans/implementation/)
- [Implementation grain directory](https://learn.microsoft.com/dotnet/orleans/implementation/grain-directory)
- [Orleans lifecycle](https://learn.microsoft.com/dotnet/orleans/implementation/orleans-lifecycle)
- [Messaging delivery guarantees](https://learn.microsoft.com/dotnet/orleans/implementation/messaging-delivery-guarantees)
- [Scheduler](https://learn.microsoft.com/dotnet/orleans/implementation/scheduler)
- [Cluster management](https://learn.microsoft.com/dotnet/orleans/implementation/cluster-management)
- [Streams implementation overview](https://learn.microsoft.com/dotnet/orleans/implementation/streams-implementation/)
- [Azure Queue streams implementation](https://learn.microsoft.com/dotnet/orleans/implementation/streams-implementation/azure-queue-streams)
- [Load balancing](https://learn.microsoft.com/dotnet/orleans/implementation/load-balancing)
- [Unit testing](https://learn.microsoft.com/dotnet/orleans/implementation/testing)

## Resources

- [Frequently asked questions](https://learn.microsoft.com/dotnet/orleans/resources/frequently-asked-questions)
- [Design principles](https://learn.microsoft.com/dotnet/orleans/resources/orleans-architecture-principles-and-approach)
- [Applicability](https://learn.microsoft.com/dotnet/orleans/resources/orleans-thinking-big-and-small)
- [NuGet packages](https://learn.microsoft.com/dotnet/orleans/resources/nuget-packages)
- [Best practices](https://learn.microsoft.com/dotnet/orleans/resources/best-practices)
- [Student projects](https://learn.microsoft.com/dotnet/orleans/resources/student-projects)
- [External links](https://learn.microsoft.com/dotnet/orleans/resources/links)

## Official Examples

### Microsoft Learn And Samples Browser

- [Orleans samples browser](https://learn.microsoft.com/samples/browse/?expanded=dotnet&products=orleans)
- [dotnet/samples Orleans folder](https://github.com/dotnet/samples/tree/main/orleans)
- [Orleans repo sample index](https://github.com/dotnet/orleans/blob/main/samples/README.md)

### Highlighted Official Samples

- [Hello, World](https://learn.microsoft.com/samples/dotnet/samples/orleans-hello-world-sample-app)
- [Adventure](https://learn.microsoft.com/samples/dotnet/samples/orleans-text-adventure-game)
- [Chirper](https://learn.microsoft.com/samples/dotnet/samples/orleans-chirper-social-media-sample-app)
- [GPS Tracker](https://learn.microsoft.com/samples/dotnet/samples/orleans-gps-device-tracker-sample)
- [Presence Service](https://learn.microsoft.com/samples/dotnet/samples/orleans-gaming-presence-service-sample)
- [Tic Tac Toe](https://learn.microsoft.com/samples/dotnet/samples/orleans-tictactoe-web-based-game)
- [Voting](https://learn.microsoft.com/samples/dotnet/samples/orleans-voting-sample-app-on-kubernetes)
- [Chat Room](https://learn.microsoft.com/samples/dotnet/samples/orleans-chat-room-sample)
- [Bank Account / ACID transactions](https://learn.microsoft.com/samples/dotnet/samples/orleans-bank-account-acid-transactions)
- [Blazor Server sample](https://learn.microsoft.com/samples/dotnet/samples/orleans-aspnet-core-blazor-server-sample)
- [Blazor WebAssembly sample](https://learn.microsoft.com/samples/dotnet/samples/orleans-aspnet-core-blazor-wasm-sample)
- [Stocks sample](https://learn.microsoft.com/samples/dotnet/samples/orleans-stocks-sample-app)
- [Transport Layer Security sample](https://learn.microsoft.com/samples/dotnet/samples/orleans-transport-layer-security-tls)
- [Streaming with Azure Event Hubs](https://learn.microsoft.com/samples/dotnet/samples/orleans-streaming-pubsub-with-azure-event-hub)

## GitHub Source Entry Points

- [Orleans repository README](https://github.com/dotnet/orleans)
- [Repository releases](https://github.com/dotnet/orleans/releases)
- [Repository samples README](https://github.com/dotnet/orleans/blob/main/samples/README.md)
- [dotnet/samples Orleans source tree](https://github.com/dotnet/samples/tree/main/orleans)

## Usage Guidance

- Start with the smallest relevant page instead of loading the whole tree into context.
- Use the TOC snapshot to see whether a topic already has an official page before inventing guidance.
- Use the hub snapshot to see the official top-level featured pages and resource entry points.
- Prefer Learn pages for normative guidance and sample pages for concrete wiring patterns.
- When a task needs exact package or provider naming, cross-check against the live docs page and the NuGet packages resource page.
