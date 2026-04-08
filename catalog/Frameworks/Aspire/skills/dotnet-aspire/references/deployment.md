# .NET Aspire Deployment Reference

Use this reference when the question is specifically about deployment shape, publish mode, Azure Container Apps, App Service, or manifest-oriented Aspire workflows.

Current docs home and CLI reference now live on `aspire.dev`. The homepage and CLI docs emphasize `aspire deploy` as the current deployment command, while `aspire publish` remains a preview-oriented artifact-generation path.

## Table of Contents

- [Deployment defaults](#deployment-defaults)
- [Azure Container Apps with `azd`](#azure-container-apps-with-azd)
- [App Service-specific guidance](#app-service-specific-guidance)
- [Publish-mode resource switching](#publish-mode-resource-switching)
- [CLI deploy and preview publish flows](#cli-deploy-and-preview-publish-flows)
- [Operational checks](#operational-checks)

## Deployment defaults

Current official guidance strongly favors Azure Developer CLI for the normal Azure path.

Use this ordering by default:

1. Local development and debugging with `aspire run`
2. Azure Container Apps deployment with `azd`
3. App Service only when the hosting constraints or team standards point there
4. The CLI deploy or publish pipeline only when you explicitly need Aspire-managed deployment steps or artifact generation outside the normal `azd` flow

Avoid inventing a custom deployment path before checking whether `azd` already covers the scenario.

## Azure Container Apps with `azd`

### Recommended path

```bash
azd init
azd up
```

Why this path is the default:

- `azd init` detects the AppHost and generates the right Azure-facing scaffolding
- `azd up` provisions and deploys from the distributed application model
- the workflow aligns with the official Aspire deployment tutorials

### What `azd init` is doing

In an Aspire solution, `azd init` typically:

1. scans the current directory for the AppHost
2. confirms the detected distributed application
3. generates Azure deployment files such as `azure.yaml`
4. creates environment-specific configuration under `.azure/`

### Post-deploy dashboard access

Use:

```bash
azd monitor
```

This is the preferred operational follow-up when the team wants the deployed Aspire Dashboard experience instead of hunting for URLs manually.

## App Service-specific guidance

App Service is valid, but it is not identical to ACA in how service-to-service traffic works.

### Add the integration

The current quickstart uses the Aspire App Service hosting integration:

```bash
aspire add azure-appservice
```

Then model the App Service environment in the AppHost:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.AddAzureAppServiceEnvironment("app-service-env");

var api = builder.AddProject<Projects.MyShop_Api>("api")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");
```

Key point:

- App Service currently needs externally reachable HTTP endpoints for service-to-service communication in this hosting model

Do not copy a Container Apps mental model here and assume internal-only AppHost endpoints will just work.

## Publish-mode resource switching

Use publish-mode branching to move from local development resources to managed Azure services cleanly:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var database = builder.ExecutionContext.IsPublishMode
    ? builder.AddAzurePostgresFlexibleServer("db").AddDatabase("catalog")
    : builder.AddPostgres("db").AddDatabase("catalog");

var cache = builder.ExecutionContext.IsPublishMode
    ? builder.AddAzureRedis("cache")
    : builder.AddRedis("cache");

builder.AddProject<Projects.MyShop_Api>("api")
    .WithReference(database)
    .WithReference(cache)
    .WaitFor(database)
    .WaitFor(cache);
```

This is the normal way to express:

- local containers or emulators for dev
- managed Azure services for publish mode

Do not keep separate hand-maintained topologies unless there is a real deployment-system constraint.

## CLI deploy and preview publish flows

The current Aspire CLI exposes two closely related preview-sensitive paths:

- `aspire deploy` for running the deploy pipeline registered in the app model
- `aspire publish` for explicit publish and artifact-generation scenarios

Use `aspire deploy` when the task explicitly calls for:

- driving the app model's deploy pipeline directly from the CLI
- generating deploy output while letting the AppHost select and run dependent pipeline steps
- validating a repo-specific Aspire deployment story outside `azd`

Use `aspire publish` when the task explicitly calls for:

- deployment artifact generation
- manifest-oriented handoff to another toolchain
- inspection of the publish output instead of a direct `azd` deployment

Treat both flows as version-sensitive and preview-sensitive. Verify current docs and command behavior before encoding them into long-lived repo automation.

## Operational checks

Before calling a deployment story "done", verify:

1. The local AppHost still runs after any publish-mode branching changes.
2. Resources that must be external are explicitly external, and resources that should remain internal are not accidentally exposed.
3. The dashboard, traces, and health endpoints reflect the expected topology after deployment.
4. Environment-specific parameters and secrets are injected through the AppHost model or deployment tooling rather than copy-pasted config files.
5. The team can explain why the target is ACA, App Service, `aspire deploy`, or `aspire publish`.
