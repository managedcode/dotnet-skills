---
title: Model providers
description: Compare model inference providers available to Agent Framework applications.
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: reference
ms.author: edvan
ms.date: 07/30/2026
ms.service: agent-framework
---

<!--
  Language parity table - keep in sync when adding/removing sections.

  | Section               | C# | Python | Go | Notes                          |
  |-----------------------|:--:|:------:|:--:|--------------------------------|
  | Provider comparison   | ✅ |   ✅   | ✅ | Shared                         |
  | Available providers   | ✅ |   ✅   | ✅ | Availability differs by SDK    |
  | Conversation history  | ✅ |   ❌   | ❌ | .NET connection comparison     |
  | SDK and endpoints     | ✅ |   ❌   | ❌ | .NET reference                 |
  | Provider construction | ❌ |   ❌   | ✅ | Go constructor example         |
-->

# Model providers

Model providers supply the inference client used by an Agent Framework agent. Your application owns the agent definition, instructions, tools, middleware, and session policy while the provider supplies model inference and provider-hosted capabilities.

For remote or managed runtimes that own an agent definition, permissions, or service-side execution, see [Agent Services](../agent-services/index.md). For custom framework agent implementations, see [Custom agents](../../../concepts/agents/custom-agents.md).

## Provider comparison

| Provider | Function Tools | Structured Outputs | Code Interpreter | File Search | MCP Tools | Background Responses |
|----------|:---:|:---:|:---:|:---:|:---:|:---:|
| [Azure OpenAI](./azure-openai.md) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| [OpenAI](./openai.md) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| [Microsoft Foundry](./microsoft-foundry.md) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| [Anthropic](./anthropic.md) | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ |
| [Ollama](./ollama.md) | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| [Foundry Local](./foundry-local.md) | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| [Amazon Bedrock](./amazon-bedrock.md) | ✅ | Varies | ❌ | ❌ | ❌ | ❌ |
| [Google Gemini](./google-gemini.md) | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ |
| [ONNX](./onnx.md) | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| [Dapr](./dapr.md) | Varies | Varies | Varies | Varies | Varies | Varies |
| [Mistral](./mistral.md) | N/A | N/A | N/A | N/A | N/A | N/A |

> [!IMPORTANT]
> If you use Microsoft Agent Framework to build applications that operate with any third-party servers, agents, code, or non-Azure Direct models ("Third-Party Systems"), you do so at your own risk. Third-Party Systems are Non-Microsoft Products under the Microsoft Product Terms and are governed by their own third-party license terms. You are responsible for any usage and associated costs.
>
> We recommend reviewing all data being shared with and received from Third-Party Systems and being cognizant of third-party practices for handling, sharing, retention and location of data. It is your responsibility to manage whether your data will flow outside of your organization's Azure compliance and geographic boundaries and any related implications, and that appropriate permissions, boundaries and approvals are provisioned.
>
> You are responsible for carefully reviewing and testing applications you build using Microsoft Agent Framework in the context of your specific use cases, and making all appropriate decisions and customizations. This includes implementing your own responsible AI mitigations such as metaprompt, content filters, or other safety systems, and ensuring your applications meet appropriate quality, reliability, security, and trustworthiness standards. See also: [Transparency FAQ](https://github.com/microsoft/agent-framework/blob/main/TRANSPARENCY_FAQS.md)

:::zone pivot="programming-language-csharp"

## Available providers

Any inference service that provides a `Microsoft.Extensions.AI.IChatClient` implementation can back a `ChatClientAgent`.

- **[Azure OpenAI](./azure-openai.md)** — Azure-hosted OpenAI inference with Azure identity support.
- **[OpenAI](./openai.md)** — OpenAI Chat Completions and Responses APIs.
- **[Microsoft Foundry](./microsoft-foundry.md)** — Model inference through a Microsoft Foundry project.
- **[Anthropic](./anthropic.md)** — Claude model inference through Anthropic and supported hosted endpoints.
- **[Ollama](./ollama.md)** — Local open-source model inference.
- **[Amazon Bedrock](./amazon-bedrock.md)** — AWS-managed foundation model inference.
- **[Google Gemini](./google-gemini.md)** — Gemini Developer API or Vertex AI inference.
- **[ONNX](./onnx.md)** — Local ONNX Runtime GenAI inference.
- **[Dapr](./dapr.md)** — Inference routed through the Dapr Conversation building block.

### Conversation history support

The selected API determines whether the remote service can own conversation history and whether the agent can instead use an in-memory or custom `ChatHistoryProvider`.

| Agent connection | Service-managed history | In-memory or custom history |
|---|:---:|:---:|
| [Microsoft Foundry Prompt or Hosted Agent](../agent-services/foundry.md) | ✅ | ❌ |
| [Microsoft Foundry Responses](./microsoft-foundry.md) | ✅ | ✅ |
| [Azure OpenAI Responses](./azure-openai.md) | ✅ | ✅ |
| [Azure OpenAI Chat Completions](./azure-openai.md) | ❌ | ✅ |
| [OpenAI Responses](./openai.md) | ✅ | ✅ |
| [OpenAI Chat Completions](./openai.md) | ❌ | ✅ |
| [Anthropic](./anthropic.md) | ❌ | ✅ |
| Any other `IChatClient` | Varies | Varies |

Service-managed history availability can also depend on the selected service options. See the provider page for configuration details.

### SDK and endpoint selection

Several .NET SDKs can connect to Microsoft Foundry, Azure OpenAI, OpenAI, or Anthropic. Choose the SDK that matches the service endpoint and authentication model.

| AI service | Client SDK | NuGet packages | Endpoint or identifier |
|---|---|---|---|
| Microsoft Foundry project | Azure AI Projects | `Azure.AI.Projects`, `Microsoft.Agents.AI.Foundry` | `https://<resource>.services.ai.azure.com/api/projects/<project>` |
| Microsoft Foundry Models through OpenAI v1 | OpenAI | `OpenAI`, `Microsoft.Agents.AI.OpenAI` | `https://<resource>.services.ai.azure.com/openai/v1/` |
| Azure OpenAI | Azure OpenAI | `Azure.AI.OpenAI`, `Microsoft.Agents.AI.OpenAI` | `https://<resource>.openai.azure.com/` |
| Azure OpenAI through OpenAI v1 | OpenAI | `OpenAI`, `Microsoft.Agents.AI.OpenAI` | `https://<resource>.openai.azure.com/openai/v1/` |
| OpenAI | OpenAI | `OpenAI`, `Microsoft.Agents.AI.OpenAI` | Default OpenAI endpoint |
| Anthropic on Microsoft Foundry | Anthropic Foundry | `Anthropic.Foundry`, `Microsoft.Agents.AI.Anthropic` | Foundry resource name |
| Anthropic | Anthropic | `Anthropic`, `Microsoft.Agents.AI.Anthropic` | Default Anthropic endpoint |

Use the [Microsoft Foundry](./microsoft-foundry.md), [Azure OpenAI](./azure-openai.md), [OpenAI](./openai.md), or [Anthropic](./anthropic.md) page for client construction and authentication examples.

:::zone-end

:::zone pivot="programming-language-python"

## Available providers

Agent Framework Python exposes provider-specific chat clients behind the common agent interface.

- **[Azure OpenAI](./azure-openai.md)** — Azure-hosted OpenAI inference with Azure identity support.
- **[OpenAI](./openai.md)** — OpenAI Chat Completions and Responses APIs.
- **[Microsoft Foundry](./microsoft-foundry.md)** — Model inference through a Microsoft Foundry project.
- **[Foundry Local](./foundry-local.md)** — Run supported Foundry models locally.
- **[Anthropic](./anthropic.md)** — Claude inference through Anthropic, Foundry, Amazon Bedrock, or Vertex AI.
- **[Ollama](./ollama.md)** — Local open-source model inference.
- **[Amazon Bedrock](./amazon-bedrock.md)** — AWS-managed foundation model inference.
- **[Google Gemini](./google-gemini.md)** — Gemini Developer API or Vertex AI inference.
- **[Mistral](./mistral.md)** — Mistral AI embedding generation.

:::zone-end

:::zone pivot="programming-language-go"

## Available providers

The Go SDK creates a standard `*agent.Agent` through provider-specific constructors.

| Provider | Package | Import Path |
|---|---|---|
| Microsoft Foundry | `foundryprovider` | `github.com/microsoft/agent-framework-go/provider/foundryprovider` |
| OpenAI Chat Completions | `openaiprovider` | `github.com/microsoft/agent-framework-go/provider/openaiprovider` |
| OpenAI Responses | `openaiprovider` | `github.com/microsoft/agent-framework-go/provider/openaiprovider` |
| Anthropic | `anthropicprovider` | `github.com/microsoft/agent-framework-go/provider/anthropicprovider` |
| Google Gemini | `geminiprovider` | `github.com/microsoft/agent-framework-go/provider/geminiprovider` |

For example, create a Foundry-backed agent from a project endpoint, credential, and model deployment:

```go
a := foundryprovider.NewAgent(endpoint, token, foundryprovider.ModelDeployment(model), foundryprovider.AgentConfig{
    Instructions: "You are a helpful assistant.",
    Config: agent.Config{
        Name: "MyAgent",
    },
})
```

:::zone-end

## Next steps

> [!div class="nextstepaction"]
> [Azure OpenAI](./azure-openai.md)
