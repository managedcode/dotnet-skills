---
name: mixed-reality
description: "Work on C# and .NET-adjacent mixed-reality solutions around HoloLens, MRTK, OpenXR, Azure services, and integration boundaries where .NET participates in the stack."
compatibility: "Best for HoloLens, MRTK, Unity+C#, and service integrations around mixed reality."
---

# Mixed Reality with .NET

## Trigger On

- building or integrating mixed-reality solutions with C#
- working on HoloLens, MRTK, Azure mixed-reality services, or OpenXR-related code
- reviewing how .NET services support a mixed-reality client

## Workflow

1. Acknowledge that much of Microsoft mixed-reality guidance is Unity-centered even when the implementation language is C#; do not pretend it is a standard .NET desktop stack.
2. Separate engine-side concerns, device capability concerns, and backend service integration so the system boundary stays understandable.
3. Use MRTK and OpenXR guidance intentionally, and verify current toolkit status before choosing a package or template path. See [references/patterns.md](references/patterns.md) for established architecture patterns.
4. Treat performance, input, and spatial UX as core constraints, not polish items.
5. When .NET mostly lives on the backend for a mixed-reality product, route that backend work through the relevant ASP.NET Core, SignalR, or Azure skill instead of overloading this one.
6. Validate with the actual device or emulator path whenever possible because editor-only success is not enough.

## References

- [references/patterns.md](references/patterns.md) - MRTK service architecture, OpenXR feature plugins, input action patterns, spatial awareness observers, and cross-cutting patterns for dependency injection, object pooling, and graceful degradation.
- [references/examples.md](references/examples.md) - Common HoloLens scenarios including spatial anchors, hand menus, spatial mapping physics, eye tracking, remote rendering, voice commands, QR code tracking, and shared multi-user experiences.

## Deliver

- clear boundaries between device code and backend services
- mixed-reality guidance grounded in current Microsoft tooling
- realistic validation expectations for MR scenarios

## Validate

- the chosen toolkit path is current enough for the project
- device-specific constraints are explicit
- backend and client responsibilities are not blurred
