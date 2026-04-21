# `CommunityToolkit/Aspire` Reference

Use this reference when official first-party Aspire integrations do not cover the scenario or when the user explicitly asks about `CommunityToolkit/Aspire`.

Last verified against:

- `CommunityToolkit/Aspire` release `v13.1.1` published on `2026-01-16`
- the current repository README package index and Microsoft Learn Community Toolkit pages

## Table of Contents

- [When to use the toolkit](#when-to-use-the-toolkit)
- [Selection rules](#selection-rules)
- [Package families](#package-families)
- [What not to do](#what-not-to-do)

## When to use the toolkit

Reach for `CommunityToolkit/Aspire` when you need one of these:

- polyglot host integrations beyond first-party Aspire coverage
- community-maintained resource integrations that official Aspire does not provide
- extra development-time utilities around existing resource types
- niche infrastructure or test-tool resources that should still live in the AppHost model

Do not reach for it simply because it exists. First-party Aspire remains the default for core official scenarios.

## Selection rules

### Prefer first-party Aspire when

- official Aspire already has the integration you need
- the scenario is a mainstream database, cache, Azure resource, messaging broker, or normal .NET service topology
- the repo benefits from staying as close as possible to Microsoft-maintained docs and examples

### Prefer `CommunityToolkit/Aspire` when

- you need to host non-.NET apps such as Go, Java, PowerShell, Deno, Bun, or Rust in the AppHost
- you need SQLite-specific hosting or related EF and client wiring
- you want extra dev-time tools such as MailPit, ngrok, k6, McpInspector, Adminer, or DbGate in the topology
- you need community-maintained integrations such as Meilisearch, MinIO, RavenDB, SurrealDB, KurrentDB, LavinMQ, or Zitadel
- you need extension packages around existing first-party resources, such as Redis, PostgreSQL, SQL Server, MySQL, MongoDB, Keycloak, Elasticsearch, or OpenTelemetry Collector support

## Package families

The toolkit surface is large. Keep the selection practical and grouped by problem type rather than trying to memorize every package name.

### Polyglot and executable app hosting

Use these when the AppHost must orchestrate non-.NET executable projects:

- `CommunityToolkit.Aspire.Hosting.Golang`
- `CommunityToolkit.Aspire.Hosting.Java`
- `CommunityToolkit.Aspire.Hosting.Python.Extensions`
- `CommunityToolkit.Aspire.Hosting.JavaScript.Extensions`
- `CommunityToolkit.Aspire.Hosting.PowerShell`
- `CommunityToolkit.Aspire.Hosting.Deno`
- `CommunityToolkit.Aspire.Hosting.Bun`
- `CommunityToolkit.Aspire.Hosting.Rust`

These are especially important because current AppHost guidance explicitly points to toolkit integrations for Go and Java, while JavaScript has first-party coverage and Python often routes through the toolkit extension path.

### Databases, object stores, and search

Use these when the missing capability is a specific backing technology:

- `CommunityToolkit.Aspire.Hosting.Sqlite`
- `CommunityToolkit.Aspire.Microsoft.Data.Sqlite`
- `CommunityToolkit.Aspire.Microsoft.EntityFrameworkCore.Sqlite`
- `CommunityToolkit.Aspire.Hosting.RavenDB`
- `CommunityToolkit.Aspire.RavenDB.Client`
- `CommunityToolkit.Aspire.Hosting.Meilisearch`
- `CommunityToolkit.Aspire.Meilisearch`
- `CommunityToolkit.Aspire.Hosting.Minio`
- `CommunityToolkit.Aspire.Minio.Client`
- `CommunityToolkit.Aspire.Hosting.KurrentDB`
- `CommunityToolkit.Aspire.KurrentDB`
- `CommunityToolkit.Aspire.Hosting.SurrealDb`
- `CommunityToolkit.Aspire.SurrealDb`
- `CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects`

This family is often the fastest way to keep uncommon data dependencies inside the Aspire app model instead of documenting them as external setup steps.

### Messaging, eventing, and feature flags

- `CommunityToolkit.Aspire.Hosting.ActiveMQ`
- `CommunityToolkit.Aspire.Hosting.LavinMQ`
- `CommunityToolkit.Aspire.MassTransit.RabbitMQ`
- `CommunityToolkit.Aspire.Hosting.Flagd`
- `CommunityToolkit.Aspire.Hosting.GoFeatureFlag`
- `CommunityToolkit.Aspire.GoFeatureFlag`
- `CommunityToolkit.Aspire.Hosting.Dapr`
- `CommunityToolkit.Aspire.Hosting.Azure.Dapr`
- `CommunityToolkit.Aspire.Hosting.Azure.Dapr.Redis`

Use this family when the distributed topology needs a real eventing, pub-sub, Dapr, or feature-flagged development story instead of a plain HTTP-only graph.

### Platform extensions around existing resources

These extend or deepen the official resource story:

- `CommunityToolkit.Aspire.Hosting.PostgreSQL.Extensions`
- `CommunityToolkit.Aspire.Hosting.SqlServer.Extensions`
- `CommunityToolkit.Aspire.Hosting.Redis.Extensions`
- `CommunityToolkit.Aspire.Hosting.MySql.Extensions`
- `CommunityToolkit.Aspire.Hosting.MongoDB.Extensions`
- `CommunityToolkit.Aspire.Hosting.Elasticsearch.Extensions`
- `CommunityToolkit.Aspire.Hosting.Keycloak.Extensions`
- `CommunityToolkit.Aspire.Hosting.Azure.Extensions`
- `CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder`
- `CommunityToolkit.Aspire.Hosting.OpenTelemetryCollector`
- `CommunityToolkit.Aspire.Hosting.Flyway`

Choose these when first-party Aspire gets you most of the way there, but the real missing piece is a dev tool, extension, or advanced integration surface.

### Dev-time utilities and diagnostics

- `CommunityToolkit.Aspire.Hosting.MailPit`
- `CommunityToolkit.Aspire.Hosting.PapercutSmtp`
- `CommunityToolkit.Aspire.Hosting.Ngrok`
- `CommunityToolkit.Aspire.Hosting.McpInspector`
- `CommunityToolkit.Aspire.Hosting.DbGate`
- `CommunityToolkit.Aspire.Hosting.Adminer`
- `CommunityToolkit.Aspire.Hosting.k6`
- `CommunityToolkit.Aspire.Hosting.Umami`

Use these to keep development-only infrastructure visible in the AppHost instead of managing them out-of-band.

### AI, file transfer, commerce, and identity

- `CommunityToolkit.Aspire.Hosting.Ollama`
- `CommunityToolkit.Aspire.OllamaSharp`
- `CommunityToolkit.Aspire.Hosting.Sftp`
- `CommunityToolkit.Aspire.Sftp`
- `CommunityToolkit.Aspire.Hosting.Stripe`
- `CommunityToolkit.Aspire.Hosting.Zitadel`

These are useful when a distributed application needs more than the typical database and cache story.

## What not to do

- Do not add toolkit packages just because they look interesting. Tie each choice to a concrete gap.
- Do not present community-maintained integrations as if they were automatically equivalent to first-party Aspire support.
- Do not assume every package family is equally mature. Verify package recency, README quality, and real maintenance activity.
- Do not let a dev-only tool become a hidden production dependency.
