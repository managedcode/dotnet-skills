---
title: Compaction
description: Learn how to manage conversation history size with compaction strategies that keep context within token limits.
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 07/30/2026
ms.service: agent-framework
---

<!--
    Feature parity table – highlight impactful SDK differences between C#, Python, and Go.
  Keep in sync when features are added or removed.

    | Feature                                  | C# | Python | Go | Notes |
    |------------------------------------------|:--:|:------:|:--:|-------|
    | Truncation strategy                      | ✅ |   ✅   | ❌ | Not shown for Go |
    | Sliding window strategy                  | ✅ |   ✅   | ✅ |       |
    | Tool-result collapse strategy            | ✅ |   ✅   | ✅ |       |
    | Summarization strategy                   | ✅ |   ✅   | ❌ | Python adds a bounded summarizer input budget |
    | Selective tool-call exclusion strategy   | ❌ |   ✅   | ❌ | Python-only: fully drops older tool-call groups; C# and Go tool-result strategies collapse instead |
    | Trigger / predicate system               | ✅ |   ❌   | ✅ | C# and Go expose trigger predicates; Python strategies use internal parameters |
    | Composed pipeline strategy               | ✅ |   ✅   | ✅ | C#: PipelineCompactionStrategy; Python: TokenBudgetComposedStrategy; Go: PipelineStrategy |
    | Direct Agent strategy/tokenizer setup     | ❌ |   ✅   | ❌ | Python: configure `Agent(compaction_strategy=..., tokenizer=...)` |
    | Harness convenience wiring               | ✅ |   ✅   | ❌ | C#: HarnessAgentOptions; Python: create_harness_agent |
-->

# Compaction

As conversations grow, the token count of the chat history can exceed model context windows or drive up costs. Compaction strategies reduce the size of conversation history while preserving important context, so agents can continue functioning over long-running interactions.

:::zone pivot="programming-language-csharp"

> [!IMPORTANT]
> The compaction framework is currently experimental. To use it, you will need to add `#pragma warning disable MAAI001`.

:::zone-end

:::zone pivot="programming-language-python"

> [!IMPORTANT]
> The compaction framework is currently experimental in Python. Import compaction types from `agent_framework`.

:::zone-end

:::zone pivot="programming-language-go"

Go supports compaction through the `agent/compaction` package. Register a compaction strategy as an `agent.ContextProvider` so it can compact session history before each run.

:::zone-end

## Why compaction matters

Every call to an LLM includes the full conversation history. Without compaction:

- **Token limits** — Conversations eventually exceed the model's context window, causing errors.
- **Cost** — Larger prompts consume more tokens, increasing API costs.
- **Latency** — More input tokens means slower response times.

Compaction solves these problems by selectively removing, collapsing, or summarizing older portions of the conversation.

## Core concepts

### Applicability: In-memory history agents only

Compaction applies only to agents that manage their own conversation history in memory. Agents that rely on service-managed context or conversation state do not benefit from compaction because the service already handles context management. Examples of service-managed agents include:

- **Foundry Agents** — context is managed server-side by the Microsoft Foundry service.
- **Responses API with store enabled** (the default) — conversation state is stored and managed by the OpenAI service.
- **Copilot Studio agents** — conversation context is maintained by the Copilot Studio service.

For these agent types, configuring a compaction strategy has no effect. Compaction is only relevant when the agent maintains its own in-memory message list and passes the full history to the model on each call.

:::zone pivot="programming-language-csharp"

Compaction operates on a **`MessageIndex`** — a structured view of the flat message list that groups messages into atomic units called **`MessageGroup`** instances. Each group tracks its message count, byte count, and estimated token count.

### Message groups

A `MessageGroup` represents logically related messages that must be kept or removed together. For example, an assistant message containing tool calls and its corresponding tool result messages form an atomic group — removing one without the other would cause LLM API errors.

Each group has a `MessageGroupKind`:

| Kind | Description |
|---|---|
| `System` | One or more system messages. Always preserved during compaction. |
| `User` | A single user message that starts a new turn. |
| `AssistantText` | A plain assistant text response (no tool calls). |
| `ToolCall` | An assistant message with tool calls and the corresponding tool result messages, treated as an atomic unit. |
| `Summary` | A condensed message produced by summarization compaction. |

### Triggers

A `CompactionTrigger` is a delegate that evaluates whether compaction should proceed based on current `MessageIndex` metrics:

```csharp
public delegate bool CompactionTrigger(MessageIndex index);
```

The `CompactionTriggers` class provides common factory methods:

| Trigger | Fires when |
|---|---|
| `CompactionTriggers.Always` | Every time (unconditionally). |
| `CompactionTriggers.Never` | Never (disables compaction). |
| `CompactionTriggers.TokensExceed(maxTokens)` | Included token count exceeds the threshold. |
| `CompactionTriggers.MessagesExceed(maxMessages)` | Included message count exceeds the threshold. |
| `CompactionTriggers.TurnsExceed(maxTurns)` | Included user turn count exceeds the threshold. |
| `CompactionTriggers.GroupsExceed(maxGroups)` | Included group count exceeds the threshold. |
| `CompactionTriggers.HasToolCalls()` | At least one non-excluded tool call group exists. |

Combine triggers with `CompactionTriggers.All(...)` (logical AND) or `CompactionTriggers.Any(...)` (logical OR):

```csharp
// Compact only when there are tool calls AND tokens exceed 2000
CompactionTrigger trigger = CompactionTriggers.All(
    CompactionTriggers.HasToolCalls(),
    CompactionTriggers.TokensExceed(2000));
```

### Trigger vs. target

Every strategy has two predicates:

- **Trigger** — Controls *when* compaction begins. If the trigger returns `false`, the strategy is skipped entirely.
- **Target** — Controls *when* compaction stops. Strategies incrementally exclude groups and re-evaluate the target after each step, stopping as soon as the target returns `true`.

When no target is specified, it defaults to the inverse of the trigger — compaction stops as soon as the trigger condition would no longer fire.

:::zone-end

:::zone pivot="programming-language-python"

Compaction operates on a flat list of `Message` objects. Messages are annotated with lightweight group metadata, and strategies mutate those annotations in place to mark groups as excluded before the message list is projected to the model.

### Message groups

Messages are grouped into atomic units. Each group is assigned a `GroupKind`:

| Kind | Description |
|---|---|
| `system` | System messages. Always preserved during compaction. |
| `user` | A single user message. |
| `assistant_text` | A plain assistant text response (no function calls). |
| `tool_call` | An assistant message with function calls plus the corresponding tool result messages, treated as an atomic unit. |

### Compaction strategies

A `CompactionStrategy` is a protocol — any `async` callable that accepts a `list[Message]` and mutates it in place, returning `True` when it changed anything:

```python
class CompactionStrategy(Protocol):
    async def __call__(self, messages: list[Message]) -> bool: ...
```

### Tokenizer

Token-aware strategies accept a `TokenizerProtocol` implementation. The built-in `CharacterEstimatorTokenizer` uses a 4-character-per-token heuristic:

```python
from agent_framework import CharacterEstimatorTokenizer

tokenizer = CharacterEstimatorTokenizer()
```

Pass a custom tokenizer when you need accurate token counts for a specific model's encoding.

:::zone-end

:::zone pivot="programming-language-go"

Compaction runs as a context provider. Strategies can inspect session history before each run and reduce older message groups while preserving the newest context.

The Go package includes strategy types such as `ToolResultStrategy`, `SlidingWindowStrategy`, and `PipelineStrategy`, plus trigger helpers such as `MessagesExceed` and `TurnsExceed`.

:::zone-end

## Compaction strategies

:::zone pivot="programming-language-csharp"

All strategies inherit from the abstract `CompactionStrategy` base class. Each strategy preserves system messages and respects a `MinimumPreserved` floor that protects the most-recent non-system groups from removal.

:::zone-end

:::zone pivot="programming-language-python"

Compaction strategies are imported from `agent_framework`.

:::zone-end

:::zone pivot="programming-language-go"

Compaction strategies are imported from `github.com/microsoft/agent-framework-go/agent/compaction`.

:::zone-end

:::zone pivot="programming-language-csharp"
### TruncationCompactionStrategy
:::zone-end

:::zone pivot="programming-language-python"
### TruncationStrategy
:::zone-end

The most straightforward approach: removes the oldest non-system message groups until the target condition is met.

- Respects atomic group boundaries (tool call and result messages are removed together).
- Best for hard token-budget backstops.

:::zone pivot="programming-language-csharp"

- `MinimumPreserved` defaults to `32`.

```csharp
// Drop oldest groups when tokens exceed 32K, keeping at least 10 recent groups
TruncationCompactionStrategy truncation = new(
    trigger: CompactionTriggers.TokensExceed(0x8000),
    minimumPreserved: 10);
```

:::zone-end

:::zone pivot="programming-language-python"

- When a `tokenizer` is provided, the metric is token count; otherwise it is included message count.
- `preserve_system` defaults to `True`.

```python
from agent_framework import CharacterEstimatorTokenizer, TruncationStrategy

# Exclude oldest groups when tokens exceed 32 000, trimming to 16 000
truncation = TruncationStrategy(
    max_n=32_000,
    compact_to=16_000,
    tokenizer=CharacterEstimatorTokenizer(),
)
```

:::zone-end

:::zone pivot="programming-language-csharp"
### SlidingWindowCompactionStrategy
:::zone-end

:::zone pivot="programming-language-python"
### SlidingWindowStrategy
:::zone-end

Removes older conversation content to keep only the most recent window of exchanges, respecting logical conversation units rather than arbitrary message counts. System messages are preserved throughout.

- Best for bounding conversation length predictably.

:::zone pivot="programming-language-csharp"

Removes the oldest user **turns** and their associated response groups, operating on logical turn boundaries rather than individual groups.

- A turn starts with a user message and includes all subsequent assistant and tool-call groups until the next user message.
- `MinimumPreserved` defaults to `1` (preserves at least the most recent non-system group).

```csharp
// Keep only the last 4 user turns
SlidingWindowCompactionStrategy slidingWindow = new(
    trigger: CompactionTriggers.TurnsExceed(4));
```

:::zone-end

:::zone pivot="programming-language-python"

Keeps only the most recent `keep_last_groups` non-system groups, excluding everything older.

- `preserve_system` defaults to `True`.

```python
from agent_framework import SlidingWindowStrategy

# Keep only the last 20 non-system groups
sliding_window = SlidingWindowStrategy(keep_last_groups=20)
```

:::zone-end

### ToolResultCompactionStrategy

Collapses older tool-call groups into compact summary messages, preserving a readable trace without the full message overhead.

- Does not touch user messages or plain assistant responses.
- Best as a first-pass strategy to reclaim space from verbose tool results.

:::zone pivot="programming-language-csharp"

- Replaces multi-message tool call groups (assistant call + tool results) with a short summary like `[Tool calls: get_weather, search_docs]`.
- `MinimumPreserved` defaults to `2`, ensuring the current turn's tool interactions remain visible.

```csharp
// Collapse old tool results when tokens exceed 512
ToolResultCompactionStrategy toolCompaction = new(
    trigger: CompactionTriggers.TokensExceed(0x200));
```

:::zone-end

:::zone pivot="programming-language-python"

- Collapses into compact summary messages such as `[Tool results: get_weather: sunny, 18°C]`.
- The most recent `keep_last_tool_call_groups` tool-call groups are left untouched.

```python
from agent_framework import ToolResultCompactionStrategy

# Collapse all but the newest tool-call group
tool_result = ToolResultCompactionStrategy(keep_last_tool_call_groups=1)
```

:::zone-end

:::zone pivot="programming-language-csharp"
### SummarizationCompactionStrategy
:::zone-end

:::zone pivot="programming-language-python"
### SummarizationStrategy
:::zone-end

Uses an LLM to summarize older portions of the conversation, replacing them with a single summary message.

- A default prompt preserves key facts, decisions, user preferences, and tool call outcomes.
- Requires a separate LLM client for summarization — a smaller, faster model is recommended.
- Best for preserving conversational context while significantly reducing token count.
- You can provide a custom summarization prompt.

:::zone pivot="programming-language-csharp"

- Protects system messages and the most recent `MinimumPreserved` non-system groups (default: `4`).
- Sends the older messages to a separate `IChatClient` with a summarization prompt, then inserts the summary as a `MessageGroupKind.Summary` group.

```csharp
// Summarize older messages when tokens exceed 1280, keeping the last 4 groups
SummarizationCompactionStrategy summarization = new(
    chatClient: summarizerChatClient,
    trigger: CompactionTriggers.TokensExceed(0x500),
    minimumPreserved: 4);
```

You can provide a custom summarization prompt:

```csharp
SummarizationCompactionStrategy summarization = new(
    chatClient: summarizerChatClient,
    trigger: CompactionTriggers.TokensExceed(0x500),
    summarizationPrompt: "Summarize the key decisions and user preferences only.");
```

:::zone-end

:::zone pivot="programming-language-python"

- Triggers when included non-system message count exceeds `target_count + threshold`.
- Retains recent messages near `target_count`, subject to message-group boundaries; summarizes the oldest complete groups that fit the summarizer input budget.
- Requires a `SupportsChatGetResponse` client.
- Bounds the summarizer prompt and transcript to 8,000 estimated tokens by default. It selects complete message groups, so a group is never split to fit the budget.
- Set `max_summary_input_tokens=None` to disable the summarizer input bound, or pass `tokenizer=` when you need model-specific token counting. If no complete group fits, summarization is skipped and the existing history remains unchanged.

```python
from agent_framework import SummarizationStrategy

# Summarize when non-system message count exceeds 6, retaining the 4 newest
summarization = SummarizationStrategy(
    client=summarizer_client,
    target_count=4,
    threshold=2,
)
```

Provide a custom summarization prompt:

```python
summarization = SummarizationStrategy(
    client=summarizer_client,
    target_count=4,
    prompt="Summarize the key decisions and user preferences only.",
)
```

Control the summarizer request budget independently from the retained-history target:

```python
from agent_framework import CharacterEstimatorTokenizer, SummarizationStrategy

summarization = SummarizationStrategy(
    client=summarizer_client,
    target_count=4,
    threshold=2,
    max_summary_input_tokens=16_000,
    tokenizer=CharacterEstimatorTokenizer(),
)
```

:::zone-end

:::zone pivot="programming-language-csharp"

### PipelineCompactionStrategy

Composes multiple strategies into a sequential pipeline. Each strategy operates on the result of the previous one, enabling layered compaction from gentle to aggressive.

- The pipeline's own trigger is `CompactionTriggers.Always` — each child strategy evaluates its own trigger independently.
- Strategies execute in order, so put the gentlest strategies first.

```csharp
PipelineCompactionStrategy pipeline = new(
    new ToolResultCompactionStrategy(CompactionTriggers.TokensExceed(0x200)),
    new SummarizationCompactionStrategy(summarizerChatClient, CompactionTriggers.TokensExceed(0x500)),
    new SlidingWindowCompactionStrategy(CompactionTriggers.TurnsExceed(4)),
    new TruncationCompactionStrategy(CompactionTriggers.TokensExceed(0x8000)));
```

This pipeline:

1. Collapses old tool results (gentle).
2. Summarizes older conversation spans (moderate).
3. Keeps only the last 4 user turns (aggressive).
4. Drops oldest groups if still over budget (emergency backstop).

:::zone-end

:::zone pivot="programming-language-python"

### SelectiveToolCallCompactionStrategy

Fully excludes older tool-call groups, keeping only the last `keep_last_tool_call_groups`.

- Does not touch user or plain assistant messages.
- Best when tool chatter dominates token usage and the full tool history is not needed.

```python
from agent_framework import SelectiveToolCallCompactionStrategy

# Keep only the most recent tool-call group
selective_tool = SelectiveToolCallCompactionStrategy(keep_last_tool_call_groups=1)
```

### TokenBudgetComposedStrategy

Composes multiple strategies into a sequential pipeline driven by a token budget. Each child strategy runs in order, stopping early once the budget is satisfied. A built-in fallback excludes the oldest groups if the strategies alone cannot reach the target.

- Strategies execute in order; place the gentlest strategies first.
- `early_stop=True` (the default) stops as soon as the token budget is satisfied.

```python
from agent_framework import (
    CharacterEstimatorTokenizer,
    SelectiveToolCallCompactionStrategy,
    SlidingWindowStrategy,
    SummarizationStrategy,
    TokenBudgetComposedStrategy,
    ToolResultCompactionStrategy,
)

tokenizer = CharacterEstimatorTokenizer()

pipeline = TokenBudgetComposedStrategy(
    token_budget=16_000,
    tokenizer=tokenizer,
    strategies=[
        ToolResultCompactionStrategy(keep_last_tool_call_groups=1),
        SummarizationStrategy(client=summarizer_client, target_count=4, threshold=2),
        SlidingWindowStrategy(keep_last_groups=20),
    ],
)
```

This pipeline:

1. Collapses old tool results (gentle).
2. Summarizes older conversation spans (moderate).
3. Keeps only the last 20 groups (aggressive).
4. Falls back to oldest-first exclusion if still over budget (emergency backstop).

:::zone-end

## Using compaction with an agent

:::zone pivot="programming-language-csharp"

Wrap a compaction strategy in a `CompactionProvider` and register it as an `AIContextProvider`. Pass either a single strategy or a `PipelineCompactionStrategy` to the constructor.

### Registering with the builder API

Register the provider on the `ChatClientBuilder` using `UseAIContextProviders`. The provider runs inside the tool-calling loop, compacting messages before each LLM call.

```csharp
IChatClient agentChatClient = openAIClient.GetChatClient(deploymentName).AsIChatClient();
IChatClient summarizerChatClient = openAIClient.GetChatClient(deploymentName).AsIChatClient();

PipelineCompactionStrategy compactionPipeline =
    new(
        new ToolResultCompactionStrategy(CompactionTriggers.TokensExceed(0x200)),
        new SummarizationCompactionStrategy(summarizerChatClient, CompactionTriggers.TokensExceed(0x500)),
        new SlidingWindowCompactionStrategy(CompactionTriggers.TurnsExceed(4)),
        new TruncationCompactionStrategy(CompactionTriggers.TokensExceed(0x8000)));

AIAgent agent =
    agentChatClient
        .AsBuilder()
        .UseAIContextProviders(new CompactionProvider(compactionPipeline))
        .BuildAIAgent(
            new ChatClientAgentOptions
            {
                Name = "ShoppingAssistant",
                ChatOptions = new()
                {
                    Instructions = "You are a helpful shopping assistant.",
                    Tools = [AIFunctionFactory.Create(LookupPrice)],
                },
            });

AgentSession session = await agent.CreateSessionAsync();
Console.WriteLine(await agent.RunAsync("What's the price of a laptop?", session));
```

> [!TIP]
> Use a smaller, cheaper model (such as `gpt-4o-mini`) for the summarization chat client to reduce costs while maintaining summary quality.

If only one strategy is needed, pass it directly to `CompactionProvider` without wrapping it in a `PipelineCompactionStrategy`:

```csharp
agentChatClient
    .AsBuilder()
    .UseAIContextProviders(new CompactionProvider(
        new SlidingWindowCompactionStrategy(CompactionTriggers.TurnsExceed(20))))
    .BuildAIAgent(...);
```

### Registering through `ChatClientAgentOptions`

The provider can also be specified directly on `ChatClientAgentOptions.AIContextProviders`:

```csharp
AIAgent agent = agentChatClient
    .AsBuilder()
    .BuildAIAgent(new ChatClientAgentOptions
    {
        AIContextProviders = [new CompactionProvider(compactionPipeline)]
    });
```

> [!NOTE]
> When registered through `ChatClientAgentOptions`, the `CompactionProvider` is **not** engaged during the tool-calling loop. Agent-level context providers run before chat history is stored, so any synthetic summary messages produced by `CompactionProvider` can become part of the persisted history when using `ChatHistoryProvider`. To compact only the in-flight request context while preserving the original stored history, register the provider on the `ChatClientBuilder` via `UseAIContextProviders(...)` instead.

### Ad-hoc compaction

`CompactionProvider.CompactAsync` applies a strategy to an arbitrary message list without an active agent session:

```csharp
IEnumerable<ChatMessage> compacted = await CompactionProvider.CompactAsync(
    new TruncationCompactionStrategy(CompactionTriggers.TokensExceed(8000)),
    existingMessages);
```

:::zone-end

:::zone pivot="programming-language-python"

Configure the selected compaction strategy and tokenizer directly on `Agent`. Agent-level values override defaults configured on the underlying chat client, and a single `agent.run(...)` call can override both again.

### Registering with an agent

```python
from agent_framework import (
    Agent,
    CharacterEstimatorTokenizer,
    SlidingWindowStrategy,
    SummarizationStrategy,
    TokenBudgetComposedStrategy,
    ToolResultCompactionStrategy,
    TruncationStrategy,
)

tokenizer = CharacterEstimatorTokenizer()

strategy = TokenBudgetComposedStrategy(
    token_budget=16_000,
    tokenizer=tokenizer,
    strategies=[
        ToolResultCompactionStrategy(keep_last_tool_call_groups=1),
        SummarizationStrategy(client=summarizer_client, target_count=4, threshold=2),
        SlidingWindowStrategy(keep_last_groups=20),
    ],
)

agent = Agent(
    client=client,
    name="ShoppingAssistant",
    instructions="You are a helpful shopping assistant.",
    compaction_strategy=strategy,
    tokenizer=tokenizer,
)

session = agent.create_session()
print(await agent.run("What's the price of a laptop?", session=session))
```

> [!TIP]
> Use a smaller, cheaper model (such as `gpt-4o-mini`) for the summarization client to reduce costs while maintaining summary quality.

If only one strategy is needed, pass it directly:

```python
agent = Agent(
    client=client,
    compaction_strategy=SlidingWindowStrategy(keep_last_groups=20),
    tokenizer=CharacterEstimatorTokenizer(),
)
```

### Override compaction for one run

Pass `compaction_strategy` and `tokenizer` to `agent.run(...)` when one request needs a different policy:

```python
response = await agent.run(
    "Summarize the rollout risks.",
    compaction_strategy=TruncationStrategy(max_n=8_000, compact_to=4_000),
    tokenizer=tokenizer,
)
```

### Ad-hoc compaction

`apply_compaction` applies a strategy to an arbitrary message list outside an active agent session:

```python
from agent_framework import apply_compaction, TruncationStrategy, CharacterEstimatorTokenizer

tokenizer = CharacterEstimatorTokenizer()

compacted = await apply_compaction(
    messages,
    strategy=TruncationStrategy(
        max_n=8_000,
        compact_to=4_000,
        tokenizer=tokenizer,
    ),
    tokenizer=tokenizer,
)
```

:::zone-end

:::zone pivot="programming-language-go"

Create a compaction context provider and add it to the agent's context providers:

```go
import (
    "github.com/microsoft/agent-framework-go/agent"
    "github.com/microsoft/agent-framework-go/agent/compaction"
    "github.com/microsoft/agent-framework-go/provider/foundryprovider"
)

compactionProvider := compaction.NewContextProvider(compaction.ContextProviderConfig{
    Strategy: &compaction.PipelineStrategy{
        Strategies: []compaction.Strategy{
            &compaction.ToolResultStrategy{
                Trigger:                compaction.MessagesExceed(7),
                MinimumPreservedGroups: 4,
            },
            &compaction.SlidingWindowStrategy{
                Trigger:               compaction.TurnsExceed(4),
                MinimumPreservedTurns: 4,
            },
        },
    },
})

a := foundryprovider.NewAgent(endpoint, token, foundryprovider.ModelDeployment(model), foundryprovider.AgentConfig{
    Config: agent.Config{
        ContextProviders: []agent.ContextProvider{compactionProvider},
    },
})
```

> [!TIP]
> See the [compaction pipeline sample](https://github.com/microsoft/agent-framework-go/blob/main/examples/02-agents/agents/step18_compaction_pipeline/main.go) for a complete runnable example.

:::zone-end

## Use compaction with Harness Agent

The manual setup above gives full control over where a compaction provider runs. Harness Agent instead wires compaction into its per-service-call history pipeline so long tool-calling loops can compact between model calls.

:::zone pivot="programming-language-csharp"

Compaction is off by default. Set both `HarnessAgentOptions.MaxContextWindowTokens` and `MaxOutputTokens` to create a default `ContextWindowCompactionStrategy`, or set `CompactionStrategy` to supply your own strategy.

```csharp
HarnessAgent agent = chatClient.AsHarnessAgent(new HarnessAgentOptions
{
    MaxContextWindowTokens = 128_000,
    MaxOutputTokens = 16_384,
});
```

The harness runs the resolved strategy before every model call in the function-invocation loop. When it also creates the default `InMemoryChatHistoryProvider`, it configures that provider with the strategy's chat reducer. A custom `ChatHistoryProvider` is used as supplied. `DisableCompaction = true` overrides the token settings and `CompactionStrategy`; `MaxOutputTokens` still sets `ChatOptions.MaxOutputTokens` when compaction is enabled or disabled.

`chatClient.AsHarnessAgent(options)` and `new HarnessAgent(chatClient, options)` use the same `HarnessAgentOptions`.

:::zone-end

:::zone pivot="programming-language-python"

Compaction is off by default unless both `max_context_window_tokens` and `max_output_tokens` are set, or a custom phase strategy is supplied.

```python
agent = create_harness_agent(
    client,
    max_context_window_tokens=128_000,
    max_output_tokens=16_384,
)
```

With the token settings, the harness reuses one `ContextWindowCompactionStrategy` for both phases: the before phase runs before every model call inside the tool loop, while the after phase compacts persisted history after the run. Override either phase independently with `before_compaction_strategy` or `after_compaction_strategy`; set `tokenizer=` for a custom tokenizer, `history_provider=` for another history store, or `disable_compaction=True` to disable both phases. `max_output_tokens` also supplies the default `max_tokens` chat option.

:::zone-end

:::zone pivot="programming-language-go"

Harness Agent isn't currently available in the Go SDK. Register a compaction context provider manually as shown above.

:::zone-end

## Choosing a strategy

:::zone pivot="programming-language-csharp"

| Strategy | Aggressiveness | Preserves context | Requires LLM | Best for |
|---|---|---|---|---|
| `ToolResultCompactionStrategy` | Low | High — only collapses tool results | No | Reclaiming space from verbose tool output |
| `SummarizationCompactionStrategy` | Medium | Medium — replaces history with a summary | Yes | Long conversations where context matters |
| `SlidingWindowCompactionStrategy` | High | Low — drops entire turns | No | Hard turn-count limits |
| `TruncationCompactionStrategy` | High | Low — drops oldest groups | No | Emergency token-budget backstops |
| `PipelineCompactionStrategy` | Configurable | Depends on child strategies | Depends | Layered compaction with multiple fallbacks |

:::zone-end

:::zone pivot="programming-language-python"

| Strategy | Aggressiveness | Preserves context | Requires LLM | Best for |
|---|---|---|---|---|
| `ToolResultCompactionStrategy` | Low | High — collapses tool results into summary messages | No | Reclaiming space from verbose tool output |
| `SelectiveToolCallCompactionStrategy` | Low–Medium | Medium — fully excludes old tool-call groups | No | Removing tool history when results are no longer needed |
| `SummarizationStrategy` | Medium | Medium — replaces history with a summary | Yes | Long conversations where context matters |
| `SlidingWindowStrategy` | High | Low — drops oldest groups | No | Hard group-count limits |
| `TruncationStrategy` | High | Low — drops oldest groups | No | Emergency message- or token-budget backstops |
| `TokenBudgetComposedStrategy` | Configurable | Depends on child strategies | Depends | Layered compaction with a token-budget goal and multiple fallbacks |

:::zone-end

:::zone pivot="programming-language-go"

| Strategy | Aggressiveness | Preserves context | Requires LLM | Best for |
|---|---|---|---|---|
| `ToolResultStrategy` | Low | High - collapses verbose tool results | No | Reclaiming space from verbose tool output |
| `SlidingWindowStrategy` | High | Low - keeps the most recent turns | No | Hard turn-count limits |
| `PipelineStrategy` | Configurable | Depends on child strategies | Depends | Layered compaction with multiple fallbacks |

:::zone-end
## Next steps

> [!div class="nextstepaction"]
> [Middleware](../middleware/index.md)
