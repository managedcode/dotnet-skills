---
name: dotnet-managedcode-mimetypes
description: "Use ManagedCode.MimeTypes when a .NET application needs consistent MIME type detection, extension mapping, and content-type decisions for uploads, downloads, or HTTP responses."
compatibility: "Requires a .NET application that integrates ManagedCode.MimeTypes or evaluates MIME type mapping behavior."
---

# ManagedCode.MimeTypes

## Trigger On

- integrating `ManagedCode.MimeTypes` into upload or download flows
- mapping file extensions to content types in APIs or background processing
- reviewing content-type handling for files, blobs, or attachments
- documenting a reusable MIME-type decision point in a .NET application

## Workflow

1. Identify where the application needs stable MIME-type decisions:
   - upload validation
   - download response headers
   - storage metadata
   - attachment processing
2. Centralize content-type mapping instead of scattering ad-hoc string tables across the codebase.
3. Use one library boundary for extension and MIME lookups.
4. Validate the extensions and media types that matter to the product.
5. Document any product-specific overrides separately from the library defaults.

```mermaid
flowchart LR
  A["File name or extension"] --> B["ManagedCode.MimeTypes lookup"]
  B --> C["Resolved MIME type"]
  C --> D["Upload validation, storage metadata, or HTTP response"]
```

## Deliver

- guidance on where MIME lookup belongs in application code
- recommendations for centralized content-type decisions
- validation expectations for real file types used by the product

## Validate

- MIME mapping is not duplicated across multiple services or controllers
- important file types are verified explicitly
- response or storage code uses the resolved type consistently
