---
name: managedcode-communication
description: "Use ManagedCode.Communication when a .NET application needs explicit result objects, structured errors, and predictable service or API boundaries instead of exception-driven control flow."
compatibility: "Requires a .NET application, service layer, or API boundary that integrates ManagedCode.Communication."
---

# ManagedCode.Communication

## Trigger On

- integrating `ManagedCode.Communication` into services or APIs
- replacing exception-driven result handling with explicit results
- reviewing service boundaries that return success or failure payloads
- documenting result-pattern usage across ASP.NET Core or application services

## Workflow

1. Confirm the boundary where the library belongs:
   - service result contracts
   - application manager boundaries
   - API endpoints that translate results into HTTP responses
2. Keep result creation and error mapping explicit instead of mixing exceptions, nulls, and ad-hoc tuples.
3. Pattern-match result objects at the boundary that converts them into user-facing responses.
4. Do not hide domain failures behind generic success wrappers.
5. Validate positive, negative, and error-path handling after integration.

```mermaid
flowchart LR
  A["Domain or service operation"] --> B["ManagedCode.Communication result"]
  B --> C["Application or API boundary"]
  C --> D["HTTP response or caller-visible contract"]
```

## Deliver

- guidance on where explicit result objects improve clarity
- usage boundaries for translating results into API or caller responses
- validation expectations for success and failure flows

## Validate

- result handling is consistent across the boundary that uses the library
- callers do not fall back to exception-only logic for normal failure cases
- negative and error scenarios are documented and tested
