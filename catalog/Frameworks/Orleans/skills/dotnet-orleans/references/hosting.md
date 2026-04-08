# Hosting, Configuration, and Operations

Use this reference when the main question is about running Orleans, wiring providers, or operating a cluster.

## Host and Client Entry Points

| Need | Official Source | What It Covers |
|---|---|---|
| Connect external processes to a cluster | [Clients](https://learn.microsoft.com/dotnet/orleans/host/client) | `UseOrleansClient`, gateways, and client topology |
| Add operational visibility | [Dashboard](https://learn.microsoft.com/dotnet/orleans/dashboard/) | Orleans Dashboard setup and operational usage |
| Wire Orleans through Aspire | [.NET Aspire integration](https://learn.microsoft.com/dotnet/orleans/host/aspire-integration) | AppHost resources, `.AsClient()`, and orchestration wiring |
| Understand silo host stages | [Silo lifecycle](https://learn.microsoft.com/dotnet/orleans/host/silo-lifecycle) | Silo startup and shutdown lifecycle |
| Run mixed silo roles | [Heterogeneous silos](https://learn.microsoft.com/dotnet/orleans/host/heterogeneous-silos) | Different silo capabilities in one cluster |
| Reason about activation lookups | [Grain directory](https://learn.microsoft.com/dotnet/orleans/host/grain-directory) | Directory behavior and placement lookup mechanics |
| Secure transport | [Transport Layer Security (TLS)](https://learn.microsoft.com/dotnet/orleans/host/transport-layer-security) | TLS between Orleans cluster participants |

## Configuration Guide

| Need | Official Source | What It Covers |
|---|---|---|
| Start with cluster configuration | [Configuration overview](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/) | Main configuration surface |
| Set up local development | [Local development configuration](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/local-development-configuration) | Dev cluster setup and local defaults |
| Configure clients | [Client configuration](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/client-configuration) | Client-side settings and connectivity |
| Configure silos | [Server configuration](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/server-configuration) | Silo-side options and runtime wiring |
| Review common recipes | [Typical configurations](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/typical-configurations) | Canonical configuration examples |
| Look up available options | [List of options classes](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/list-of-options-classes) | Option types exposed by Orleans |
| Add metadata to silos | [Silo metadata](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/silo-metadata) | Metadata and node labeling |
| Tune deactivation | [Activation collection](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/activation-collection) | Activation cleanup and collection rules |
| Tune GC for Orleans | [Configure .NET garbage collection](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/configuring-garbage-collection) | GC recommendations for Orleans hosts |
| Configure relational providers | [Configure ADO.NET providers](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/configuring-ado-dot-net-providers) | ADO.NET provider registration and setup |
| Set up ADO.NET databases | [ADO.NET database configuration](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/adonet-configuration) | Database-side setup for Orleans SQL providers |
| Understand Orleans serialization | [Serialization overview](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/serialization) | Serializer model and contracts |
| Use immutable types | [Serialization of immutable types](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/serialization-immutability) | Immutable-type handling |
| Configure serialization | [Configure serialization](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/serialization-configuration) | Serializer configuration switches |
| Customize serializers | [Customize serialization](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/serialization-customization) | Custom serializers and codecs |
| Run startup hooks | [Startup tasks](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/startup-tasks) | Startup task registration |
| Shut clusters down cleanly | [Graceful shutdown](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/shutting-down-orleans) | Drain and shutdown behavior |

## Observability

| Need | Official Source | What It Covers |
|---|---|---|
| Start with monitoring | [Observability overview](https://learn.microsoft.com/dotnet/orleans/host/monitoring/) | Logs, metrics, and monitoring guidance |
| Decode silo-side errors | [Silo error code monitoring](https://learn.microsoft.com/dotnet/orleans/host/monitoring/silo-error-code-monitoring) | Error-code reference for silo issues |
| Decode client-side errors | [Client error code monitoring](https://learn.microsoft.com/dotnet/orleans/host/monitoring/client-error-code-monitoring) | Error-code reference for clients |

## Deployment and Failures

| Need | Official Source | What It Covers |
|---|---|---|
| Start from deployment basics | [Running the app](https://learn.microsoft.com/dotnet/orleans/deployment/) | Deployment overview and entry points |
| Deploy to Azure App Service | [Azure App Service](https://learn.microsoft.com/dotnet/orleans/deployment/deploy-to-azure-app-service) | App Service hosting guidance |
| Deploy to Azure Container Apps | [Azure Container Apps](https://learn.microsoft.com/dotnet/orleans/deployment/deploy-to-azure-container-apps) | ACA deployment shape |
| Deploy to Kubernetes | [Kubernetes](https://learn.microsoft.com/dotnet/orleans/deployment/kubernetes) | K8s deployment and clustering |
| Deploy to Service Fabric | [Service Fabric](https://learn.microsoft.com/dotnet/orleans/deployment/service-fabric) | Service Fabric runtime integration |
| Handle cluster failures | [Handle failures](https://learn.microsoft.com/dotnet/orleans/deployment/handling-failures) | Failure modes and recovery patterns |
| Deploy with Consul | [Consul deployments](https://learn.microsoft.com/dotnet/orleans/deployment/consul-deployment) | Consul-based clustering |
| Troubleshoot deployments | [Troubleshoot deployments](https://learn.microsoft.com/dotnet/orleans/deployment/troubleshooting-deployments) | Common deployment diagnostics |
| Troubleshoot legacy Azure Cloud Services | [Azure Cloud Services troubleshooting](https://learn.microsoft.com/dotnet/orleans/deployment/troubleshooting-azure-cloud-services-deployments) | Legacy deployment troubleshooting |

## Usage Guidance

- Start here when the problem is cluster wiring, clients, provider registration, or operational readiness.
- Use [grains.md](grains.md) when the problem is inside a grain rather than in the hosting model.
- Use [implementation.md](implementation.md) when you need runtime internals, messaging guarantees, or testing behavior.
- Use [testing-patterns.md](testing-patterns.md) when the hosting question is specifically about mixing Orleans, Aspire, `WebApplicationFactory`, SignalR, or Playwright in integration tests.
