# Hosting and Integration Surfaces

## Keep Hosting Separate From Core Logic

The core rule is simple:

- the agent or workflow is your core execution model
- hosting libraries are protocol adapters around it

Do not choose your architecture because a protocol package exists. Choose the runtime model first, then attach the hosting surface you actually need.

## Core Hosting Library

`Microsoft.Agents.AI.Hosting` is the base ASP.NET Core hosting layer.

Use it to:

- register `AIAgent` instances in DI
- register workflows
- attach tools and thread stores
- expose workflows as `AIAgent` surfaces when a protocol needs an agent

Representative shape:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(chatClient);

var pirateAgent = builder.AddAIAgent(
    "pirate",
    instructions: "You are a pirate. Speak like a pirate.");

var workflow = builder.AddWorkflow("science-workflow", (sp, key) => { /* build workflow */ })
    .AddAsAIAgent();
```

## Hosted Builder Extensions That Matter

The official docs repeatedly rely on these extensions:

- `.WithAITool(...)`
- `.WithInMemoryThreadStore()`
- `.AddAsAIAgent()` for workflows

That means the hosting layer is not just for HTTP exposure. It is also the composition point for common infrastructure around the agent.

## Protocol Adapter Matrix

| Surface | Package Family | Use It For | Key Rule |
| --- | --- | --- | --- |
| Core hosting | `Microsoft.Agents.AI.Hosting` | DI registration and local hosting composition | Start here |
| OpenAI-compatible HTTP | `Microsoft.Agents.AI.Hosting.OpenAI` | Chat Completions, Responses, Conversations endpoints | Prefer Responses for new work |
| A2A | `Microsoft.Agents.AI.Hosting.A2A` and `.AspNetCore` | agent-to-agent interoperability | Agent cards and task semantics matter |
| AG-UI | `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` | rich web/mobile UI protocols | Treat browser input as hostile unless mediated |
| Azure Functions durable | `Microsoft.Agents.AI.Hosting.AzureFunctions` | long-running durable hosting | Choose only for real durability needs |

## OpenAI-Compatible Hosting

The docs expose three related protocol families:

- Chat Completions
- Responses
- Conversations

Key builder and mapping calls:

- `builder.AddOpenAIChatCompletions()`
- `app.MapOpenAIChatCompletions(agent)`
- `builder.AddOpenAIResponses()`
- `app.MapOpenAIResponses(agent)`
- `builder.AddOpenAIConversations()`
- `app.MapOpenAIConversations()`

Choose Responses when:

- building new endpoints
- you want richer response semantics
- background responses or server-side conversation support matter

Choose Chat Completions when:

- integrating with existing clients that already speak that shape
- the endpoint is intentionally simple and stateless

## A2A Hosting

A2A is the right surface when the caller is another agent platform rather than a generic HTTP app.

Representative mapping:

```csharp
app.MapA2A(agent, "/a2a/my-agent", agentCard: new()
{
    Name = "My Agent",
    Description = "A helpful agent.",
    Version = "1.0"
});
```

A2A adds:

- agent discovery via agent cards
- message-based interoperability
- long-running task semantics
- cross-framework agent communication

If your real problem is tool exchange, use MCP instead. If your real problem is human UI, use AG-UI instead.

## AG-UI Hosting

AG-UI is for rich human-facing agent interfaces over HTTP plus SSE.

Representative mapping:

```csharp
app.MapAGUI("/", agent);
```

What AG-UI adds beyond direct agent usage:

- remote service hosting
- SSE streaming for UI updates
- thread and state synchronization
- approval workflows
- backend and frontend tool rendering patterns

Important security rule from the docs:

- do not expose AG-UI directly to untrusted browser clients without a trusted frontend mediation layer

## Durable Azure Functions Hosting

Use `Microsoft.Agents.AI.Hosting.AzureFunctions` only when durable execution is a real requirement.

Representative shape:

```csharp
using IHost app = FunctionsApplication
    .CreateBuilder(args)
    .ConfigureFunctionsWebApplication()
    .ConfigureDurableAgents(options => options.AddAIAgent(agent))
    .Build();
```

This is the right path for:

- replayable orchestration
- persistent threads
- failure recovery across long runs
- serverless Azure hosting

## Purview Integration

The official docs also call out `Microsoft.Agents.AI.Purview`.

Use it when:

- prompts and responses need governance checks
- policy enforcement or audit requirements are enterprise-critical
- your rollout requires explicit compliance integration

This is not a universal default. It is a targeted enterprise control layer.

## Production Rules

- Keep the in-process agent or workflow protocol-agnostic.
- Expose one clear protocol surface per endpoint.
- Use workflows-as-agents only when a protocol layer requires an `AIAgent`.
- Keep DevUI separate from production hosting.
- Document the trust boundary for AG-UI and MCP explicitly.

## Source Pages

- `references/official-docs/user-guide/hosting/index.md`
- `references/official-docs/user-guide/hosting/openai-integration.md`
- `references/official-docs/user-guide/hosting/agent-to-agent-integration.md`
- `references/official-docs/integrations/ag-ui/index.md`
- `references/official-docs/integrations/ag-ui/security-considerations.md`
- `references/official-docs/tutorials/agents/create-and-run-durable-agent.md`
- `references/official-docs/tutorials/plugins/use-purview-with-agent-framework-sdk.md`
