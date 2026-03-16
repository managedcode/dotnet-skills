# Quick-Start and Tutorial Recipes

Use this file when you need the smallest official proof that a pattern exists before you design the production version.

## Foundation

| Need | Official Source Path | First Proof | Production Follow-Up |
| --- | --- | --- | --- |
| Understand the framework split | `overview/agent-framework-overview.md` | Agent versus workflow guidance | Route the architecture in `patterns.md` |
| Get a minimal install and first run | `tutorials/quick-start.md` | Smallest working setup | Convert the sample to your real provider and state model |
| See the tutorial families | `tutorials/overview.md` | Discover supported paths | Pick the smallest targeted walkthrough below |

## Agent Recipes

| Need | Official Source Path | First Proof | Production Follow-Up |
| --- | --- | --- | --- |
| Basic single agent | `tutorials/agents/run-agent.md` | `AsAIAgent`, standard run flow | Decide thread model and middleware |
| Multi-turn conversation | `tutorials/agents/multi-turn-conversation.md` | `AgentThread` reuse | Persist the serialized thread |
| Persist and resume conversations | `tutorials/agents/persisted-conversation.md` | serialize and restore thread | Design storage and compatibility rules |
| Store history outside memory | `tutorials/agents/third-party-chat-history-storage.md` | custom `ChatMessageStore` | enforce keying and reduction strategy |
| Add memory augmentation | `tutorials/agents/memory.md` | `AIContextProvider` hooks | separate memory from raw chat history |
| Add function tools | `tutorials/agents/function-tools.md` | direct tool registration | narrow contracts and approval rules |
| Add approval to tools | `tutorials/agents/function-tools-approvals.md` | tool approval flow | decide whether approval belongs in middleware or workflows |
| Structured output | `tutorials/agents/structured-output.md` | typed output shape | keep schema contracts explicit |
| Images or multimodal input | `tutorials/agents/images.md` | non-text content path | verify backend multimodal support |
| Add middleware | `tutorials/agents/middleware.md` | run/function/client interception | separate policy by layer |
| Use an agent as a tool | `tutorials/agents/agent-as-function-tool.md` | bounded delegation | escalate to workflows if control flow matters |
| Expose an agent as an MCP tool | `tutorials/agents/agent-as-mcp-tool.md` | MCP-facing tool wrapper | use A2A if the remote thing should stay an agent |
| Enable observability | `tutorials/agents/enable-observability.md` | tracing and instrumentation | add repo-specific correlation and policy spans |
| Durable hosted agent | `tutorials/agents/create-and-run-durable-agent.md` | Azure Functions durable path | only keep it if durability is genuinely required |
| Orchestrate durable agents | `tutorials/agents/orchestrate-durable-agents.md` | deterministic multi-agent orchestration | compare against ordinary workflows first |

## Workflow Recipes

| Need | Official Source Path | First Proof | Production Follow-Up |
| --- | --- | --- | --- |
| Sequential workflow | `tutorials/workflows/simple-sequential-workflow.md` | ordered stage execution | verify stage boundaries and error handling |
| Concurrent workflow | `tutorials/workflows/simple-concurrent-workflow.md` | fan-out and aggregation | make aggregation deterministic |
| Agents inside workflows | `tutorials/workflows/agents-in-workflows.md` | specialist composition | keep agent versus executor responsibilities clear |
| Branching logic | `tutorials/workflows/workflow-with-branching-logic.md` | conditional routing | move branch policy out of prompts |
| Builder with factories | `tutorials/workflows/workflow-builder-with-factories.md` | construction patterns | watch state isolation and reuse |
| External requests and responses | `tutorials/workflows/requests-and-responses.md` | `InputPort` and `RequestInfoEvent` | use this for approval and async callbacks |
| Checkpointing and resuming | `tutorials/workflows/checkpointing-and-resuming.md` | save and restore flow state | explicitly checkpoint custom executor state |

## Hosting And Integration Recipes

| Need | Official Source Path | First Proof | Production Follow-Up |
| --- | --- | --- | --- |
| Core ASP.NET Core hosting | `user-guide/hosting/index.md` | `AddAIAgent`, `AddWorkflow`, thread store wiring | keep runtime model protocol-agnostic |
| OpenAI-compatible endpoint | `user-guide/hosting/openai-integration.md` | map Chat Completions or Responses | prefer Responses for new clients |
| A2A endpoint | `user-guide/hosting/agent-to-agent-integration.md` | `MapA2A` and agent card | decide discovery and task semantics |
| AG-UI surface | `integrations/ag-ui/index.md` | SSE and UI protocol mapping | treat browser trust boundaries explicitly |
| Purview integration | `tutorials/plugins/use-purview-with-agent-framework-sdk.md` | policy/governance flow | use only when governance is a real requirement |
| Workflow as agent | `user-guide/workflows/as-agents.md` | wrap workflow behind `AIAgent` API | keep the workflow explicit in code and docs |
| DevUI smoke testing | `user-guide/devui/index.md` | local sample-driven testing | do not let it become production architecture |

## Source Pages

- `references/official-docs/tutorials/overview.md`
- `references/official-docs/tutorials/quick-start.md`
- `references/official-docs/tutorials/agents/run-agent.md`
- `references/official-docs/tutorials/workflows/simple-sequential-workflow.md`
- `references/official-docs/user-guide/hosting/index.md`
- `references/official-docs/integrations/ag-ui/index.md`
