---
title: Agent capabilities
description: Browse built-in Agent Framework capabilities for multimodal input, tools, retrieval, evaluation, security, and autonomous execution.
author: eavanvalkenburg
ms.topic: overview
ms.author: edvan
ms.date: 07/30/2026
ms.service: agent-framework
---

# Agent capabilities

Agent capabilities are opt-in features that extend what an agent can understand, produce, retrieve, execute, observe, or secure. For the runtime, type, conversation, and middleware foundations behind these features, see [Agents](../concepts/agents/index.md).

## Agent types and model connections

Looking for the agent-type and SDK-selection guidance previously hosted on this page?

- [Agent concepts](../concepts/agents/index.md#chat-client-agents) explains application-owned chat-client agents, custom agents, and remote agent types.
- [Model providers](../integrations/by-component/model-providers/index.md) compares inference providers, conversation-history support, and .NET SDK and endpoint options.
- [Agent services](../integrations/by-component/agent-services/index.md) covers managed and protocol-backed remote agent runtimes.

## Input and output

| Capability | Purpose |
|---|---|
| [Multimodal](multimodal.md) | Send images and other supported content to an agent. |
| [Structured outputs](structured-outputs.md) | Return values that conform to a schema or application type. |
| [Background responses](background-responses.md) | Continue, poll, and reconnect to long-running responses. |

## Context and knowledge

| Capability | Purpose |
|---|---|
| [RAG](rag.md) | Ground responses with retrieved application knowledge. |
| [Declarative agents](declarative.md) | Define supported agents through YAML or JSON. |
| [Agent Skills](skills.md) | Discover and progressively load reusable instructions, resources, and scripts. |

## Execution and autonomy

| Capability | Purpose |
|---|---|
| [Tools](tools/index.md) | Let agents call functions and provider-hosted capabilities. |
| [CodeAct](code_act.md) | Let the model write programs that coordinate tools through a managed execution provider. |
| [Looping](looping.md) | Re-run an agent until a bounded completion condition is met. |
| [Background agents](background-agents.md) | Delegate work to background agents and retrieve task results. |
| [Planning and todos](planning-and-todos.md) | Track plans, operational todos, dependencies, and completion. |

## Operations and trust

| Capability | Purpose |
|---|---|
| [Observability](observability.md) | Export traces, metrics, and logs. |
| [Evaluation](evaluation.md) | Measure agent quality, safety, and correctness. |
| [Agent Security with FIDES](security.md) | Enforce information-flow controls across agent data and tools. |

The [Harness Agent](../concepts/harness.md) assembles many of these capabilities into an opinionated operational agent.

## Next steps

> [!div class="nextstepaction"]
> [Add tools to an agent](tools/index.md)
