---
name: dotnet-microsoft-extensions
version: "1.0.0"
category: "Core"
description: "Use the Microsoft.Extensions stack correctly across Generic Host, dependency injection, configuration, logging, options, HttpClientFactory, and other shared infrastructure patterns."
compatibility: "Relevant to console apps, workers, ASP.NET Core apps, functions, and reusable libraries."
---

# Microsoft.Extensions for .NET

## Trigger On

- wiring dependency injection, configuration, logging, or options
- introducing Generic Host patterns into non-web .NET apps
- cleaning up service registration, typed HTTP clients, or shared infrastructure code

## Workflow

1. Prefer the Generic Host for apps that need configuration, DI, logging, hosted services, or coordinated startup.
2. Keep service registration predictable: composition at the edge, concrete implementations hidden behind interfaces only where that abstraction buys flexibility.
3. Use options binding for structured configuration and validate configuration at startup when bad settings would fail later at runtime.
4. Prefer `IHttpClientFactory` and typed or named clients for outbound HTTP instead of ad-hoc singleton or per-call `HttpClient` usage.
5. Use logging categories and config-driven log levels rather than scattered ad-hoc logging behavior.
6. Avoid building mini-frameworks over Microsoft.Extensions unless the repo genuinely needs reusable composition primitives.

## Deliver

- clean host wiring and service registration
- configuration and logging that are observable and testable
- infrastructure code that fits naturally with the .NET stack

## Validate

- service lifetimes are correct
- configuration is strongly typed where it matters
- host setup remains easy to debug and reason about

## References

- [patterns.md](references/patterns.md) - DI patterns, Configuration patterns, Options pattern, Logging patterns, HttpClientFactory patterns, Hosted Service patterns
- [anti-patterns.md](references/anti-patterns.md) - Common mistakes with DI, configuration, options, logging, HttpClient, and hosted services
