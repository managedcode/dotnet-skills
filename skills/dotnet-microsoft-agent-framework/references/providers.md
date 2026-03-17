# Providers, SDKs, and Endpoint Choices

## Choose The Runtime Shape Before The SDK

The provider decision has three layers:

1. Which runtime shape do you need:
   - `ChatClientAgent`
   - hosted agent
   - Responses-based agent
   - Chat Completions-based agent
2. Which state model do you need:
   - local history
   - service-managed history
   - custom chat store
3. Which SDK and endpoint best match that runtime shape

If you start from the SDK alone, you usually miss the thread and hosting consequences.

## Default Recommendations

- Prefer `ChatClientAgent` when you want the broadest `.NET` composition model.
- Prefer Responses-based agents for new OpenAI-compatible integrations.
- Prefer Chat Completions only when compatibility or simplicity beats richer server-side behavior.
- Prefer hosted agents only when managed service resources, managed tools, or managed thread storage are actual requirements.
- Prefer the OpenAI SDK where the official docs say it is a viable fit across OpenAI-style services.

## Provider Matrix

| Backend | Typical `.NET` Shape | History Support | Best For | Main Watchout |
| --- | --- | --- | --- | --- |
| Any `IChatClient` | `new ChatClientAgent(chatClient, ...)` or `chatClient.AsAIAgent(...)` | Depends on provider | Broadest integration surface | Tooling and history are only as good as the concrete client |
| Azure OpenAI Chat Completions | `AzureOpenAIClient(...).GetChatClient(...).AsAIAgent(...)` | Local or custom store | Simple chat flows | You own conversation persistence |
| Azure OpenAI Responses | `AzureOpenAIClient(...).GetOpenAIResponseClient(...).AsAIAgent(...)` | Service-backed or local, depending on mode | New OpenAI-style apps | Preview packages and mode-specific behavior |
| OpenAI Chat Completions | `OpenAIClient(...).GetChatClient(...).AsAIAgent(...)` | Local or custom store | Straightforward request/response chat | No service-backed history by default |
| OpenAI Responses | `OpenAIClient(...).GetOpenAIResponseClient(...).AsAIAgent(...)` | Service-backed or local, depending on mode | Long-running or richer response flows | Requires discipline about state mode |
| Azure AI Foundry Agents | `PersistentAgentsClient.CreateAIAgentAsync(...)` | Service-stored only | Managed agent resources and managed tools | Lower portability and provider-specific lifecycle |
| OpenAI Assistants | provider-specific assistant client `CreateAIAgentAsync(...)` | Service-stored only | Existing assistant workloads | Not the forward-looking default |
| A2A proxy agent | A2A client/proxy agent | Remote service-managed | Calling remote agents | Not a model provider choice |

## Service History Support

The official C# docs make these differences explicit:

| Service | Service History | Custom History |
| --- | --- | --- |
| Azure AI Foundry Agents | Yes | No |
| Azure AI Foundry Models Chat Completions | No | Yes |
| Azure AI Foundry Models Responses | No | Yes |
| Azure OpenAI Chat Completions | No | Yes |
| Azure OpenAI Responses | Yes | Yes |
| OpenAI Chat Completions | No | Yes |
| OpenAI Responses | Yes | Yes |
| OpenAI Assistants | Yes | No |
| Other `IChatClient` implementations | Varies | Varies |

This table matters more than it looks. It decides whether your `AgentThread` stores full messages, a remote conversation ID, or custom serialized store state.

## SDK And Endpoint Matrix

| AI Service | SDK | Package | URL Pattern |
| --- | --- | --- | --- |
| Azure AI Foundry Models | OpenAI SDK | `OpenAI` | `https://ai-foundry-<resource>.services.ai.azure.com/openai/v1/` |
| Azure AI Foundry Models | Azure OpenAI SDK | `Azure.AI.OpenAI` | `https://ai-foundry-<resource>.services.ai.azure.com/` |
| Azure AI Foundry Models | Azure AI Inference SDK | `Azure.AI.Inference` | `https://ai-foundry-<resource>.services.ai.azure.com/models` |
| Azure AI Foundry Agents | Persistent Agents SDK | `Azure.AI.Agents.Persistent` | `https://ai-foundry-<resource>.services.ai.azure.com/api/projects/ai-project-<project>` |
| Azure OpenAI | Azure OpenAI SDK | `Azure.AI.OpenAI` | `https://<resource>.openai.azure.com/` |
| Azure OpenAI | OpenAI SDK | `OpenAI` | `https://<resource>.openai.azure.com/openai/v1/` |
| OpenAI | OpenAI SDK | `OpenAI` | default OpenAI endpoint |

## OpenAI SDK Versus Azure OpenAI SDK

Use the OpenAI SDK when:

- you want one client model across OpenAI-style services
- you want to target OpenAI and Azure/OpenAI-style services with similar composition
- the official docs already show the OpenAI SDK path as first-class

Use the Azure OpenAI SDK when:

- the repo already standardizes on Azure SDK clients
- you need Azure SDK-specific ergonomics or auth integration
- the service example you follow is already written that way

The important point is consistency inside the app, not ideological loyalty to one SDK.

## Responses Versus Chat Completions

Choose Responses when:

- you are building something new
- server-side conversation or response-chain tracking helps
- you need richer eventing
- background responses or long-running operations matter
- you plan to expose OpenAI-compatible endpoints from your app

Choose Chat Completions when:

- you are migrating an existing client contract
- your app already owns state explicitly
- you want the simplest request/response model
- ecosystem compatibility is more important than richer semantics

## Hosted Agents Versus `ChatClientAgent`

Choose a hosted agent when:

- the managed service gives you capabilities you actually need
- service-owned tools or thread storage are a feature, not an inconvenience
- operational ownership belongs in the provider

Choose `ChatClientAgent` when:

- your application wants to own composition, DI, middleware, and policies
- portability matters
- you want one consistent abstraction over multiple model providers

## Local Models And Custom Clients

`ChatClientAgent` is also the correct escape hatch for:

- Ollama-backed clients
- custom `IChatClient` adapters
- future provider integrations that expose the `Microsoft.Extensions.AI` surface

Before you commit to a local or custom model path, verify:

- function calling actually works
- multimodal content is truly supported
- response streaming behaves the way your UI expects
- you understand whether history is local only

## Provider Selection Checklist

- Which service owns conversation state?
- Does the service support the tools you plan to expose?
- Are you choosing Responses or Chat Completions deliberately?
- Is the required SDK stable enough for the repo's risk tolerance?
- Does the endpoint format match the chosen SDK?
- Do you need service-managed agent resources or only inference?

## Source Pages

- `references/official-docs/user-guide/agents/agent-types/index.md`
- `references/official-docs/user-guide/agents/agent-types/chat-client-agent.md`
- `references/official-docs/user-guide/agents/agent-types/azure-openai-chat-completion-agent.md`
- `references/official-docs/user-guide/agents/agent-types/azure-openai-responses-agent.md`
- `references/official-docs/user-guide/agents/agent-types/openai-chat-completion-agent.md`
- `references/official-docs/user-guide/agents/agent-types/openai-responses-agent.md`
- `references/official-docs/user-guide/agents/agent-types/microsoft-foundry-agents.md`
