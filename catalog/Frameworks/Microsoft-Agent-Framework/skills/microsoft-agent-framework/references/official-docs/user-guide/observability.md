---
title: Observability
description: Learn how to use observability with Agent Framework
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: reference
ms.author: edvan
ms.date: 12/16/2025
ms.service: agent-framework
---

# Observability

Observability is a key aspect of building reliable and maintainable systems. Agent Framework provides built-in support for observability, allowing you to monitor the behavior of your agents.

This guide will walk you through the steps to enable observability with Agent Framework to help you understand how your agents are performing and diagnose any issues that might arise.

## OpenTelemetry Integration

Agent Framework integrates with [OpenTelemetry](https://opentelemetry.io/), and more specifically Agent Framework emits traces, logs, and metrics according to the [OpenTelemetry GenAI Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/).

::: zone pivot="programming-language-csharp"

## Enable Observability (C#)

To enable observability for your chat client, you need to build the chat client as follows:

```csharp
// Using the Azure OpenAI client as an example
var instrumentedChatClient = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient() // Converts a native OpenAI SDK ChatClient into a Microsoft.Extensions.AI.IChatClient
    .AsBuilder()
    .UseOpenTelemetry(sourceName: "MyApplication", configure: (cfg) => cfg.EnableSensitiveData = true)    // Enable OpenTelemetry instrumentation with sensitive data
    .Build();
```

To enable observability for your agent, you need to build the agent as follows:

```csharp
var agent = new ChatClientAgent(
    instrumentedChatClient,
    name: "OpenTelemetryDemoAgent",
    instructions: "You are a helpful assistant that provides concise and informative responses.",
    tools: [AIFunctionFactory.Create(GetWeatherAsync)]
).WithOpenTelemetry(sourceName: "MyApplication", enableSensitiveData: true);    // Enable OpenTelemetry instrumentation with sensitive data
```

> [!IMPORTANT]
> When you enable observability for your chat clients and agents, you might see duplicated information, especially when sensitive data is enabled. The chat context (including prompts and responses) that is captured by both the chat client and the agent will be included in both spans. Depending on your needs, you might choose to enable observability only on the chat client or only on the agent to avoid duplication. See the [GenAI Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/) for more details on the attributes captured for LLM and Agents.

> [!NOTE]
> Only enable sensitive data in development or testing environments, as it might expose user information in production logs and traces. Sensitive data includes prompts, responses, function call arguments, and results.

### Configuration

Now that your chat client and agent are instrumented, you can configure the OpenTelemetry exporters to send the telemetry data to your desired backend.

#### Traces

To export traces to the desired backend, you can configure the OpenTelemetry SDK in your application startup code. For example, to export traces to an Azure Monitor resource:

```csharp
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using System;

var SourceName = "MyApplication";

var applicationInsightsConnectionString = Environment.GetEnvironmentVariable("APPLICATION_INSIGHTS_CONNECTION_STRING")
    ?? throw new InvalidOperationException("APPLICATION_INSIGHTS_CONNECTION_STRING is not set.");

var resourceBuilder = ResourceBuilder
    .CreateDefault()
    .AddService(ServiceName);

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddSource(SourceName)
    .AddSource("*Microsoft.Extensions.AI") // Listen to the Experimental.Microsoft.Extensions.AI source for chat client telemetry.
    .AddSource("*Microsoft.Extensions.Agents*") // Listen to the Experimental.Microsoft.Extensions.Agents source for agent telemetry.
    .AddAzureMonitorTraceExporter(options => options.ConnectionString = applicationInsightsConnectionString)
    .Build();
```

> [!TIP]
> Depending on your backend, you can use different exporters. For more information, see the [OpenTelemetry .NET documentation](https://opentelemetry.io/docs/instrumentation/net/exporters/). For local development, consider using the [Aspire Dashboard](#aspire-dashboard).

#### Metrics

Similarly, to export metrics to the desired backend, you can configure the OpenTelemetry SDK in your application startup code. For example, to export metrics to an Azure Monitor resource:

```csharp
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using System;

var applicationInsightsConnectionString = Environment.GetEnvironmentVariable("APPLICATION_INSIGHTS_CONNECTION_STRING")
    ?? throw new InvalidOperationException("APPLICATION_INSIGHTS_CONNECTION_STRING is not set.");

var resourceBuilder = ResourceBuilder
    .CreateDefault()
    .AddService(ServiceName);

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddSource(SourceName)
    .AddMeter("*Microsoft.Agents.AI") // Agent Framework metrics
    .AddAzureMonitorMetricExporter(options => options.ConnectionString = applicationInsightsConnectionString)
    .Build();
```

#### Logs

Logs are captured via the logging framework you are using, for example `Microsoft.Extensions.Logging`. To export logs to an Azure Monitor resource, you can configure the logging provider in your application startup code:

```csharp
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Logging;

var applicationInsightsConnectionString = Environment.GetEnvironmentVariable("APPLICATION_INSIGHTS_CONNECTION_STRING")
    ?? throw new InvalidOperationException("APPLICATION_INSIGHTS_CONNECTION_STRING is not set.");

using var loggerFactory = LoggerFactory.Create(builder =>
{
    // Add OpenTelemetry as a logging provider
    builder.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(resourceBuilder);
        options.AddAzureMonitorLogExporter(options => options.ConnectionString = applicationInsightsConnectionString);
        // Format log messages. This is default to false.
        options.IncludeFormattedMessage = true;
        options.IncludeScopes = true;
    })
    .SetMinimumLevel(LogLevel.Debug);
});

// Create a logger instance for your application
var logger = loggerFactory.CreateLogger<Program>();
```

## Aspire Dashboard

Consider using the Aspire Dashboard as a quick way to visualize your traces and metrics during development. To Learn more, see [Aspire Dashboard documentation](/dotnet/aspire/fundamentals/dashboard/overview). The Aspire Dashboard receives data via an OpenTelemetry Collector, which you can add to your tracer provider as follows:

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddSource(SourceName)
    .AddSource("*Microsoft.Extensions.AI") // Listen to the Experimental.Microsoft.Extensions.AI source for chat client telemetry.
    .AddSource("*Microsoft.Extensions.Agents*") // Listen to the Experimental.Microsoft.Extensions.Agents source for agent telemetry.
    .AddOtlpExporter(options => options.Endpoint = new Uri("http://localhost:4317"))
    .Build();
```

## Getting started

See a full example of an agent with OpenTelemetry enabled in the [Agent Framework repository](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/GettingStarted/AgentOpenTelemetry).

::: zone-end

::: zone pivot="programming-language-python"

## Dependencies

### Included packages
To enable observability in your Python application, the following OpenTelemetry packages are installed by default:
- [opentelemetry-api](https://pypi.org/project/opentelemetry-api/)
- [opentelemetry-sdk](https://pypi.org/project/opentelemetry-sdk/)
- [opentelemetry-semantic-conventions-ai](https://pypi.org/project/opentelemetry-semantic-conventions-ai/)


### Exporters
We do *not* install exporters by default to prevent unnecessary dependencies and potential issues with auto instrumentation. There is a large variety of exporters available for different backends, so you can choose the ones that best fit your needs.

Some common exporters you may want to install based on your needs:
- For gRPC protocol support: install `opentelemetry-exporter-otlp-proto-grpc`
- For HTTP protocol support: install `opentelemetry-exporter-otlp-proto-http`
- For Azure Application Insights: install `azure-monitor-opentelemetry`

Use the [OpenTelemetry Registry](https://opentelemetry.io/ecosystem/registry/?language=python&component=instrumentation) to find more exporters and instrumentation packages.

## Enable Observability (Python)
### Five patterns for configuring observability

We've identified multiple ways to configure observability in your application, depending on your needs:

#### 1. Standard OpenTelemetry environment variables (Recommended)

The simplest approach - configure everything via environment variables:

```python
from agent_framework.observability import configure_otel_providers

# Reads OTEL_EXPORTER_OTLP_* environment variables automatically
configure_otel_providers()
```

Or if you just want console exporters:

```python
from agent_framework.observability import configure_otel_providers

configure_otel_providers(enable_console_exporters=True)
```

#### 2. Custom Exporters

For more control over the exporters, create them yourself and pass them to `configure_otel_providers()`:

```python
from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import OTLPSpanExporter
from opentelemetry.exporter.otlp.proto.grpc._log_exporter import OTLPLogExporter
from opentelemetry.exporter.otlp.proto.grpc.metric_exporter import OTLPMetricExporter
from agent_framework.observability import configure_otel_providers

# Create custom exporters with specific configuration
exporters = [
    OTLPSpanExporter(endpoint="http://localhost:4317", compression=Compression.Gzip),
    OTLPLogExporter(endpoint="http://localhost:4317"),
    OTLPMetricExporter(endpoint="http://localhost:4317"),
]

# These will be added alongside any exporters from environment variables
configure_otel_providers(exporters=exporters, enable_sensitive_data=True)
```

#### 3. Third party setup

Many third-party OpenTelemetry packages have their own setup methods. You can use those methods first, then call `enable_instrumentation()` to activate Agent Framework instrumentation code paths:

```python
from azure.monitor.opentelemetry import configure_azure_monitor
from agent_framework.observability import create_resource, enable_instrumentation

# Configure Azure Monitor first
configure_azure_monitor(
    connection_string="InstrumentationKey=...",
    resource=create_resource(),  # Uses OTEL_SERVICE_NAME, etc.
    enable_live_metrics=True,
)

# Then activate Agent Framework's telemetry code paths
# This is optional if ENABLE_INSTRUMENTATION and/or ENABLE_SENSITIVE_DATA are set in env vars
enable_instrumentation(enable_sensitive_data=False)
```

For [Langfuse](https://langfuse.com/integrations/frameworks/microsoft-agent-framework):

```python
from agent_framework.observability import enable_instrumentation
from langfuse import get_client

langfuse = get_client()

# Verify connection
if langfuse.auth_check():
    print("Langfuse client is authenticated and ready!")

# Then activate Agent Framework's telemetry code paths
enable_instrumentation(enable_sensitive_data=False)
```

#### 4. Manual setup

For complete control, you can manually set up exporters, providers, and instrumentation. Use the helper function `create_resource()` to create a resource with the appropriate service name and version. See the [OpenTelemetry Python documentation](https://opentelemetry.io/docs/languages/python/instrumentation/) for detailed guidance on manual instrumentation.

#### 5. Auto-instrumentation (zero-code)

Use the [OpenTelemetry CLI tool](https://opentelemetry.io/docs/instrumentation/python/getting-started/#automatic-instrumentation) to automatically instrument your application without code changes:

```bash
opentelemetry-instrument \
    --traces_exporter console,otlp \
    --metrics_exporter console \
    --service_name your-service-name \
    --exporter_otlp_endpoint 0.0.0.0:4317 \
    python agent_framework_app.py
```

See the [OpenTelemetry Zero-code Python documentation](https://opentelemetry.io/docs/zero-code/python/) for more information.

### Using tracers and meters

Once observability is configured, you can create custom spans or metrics:

```python
from agent_framework.observability import get_tracer, get_meter

tracer = get_tracer()
meter = get_meter()
with tracer.start_as_current_span("my_custom_span"):
    # do something
    pass
counter = meter.create_counter("my_custom_counter")
counter.add(1, {"key": "value"})
```

These are wrappers of the OpenTelemetry API that return a tracer or meter from the global provider, with `agent_framework` set as the instrumentation library name by default.

### Environment variables

The following environment variables control Agent Framework observability:

- `ENABLE_INSTRUMENTATION` - Default is `false`, set to `true` to enable OpenTelemetry instrumentation.
- `ENABLE_SENSITIVE_DATA` - Default is `false`, set to `true` to enable logging of sensitive data (prompts, responses, function call arguments, and results). Be careful with this setting as it might expose sensitive data.
- `ENABLE_CONSOLE_EXPORTERS` - Default is `false`, set to `true` to enable console output for telemetry.
- `VS_CODE_EXTENSION_PORT` - Port for AI Toolkit or Azure AI Foundry VS Code extension integration.

> [!NOTE]
> Sensitive information includes prompts, responses, and more, and should only be enabled in development or test environments. It is not recommended to enable this in production as it may expose sensitive data.

#### Standard OpenTelemetry environment variables

The `configure_otel_providers()` function automatically reads standard OpenTelemetry environment variables:

**OTLP Configuration** (for Aspire Dashboard, Jaeger, etc.):
- `OTEL_EXPORTER_OTLP_ENDPOINT` - Base endpoint for all signals (e.g., `http://localhost:4317`)
- `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT` - Traces-specific endpoint (overrides base)
- `OTEL_EXPORTER_OTLP_METRICS_ENDPOINT` - Metrics-specific endpoint (overrides base)
- `OTEL_EXPORTER_OTLP_LOGS_ENDPOINT` - Logs-specific endpoint (overrides base)
- `OTEL_EXPORTER_OTLP_PROTOCOL` - Protocol to use (`grpc` or `http`, default: `grpc`)
- `OTEL_EXPORTER_OTLP_HEADERS` - Headers for all signals (e.g., `key1=value1,key2=value2`)

**Service Identification**:
- `OTEL_SERVICE_NAME` - Service name (default: `agent_framework`)
- `OTEL_SERVICE_VERSION` - Service version (default: package version)
- `OTEL_RESOURCE_ATTRIBUTES` - Additional resource attributes

See the [OpenTelemetry spec](https://opentelemetry.io/docs/specs/otel/configuration/sdk-environment-variables/) for more details.

### Microsoft Foundry setup

Microsoft Foundry has built-in support for tracing with visualization for your spans.

Make sure you have your Foundry configured with a Azure Monitor instance, see [details](/azure/ai-foundry/how-to/monitor-applications)

#### Install the `azure-monitor-opentelemetry` package:

```bash
pip install azure-monitor-opentelemetry
```

#### Configure observability directly from the `AzureAIClient`:
For Azure AI Foundry projects, you can configure observability directly from the `AzureAIClient`:

```python
from agent_framework.azure import AzureAIClient
from azure.ai.projects.aio import AIProjectClient
from azure.identity.aio import AzureCliCredential

async def main():
    async with (
        AzureCliCredential() as credential,
        AIProjectClient(endpoint="https://<your-project>.foundry.azure.com", credential=credential) as project_client,
        AzureAIClient(project_client=project_client) as client,
    ):
        # Automatically configures Azure Monitor with connection string from project
        await client.configure_azure_monitor(enable_live_metrics=True)
```

> [!TIP]
> The arguments for `client.configure_azure_monitor()` are passed through to the underlying `configure_azure_monitor()` function from the `azure-monitor-opentelemetry` package, see [documentation](/python/api/overview/azure/monitor-opentelemetry-readme#usage) for details, we take care of setting the connection string and resource.


#### Configure azure monitor and optionally enable instrumentation:
For non-Azure AI projects with Application Insights, make sure you setup a custom agent in Foundry, see [details](/azure/ai-foundry/control-plane/register-custom-agent).

Then run your agent with the same _OpenTelemetry agent ID_ as registered in Foundry, and configure azure monitor as follows:

```python
from azure.monitor.opentelemetry import configure_azure_monitor
from agent_framework.observability import create_resource, enable_instrumentation

configure_azure_monitor(
    connection_string="InstrumentationKey=...",
    resource=create_resource(),
    enable_live_metrics=True,
)
# optional if you do not have ENABLE_INSTRUMENTATION in env vars
enable_instrumentation()

# Create your agent with the same OpenTelemetry agent ID as registered in Foundry
agent = ChatAgent(
    chat_client=...,
    name="My Agent",
    instructions="You are a helpful assistant.",
    id="<OpenTelemetry agent ID>"
)
# use the agent as normal
```

### Aspire Dashboard

For local development without Azure setup, you can use the [Aspire Dashboard](/dotnet/aspire/fundamentals/dashboard/standalone), which runs locally via Docker and provides an excellent telemetry viewing experience.

#### Setting up Aspire Dashboard with Docker

```bash
# Pull and run the Aspire Dashboard container
docker run --rm -it -d \
    -p 18888:18888 \
    -p 4317:18889 \
    --name aspire-dashboard \
    mcr.microsoft.com/dotnet/aspire-dashboard:latest
```

This command will start the dashboard with:

- **Web UI**: Available at <http://localhost:18888>
- **OTLP endpoint**: Available at `http://localhost:4317` for your applications to send telemetry data

#### Configuring your application

Set the following environment variables:

```bash
ENABLE_INSTRUMENTATION=true
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
```

Or include them in your `.env` file and run your sample.

Once your sample finishes running, navigate to <http://localhost:18888> in a web browser to see the telemetry data. Follow the [Aspire Dashboard exploration guide](/dotnet/aspire/fundamentals/dashboard/explore) to authenticate to the dashboard and start exploring your traces, logs, and metrics.

## Spans and metrics

Once everything is setup, you will start seeing spans and metrics being created automatically for you, the spans are:

- `invoke_agent <agent_name>`: This is the top level span for each agent invocation, it will contain all other spans as children.
- `chat <model_name>`: This span is created when the agent calls the underlying chat model, it will contain the prompt and response as attributes, if `enable_sensitive_data` is set to `True`.
- `execute_tool <function_name>`: This span is created when the agent calls a function tool, it will contain the function arguments and result as attributes, if `enable_sensitive_data` is set to `True`.

The metrics that are created are:

- For the chat client and `chat` operations:
  - `gen_ai.client.operation.duration` (histogram): This metric measures the duration of each operation, in seconds.
  - `gen_ai.client.token.usage` (histogram): This metric measures the token usage, in number of tokens.

- For function invocation during the `execute_tool` operations:
  - `agent_framework.function.invocation.duration` (histogram): This metric measures the duration of each function execution, in seconds.

### Example trace output

When you run an agent with observability enabled, you'll see trace data similar to the following console output:

```text
{
    "name": "invoke_agent Joker",
    "context": {
        "trace_id": "0xf2258b51421fe9cf4c0bd428c87b1ae4",
        "span_id": "0x2cad6fc139dcf01d",
        "trace_state": "[]"
    },
    "kind": "SpanKind.CLIENT",
    "parent_id": null,
    "start_time": "2025-09-25T11:00:48.663688Z",
    "end_time": "2025-09-25T11:00:57.271389Z",
    "status": {
        "status_code": "UNSET"
    },
    "attributes": {
        "gen_ai.operation.name": "invoke_agent",
        "gen_ai.system": "openai",
        "gen_ai.agent.id": "Joker",
        "gen_ai.agent.name": "Joker",
        "gen_ai.request.instructions": "You are good at telling jokes.",
        "gen_ai.response.id": "chatcmpl-CH6fgKwMRGDtGNO3H88gA3AG2o7c5",
        "gen_ai.usage.input_tokens": 26,
        "gen_ai.usage.output_tokens": 29
    }
}
```

This trace shows:

- **Trace and span identifiers**: For correlating related operations
- **Timing information**: When the operation started and ended
- **Agent metadata**: Agent ID, name, and instructions
- **Model information**: The AI system used (OpenAI) and response ID
- **Token usage**: Input and output token counts for cost tracking

## Samples

There are a number of samples in the `microsoft/agent-framework` repository that demonstrate these capabilities. For more information, see the [observability samples folder](https://github.com/microsoft/agent-framework/tree/main/python/samples/getting_started/observability). That folder includes samples for using zero-code telemetry as well.

::: zone-end
