# WCF Migration Paths

This document covers migration strategies from WCF to modern .NET alternatives.

## Migration Decision Framework

### When to Migrate

- New feature development is blocked by .NET Framework dependencies
- Client ecosystem has moved to REST/gRPC
- WS-* features are no longer required by consumers
- Deployment targets require .NET 6+ or containerization
- Security requirements exceed WCF's maintenance window

### When to Stay on WCF

- Active WS-Security, WS-ReliableMessaging, or WS-Transaction requirements
- Enterprise clients require WSDL-based contract discovery
- Named pipes or MSMQ transport dependencies with no equivalent
- Stable production system with no business driver for change

## Migration Target Selection

### gRPC Migration Path

Best for:
- High-performance internal service-to-service communication
- Streaming scenarios (client, server, or bidirectional)
- Strong contract-first development with .proto files
- Polyglot environments requiring cross-language interop

Migration steps:
1. Map WCF `[DataContract]` types to Protocol Buffer messages
2. Convert `[ServiceContract]` interfaces to gRPC service definitions
3. Replace `[OperationContract]` methods with gRPC rpc definitions
4. Migrate request-reply patterns to unary calls
5. Convert duplex contracts to bidirectional streaming
6. Update client proxies from `ChannelFactory<T>` to generated gRPC clients
7. Replace WCF behaviors with gRPC interceptors

Contract mapping:
```
WCF                          gRPC
---                          ----
[ServiceContract]       ->   service
[OperationContract]     ->   rpc
[DataContract]          ->   message
[DataMember]            ->   field
[FaultContract]         ->   Status + error details
```

### REST/ASP.NET Core Migration Path

Best for:
- Public APIs with HTTP client expectations
- Browser-based consumers
- OpenAPI/Swagger documentation requirements
- Simple request-response patterns
- Teams familiar with MVC/Web API patterns

Migration steps:
1. Create ASP.NET Core Web API project
2. Map `[ServiceContract]` to controller classes
3. Convert `[OperationContract]` to action methods with HTTP verbs
4. Replace `[DataContract]` with POCOs or record types
5. Map WCF faults to HTTP status codes and problem details
6. Replace WSDL with OpenAPI specification
7. Update clients from WCF proxies to HttpClient or typed clients

HTTP verb mapping:
```
WCF Pattern                  HTTP Verb
-----------                  ---------
GetXxx operations       ->   GET
CreateXxx operations    ->   POST
UpdateXxx operations    ->   PUT/PATCH
DeleteXxx operations    ->   DELETE
```

### CoreWCF Migration Path

Best for:
- Minimal code changes required
- Existing SOAP clients cannot be updated
- WS-* features still needed but .NET 6+ hosting required
- Bridge strategy during longer migration timelines

Migration steps:
1. Add CoreWCF NuGet packages to new .NET project
2. Copy service contracts and implementations
3. Update binding configurations to CoreWCF equivalents
4. Configure ASP.NET Core hosting for CoreWCF services
5. Test with existing SOAP clients
6. Gradually migrate endpoints to REST/gRPC as clients update

Supported bindings in CoreWCF:
- BasicHttpBinding
- NetTcpBinding
- WSHttpBinding (partial)
- NetHttpBinding
- WebHttpBinding

## Endpoint-by-Endpoint Migration

Large WCF services should migrate incrementally:

1. **Inventory phase**
   - List all endpoints with their bindings and contracts
   - Identify client dependencies per endpoint
   - Document WS-* feature usage per operation
   - Assess security requirements per endpoint

2. **Categorization phase**
   - Green: Simple request-reply, no WS-* features (migrate first)
   - Yellow: Complex contracts but no hard WCF dependencies
   - Red: Active WS-* requirements or inflexible clients

3. **Parallel operation phase**
   - Run WCF and new endpoints simultaneously
   - Route clients to new endpoints as they update
   - Monitor both stacks during transition

4. **Decommission phase**
   - Remove WCF endpoints when all clients have migrated
   - Archive WCF configuration for reference
   - Update documentation and runbooks

## Security Model Migration

### WCF to ASP.NET Core Security

```
WCF Security                 ASP.NET Core
------------                 ------------
Transport security      ->   HTTPS + TLS
Message security        ->   JWTs or API keys in headers
Windows auth            ->   Negotiate/NTLM middleware
Certificate auth        ->   Client certificate middleware
Username/password       ->   Basic auth or OAuth2 resource owner
WS-Federation           ->   OpenID Connect
```

### WCF to gRPC Security

```
WCF Security                 gRPC
------------                 ----
Transport security      ->   TLS channel credentials
Windows auth            ->   Negotiate interceptor
Certificate auth        ->   SSL credentials with client cert
Token-based             ->   Metadata credentials (call/channel)
```

## Transaction Migration

WCF `[TransactionFlow]` has no direct equivalent in gRPC or REST.

Options:
- Saga pattern for distributed transactions
- Outbox pattern for reliable messaging
- Idempotency keys for retry safety
- Eventual consistency with compensation logic

## Client Migration Checklist

- [ ] Identify all WCF client applications
- [ ] Assess client update feasibility and timeline
- [ ] Generate new client code (gRPC protos, OpenAPI, HttpClient)
- [ ] Update authentication/authorization flows
- [ ] Test error handling and fault scenarios
- [ ] Validate timeout and retry behaviors
- [ ] Update monitoring and diagnostics
- [ ] Plan rollback procedures
