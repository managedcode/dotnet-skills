---
name: dotnet-managedcode-orleans-signalr
version: "1.0.0"
category: "Distributed"
description: "Use ManagedCode.Orleans.SignalR when a distributed .NET application needs Orleans-based coordination of SignalR real-time messaging, hub delivery, and grain-driven push flows."
compatibility: "Requires a .NET application that combines Orleans and SignalR through ManagedCode.Orleans.SignalR."
---

# ManagedCode.Orleans.SignalR

## Trigger On

- integrating `ManagedCode.Orleans.SignalR` into a real-time distributed application
- coordinating SignalR delivery from Orleans grains
- reviewing grain-to-hub push flows and connection routing
- documenting how Orleans state or events become SignalR messages

## Workflow

1. Confirm the application genuinely needs both Orleans and SignalR in the same flow.
2. Identify which grain events or workflows should publish to connected clients.
3. Keep Orleans domain logic in grains and SignalR transport concerns in the integration boundary.
4. Document how user, group, or connection targeting is resolved.
5. Validate end-to-end message delivery from grain event to connected client.

```mermaid
flowchart LR
  A["Orleans grain event"] --> B["ManagedCode.Orleans.SignalR integration"]
  B --> C["SignalR hub delivery"]
  C --> D["Connected client or group"]
```

## Deliver

- guidance on where the Orleans-to-SignalR bridge belongs
- separation between grain logic and transport concerns
- validation expectations for real-time distributed delivery

## Validate

- the integration is justified instead of mixing Orleans and SignalR casually
- grain logic stays focused on domain or orchestration concerns
- real-time delivery is verified end to end, not only through registration code
