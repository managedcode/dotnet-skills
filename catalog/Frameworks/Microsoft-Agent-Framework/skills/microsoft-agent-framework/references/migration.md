# Migration Notes

## Migrate The Architecture, Not Just The API Names

The biggest migration mistake is to treat Agent Framework as a namespace rename from Semantic Kernel or AutoGen. It is not.

The framework changes:

- how threads are created
- how tools are registered
- how responses are represented
- how workflows are modeled
- how hosting is layered

## Semantic Kernel To Agent Framework

### Concept Mapping

| Semantic Kernel Pattern | Agent Framework Pattern | Important Difference |
| --- | --- | --- |
| `Kernel`-centric agent composition | `AIAgent` or `ChatClientAgent` over `IChatClient` | the agent is no longer a thin wrapper around a `Kernel` |
| caller-created provider thread types | `await agent.GetNewThreadAsync()` | thread creation moves behind the agent abstraction |
| `InvokeAsync` / `InvokeStreamingAsync` | `RunAsync` / `RunStreamingAsync` | return models are different |
| `KernelFunction` plugins | `AIFunctionFactory.Create(...)` | tool registration is direct and agent-first |
| `KernelArguments` and prompt settings | `ChatClientAgentRunOptions` with `ChatOptions` | options become more localized to the agent type |
| plugin-heavy agent wiring | direct agent construction | less ceremony, but different extension points |

### Mechanical Rewrite Points

1. Move namespaces to `Microsoft.Agents.AI` and `Microsoft.Extensions.AI`.
2. Replace provider-specific thread construction with `GetNewThreadAsync()`.
3. Replace plugin-style tool registration with direct `AIFunctionFactory.Create(...)`.
4. Replace `Invoke*` calls with `Run*` calls.
5. Re-test response handling because the result model is not the same.

### Behavioral Shifts

- non-streaming now returns one `AgentResponse`, not a streaming-shaped sequence
- `AgentResponse` can include tool calls, tool results, and metadata, not just final text
- thread cleanup for hosted providers is provider-specific and may require the provider SDK
- Responses-based services are the forward-looking direction; Assistants-style hosted threads are no longer the main path

## AutoGen To Agent Framework

The AutoGen migration guide is Python-oriented, but the architectural lessons still matter for `.NET`.

| AutoGen Concept | Agent Framework Concept | Main Shift |
| --- | --- | --- |
| team orchestration loops | typed `Workflow` graphs | structure becomes explicit and typed |
| group chat coordination | group chat or Magentic orchestrations | still available, but modeled as workflow patterns |
| event-driven human loops | request and response via workflow boundaries | external interaction becomes a first-class workflow primitive |
| runtime recovery and resume | checkpoints | recovery is designed in, not bolted on |

The `.NET` takeaway is to translate concepts, not to fabricate `.NET` APIs from Python examples.

## Migration Sequence That Usually Works

1. Re-evaluate whether the old design should stay a single agent.
2. Decide whether the new design should be:
   - single `ChatClientAgent`
   - typed `Workflow`
   - durable orchestration
3. Replace thread creation and persistence first.
4. Replace tool registration next.
5. Re-test streaming and non-streaming behavior.
6. Revisit hosting last.

## High-Risk Areas During Migration

- Assuming old thread IDs map cleanly to new thread models
- Blindly porting plugin catalogs into giant tool sets
- Treating Responses and Chat Completions as interchangeable
- Forgetting provider-specific cleanup for hosted threads
- Hiding old orchestration loops inside prompts instead of moving them to workflows

## Migration Checklist

- Is the target architecture smaller or clearer than the source one?
- Are tool approvals and side-effect rules still explicit?
- Are serialized threads stored as full opaque objects?
- Have streaming and non-streaming response consumers been updated?
- Has the hosting surface been re-chosen deliberately instead of copied forward?

## Source Pages

- `references/official-docs/migration-guide/from-semantic-kernel/index.md`
- `references/official-docs/migration-guide/from-semantic-kernel/samples.md`
- `references/official-docs/migration-guide/from-autogen/index.md`
