# Quick-Start and Tutorial Recipes

Use this file when you need the smallest official proof that a pattern exists before you design the production version.

## Foundation

| Need | Official Source Path | First Proof | Production Follow-Up |
| --- | --- | --- | --- |
| Understand the framework split | `overview/index.md` | Agent versus workflow guidance | Route the architecture in `patterns.md` |
| Get a minimal install and first run | `get-started/your-first-agent.md` | Smallest working setup | Convert the sample to your real provider and state model |
| See the guided starting points | `get-started/index.md` | Discover supported paths | Pick the smallest targeted walkthrough below |

## Agent Recipes

| Need | Official Source Path | First Proof | Production Follow-Up |
| --- | --- | --- | --- |
| Basic single agent | `get-started/your-first-agent.md` | standard run flow | Decide session model and middleware |
| Batteries-included long-task agent | live `https://learn.microsoft.com/agent-framework/agents/harness` | `AsHarnessAgent`, session, plan/todo, compaction | Scope file, shell, web, approval, loop, and background-agent capabilities in `harness.md` |
| Multi-turn conversation | `concepts/agents/conversations/session.md` | `AgentSession` reuse | Persist the serialized session through the provider's supported contract |
| Persist and resume conversations | `concepts/agents/conversations/storage.md` | serialize and restore session state | Design storage and compatibility rules |
| Store history outside memory | `concepts/agents/conversations/chat-history-memory-provider.md` | chat-history memory provider | enforce keying and reduction strategy |
| Add memory augmentation | `get-started/memory.md` | `AIContextProvider` hooks | separate memory from raw chat history |
| Add function tools | `agents/tools/function-tools.md` | direct tool registration | narrow contracts, hide runtime-only values from the schema, and add approval rules |
| Add approval to tools | `agents/tools/tool-approval.md` | tool approval flow | decide whether approval belongs in middleware or workflows |
| Structured output | `agents/structured-outputs.md` | typed output shape | keep schema contracts explicit |
| Images or multimodal input | `agents/multimodal.md` | non-text content path | verify backend multimodal support |
| Add middleware | `concepts/agents/middleware/index.md` | run/function/client interception with the current `AgentSession` callback signatures | separate policy by layer |
| Use an agent as a tool | `journey/agents-as-tools.md` | bounded delegation | escalate to workflows if control flow matters |
| Expose an agent as an MCP tool | `agents/tools/hosted-mcp-tools.md` | MCP-facing tool wrapper | use A2A if the remote thing should stay an agent |
| Enable observability | `agents/observability.md` | tracing and instrumentation | add repo-specific correlation and policy spans |
| Durable hosted agent | `hosting/azure-functions.md` | Azure Functions durable path | only keep it if durability is genuinely required |
| Orchestrate durable agents | `hosting/azure-functions.md` | durable hosted execution | compare against ordinary workflows first |

## Workflow Recipes

| Need | Official Source Path | First Proof | Production Follow-Up |
| --- | --- | --- | --- |
| Sequential workflow | `workflows/orchestrations/sequential.md` | ordered stage execution and context flow | verify stage boundaries, context policy, and error handling |
| Concurrent workflow | `workflows/orchestrations/concurrent.md` | fan-out and aggregation | make aggregation deterministic |
| Agents inside workflows | `workflows/agents-in-workflows.md` | specialist composition | keep agent versus executor responsibilities clear |
| Branching logic | `concepts/workflows/edges.md` | conditional routing | move branch policy out of prompts |
| Builder and execution | `concepts/workflows/builder-and-execution.md` | construction patterns | watch state isolation and reuse |
| External requests and responses | `workflows/human-in-the-loop.md` | request and approval events | use this for approval and async callbacks |
| Checkpointing and resuming | `workflows/checkpoints.md` | save and restore flow state | explicitly checkpoint custom executor state |

## Hosting And Integration Recipes

| Need | Official Source Path | First Proof | Production Follow-Up |
| --- | --- | --- | --- |
| Integration discovery | `integrations/index.md` | choose hosted-agent, UI, history, memory, RAG, or vector-store capability | verify .NET availability and maturity for the selected provider |
| OpenAI-compatible endpoint | `hosting/self-hosting/openai-endpoints.md` | map Chat Completions or Responses | prefer Responses for new clients |
| A2A endpoint | `hosting/self-hosting/a2a/dotnet.md` | A2A endpoint and agent card | decide discovery and task semantics |
| AG-UI surface | `integrations/by-component/ui/ag-ui/index.md` | SSE and UI protocol mapping | treat browser trust boundaries explicitly |
| Purview integration | `integrations/by-component/middleware/purview.md` | policy/governance flow | use only when governance is a real requirement |
| Workflow as agent | `workflows/as-agents.md` | wrap workflow behind `AIAgent` API | keep the workflow explicit in code and docs |
| DevUI smoke testing | `integrations/by-component/ui/devui/index.md` | local sample-driven testing | do not let it become production architecture |

## Source Pages

- `references/official-docs/get-started/index.md`
- `references/official-docs/get-started/your-first-agent.md`
- `references/official-docs/get-started/multi-turn.md`
- `references/official-docs/workflows/orchestrations/sequential.md`
- `references/official-docs/hosting/index.md`
- `references/official-docs/integrations/by-component/ui/ag-ui/index.md`
