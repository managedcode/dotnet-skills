# Official Docs Snapshot

Use this reference when the summarized guidance in the skill is not enough and you need the current Microsoft Learn markdown that informed the skill.

The local snapshot lives under `references/official-docs/` and is synchronized from the official `microsoft/semantic-kernel-docs` Agent Framework tree.

## Scope

- Local snapshot: `172` markdown pages
- Scope: current Agent Framework agents, concepts, get-started guides, hosting, integrations, journeys, migrations, overview, support, and workflows
- Excluded: repository governance files and Python-only upgrade guides; mixed-language pages remain when they also carry .NET guidance
- Generated API references are not mirrored page-by-page; use the live .NET API landing page for exact symbols

## Section Map

| Section | Count | Start Here |
| --- | ---: | --- |
| Agents | 23 | [`official-docs/agents/index.md`](official-docs/agents/index.md) |
| Concepts | 33 | [`official-docs/concepts/index.md`](official-docs/concepts/index.md) |
| Get Started | 8 | [`official-docs/get-started/index.md`](official-docs/get-started/index.md) |
| Hosting | 11 | [`official-docs/hosting/index.md`](official-docs/hosting/index.md) |
| Integrations | 63 | [`official-docs/integrations/index.md`](official-docs/integrations/index.md) |
| Journey | 10 | [`official-docs/journey/index.md`](official-docs/journey/index.md) |
| Migration Guide | 5 | [`official-docs/migration-guide/index.md`](official-docs/migration-guide/index.md) |
| Overview | 1 | [`official-docs/overview/index.md`](official-docs/overview/index.md) |
| Support | 4 | [`official-docs/support/index.md`](official-docs/support/index.md) |
| Workflows | 14 | [`official-docs/workflows/index.md`](official-docs/workflows/index.md) |

## High-Value Entry Points

- First agent: [`official-docs/get-started/your-first-agent.md`](official-docs/get-started/your-first-agent.md)
- Multi-turn sessions: [`official-docs/concepts/agents/conversations/session.md`](official-docs/concepts/agents/conversations/session.md)
- Function tools and approvals: [`official-docs/agents/tools/function-tools.md`](official-docs/agents/tools/function-tools.md) and [`official-docs/agents/tools/tool-approval.md`](official-docs/agents/tools/tool-approval.md)
- Middleware: [`official-docs/concepts/agents/middleware/index.md`](official-docs/concepts/agents/middleware/index.md)
- Memory and context providers: [`official-docs/get-started/memory.md`](official-docs/get-started/memory.md) and [`official-docs/concepts/agents/conversations/context-providers.md`](official-docs/concepts/agents/conversations/context-providers.md)
- Workflows: [`official-docs/get-started/workflows.md`](official-docs/get-started/workflows.md) and [`official-docs/workflows/index.md`](official-docs/workflows/index.md)
- Self-hosting: [`official-docs/hosting/self-hosting/index.md`](official-docs/hosting/self-hosting/index.md)
- Azure Functions durable hosting: [`official-docs/hosting/azure-functions.md`](official-docs/hosting/azure-functions.md)
- MCP tools and hosting: [`official-docs/agents/tools/hosted-mcp-tools.md`](official-docs/agents/tools/hosted-mcp-tools.md), [`official-docs/agents/tools/local-mcp-tools.md`](official-docs/agents/tools/local-mcp-tools.md), and [`official-docs/hosting/self-hosting/mcp.md`](official-docs/hosting/self-hosting/mcp.md)
- AG-UI: [`official-docs/integrations/by-component/ui/ag-ui/index.md`](official-docs/integrations/by-component/ui/ag-ui/index.md)
- DevUI: [`official-docs/integrations/by-component/ui/devui/index.md`](official-docs/integrations/by-component/ui/devui/index.md)
- Migration: [`official-docs/migration-guide/index.md`](official-docs/migration-guide/index.md)
- Support and upgrades: [`official-docs/support/index.md`](official-docs/support/index.md) and [`official-docs/support/upgrade/index.md`](official-docs/support/upgrade/index.md)

## Complete Local File Map

### Agents

- [`official-docs/agents/background-agents.md`](official-docs/agents/background-agents.md) — Background agents
- [`official-docs/agents/background-responses.md`](official-docs/agents/background-responses.md) — Agent Background Responses
- [`official-docs/agents/code_act.md`](official-docs/agents/code_act.md) — CodeAct
- [`official-docs/agents/declarative.md`](official-docs/agents/declarative.md) — Declarative Agents
- [`official-docs/agents/evaluation.md`](official-docs/agents/evaluation.md) — Evaluation
- [`official-docs/agents/index.md`](official-docs/agents/index.md) — Agent capabilities
- [`official-docs/agents/looping.md`](official-docs/agents/looping.md) — Agent looping
- [`official-docs/agents/multimodal.md`](official-docs/agents/multimodal.md) — Using images with an agent
- [`official-docs/agents/observability.md`](official-docs/agents/observability.md) — Observability
- [`official-docs/agents/planning-and-todos.md`](official-docs/agents/planning-and-todos.md) — Planning and todos
- [`official-docs/agents/rag.md`](official-docs/agents/rag.md) — RAG
- [`official-docs/agents/security.md`](official-docs/agents/security.md) — Agent Security with FIDES
- [`official-docs/agents/skills.md`](official-docs/agents/skills.md) — Agent Skills
- [`official-docs/agents/structured-outputs.md`](official-docs/agents/structured-outputs.md) — Producing Structured Outputs with Agents
- [`official-docs/agents/tools/code-interpreter.md`](official-docs/agents/tools/code-interpreter.md) — Code Interpreter
- [`official-docs/agents/tools/controlling-tool-availability.md`](official-docs/agents/tools/controlling-tool-availability.md) — Controlling tool availability
- [`official-docs/agents/tools/file-search.md`](official-docs/agents/tools/file-search.md) — File Search
- [`official-docs/agents/tools/function-tools.md`](official-docs/agents/tools/function-tools.md) — Using function tools with an agent
- [`official-docs/agents/tools/hosted-mcp-tools.md`](official-docs/agents/tools/hosted-mcp-tools.md) — Using hosted MCP tools with agents
- [`official-docs/agents/tools/index.md`](official-docs/agents/tools/index.md) — Tools Overview
- [`official-docs/agents/tools/local-mcp-tools.md`](official-docs/agents/tools/local-mcp-tools.md) — Using MCP tools with Agents
- [`official-docs/agents/tools/tool-approval.md`](official-docs/agents/tools/tool-approval.md) — Using function tools with human in the loop approvals
- [`official-docs/agents/tools/web-search.md`](official-docs/agents/tools/web-search.md) — Web Search

### Concepts

- [`official-docs/concepts/agents/agent-pipeline.md`](official-docs/concepts/agents/agent-pipeline.md) — Agent pipeline architecture
- [`official-docs/concepts/agents/conversations/chat-history-memory-provider.md`](official-docs/concepts/agents/conversations/chat-history-memory-provider.md) — Chat History Memory Provider
- [`official-docs/concepts/agents/conversations/compaction.md`](official-docs/concepts/agents/conversations/compaction.md) — Compaction
- [`official-docs/concepts/agents/conversations/context-providers.md`](official-docs/concepts/agents/conversations/context-providers.md) — Context Providers
- [`official-docs/concepts/agents/conversations/index.md`](official-docs/concepts/agents/conversations/index.md) — Conversations & Memory overview
- [`official-docs/concepts/agents/conversations/session.md`](official-docs/concepts/agents/conversations/session.md) — Session
- [`official-docs/concepts/agents/conversations/storage.md`](official-docs/concepts/agents/conversations/storage.md) — Storage
- [`official-docs/concepts/agents/custom-agents.md`](official-docs/concepts/agents/custom-agents.md) — Custom Agents
- [`official-docs/concepts/agents/index.md`](official-docs/concepts/agents/index.md) — Agent concepts
- [`official-docs/concepts/agents/middleware/agent-vs-run-scope.md`](official-docs/concepts/agents/middleware/agent-vs-run-scope.md) — Agent vs Run Scope
- [`official-docs/concepts/agents/middleware/chat-middleware.md`](official-docs/concepts/agents/middleware/chat-middleware.md) — Chat-Level Middleware
- [`official-docs/concepts/agents/middleware/defining-middleware.md`](official-docs/concepts/agents/middleware/defining-middleware.md) — Adding Middleware to Agents
- [`official-docs/concepts/agents/middleware/exception-handling.md`](official-docs/concepts/agents/middleware/exception-handling.md) — Exception Handling
- [`official-docs/concepts/agents/middleware/index.md`](official-docs/concepts/agents/middleware/index.md) — Agent Middleware
- [`official-docs/concepts/agents/middleware/result-overrides.md`](official-docs/concepts/agents/middleware/result-overrides.md) — Result Overrides
- [`official-docs/concepts/agents/middleware/runtime-context.md`](official-docs/concepts/agents/middleware/runtime-context.md) — Runtime Context
- [`official-docs/concepts/agents/middleware/shared-state.md`](official-docs/concepts/agents/middleware/shared-state.md) — Shared State
- [`official-docs/concepts/agents/middleware/termination.md`](official-docs/concepts/agents/middleware/termination.md) — Termination & Guardrails
- [`official-docs/concepts/agents/running-agents.md`](official-docs/concepts/agents/running-agents.md) — Running Agents
- [`official-docs/concepts/agents/safety.md`](official-docs/concepts/agents/safety.md) — Agent Safety
- [`official-docs/concepts/harness.md`](official-docs/concepts/harness.md) — Agent Harness
- [`official-docs/concepts/index.md`](official-docs/concepts/index.md) — Agent Framework concepts
- [`official-docs/concepts/workflows/advanced/agent-executor.md`](official-docs/concepts/workflows/advanced/agent-executor.md) — Agent Executor
- [`official-docs/concepts/workflows/advanced/execution-modes.md`](official-docs/concepts/workflows/advanced/execution-modes.md) — Workflow Execution Modes
- [`official-docs/concepts/workflows/advanced/resettable-executors.md`](official-docs/concepts/workflows/advanced/resettable-executors.md) — Resettable Executors
- [`official-docs/concepts/workflows/advanced/sub-workflows.md`](official-docs/concepts/workflows/advanced/sub-workflows.md) — Sub-Workflows
- [`official-docs/concepts/workflows/builder-and-execution.md`](official-docs/concepts/workflows/builder-and-execution.md) — Workflow Builder & Execution
- [`official-docs/concepts/workflows/edges.md`](official-docs/concepts/workflows/edges.md) — Edges
- [`official-docs/concepts/workflows/events.md`](official-docs/concepts/workflows/events.md) — Events
- [`official-docs/concepts/workflows/executors.md`](official-docs/concepts/workflows/executors.md) — Executors
- [`official-docs/concepts/workflows/functional.md`](official-docs/concepts/workflows/functional.md) — Functional Workflow API
- [`official-docs/concepts/workflows/index.md`](official-docs/concepts/workflows/index.md) — Workflow concepts
- [`official-docs/concepts/workflows/state.md`](official-docs/concepts/workflows/state.md) — Microsoft Agent Framework Workflows - State

### Get Started

- [`official-docs/get-started/add-tools.md`](official-docs/get-started/add-tools.md) — Step 2: Add Tools
- [`official-docs/get-started/harness.md`](official-docs/get-started/harness.md) — Step 6: Agent Harness
- [`official-docs/get-started/hosting.md`](official-docs/get-started/hosting.md) — Step 7: Host Your Agent
- [`official-docs/get-started/index.md`](official-docs/get-started/index.md) — Get started with Agent Framework
- [`official-docs/get-started/memory.md`](official-docs/get-started/memory.md) — Step 4: Memory & Persistence
- [`official-docs/get-started/multi-turn.md`](official-docs/get-started/multi-turn.md) — Step 3: Multi-Turn Conversations
- [`official-docs/get-started/workflows.md`](official-docs/get-started/workflows.md) — Step 5: Workflows
- [`official-docs/get-started/your-first-agent.md`](official-docs/get-started/your-first-agent.md) — Step 1: Your First Agent

### Hosting

- [`official-docs/hosting/azure-functions.md`](official-docs/hosting/azure-functions.md) — Durable Extension
- [`official-docs/hosting/foundry-hosted-agent.md`](official-docs/hosting/foundry-hosted-agent.md) — Foundry Hosted Agents
- [`official-docs/hosting/index.md`](official-docs/hosting/index.md) — Hosting Agent Framework applications
- [`official-docs/hosting/self-hosting/a2a/dotnet.md`](official-docs/hosting/self-hosting/a2a/dotnet.md) — A2A Hosting
- [`official-docs/hosting/self-hosting/a2a/index.md`](official-docs/hosting/self-hosting/a2a/index.md) — Self-host A2A agents
- [`official-docs/hosting/self-hosting/a2a/server.md`](official-docs/hosting/self-hosting/a2a/server.md) — Host agents with A2A
- [`official-docs/hosting/self-hosting/index.md`](official-docs/hosting/self-hosting/index.md) — Self-host Agent Framework applications
- [`official-docs/hosting/self-hosting/mcp.md`](official-docs/hosting/self-hosting/mcp.md) — Self-host agents as MCP tools
- [`official-docs/hosting/self-hosting/openai-endpoints.md`](official-docs/hosting/self-hosting/openai-endpoints.md) — OpenAI-Compatible Endpoints
- [`official-docs/hosting/self-hosting/responses.md`](official-docs/hosting/self-hosting/responses.md) — Self-host OpenAI Responses endpoints
- [`official-docs/hosting/self-hosting/telegram.md`](official-docs/hosting/self-hosting/telegram.md) — Self-host Telegram bots

### Integrations

- [`official-docs/integrations/by-component/agent-services/a2a.md`](official-docs/integrations/by-component/agent-services/a2a.md) — A2A agent service
- [`official-docs/integrations/by-component/agent-services/anthropic-claude.md`](official-docs/integrations/by-component/agent-services/anthropic-claude.md) — Anthropic Claude
- [`official-docs/integrations/by-component/agent-services/copilot-studio.md`](official-docs/integrations/by-component/agent-services/copilot-studio.md) — Copilot Studio
- [`official-docs/integrations/by-component/agent-services/foundry.md`](official-docs/integrations/by-component/agent-services/foundry.md) — Microsoft Foundry Agent Service
- [`official-docs/integrations/by-component/agent-services/github-copilot.md`](official-docs/integrations/by-component/agent-services/github-copilot.md) — GitHub Copilot
- [`official-docs/integrations/by-component/agent-services/index.md`](official-docs/integrations/by-component/agent-services/index.md) — Agent services
- [`official-docs/integrations/by-component/context-providers/azure-ai-search.md`](official-docs/integrations/by-component/context-providers/azure-ai-search.md) — Azure AI Search
- [`official-docs/integrations/by-component/context-providers/azure-content-understanding.md`](official-docs/integrations/by-component/context-providers/azure-content-understanding.md) — Azure Content Understanding
- [`official-docs/integrations/by-component/context-providers/azure-cosmos.md`](official-docs/integrations/by-component/context-providers/azure-cosmos.md) — Azure Cosmos DB
- [`official-docs/integrations/by-component/context-providers/hyperlight.md`](official-docs/integrations/by-component/context-providers/hyperlight.md) — Hyperlight
- [`official-docs/integrations/by-component/context-providers/index.md`](official-docs/integrations/by-component/context-providers/index.md) — Context provider integrations
- [`official-docs/integrations/by-component/context-providers/local.md`](official-docs/integrations/by-component/context-providers/local.md) — Local (.NET)
- [`official-docs/integrations/by-component/context-providers/mem0.md`](official-docs/integrations/by-component/context-providers/mem0.md) — Mem0
- [`official-docs/integrations/by-component/context-providers/microsoft-foundry.md`](official-docs/integrations/by-component/context-providers/microsoft-foundry.md) — Microsoft Foundry
- [`official-docs/integrations/by-component/context-providers/monty.md`](official-docs/integrations/by-component/context-providers/monty.md) — Monty
- [`official-docs/integrations/by-component/context-providers/neo4j.md`](official-docs/integrations/by-component/context-providers/neo4j.md) — Neo4j
- [`official-docs/integrations/by-component/context-providers/redis.md`](official-docs/integrations/by-component/context-providers/redis.md) — Redis
- [`official-docs/integrations/by-component/context-providers/valkey.md`](official-docs/integrations/by-component/context-providers/valkey.md) — Valkey
- [`official-docs/integrations/by-component/evaluation/microsoft-foundry.md`](official-docs/integrations/by-component/evaluation/microsoft-foundry.md) — Microsoft Foundry evaluation
- [`official-docs/integrations/by-component/index.md`](official-docs/integrations/by-component/index.md) — Integrations by component
- [`official-docs/integrations/by-component/middleware/purview.md`](official-docs/integrations/by-component/middleware/purview.md) — Microsoft Purview
- [`official-docs/integrations/by-component/model-providers/amazon-bedrock.md`](official-docs/integrations/by-component/model-providers/amazon-bedrock.md) — Amazon Bedrock
- [`official-docs/integrations/by-component/model-providers/anthropic.md`](official-docs/integrations/by-component/model-providers/anthropic.md) — Anthropic
- [`official-docs/integrations/by-component/model-providers/azure-openai.md`](official-docs/integrations/by-component/model-providers/azure-openai.md) — Azure OpenAI
- [`official-docs/integrations/by-component/model-providers/dapr.md`](official-docs/integrations/by-component/model-providers/dapr.md) — Dapr
- [`official-docs/integrations/by-component/model-providers/foundry-local.md`](official-docs/integrations/by-component/model-providers/foundry-local.md) — Foundry Local
- [`official-docs/integrations/by-component/model-providers/google-gemini.md`](official-docs/integrations/by-component/model-providers/google-gemini.md) — Google Gemini
- [`official-docs/integrations/by-component/model-providers/index.md`](official-docs/integrations/by-component/model-providers/index.md) — Model providers
- [`official-docs/integrations/by-component/model-providers/microsoft-foundry.md`](official-docs/integrations/by-component/model-providers/microsoft-foundry.md) — Microsoft Foundry model provider
- [`official-docs/integrations/by-component/model-providers/mistral.md`](official-docs/integrations/by-component/model-providers/mistral.md) — Mistral
- [`official-docs/integrations/by-component/model-providers/ollama.md`](official-docs/integrations/by-component/model-providers/ollama.md) — Ollama
- [`official-docs/integrations/by-component/model-providers/onnx.md`](official-docs/integrations/by-component/model-providers/onnx.md) — ONNX
- [`official-docs/integrations/by-component/model-providers/openai.md`](official-docs/integrations/by-component/model-providers/openai.md) — OpenAI
- [`official-docs/integrations/by-component/tools/foundry-toolbox.md`](official-docs/integrations/by-component/tools/foundry-toolbox.md) — Microsoft Foundry Toolbox
- [`official-docs/integrations/by-component/tools/index.md`](official-docs/integrations/by-component/tools/index.md) — Tool integrations
- [`official-docs/integrations/by-component/tools/shell-tools.md`](official-docs/integrations/by-component/tools/shell-tools.md) — Shell tools
- [`official-docs/integrations/by-component/ui/ag-ui/backend-tool-rendering.md`](official-docs/integrations/by-component/ui/ag-ui/backend-tool-rendering.md) — Backend Tool Rendering with AG-UI
- [`official-docs/integrations/by-component/ui/ag-ui/frontend-tools.md`](official-docs/integrations/by-component/ui/ag-ui/frontend-tools.md) — Frontend Tool Rendering with AG-UI
- [`official-docs/integrations/by-component/ui/ag-ui/getting-started.md`](official-docs/integrations/by-component/ui/ag-ui/getting-started.md) — Getting Started with AG-UI
- [`official-docs/integrations/by-component/ui/ag-ui/human-in-the-loop.md`](official-docs/integrations/by-component/ui/ag-ui/human-in-the-loop.md) — Human-in-the-Loop with AG-UI
- [`official-docs/integrations/by-component/ui/ag-ui/index.md`](official-docs/integrations/by-component/ui/ag-ui/index.md) — AG-UI Integration with Agent Framework
- [`official-docs/integrations/by-component/ui/ag-ui/mcp-apps.md`](official-docs/integrations/by-component/ui/ag-ui/mcp-apps.md) — MCP Apps Compatibility with AG-UI
- [`official-docs/integrations/by-component/ui/ag-ui/security-considerations.md`](official-docs/integrations/by-component/ui/ag-ui/security-considerations.md) — Security Considerations for AG-UI
- [`official-docs/integrations/by-component/ui/ag-ui/state-management.md`](official-docs/integrations/by-component/ui/ag-ui/state-management.md) — State Management with AG-UI
- [`official-docs/integrations/by-component/ui/ag-ui/testing-with-dojo.md`](official-docs/integrations/by-component/ui/ag-ui/testing-with-dojo.md) — Testing with AG-UI Dojo
- [`official-docs/integrations/by-component/ui/ag-ui/workflows.md`](official-docs/integrations/by-component/ui/ag-ui/workflows.md) — Workflows with AG-UI
- [`official-docs/integrations/by-component/ui/chatkit.md`](official-docs/integrations/by-component/ui/chatkit.md) — ChatKit
- [`official-docs/integrations/by-component/ui/devui/api-reference.md`](official-docs/integrations/by-component/ui/devui/api-reference.md) — API Reference
- [`official-docs/integrations/by-component/ui/devui/directory-discovery.md`](official-docs/integrations/by-component/ui/devui/directory-discovery.md) — Directory Discovery
- [`official-docs/integrations/by-component/ui/devui/index.md`](official-docs/integrations/by-component/ui/devui/index.md) — DevUI
- [`official-docs/integrations/by-component/ui/devui/samples.md`](official-docs/integrations/by-component/ui/devui/samples.md) — Samples
- [`official-docs/integrations/by-component/ui/devui/security.md`](official-docs/integrations/by-component/ui/devui/security.md) — Security & Deployment
- [`official-docs/integrations/by-component/ui/devui/tracing.md`](official-docs/integrations/by-component/ui/devui/tracing.md) — Tracing & Observability
- [`official-docs/integrations/by-provider/amazon-web-services.md`](official-docs/integrations/by-provider/amazon-web-services.md) — Amazon Web Services integrations
- [`official-docs/integrations/by-provider/anthropic.md`](official-docs/integrations/by-provider/anthropic.md) — Anthropic integrations
- [`official-docs/integrations/by-provider/google.md`](official-docs/integrations/by-provider/google.md) — Google integrations
- [`official-docs/integrations/by-provider/index.md`](official-docs/integrations/by-provider/index.md) — Integrations by provider
- [`official-docs/integrations/by-provider/microsoft-azure.md`](official-docs/integrations/by-provider/microsoft-azure.md) — Microsoft Azure integrations
- [`official-docs/integrations/by-provider/microsoft-foundry.md`](official-docs/integrations/by-provider/microsoft-foundry.md) — Microsoft Foundry integrations
- [`official-docs/integrations/by-provider/mistral.md`](official-docs/integrations/by-provider/mistral.md) — Mistral integrations
- [`official-docs/integrations/by-provider/ollama.md`](official-docs/integrations/by-provider/ollama.md) — Ollama integrations
- [`official-docs/integrations/by-provider/openai.md`](official-docs/integrations/by-provider/openai.md) — OpenAI integrations
- [`official-docs/integrations/index.md`](official-docs/integrations/index.md) — Agent Framework Integrations

### Journey

- [`official-docs/journey/adding-context-providers.md`](official-docs/journey/adding-context-providers.md) — Adding Context Providers
- [`official-docs/journey/adding-middleware.md`](official-docs/journey/adding-middleware.md) — Adding Middleware
- [`official-docs/journey/adding-skills.md`](official-docs/journey/adding-skills.md) — Adding Skills
- [`official-docs/journey/adding-tools.md`](official-docs/journey/adding-tools.md) — Adding Tools
- [`official-docs/journey/agent-to-agent.md`](official-docs/journey/agent-to-agent.md) — Agent-to-Agent (A2A)
- [`official-docs/journey/agents-as-tools.md`](official-docs/journey/agents-as-tools.md) — Agents as Tools
- [`official-docs/journey/from-llms-to-agents.md`](official-docs/journey/from-llms-to-agents.md) — From LLMs to Agents
- [`official-docs/journey/index.md`](official-docs/journey/index.md) — The Agent Development Journey
- [`official-docs/journey/llm-fundamentals.md`](official-docs/journey/llm-fundamentals.md) — LLM Fundamentals
- [`official-docs/journey/workflows.md`](official-docs/journey/workflows.md) — Workflows

### Migration Guide

- [`official-docs/migration-guide/agent-to-agent-sdk-v1.md`](official-docs/migration-guide/agent-to-agent-sdk-v1.md) — A2A SDK v1 Migration Guide
- [`official-docs/migration-guide/from-autogen/index.md`](official-docs/migration-guide/from-autogen/index.md) — AutoGen to Microsoft Agent Framework Migration Guide
- [`official-docs/migration-guide/from-semantic-kernel/index.md`](official-docs/migration-guide/from-semantic-kernel/index.md) — Semantic Kernel to Agent Framework Migration Guide
- [`official-docs/migration-guide/from-semantic-kernel/samples.md`](official-docs/migration-guide/from-semantic-kernel/samples.md) — Semantic Kernel to Agent Framework Migration Samples
- [`official-docs/migration-guide/index.md`](official-docs/migration-guide/index.md) — Migration Guide

### Overview

- [`official-docs/overview/index.md`](official-docs/overview/index.md) — Microsoft Agent Framework

### Support

- [`official-docs/support/faq.md`](official-docs/support/faq.md) — Frequently Asked Questions
- [`official-docs/support/index.md`](official-docs/support/index.md) — Support for Agent Framework
- [`official-docs/support/troubleshooting.md`](official-docs/support/troubleshooting.md) — Troubleshooting
- [`official-docs/support/upgrade/index.md`](official-docs/support/upgrade/index.md) — Upgrade guides

### Workflows

- [`official-docs/workflows/agents-in-workflows.md`](official-docs/workflows/agents-in-workflows.md) — Agents in Workflows
- [`official-docs/workflows/as-agents.md`](official-docs/workflows/as-agents.md) — Microsoft Agent Framework Workflows - Using Workflows as Agents
- [`official-docs/workflows/checkpoints.md`](official-docs/workflows/checkpoints.md) — Microsoft Agent Framework Workflows - Checkpoints
- [`official-docs/workflows/declarative.md`](official-docs/workflows/declarative.md) — Declarative Workflows - Overview
- [`official-docs/workflows/human-in-the-loop.md`](official-docs/workflows/human-in-the-loop.md) — Microsoft Agent Framework Workflows - Human-in-the-loop (HITL)
- [`official-docs/workflows/index.md`](official-docs/workflows/index.md) — Workflow capabilities
- [`official-docs/workflows/observability.md`](official-docs/workflows/observability.md) — Microsoft Agent Framework Workflows - Observability
- [`official-docs/workflows/orchestrations/concurrent.md`](official-docs/workflows/orchestrations/concurrent.md) — Microsoft Agent Framework Workflows Orchestrations - Concurrent
- [`official-docs/workflows/orchestrations/group-chat.md`](official-docs/workflows/orchestrations/group-chat.md) — Microsoft Agent Framework Workflows Orchestrations - Group Chat
- [`official-docs/workflows/orchestrations/handoff.md`](official-docs/workflows/orchestrations/handoff.md) — Microsoft Agent Framework Workflows Orchestrations - Handoff
- [`official-docs/workflows/orchestrations/index.md`](official-docs/workflows/orchestrations/index.md) — Workflow orchestrations
- [`official-docs/workflows/orchestrations/magentic.md`](official-docs/workflows/orchestrations/magentic.md) — Microsoft Agent Framework Workflows Orchestrations - Magentic
- [`official-docs/workflows/orchestrations/sequential.md`](official-docs/workflows/orchestrations/sequential.md) — Microsoft Agent Framework Workflows Orchestrations - Sequential
- [`official-docs/workflows/visualization.md`](official-docs/workflows/visualization.md) — Microsoft Agent Framework Workflows - Visualization

## API Reference Pointer

- `.NET` API landing page: `https://learn.microsoft.com/dotnet/api/microsoft.agents.ai`

## Usage Guidance

- Start with the smallest relevant local page rather than loading the whole mirror.
- Use the local mirror for exact wording, edge-case features, migration notes, or preview limitations.
- The mirror preserves upstream Learn directives. Resolve referenced non-markdown assets against the live page when they are not present locally.
- Verify exact package versions and experimental annotations before committing production architecture.
