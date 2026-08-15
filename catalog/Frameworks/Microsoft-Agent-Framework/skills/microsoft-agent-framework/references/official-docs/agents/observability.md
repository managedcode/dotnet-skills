---
title: Observability
description: Learn how to use observability with Agent Framework
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: reference
ms.author: edvan
ms.date: 07/30/2026
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
// Using the AIProjectClient as an example
var instrumentedChatClient = new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential())
    .GetProjectOpenAIClient()
    .GetProjectResponsesClient()
    .AsIChatClient(deploymentName) // Converts into a Microsoft.Extensions.AI.IChatClient
    .AsBuilder()
    .UseOpenTelemetry(sourceName: SourceName, configure: (cfg) => cfg.EnableSensitiveData = true)    // Enable OpenTelemetry instrumentation with sensitive data
    .Build();
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

To enable observability for your agent, you need to build the agent as follows:

```csharp
var agent = new ChatClientAgent(
    instrumentedChatClient,
    name: "OpenTelemetryDemoAgent",
    instructions: "You are a helpful assistant that provides concise and informative responses.",
    tools: [AIFunctionFactory.Create(GetWeatherAsync)]
).WithOpenTelemetry(sourceName: SourceName, configure: (cfg) => cfg.EnableSensitiveData = true); // Enable OpenTelemetry instrumentation with sensitive data
```

> [!IMPORTANT]
> When you enable observability for your chat clients and agents, you might see duplicated information, especially when sensitive data is enabled. The chat context (including prompts and responses) that is captured by both the chat client and the agent will be included in both spans. Depending on your needs, you might choose to enable observability only on the chat client or only on the agent to avoid duplication. See the [GenAI Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/) for more details on the attributes captured for LLM and Agents.

> [!WARNING]
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

// The source name under which all activities, metrics, and logs will be emitted.
const string SourceName = "MyApplication";
const string ServiceName = "AgentOpenTelemetry";

var applicationInsightsConnectionString = Environment.GetEnvironmentVariable("APPLICATION_INSIGHTS_CONNECTION_STRING")
    ?? throw new InvalidOperationException("APPLICATION_INSIGHTS_CONNECTION_STRING is not set.");

var resourceBuilder = ResourceBuilder
    .CreateDefault()
    .AddService(ServiceName);

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddSource(SourceName)
    .AddAzureMonitorTraceExporter(options => options.ConnectionString = applicationInsightsConnectionString)
    .Build();
```

> [!TIP]
> The `AddSource` method is used to specify the source name which the provider will listen to. Make sure it matches the source name you used in your instrumentation code (e.g., `UseOpenTelemetry(sourceName: SourceName)`). If a source name is not specified in the instrumentation code, it will default to `Experimental.Microsoft.Agents.AI`, in which case you should use `AddSource("Experimental.Microsoft.Agents.AI")` in your tracer provider and meter provider configuration.

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
    .AddOtlpExporter(options => options.Endpoint = new Uri("http://localhost:4317"))
    .Build();
```

## Getting started

See a full example of an agent with OpenTelemetry enabled in the [Agent Framework repository](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/AgentOpenTelemetry).

> [!TIP]
> See the [.NET samples](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples) for complete runnable examples.

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

### MCP trace propagation

Whenever there is an active OpenTelemetry span context, Agent Framework automatically propagates trace context to MCP servers via the `params._meta` field of `tools/call` requests. It uses the globally-configured OpenTelemetry propagator(s) (W3C Trace Context by default, producing `traceparent` and `tracestate`), so custom propagators (B3, Jaeger, etc.) are also supported. This enables distributed tracing across agent-to-MCP-server boundaries, compliant with the [MCP `_meta` specification](https://modelcontextprotocol.io/specification/2025-11-25/basic#_meta).

**Scope:** automatic `_meta` injection applies only to MCP sessions that the agent process itself opens — `MCPStreamableHTTPTool`, `MCPStdioTool`, and `MCPWebsocketTool` (or any other client-opened `MCPTool` subclass). It does **not** apply to hosted/provider-managed MCP tool configurations such as `FoundryChatClient.get_mcp_tool(...)`, `OpenAIChatClient.get_mcp_tool(...)`, `AnthropicClient.get_mcp_tool(...)`, `GeminiChatClient.get_mcp_tool(...)`, or Foundry hosted-agent toolboxes, because in those cases the `tools/call` message is issued by the provider service runtime rather than by the agent process. As a result, the framework has no opportunity to inject trace context into those requests, and propagating `traceparent`/`tracestate` across that hosted-service boundary is the responsibility of the service runtime, not Agent Framework. If end-to-end distributed tracing to the downstream MCP server is required, use a client-opened MCP transport instead of a hosted connector.

### Five patterns for configuring observability

We've identified multiple ways to configure observability in your application, depending on your needs:

#### 1. Standard OpenTelemetry environment variables (Recommended)

The simplest approach - configure everything via environment variables:

```python
from agent_framework.observability import configure_otel_providers

# Reads OTEL_EXPORTER_OTLP_* environment variables automatically
configure_otel_providers()
```

Or if you just want console exporters, set the `ENABLE_CONSOLE_EXPORTERS` environment variable:

```bash
ENABLE_CONSOLE_EXPORTERS=true
```

```python
from agent_framework.observability import configure_otel_providers

# Console exporters are enabled via the ENABLE_CONSOLE_EXPORTERS env var
configure_otel_providers()
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

- `ENABLE_INSTRUMENTATION` - Default is `true`; set to `false` to disable OpenTelemetry instrumentation.
- `ENABLE_SENSITIVE_DATA` - Default is `false`, set to `true` to enable logging of sensitive data (prompts, responses, function call arguments, and results). Be careful with this setting as it might expose sensitive data.
- `ENABLE_CONSOLE_EXPORTERS` - Default is `false`, set to `true` to enable console output for telemetry.
- `VS_CODE_EXTENSION_PORT` - Port for AI Toolkit or Microsoft Foundry VS Code extension integration.

Agent Framework also adds its package and version to the User-Agent of supported client requests. Approved Microsoft Foundry and Azure OpenAI request paths can include a process-wide feature-usage token that encodes framework feature categories, not prompt or response content. Set these variables before starting the process:

- `AGENT_FRAMEWORK_FEATURE_MASK_DISABLED=true` - Disables only the feature-usage token and keeps the package/version User-Agent.
- `AGENT_FRAMEWORK_USER_AGENT_DISABLED=true` - Disables the entire Agent Framework User-Agent contribution, including the feature token.

> [!WARNING]
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

#### Configure observability directly from the `FoundryChatClient`

For Foundry projects, you can configure observability directly from the `FoundryChatClient`:

```python
import os

from agent_framework.foundry import FoundryChatClient
from azure.identity.aio import AzureCliCredential

async def main():
    async with AzureCliCredential() as credential:
        client = FoundryChatClient(
            project_endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
            model=os.environ["FOUNDRY_MODEL"],
            credential=credential,
        )

        # Automatically configures Azure Monitor with the connection string from the Foundry project
        await client.configure_azure_monitor(enable_live_metrics=True)
```

> [!TIP]
> The arguments for `client.configure_azure_monitor()` are passed through to the underlying `configure_azure_monitor()` function from the `azure-monitor-opentelemetry` package, see [documentation](/python/api/overview/azure/monitor-opentelemetry-readme#usage) for details, we take care of setting the connection string and resource.

#### Configure azure monitor and optionally enable instrumentation

For non-Foundry projects with Application Insights, make sure you setup a custom agent in Foundry, see [details](/azure/ai-foundry/control-plane/register-custom-agent).

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
agent = Agent(
    client=...,
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

Or include them in your `.env` file and ensure you call `load_dotenv()` at the start of your application (Agent Framework does not automatically load `.env` files).

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

There are a number of samples in the `microsoft/agent-framework` repository that demonstrate these capabilities. For more information, see the [observability samples folder](https://github.com/microsoft/agent-framework/tree/main/python/samples/02-agents/observability). That folder includes samples for using zero-code telemetry as well.

### Complete example

```python
# Copyright (c) Microsoft. All rights reserved.

import asyncio
from random import randint
from typing import Annotated

from agent_framework import Agent, tool
from agent_framework.observability import configure_otel_providers, get_tracer
from agent_framework.openai import OpenAIChatClient
from opentelemetry.trace import SpanKind
from opentelemetry.trace.span import format_trace_id
from pydantic import Field

"""
This sample shows how you can observe an agent in Agent Framework by using the
same observability setup function.
"""


# NOTE: approval_mode="never_require" is for sample brevity. Use "always_require" in production; see samples/02-agents/tools/function_tool_with_approval.py and samples/02-agents/tools/function_tool_with_approval_and_sessions.py.
@tool(approval_mode="never_require")
async def get_weather(
    location: Annotated[str, Field(description="The location to get the weather for.")],
) -> str:
    """Get the weather for a given location."""
    await asyncio.sleep(randint(0, 10) / 10.0)  # Simulate a network call
    conditions = ["sunny", "cloudy", "rainy", "stormy"]
    return f"The weather in {location} is {conditions[randint(0, 3)]} with a high of {randint(10, 30)}°C."


async def main():
    # calling `configure_otel_providers` will *enable* tracing and create the necessary tracing, logging
    # and metrics providers based on environment variables.
    # See the .env.example file for the available configuration options.
    configure_otel_providers()

    questions = ["What's the weather in Amsterdam?", "and in Paris, and which is better?", "Why is the sky blue?"]

    with get_tracer().start_as_current_span("Scenario: Agent Chat", kind=SpanKind.CLIENT) as current_span:
        print(f"Trace ID: {format_trace_id(current_span.get_span_context().trace_id)}")

        agent = Agent(
            client=OpenAIChatClient(),
            tools=get_weather,
            name="WeatherAgent",
            instructions="You are a weather assistant.",
            id="weather-agent",
        )
        thread = agent.create_session()
        for question in questions:
            print(f"\nUser: {question}")
            print(f"{agent.name}: ", end="")
            async for update in agent.run(
                question,
                session=thread,
                stream=True,
            ):
                if update.text:
                    print(update.text, end="")


if __name__ == "__main__":
    asyncio.run(main())
```

::: zone-end

::: zone pivot="programming-language-go"
## Observability with OpenTelemetry

The Go Agent Framework includes an OpenTelemetry middleware that automatically traces agent invocations.

### Setup

```go
import (
    "github.com/microsoft/agent-framework-go/provider/otelprovider"

    "go.opentelemetry.io/otel/exporters/stdout/stdouttrace"
    sdktrace "go.opentelemetry.io/otel/sdk/trace"
    otellib "go.opentelemetry.io/otel"
)

// Create a tracer provider with a console exporter
exporter, _ := stdouttrace.New(stdouttrace.WithPrettyPrint())
tp := sdktrace.NewTracerProvider(sdktrace.WithBatcher(exporter))
defer tp.Shutdown(context.Background())
otellib.SetTracerProvider(tp)
```

### Add the middleware to your agent

```go
a := foundryprovider.NewAgent(endpoint, token, foundryprovider.ModelDeployment(model), foundryprovider.AgentConfig{
    Instructions: "You are a helpful assistant.",
    Config: agent.Config{
        Middlewares: []agent.Middleware{
            otelprovider.NewMiddleware(otelprovider.MiddlewareConfig{}), // OpenTelemetry tracing
        },
    },
})
```

The middleware emits spans with attributes including:

- `gen_ai.provider.name` — The provider name (e.g., "openai")
- `gen_ai.agent.id` — The agent's unique ID
- `gen_ai.agent.name` — The agent's display name
- `gen_ai.agent.description` — The agent's description

> [!TIP]
> See the [full sample](https://github.com/microsoft/agent-framework-go/blob/main/examples/02-agents/agents/step08_observability/main.go) for a complete runnable example.

::: zone-end

<a id="use-observability-with-harnessed-agent"></a>

## Use observability with Harness Agent

::: zone pivot="programming-language-csharp"

For a plain agent, add OpenTelemetry to the chat-client or agent pipeline with `UseOpenTelemetry` or `WithOpenTelemetry`, as shown earlier. A `HarnessAgent` adds both chat-client and agent OpenTelemetry instrumentation by default:

```csharp
using Microsoft.Agents.AI;
using OpenTelemetry;
using OpenTelemetry.Trace;

const string SourceName = "MyApplication.Harness";

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource(SourceName)
    .AddOtlpExporter()
    .Build();

AIAgent agent = chatClient.AsHarnessAgent(new HarnessAgentOptions
{
    OpenTelemetrySourceName = SourceName,
});
```

`OpenTelemetrySourceName` defaults to `Experimental.Microsoft.Agents.AI`. The name passed to `AddSource` must match it. Set `DisableOpenTelemetry = true` to omit both Harness-added instrumentation layers.

The Harness configures instrumentation, but you still own the `TracerProvider`, exporters, credentials, flushing, and shutdown. Don't pre-instrument the same chat client and then leave Harness instrumentation enabled unless you intentionally want duplicate spans.

Telemetry contains metadata by default. Setting `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT=true` also records prompts, responses, tool arguments, and tool results; only enable it when the exporter and retention policy are appropriate for that data.

`HarnessAgent` is available from the `Microsoft.Agents.AI.Harness` package.

::: zone-end

::: zone pivot="programming-language-python"

Plain `Agent` instances already include the telemetry layer; configure OpenTelemetry providers and exporters with `configure_otel_providers()` or your own OpenTelemetry SDK setup. `create_harness_agent` uses the same global configuration and assigns a Harness-specific provider name:

```python
from agent_framework import create_harness_agent
from agent_framework.observability import configure_otel_providers

configure_otel_providers()

agent = create_harness_agent(
    client=client,
    otel_provider_name="my.application.harness",
)
```

`otel_provider_name` controls the provider name recorded on Harness telemetry. It defaults to `microsoft.agent_framework.harness`; it doesn't configure an exporter or telemetry destination. Instrumentation is enabled by default, sensitive-data capture is disabled by default, and no exporter is installed or configured automatically.

OpenTelemetry providers are process-wide resources. Configure them once, secure exporter credentials and endpoints, and flush or shut them down according to the OpenTelemetry SDK and exporter you selected. Set `ENABLE_INSTRUMENTATION=false` or call `disable_instrumentation()` when telemetry must be disabled. Enabling `ENABLE_SENSITIVE_DATA` adds raw messages, tool arguments, and tool results.

`create_harness_agent` is released in `agent-framework-core`.

::: zone-end

::: zone pivot="programming-language-go"

A packaged Go Harness isn't currently available. Configure the OpenTelemetry middleware directly on a plain Go agent as shown earlier.

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Agent Skills](skills.md)
