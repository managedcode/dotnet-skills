---
name: managedcode-storage
description: "Use ManagedCode.Storage when a .NET application needs a provider-agnostic storage abstraction with explicit configuration, container selection, upload and download flows, and backend-specific integration kept behind one library contract."
compatibility: "Requires a .NET application that integrates ManagedCode.Storage or evaluates it as a storage abstraction."
---

# ManagedCode.Storage

## Trigger On

- integrating `ManagedCode.Storage` into a .NET application
- reviewing how a project abstracts file or object storage
- deciding whether to centralize storage provider differences behind one library
- documenting upload, download, container, or blob-handling flows with ManagedCode.Storage

## Workflow

1. Identify the actual storage use case:
   - blob or file storage
   - provider abstraction across environments
   - app-service integration and configuration
2. Verify whether the project wants one storage contract instead of provider-specific SDK calls scattered across the codebase.
3. Keep application code dependent on the library abstraction, not directly on backend-specific storage SDKs unless a provider-only feature is truly required.
4. Centralize provider configuration, credentials, and container naming in composition-root code and typed settings.
5. Validate the real upload, download, existence-check, and deletion flows after wiring the library.

```mermaid
flowchart LR
  A["Application service"] --> B["ManagedCode.Storage abstraction"]
  B --> C["Provider-specific storage implementation"]
  C --> D["Blob or object storage backend"]
```

## Deliver

- concrete guidance on when ManagedCode.Storage is the right abstraction
- wiring guidance that keeps provider concerns out of business code
- verification steps for the storage flows the application actually uses

## Validate

- the project really benefits from a storage abstraction and is not hiding provider-specific behavior it still needs
- storage configuration is centralized and explicit
- code reviews check real read and write paths, not only registration snippets
