# Tools and Tool Approval

## Tool Support Depends On The Concrete Agent

`AIAgent` itself does not promise a universal tool model. Tooling behavior comes from the actual agent type and the underlying service.

For most `.NET` work, `ChatClientAgent` is the practical default because it supports:

- custom function tools
- service-provided tools where the backend exposes them
- per-agent and per-run tool injection

## Tool Categories

| Tool Category | Source | Typical Use | Main Risk |
| --- | --- | --- | --- |
| Function tools | Your `.NET` methods exposed through `AIFunctionFactory.Create` | domain actions, lookups, side effects | poor contracts and unsafe side effects |
| Service-provided tools | Backend-specific `AITool` implementations | code interpreter, file search, managed web search, hosted MCP | portability and provider lock-in |
| Agent-as-tool | Another agent exposed as an `AIFunction` | bounded delegation | hiding orchestration complexity inside tool calls |
| MCP tools | Remote tool servers integrated into the agent | external tool ecosystems and context servers | trust, auth, and data exfiltration |

## Function Tool Design Rules

Function tools should be:

- narrow
- deterministic where possible
- clearly described
- explicit about side effects
- easy to audit

```csharp
[Description("Get the weather for a location.")]
static string GetWeather([Description("City or region.")] string location)
    => $"Weather in {location}: cloudy and 15C";

AIAgent agent = chatClient.AsAIAgent(
    instructions: "You are a helpful assistant.",
    tools: [AIFunctionFactory.Create(GetWeather)]);
```

Minimum hygiene:

- add `Description` to the method
- add `Description` to parameters
- avoid ambiguous names
- avoid giant "do everything" tools

## Per-Agent Versus Per-Run Tools

Register a tool at agent construction when:

- every run should see the tool
- the tool contract is stable
- the tool does not depend on request-scoped auth or tenant data

Register a tool per run when:

- authorization is request-specific
- the available tools depend on the user or tenant
- credentials are short-lived
- temporary capabilities should not persist

```csharp
var chatOptions = new ChatOptions
{
    Tools = [AIFunctionFactory.Create(GetWeather)]
};

var options = new ChatClientAgentRunOptions(chatOptions);
AgentResponse response = await agent.RunAsync(
    "What is the weather like in Amsterdam?",
    options: options);
```

## Service-Provided Tools

Hosted or provider-native tools are backend-specific.

Typical examples called out in the docs:

- code interpreter
- file search
- web search
- hosted MCP

These should be treated as provider features, not baseline framework guarantees.

## Approval Strategy

Use approval for:

- destructive writes
- money movement
- sensitive data access
- third-party calls that can leak data
- actions that have legal or operational consequences

If the backend does not offer built-in approvals:

1. use function middleware for gatekeeping
2. use workflows with request and response when human approval is a real state transition
3. log the attempted tool call whether it executes or not

## Agent As Tool

Use agent-as-tool when one agent needs a bounded specialist capability without promoting the relationship to a full workflow.

```csharp
AIAgent weatherAgent = chatClient.AsAIAgent(
    name: "WeatherAgent",
    description: "Answers weather questions.",
    instructions: "You answer questions about weather.",
    tools: [AIFunctionFactory.Create(GetWeather)]);

AIAgent coordinator = chatClient.AsAIAgent(
    instructions: "Delegate weather questions when needed.",
    tools: [weatherAgent.AsAIFunction()]);
```

Use this when:

- the delegated behavior is narrow
- the caller stays in control
- failures and retries do not need explicit workflow semantics

Escalate to workflows when:

- handoff logic matters
- retries and fallback paths matter
- multiple specialists coordinate in known patterns

## Tool Output Is Untrusted Input

Treat tool output as untrusted when it comes from:

- remote systems
- MCP servers
- generated code
- file or web search results
- any third-party service

Never assume a tool result is safe just because your agent called it.

## Common Tool Smells

- one agent with a huge tool inventory that no human can reason about
- tools with broad side effects and vague names
- credentials baked into long-lived tool registration
- no approval layer for dangerous tools
- mixing provider-native and custom tools without documenting which backend guarantees what

## Practical Tool Checklist

- Is the tool surface the minimum useful set?
- Does each risky tool have approval or denial behavior?
- Are per-run credentials actually per-run?
- Is the tool output logged or at least observable?
- Is the tool portable, or is it provider-specific by design?

## Source Pages

- `references/official-docs/user-guide/agents/agent-tools.md`
- `references/official-docs/tutorials/agents/function-tools.md`
- `references/official-docs/tutorials/agents/function-tools-approvals.md`
- `references/official-docs/tutorials/agents/agent-as-function-tool.md`
- `references/official-docs/tutorials/agents/agent-as-mcp-tool.md`
