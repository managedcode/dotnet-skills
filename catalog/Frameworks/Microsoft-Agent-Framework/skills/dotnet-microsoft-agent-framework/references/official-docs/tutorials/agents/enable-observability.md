---
title: Enabling observability for Agents
description: Enable OpenTelemetry for an agent so agent interactions are automatically logged
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: tutorial
ms.author: westey
ms.date: 09/18/2025
ms.service: agent-framework
---

# Enabling observability for Agents

::: zone pivot="programming-language-csharp"

This tutorial shows how to enable OpenTelemetry on an agent so that interactions with the agent are automatically logged and exported.
In this tutorial, output is written to the console using the OpenTelemetry console exporter.

> [!NOTE]
> For more information about the standards followed by Microsoft Agent Framework, see [Semantic Conventions for GenAI agent and framework spans](https://opentelemetry.io/docs/specs/semconv/gen-ai/gen-ai-agent-spans/) from Open Telemetry.

## Prerequisites

For prerequisites, see the [Create and run a simple agent](./run-agent.md#prerequisites) step in this tutorial.

## Install NuGet packages

To use Microsoft Agent Framework with Azure OpenAI, you need to install the following NuGet packages:

```dotnetcli
dotnet add package Azure.AI.OpenAI --prerelease
dotnet add package Azure.Identity
dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
```

To also add OpenTelemetry support, with support for writing to the console, install these additional packages:

```dotnetcli
dotnet add package OpenTelemetry
dotnet add package OpenTelemetry.Exporter.Console
```

## Enable OpenTelemetry in your app

Enable Agent Framework telemetry and create an OpenTelemetry `TracerProvider` that exports to the console.
The `TracerProvider` must remain alive while you run the agent so traces are exported.

```csharp
using System;
using OpenTelemetry;
using OpenTelemetry.Trace;

// Create a TracerProvider that exports to the console
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("agent-telemetry-source")
    .AddConsoleExporter()
    .Build();
```

## Create and instrument the agent

Create an agent, and using the builder pattern, call `UseOpenTelemetry` to provide a source name.
Note that the string literal `agent-telemetry-source` is the OpenTelemetry source name
that you used when you created the tracer provider.

```csharp
using System;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using OpenAI;

// Create the agent and enable OpenTelemetry instrumentation
AIAgent agent = new AzureOpenAIClient(
    new Uri("https://<myresource>.openai.azure.com"),
    new AzureCliCredential())
        .GetChatClient("gpt-4o-mini")
        .AsAIAgent(instructions: "You are good at telling jokes.", name: "Joker")
        .AsBuilder()
        .UseOpenTelemetry(sourceName: "agent-telemetry-source")
        .Build();
```

Run the agent and print the text response. The console exporter will show trace data on the console.

```csharp
Console.WriteLine(await agent.RunAsync("Tell me a joke about a pirate."));
```

The expected output will be something like this, where the agent invocation trace is shown first, followed by the text response from the agent.

```powershell
Activity.TraceId:            f2258b51421fe9cf4c0bd428c87b1ae4
Activity.SpanId:             2cad6fc139dcf01d
Activity.TraceFlags:         Recorded
Activity.DisplayName:        invoke_agent Joker
Activity.Kind:               Client
Activity.StartTime:          2025-09-18T11:00:48.6636883Z
Activity.Duration:           00:00:08.6077009
Activity.Tags:
    gen_ai.operation.name: chat
    gen_ai.request.model: gpt-4o-mini
    gen_ai.provider.name: openai
    server.address: <myresource>.openai.azure.com
    server.port: 443
    gen_ai.agent.id: 19e310a72fba4cc0b257b4bb8921f0c7
    gen_ai.agent.name: Joker
    gen_ai.response.finish_reasons: ["stop"]
    gen_ai.response.id: chatcmpl-CH6fgKwMRGDtGNO3H88gA3AG2o7c5
    gen_ai.response.model: gpt-4o-mini-2024-07-18
    gen_ai.usage.input_tokens: 26
    gen_ai.usage.output_tokens: 29
Instrumentation scope (ActivitySource):
    Name: agent-telemetry-source
Resource associated with Activity:
    telemetry.sdk.name: opentelemetry
    telemetry.sdk.language: dotnet
    telemetry.sdk.version: 1.13.1
    service.name: unknown_service:Agent_Step08_Telemetry

Why did the pirate go to school?

Because he wanted to improve his "arrr-ticulation"! ?????
```

## Next steps

> [!div class="nextstepaction"]
> [Persisting conversations](./persisted-conversation.md)

::: zone-end
::: zone pivot="programming-language-python"

This tutorial shows how to quickly enable OpenTelemetry on an agent so that interactions with the agent are automatically logged and exported.

For comprehensive documentation on observability including all configuration options, environment variables, and advanced scenarios, see the [Observability user guide](../../user-guide/observability.md).

## Prerequisites

For prerequisites, see the [Create and run a simple agent](./run-agent.md) step in this tutorial.

## Install packages

To use Agent Framework with OpenTelemetry, install the framework:

```bash
pip install agent-framework --pre
```

For console output during development, no additional packages are needed. For other exporters, see the [Dependencies section](../../user-guide/observability.md#dependencies) in the user guide.

## Enable OpenTelemetry in your app

The simplest way to enable observability is using `configure_otel_providers()`:

```python
from agent_framework.observability import configure_otel_providers

# Enable console output for local development
configure_otel_providers(enable_console_exporters=True)
```

Or use environment variables for more flexibility:

```bash
export ENABLE_INSTRUMENTATION=true
export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
```

```python
from agent_framework.observability import configure_otel_providers

# Reads OTEL_EXPORTER_OTLP_* environment variables automatically
configure_otel_providers()
```

## Create and run the agent

Create an agent using Agent Framework. Observability is automatically enabled once `configure_otel_providers()` has been called.

```python
from agent_framework import ChatAgent
from agent_framework.openai import OpenAIChatClient

# Create the agent - telemetry is automatically enabled
agent = ChatAgent(
    chat_client=OpenAIChatClient(),
    name="Joker",
    instructions="You are good at telling jokes."
)

# Run the agent
result = await agent.run("Tell me a joke about a pirate.")
print(result.text)
```

The console exporter will show trace data similar to:

```text
{
    "name": "invoke_agent Joker",
    "context": {
        "trace_id": "0xf2258b51421fe9cf4c0bd428c87b1ae4",
        "span_id": "0x2cad6fc139dcf01d"
    },
    "attributes": {
        "gen_ai.operation.name": "invoke_agent",
        "gen_ai.agent.name": "Joker",
        "gen_ai.usage.input_tokens": 26,
        "gen_ai.usage.output_tokens": 29
    }
}
```

## Microsoft Foundry integration

If you're using Microsoft Foundry, there's a convenient method that automatically configures Azure Monitor with Application Insights. First ensure your Foundry project has Azure Monitor configured (see [Monitor applications](/azure/ai-foundry/how-to/monitor-applications)).

```bash
pip install azure-monitor-opentelemetry
```

```python
from agent_framework.azure import AzureAIClient
from azure.ai.projects.aio import AIProjectClient
from azure.identity.aio import AzureCliCredential

async with (
    AzureCliCredential() as credential,
    AIProjectClient(endpoint="https://<your-project>.foundry.azure.com", credential=credential) as project_client,
    AzureAIClient(project_client=project_client) as client,
):
    # Automatically configures Azure Monitor with connection string from project
    await client.configure_azure_monitor(enable_live_metrics=True)
```

### Custom agents with Foundry observability

For custom agents not created through Foundry, you can register them in the Foundry portal and use the same OpenTelemetry agent ID. See [Register custom agent](/azure/ai-foundry/control-plane/register-custom-agent) for setup instructions.

```python
from azure.monitor.opentelemetry import configure_azure_monitor
from agent_framework import ChatAgent
from agent_framework.observability import create_resource, enable_instrumentation
from agent_framework.openai import OpenAIChatClient

# Configure Azure Monitor
configure_azure_monitor(
    connection_string="InstrumentationKey=...",
    resource=create_resource(),
    enable_live_metrics=True,
)
# Optional if ENABLE_INSTRUMENTATION is already set in env vars
enable_instrumentation()

# Create your agent with the same OpenTelemetry agent ID as registered in Foundry
agent = ChatAgent(
    chat_client=OpenAIChatClient(),
    name="My Agent",
    instructions="You are a helpful assistant.",
    id="<OpenTelemetry agent ID>"  # Must match the ID registered in Foundry
)
# Use the agent as normal
```

> [!TIP]
> For more detailed setup instructions, see the [Microsoft Foundry setup](../../user-guide/observability.md#microsoft-foundry-setup) section in the user guide.

## Next steps

For more advanced observability scenarios including custom exporters, third-party integrations (Langfuse, etc.), Aspire Dashboard setup, and detailed span/metric documentation, see the [Observability user guide](../../user-guide/observability.md).

> [!div class="nextstepaction"]
> [Persisting conversations](./persisted-conversation.md)

::: zone-end
