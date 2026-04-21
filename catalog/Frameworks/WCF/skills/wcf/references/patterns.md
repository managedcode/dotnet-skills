# WCF Maintenance Patterns

This document covers patterns for maintaining and operating WCF services on .NET Framework.

## Configuration Management

### Binding Configuration Best Practices

Keep bindings explicit rather than relying on defaults:

```xml
<bindings>
  <basicHttpBinding>
    <binding name="SecureBasicHttp"
             maxReceivedMessageSize="10485760"
             receiveTimeout="00:10:00"
             sendTimeout="00:01:00">
      <security mode="Transport">
        <transport clientCredentialType="None" />
      </security>
    </binding>
  </basicHttpBinding>
</bindings>
```

Common pitfalls:
- Default `maxReceivedMessageSize` (65536) causes failures on large payloads
- Default timeouts may be too short for slow operations
- Unnamed bindings create implicit defaults that are hard to trace

### Service Behavior Configuration

```xml
<behaviors>
  <serviceBehaviors>
    <behavior name="StandardServiceBehavior">
      <serviceMetadata httpGetEnabled="true" httpsGetEnabled="true" />
      <serviceDebug includeExceptionDetailInFaults="false" />
      <serviceThrottling maxConcurrentCalls="100"
                         maxConcurrentInstances="100"
                         maxConcurrentSessions="100" />
    </behavior>
  </serviceBehaviors>
</behaviors>
```

Production rules:
- Always set `includeExceptionDetailInFaults="false"` in production
- Configure throttling based on measured capacity
- Disable metadata endpoints in production if not required

### Endpoint Configuration

```xml
<services>
  <service name="MyNamespace.MyService" behaviorConfiguration="StandardServiceBehavior">
    <endpoint address=""
              binding="basicHttpBinding"
              bindingConfiguration="SecureBasicHttp"
              contract="MyNamespace.IMyService" />
    <endpoint address="mex"
              binding="mexHttpsBinding"
              contract="IMetadataExchange" />
  </service>
</services>
```

## Contract Design Patterns

### Versioning Contracts

Use explicit namespaces and version indicators:

```csharp
[ServiceContract(Namespace = "http://example.com/services/v2")]
public interface IOrderServiceV2
{
    [OperationContract]
    OrderResponseV2 PlaceOrder(OrderRequestV2 request);
}

[DataContract(Namespace = "http://example.com/data/v2")]
public class OrderRequestV2
{
    [DataMember(Order = 1)]
    public string CustomerId { get; set; }

    [DataMember(Order = 2, IsRequired = false)]
    public string NewFieldInV2 { get; set; }
}
```

Versioning strategies:
- Add new fields as optional (`IsRequired = false`)
- Use `Order` attribute for deterministic serialization
- Maintain parallel endpoints for breaking changes
- Document version lifecycle and deprecation schedule

### Fault Contract Patterns

Define explicit faults instead of relying on generic exceptions:

```csharp
[ServiceContract]
public interface IOrderService
{
    [OperationContract]
    [FaultContract(typeof(ValidationFault))]
    [FaultContract(typeof(NotFoundFault))]
    OrderResponse GetOrder(string orderId);
}

[DataContract]
public class ValidationFault
{
    [DataMember]
    public string Field { get; set; }

    [DataMember]
    public string Message { get; set; }
}
```

Fault handling on client:

```csharp
try
{
    var response = client.GetOrder(orderId);
}
catch (FaultException<ValidationFault> ex)
{
    // Handle validation error
}
catch (FaultException<NotFoundFault> ex)
{
    // Handle not found
}
catch (FaultException ex)
{
    // Handle unexpected service fault
}
catch (CommunicationException ex)
{
    // Handle communication failure
}
```

## Hosting Patterns

### IIS Hosting

Standard .svc file approach:

```
<%@ ServiceHost Language="C#" Service="MyNamespace.MyService" %>
```

With factory for advanced scenarios:

```
<%@ ServiceHost Language="C#"
    Service="MyNamespace.MyService"
    Factory="MyNamespace.CustomServiceHostFactory" %>
```

IIS configuration in web.config:
- Configure app pool recycling to minimize service disruption
- Set idle timeout based on service usage patterns
- Enable WCF tracing in staging but not production

### Self-Hosting

```csharp
using (var host = new ServiceHost(typeof(MyService)))
{
    host.Open();
    Console.WriteLine("Service running. Press Enter to stop.");
    Console.ReadLine();
    host.Close();
}
```

Production considerations:
- Implement graceful shutdown handling
- Configure Windows Service wrapper for reliability
- Handle Faulted state and restart logic

### Windows Service Hosting

```csharp
public class MyServiceHost : ServiceBase
{
    private ServiceHost _serviceHost;

    protected override void OnStart(string[] args)
    {
        _serviceHost = new ServiceHost(typeof(MyService));
        _serviceHost.Open();
    }

    protected override void OnStop()
    {
        _serviceHost?.Close();
    }
}
```

## Security Patterns

### Transport Security with Certificates

```xml
<bindings>
  <netTcpBinding>
    <binding name="SecureNetTcp">
      <security mode="Transport">
        <transport clientCredentialType="Certificate" />
      </security>
    </binding>
  </netTcpBinding>
</bindings>
```

Certificate management:
- Store certificates in Windows Certificate Store, not file system
- Use certificate thumbprints in configuration
- Implement certificate rotation procedures
- Monitor certificate expiration

### Message Security

```xml
<bindings>
  <wsHttpBinding>
    <binding name="SecureWsHttp">
      <security mode="Message">
        <message clientCredentialType="Certificate"
                 negotiateServiceCredential="false"
                 establishSecurityContext="false" />
      </security>
    </binding>
  </wsHttpBinding>
</bindings>
```

### Custom Authorization

```csharp
public class CustomAuthorizationPolicy : IAuthorizationPolicy
{
    public bool Evaluate(EvaluationContext context, ref object state)
    {
        // Custom authorization logic
        var identity = GetIdentityFromContext(context);
        var claims = BuildClaimsForIdentity(identity);
        context.AddClaimSet(this, new DefaultClaimSet(claims));
        return true;
    }
}
```

## Diagnostics and Monitoring

### WCF Tracing

Enable tracing for troubleshooting:

```xml
<system.diagnostics>
  <sources>
    <source name="System.ServiceModel" switchValue="Warning,ActivityTracing">
      <listeners>
        <add name="traceListener"
             type="System.Diagnostics.XmlWriterTraceListener"
             initializeData="c:\logs\wcf-traces.svclog" />
      </listeners>
    </source>
  </sources>
</system.diagnostics>
```

Trace levels:
- `Off`: Production default
- `Warning`: Production troubleshooting
- `Information`: Staging
- `Verbose`: Development only (high overhead)

### Message Logging

```xml
<system.serviceModel>
  <diagnostics>
    <messageLogging logEntireMessage="true"
                    logMalformedMessages="true"
                    logMessagesAtServiceLevel="true"
                    logMessagesAtTransportLevel="false" />
  </diagnostics>
</system.serviceModel>
```

Security warning: Message logs may contain sensitive data. Encrypt or mask before storing.

### Performance Counters

Enable WCF performance counters:

```xml
<system.serviceModel>
  <diagnostics performanceCounters="All" />
</system.serviceModel>
```

Key counters to monitor:
- Calls per second
- Calls outstanding
- Calls failed
- Calls faulted
- Instances created per second

## Reliability Patterns

### Reliable Sessions

```xml
<bindings>
  <wsHttpBinding>
    <binding name="ReliableBinding">
      <reliableSession enabled="true" ordered="true" />
    </binding>
  </wsHttpBinding>
</bindings>
```

Use when:
- Message ordering is required
- Network reliability is questionable
- Exactly-once delivery semantics are needed

### Instance Management

```csharp
[ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
public class StatelessService : IMyService
{
    // New instance per call - best for scalability
}

[ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession)]
public class SessionService : IMyService
{
    // Instance per session - state across calls
}

[ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
public class SingletonService : IMyService
{
    // Single instance - use with caution
}
```

### Concurrency Management

```csharp
[ServiceBehavior(
    InstanceContextMode = InstanceContextMode.Single,
    ConcurrencyMode = ConcurrencyMode.Multiple)]
public class ThreadSafeService : IMyService
{
    private readonly object _lock = new object();

    public void Operation()
    {
        lock (_lock)
        {
            // Thread-safe operation
        }
    }
}
```

## Error Handling Patterns

### Global Error Handler

```csharp
public class GlobalErrorHandler : IErrorHandler
{
    public bool HandleError(Exception error)
    {
        // Log error
        Logger.LogException(error);
        return false; // Do not suppress
    }

    public void ProvideFault(Exception error, MessageVersion version,
                             ref Message fault)
    {
        var faultException = new FaultException<ServiceFault>(
            new ServiceFault { Message = "An error occurred" },
            new FaultReason("Service Error"));

        var msgFault = faultException.CreateMessageFault();
        fault = Message.CreateMessage(version, msgFault, faultException.Action);
    }
}
```

Register via behavior:

```csharp
public class ErrorHandlerBehavior : IServiceBehavior
{
    public void ApplyDispatchBehavior(ServiceDescription description,
                                      ServiceHostBase host)
    {
        foreach (ChannelDispatcher dispatcher in host.ChannelDispatchers)
        {
            dispatcher.ErrorHandlers.Add(new GlobalErrorHandler());
        }
    }
}
```

## Client Proxy Patterns

### Proper Client Lifecycle

```csharp
public class ServiceClientWrapper : IDisposable
{
    private readonly MyServiceClient _client;

    public ServiceClientWrapper()
    {
        _client = new MyServiceClient();
    }

    public void CallService()
    {
        try
        {
            _client.Operation();
        }
        catch (FaultException)
        {
            _client.Abort();
            throw;
        }
        catch (CommunicationException)
        {
            _client.Abort();
            throw;
        }
        catch (TimeoutException)
        {
            _client.Abort();
            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            if (_client.State == CommunicationState.Faulted)
            {
                _client.Abort();
            }
            else
            {
                _client.Close();
            }
        }
        catch
        {
            _client.Abort();
        }
    }
}
```

### Channel Factory Pattern

```csharp
public class ServiceChannelFactory<TChannel> : IDisposable
{
    private readonly ChannelFactory<TChannel> _factory;

    public ServiceChannelFactory(string endpointName)
    {
        _factory = new ChannelFactory<TChannel>(endpointName);
    }

    public TChannel CreateChannel()
    {
        return _factory.CreateChannel();
    }

    public void Dispose()
    {
        try
        {
            _factory.Close();
        }
        catch
        {
            _factory.Abort();
        }
    }
}
```

## Interoperability Patterns

### WCF to Java/.NET Interop

```csharp
[ServiceContract(Namespace = "http://example.com/services")]
public interface IInteropService
{
    [OperationContract]
    [XmlSerializerFormat] // Better interop than DataContractSerializer
    InteropResponse Process(InteropRequest request);
}
```

Use `XmlSerializerFormat` when:
- Interoperating with non-.NET SOAP clients
- Schema compatibility is critical
- Complex XML structures required

### MTOM for Large Binary Data

```xml
<bindings>
  <basicHttpBinding>
    <binding name="MtomBinding" messageEncoding="Mtom">
      <security mode="Transport" />
    </binding>
  </basicHttpBinding>
</bindings>
```

Use MTOM when transferring binary data larger than 1KB to reduce base64 encoding overhead.
