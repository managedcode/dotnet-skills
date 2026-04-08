---
name: dotnet-wcf
version: "1.0.0"
category: "Legacy"
description: "Work on WCF services, clients, bindings, contracts, and migration decisions for SOAP and multi-transport service-oriented systems on .NET Framework or compatible stacks."
compatibility: "Requires WCF or a concrete migration off WCF."
---

# Windows Communication Foundation

## Trigger On

- working on WCF services, bindings, or clients
- deciding whether a service should stay WCF or move to modern HTTP APIs
- reviewing transport, security, or interoperability settings

## Workflow

1. Use WCF where SOAP, WS-* features, or multi-transport service requirements are real; do not rewrite those needs into HTTP-only guidance by accident.
2. Keep contracts, bindings, behaviors, and hosting configuration explicit because WCF complexity compounds through configuration indirection.
3. For new REST-style services, prefer modern ASP.NET Core APIs instead of extending WCF into a shape it is no longer best suited for.
4. Plan migrations per endpoint and capability: transport, security model, transaction requirements, metadata, and client compatibility.
5. Validate interoperability and deployment assumptions with the actual client ecosystem, not only local service startup.
6. When WCF coexists with ASP.NET, be explicit about which runtime behaviors are shared and which are not.

## Deliver

- stable WCF service or client configuration
- realistic migration guidance to newer stacks where appropriate
- clear contract and binding ownership

## Validate

- WCF is used for a reason the modern stack does not replace directly
- binding and security behavior are explicit
- interop is verified with real consumers

## References

- [migration.md](references/migration.md) - WCF to gRPC/REST/CoreWCF migration paths, decision framework, and endpoint-by-endpoint migration strategy
- [patterns.md](references/patterns.md) - WCF maintenance patterns for configuration, contracts, hosting, security, diagnostics, and client proxy management
