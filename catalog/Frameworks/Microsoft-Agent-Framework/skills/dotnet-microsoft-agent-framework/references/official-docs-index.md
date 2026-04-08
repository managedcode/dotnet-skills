# Official Docs Snapshot

Use this reference when the summarized guidance in the skill is not enough and you need the actual Microsoft Learn markdown pages that informed the skill.

The local snapshot lives under `references/official-docs/`.

## Scope

- Mirrored authored docs: `100` markdown pages across overview, tutorials, user guide, integrations, migration, and support
- Live-only Learn pages added into the mirror: `support/faq.md`, `support/troubleshooting.md`, and `support/upgrade/index.md`
- Generated API references are not mirrored page-by-page; use the live `.NET` API landing page when exact symbols matter
- Intentional exclusions: media files, TOC scaffolding, breadcrumb files, DocFX support files, and Python-only upgrade pages are not mirrored into the skill

## Section Map

| Section | Count | Start Here | Covers |
| --- | --- | --- | --- |
| Overview | 1 | `official-docs/overview/agent-framework-overview.md` | Top-level framing, preview state, and agent-vs-workflow guidance |
| Tutorials | 25 | `official-docs/tutorials/overview.md` | Quick start, agents, workflows, durable agents, middleware, memory, Purview |
| User Guide | 61 | `official-docs/user-guide/overview.md` | Agent types, threads, tools, MCP, workflows, hosting, DevUI, observability |
| Integrations | 8 | `official-docs/integrations/ag-ui/index.md` | AG-UI architecture, state sync, approvals, security, and testing |
| Migration | 3 | `official-docs/migration-guide/from-semantic-kernel/index.md` | Migration from Semantic Kernel and AutoGen |
| Support | 4 | `official-docs/support/index.md` | Support entry points, FAQ, troubleshooting, and the upgrade hub |

## High-Value Entry Points

- Agent types: `official-docs/user-guide/agents/agent-types/index.md`
- Azure provider pages: `official-docs/user-guide/agents/agent-types/microsoft-foundry-agents.md`, `official-docs/user-guide/agents/agent-types/azure-openai-chat-completion-agent.md`, and `official-docs/user-guide/agents/agent-types/azure-openai-responses-agent.md`
- Running agents and conversations: `official-docs/user-guide/agents/running-agents.md`
- Tools: `official-docs/user-guide/agents/agent-tools.md`
- Middleware, memory, and RAG: `official-docs/user-guide/agents/agent-middleware.md`, `official-docs/user-guide/agents/agent-memory.md`, and `official-docs/user-guide/agents/agent-rag.md`
- MCP: `official-docs/user-guide/model-context-protocol/index.md`
- Workflow overview: `official-docs/user-guide/workflows/overview.md`
- Workflow core concepts: `official-docs/user-guide/workflows/core-concepts/overview.md`
- Workflow orchestrations: `official-docs/user-guide/workflows/orchestrations/overview.md`
- Declarative workflows: `official-docs/user-guide/workflows/declarative-workflows.md`
- Hosting and remote protocols: `official-docs/user-guide/hosting/index.md`
- A2A hosting: `official-docs/user-guide/hosting/agent-to-agent-integration.md`
- OpenAI-compatible hosting: `official-docs/user-guide/hosting/openai-integration.md`
- DevUI: `official-docs/user-guide/devui/index.md`
- AG-UI: `official-docs/integrations/ag-ui/index.md`
- Support FAQ: `official-docs/support/faq.md`
- Upgrade hub: `official-docs/support/upgrade/index.md`

## Complete Local File Map

### Overview

- [`official-docs/overview/agent-framework-overview.md`](official-docs/overview/agent-framework-overview.md)

### Tutorials

- [`official-docs/tutorials/overview.md`](official-docs/tutorials/overview.md)
- [`official-docs/tutorials/quick-start.md`](official-docs/tutorials/quick-start.md)

### Tutorials / Agents

- [`official-docs/tutorials/agents/agent-as-function-tool.md`](official-docs/tutorials/agents/agent-as-function-tool.md) — Redirect alias retained locally because the live Learn URL now resolves into the broader Function Tools surface
- [`official-docs/tutorials/agents/agent-as-mcp-tool.md`](official-docs/tutorials/agents/agent-as-mcp-tool.md)
- [`official-docs/tutorials/agents/create-and-run-durable-agent.md`](official-docs/tutorials/agents/create-and-run-durable-agent.md)
- [`official-docs/tutorials/agents/enable-observability.md`](official-docs/tutorials/agents/enable-observability.md)
- [`official-docs/tutorials/agents/function-tools-approvals.md`](official-docs/tutorials/agents/function-tools-approvals.md)
- [`official-docs/tutorials/agents/function-tools.md`](official-docs/tutorials/agents/function-tools.md)
- [`official-docs/tutorials/agents/images.md`](official-docs/tutorials/agents/images.md)
- [`official-docs/tutorials/agents/memory.md`](official-docs/tutorials/agents/memory.md)
- [`official-docs/tutorials/agents/middleware.md`](official-docs/tutorials/agents/middleware.md) — Redirect alias retained locally because the live Learn URL now resolves to the canonical middleware page
- [`official-docs/tutorials/agents/multi-turn-conversation.md`](official-docs/tutorials/agents/multi-turn-conversation.md)
- [`official-docs/tutorials/agents/orchestrate-durable-agents.md`](official-docs/tutorials/agents/orchestrate-durable-agents.md)
- [`official-docs/tutorials/agents/persisted-conversation.md`](official-docs/tutorials/agents/persisted-conversation.md)
- [`official-docs/tutorials/agents/run-agent.md`](official-docs/tutorials/agents/run-agent.md)
- [`official-docs/tutorials/agents/structured-output.md`](official-docs/tutorials/agents/structured-output.md)
- [`official-docs/tutorials/agents/third-party-chat-history-storage.md`](official-docs/tutorials/agents/third-party-chat-history-storage.md)

### Tutorials / Workflows

- [`official-docs/tutorials/workflows/agents-in-workflows.md`](official-docs/tutorials/workflows/agents-in-workflows.md)
- [`official-docs/tutorials/workflows/checkpointing-and-resuming.md`](official-docs/tutorials/workflows/checkpointing-and-resuming.md)
- [`official-docs/tutorials/workflows/requests-and-responses.md`](official-docs/tutorials/workflows/requests-and-responses.md)
- [`official-docs/tutorials/workflows/simple-concurrent-workflow.md`](official-docs/tutorials/workflows/simple-concurrent-workflow.md)
- [`official-docs/tutorials/workflows/simple-sequential-workflow.md`](official-docs/tutorials/workflows/simple-sequential-workflow.md)
- [`official-docs/tutorials/workflows/workflow-builder-with-factories.md`](official-docs/tutorials/workflows/workflow-builder-with-factories.md)
- [`official-docs/tutorials/workflows/workflow-with-branching-logic.md`](official-docs/tutorials/workflows/workflow-with-branching-logic.md)

### Tutorials / Plugins

- [`official-docs/tutorials/plugins/use-purview-with-agent-framework-sdk.md`](official-docs/tutorials/plugins/use-purview-with-agent-framework-sdk.md)

### User Guide

- [`official-docs/user-guide/observability.md`](official-docs/user-guide/observability.md)
- [`official-docs/user-guide/overview.md`](official-docs/user-guide/overview.md)

### User Guide / Agents

- [`official-docs/user-guide/agents/agent-background-responses.md`](official-docs/user-guide/agents/agent-background-responses.md)
- [`official-docs/user-guide/agents/agent-memory.md`](official-docs/user-guide/agents/agent-memory.md)
- [`official-docs/user-guide/agents/agent-middleware.md`](official-docs/user-guide/agents/agent-middleware.md)
- [`official-docs/user-guide/agents/agent-rag.md`](official-docs/user-guide/agents/agent-rag.md)
- [`official-docs/user-guide/agents/agent-tools.md`](official-docs/user-guide/agents/agent-tools.md)
- [`official-docs/user-guide/agents/multi-turn-conversation.md`](official-docs/user-guide/agents/multi-turn-conversation.md)
- [`official-docs/user-guide/agents/running-agents.md`](official-docs/user-guide/agents/running-agents.md)

### User Guide / Agents / Agent Types

- [`official-docs/user-guide/agents/agent-types/a2a-agent.md`](official-docs/user-guide/agents/agent-types/a2a-agent.md)
- [`official-docs/user-guide/agents/agent-types/anthropic-agent.md`](official-docs/user-guide/agents/agent-types/anthropic-agent.md)
- [`official-docs/user-guide/agents/agent-types/microsoft-foundry-agents.md`](official-docs/user-guide/agents/agent-types/microsoft-foundry-agents.md) — Consolidated "Microsoft Foundry Agents" page covering persistent Azure AI Foundry Agents, Foundry Models Chat Completions, and Foundry Models Responses (all three upstream URLs now resolve to this single page)
- [`official-docs/user-guide/agents/agent-types/azure-openai-chat-completion-agent.md`](official-docs/user-guide/agents/agent-types/azure-openai-chat-completion-agent.md)
- [`official-docs/user-guide/agents/agent-types/azure-openai-responses-agent.md`](official-docs/user-guide/agents/agent-types/azure-openai-responses-agent.md)
- [`official-docs/user-guide/agents/agent-types/chat-client-agent.md`](official-docs/user-guide/agents/agent-types/chat-client-agent.md)
- [`official-docs/user-guide/agents/agent-types/custom-agent.md`](official-docs/user-guide/agents/agent-types/custom-agent.md)
- [`official-docs/user-guide/agents/agent-types/index.md`](official-docs/user-guide/agents/agent-types/index.md)
- [`official-docs/user-guide/agents/agent-types/openai-assistants-agent.md`](official-docs/user-guide/agents/agent-types/openai-assistants-agent.md)
- [`official-docs/user-guide/agents/agent-types/openai-chat-completion-agent.md`](official-docs/user-guide/agents/agent-types/openai-chat-completion-agent.md)
- [`official-docs/user-guide/agents/agent-types/openai-responses-agent.md`](official-docs/user-guide/agents/agent-types/openai-responses-agent.md)

### User Guide / Agents / Agent Types / Durable Agent

- [`official-docs/user-guide/agents/agent-types/durable-agent/create-durable-agent.md`](official-docs/user-guide/agents/agent-types/durable-agent/create-durable-agent.md)
- [`official-docs/user-guide/agents/agent-types/durable-agent/features.md`](official-docs/user-guide/agents/agent-types/durable-agent/features.md)

### User Guide / Model Context Protocol

- [`official-docs/user-guide/model-context-protocol/index.md`](official-docs/user-guide/model-context-protocol/index.md)
- [`official-docs/user-guide/model-context-protocol/using-mcp-tools.md`](official-docs/user-guide/model-context-protocol/using-mcp-tools.md)
- [`official-docs/user-guide/model-context-protocol/using-mcp-with-foundry-agents.md`](official-docs/user-guide/model-context-protocol/using-mcp-with-foundry-agents.md)

### User Guide / Workflows

- [`official-docs/user-guide/workflows/as-agents.md`](official-docs/user-guide/workflows/as-agents.md)
- [`official-docs/user-guide/workflows/checkpoints.md`](official-docs/user-guide/workflows/checkpoints.md)
- [`official-docs/user-guide/workflows/declarative-workflows.md`](official-docs/user-guide/workflows/declarative-workflows.md)
- [`official-docs/user-guide/workflows/observability.md`](official-docs/user-guide/workflows/observability.md)
- [`official-docs/user-guide/workflows/overview.md`](official-docs/user-guide/workflows/overview.md)
- [`official-docs/user-guide/workflows/requests-and-responses.md`](official-docs/user-guide/workflows/requests-and-responses.md)
- [`official-docs/user-guide/workflows/shared-states.md`](official-docs/user-guide/workflows/shared-states.md)
- [`official-docs/user-guide/workflows/state-isolation.md`](official-docs/user-guide/workflows/state-isolation.md)
- [`official-docs/user-guide/workflows/using-agents.md`](official-docs/user-guide/workflows/using-agents.md)
- [`official-docs/user-guide/workflows/visualization.md`](official-docs/user-guide/workflows/visualization.md)

### User Guide / Workflows / Core Concepts

- [`official-docs/user-guide/workflows/core-concepts/edges.md`](official-docs/user-guide/workflows/core-concepts/edges.md)
- [`official-docs/user-guide/workflows/core-concepts/events.md`](official-docs/user-guide/workflows/core-concepts/events.md)
- [`official-docs/user-guide/workflows/core-concepts/executors.md`](official-docs/user-guide/workflows/core-concepts/executors.md)
- [`official-docs/user-guide/workflows/core-concepts/overview.md`](official-docs/user-guide/workflows/core-concepts/overview.md)
- [`official-docs/user-guide/workflows/core-concepts/workflows.md`](official-docs/user-guide/workflows/core-concepts/workflows.md)

### User Guide / Workflows / Orchestrations

- [`official-docs/user-guide/workflows/orchestrations/concurrent.md`](official-docs/user-guide/workflows/orchestrations/concurrent.md)
- [`official-docs/user-guide/workflows/orchestrations/group-chat.md`](official-docs/user-guide/workflows/orchestrations/group-chat.md)
- [`official-docs/user-guide/workflows/orchestrations/handoff.md`](official-docs/user-guide/workflows/orchestrations/handoff.md)
- [`official-docs/user-guide/workflows/orchestrations/human-in-the-loop.md`](official-docs/user-guide/workflows/orchestrations/human-in-the-loop.md)
- [`official-docs/user-guide/workflows/orchestrations/magentic.md`](official-docs/user-guide/workflows/orchestrations/magentic.md)
- [`official-docs/user-guide/workflows/orchestrations/overview.md`](official-docs/user-guide/workflows/orchestrations/overview.md)
- [`official-docs/user-guide/workflows/orchestrations/sequential.md`](official-docs/user-guide/workflows/orchestrations/sequential.md)

### User Guide / Workflows / Declarative Workflows

- [`official-docs/user-guide/workflows/declarative-workflows/actions-reference.md`](official-docs/user-guide/workflows/declarative-workflows/actions-reference.md)
- [`official-docs/user-guide/workflows/declarative-workflows/advanced-patterns.md`](official-docs/user-guide/workflows/declarative-workflows/advanced-patterns.md)
- [`official-docs/user-guide/workflows/declarative-workflows/expressions.md`](official-docs/user-guide/workflows/declarative-workflows/expressions.md)

### User Guide / Hosting

- [`official-docs/user-guide/hosting/agent-to-agent-integration.md`](official-docs/user-guide/hosting/agent-to-agent-integration.md)
- [`official-docs/user-guide/hosting/index.md`](official-docs/user-guide/hosting/index.md)
- [`official-docs/user-guide/hosting/openai-integration.md`](official-docs/user-guide/hosting/openai-integration.md)

### User Guide / DevUI

- [`official-docs/user-guide/devui/api-reference.md`](official-docs/user-guide/devui/api-reference.md)
- [`official-docs/user-guide/devui/directory-discovery.md`](official-docs/user-guide/devui/directory-discovery.md)
- [`official-docs/user-guide/devui/index.md`](official-docs/user-guide/devui/index.md)
- [`official-docs/user-guide/devui/samples.md`](official-docs/user-guide/devui/samples.md)
- [`official-docs/user-guide/devui/security.md`](official-docs/user-guide/devui/security.md)
- [`official-docs/user-guide/devui/tracing.md`](official-docs/user-guide/devui/tracing.md)

### Integrations / AG-UI

- [`official-docs/integrations/ag-ui/backend-tool-rendering.md`](official-docs/integrations/ag-ui/backend-tool-rendering.md)
- [`official-docs/integrations/ag-ui/frontend-tools.md`](official-docs/integrations/ag-ui/frontend-tools.md)
- [`official-docs/integrations/ag-ui/getting-started.md`](official-docs/integrations/ag-ui/getting-started.md)
- [`official-docs/integrations/ag-ui/human-in-the-loop.md`](official-docs/integrations/ag-ui/human-in-the-loop.md)
- [`official-docs/integrations/ag-ui/index.md`](official-docs/integrations/ag-ui/index.md)
- [`official-docs/integrations/ag-ui/security-considerations.md`](official-docs/integrations/ag-ui/security-considerations.md)
- [`official-docs/integrations/ag-ui/state-management.md`](official-docs/integrations/ag-ui/state-management.md)
- [`official-docs/integrations/ag-ui/testing-with-dojo.md`](official-docs/integrations/ag-ui/testing-with-dojo.md)

### Migration Guide / From AutoGen

- [`official-docs/migration-guide/from-autogen/index.md`](official-docs/migration-guide/from-autogen/index.md)

### Migration Guide / From Semantic Kernel

- [`official-docs/migration-guide/from-semantic-kernel/index.md`](official-docs/migration-guide/from-semantic-kernel/index.md)
- [`official-docs/migration-guide/from-semantic-kernel/samples.md`](official-docs/migration-guide/from-semantic-kernel/samples.md)

### Support

- [`official-docs/support/faq.md`](official-docs/support/faq.md)
- [`official-docs/support/index.md`](official-docs/support/index.md)
- [`official-docs/support/troubleshooting.md`](official-docs/support/troubleshooting.md)

### Support / Upgrade

- [`official-docs/support/upgrade/index.md`](official-docs/support/upgrade/index.md)

## API Reference Pointer

- `.NET` API landing page: `https://learn.microsoft.com/dotnet/api/microsoft.agents.ai`

## Usage Guidance

- Start with the smallest relevant local page rather than loading the whole mirror.
- Use the local mirror for exact wording, edge-case features, migration notes, or to confirm preview limitations.
- Raw Learn `:::code` and `:::image` source-asset directives are stripped from the local snapshot to keep it prose-first and avoid broken local references.
- Python-only upgrade guides are intentionally excluded from the local snapshot for this `.NET` skill.
