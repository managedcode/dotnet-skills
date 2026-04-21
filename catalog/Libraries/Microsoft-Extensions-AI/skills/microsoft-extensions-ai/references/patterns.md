# Microsoft.Extensions.AI Patterns

## Package Selection

- Applications and services should usually reference `Microsoft.Extensions.AI`.
- Provider libraries and reusable connectors should usually reference `Microsoft.Extensions.AI.Abstractions`.
- Add `Microsoft.Extensions.VectorData.Abstractions` when you need vector-store CRUD or search.
- Add `Microsoft.Extensions.DataIngestion` when you need document ingestion and preparation for RAG.
- Add `Microsoft.Extensions.AI.Evaluation.*` when prompts, tool use, or safety need measurable regression checks.

## `IChatClient` Request Model

- Use `GetResponseAsync` for whole responses and `GetStreamingResponseAsync` for UI or interactive console streaming.
- Treat `ChatResponse.Messages` as the provider-independent response payload. Do not assume a single plain-text string is the only output.
- Use `ChatOptions` for model selection, temperature, tools, additional provider properties, and raw provider-specific options.
- For stateless providers, replay the relevant message history each turn.
- For stateful providers, propagate `ConversationId` from `ChatResponse` to `ChatOptions` instead of manually resending all prior turns.

## Middleware and Builder Composition

- Build chat pipelines explicitly with `ChatClientBuilder`.
- Keep middleware order deliberate. A common pattern is: configure options, add logging or telemetry, add caching, then add function invocation.
- Use keyed DI registrations when the app needs multiple chat clients or multiple model classes.
- Prefer cross-cutting middleware over ad-hoc wrappers scattered through feature code.

## Tool Calling

- Describe tools with `AIFunction` and `AIFunctionFactory`.
- Use `FunctionInvokingChatClient` when you want automatic tool invocation instead of manually inspecting tool-related message content.
- Keep tool registration scoped to the current conversation or job. Tool descriptions count against token limits and can become a hidden cost/latency tax when the list grows.
- Remember that tools can be local .NET methods, external APIs, or MCP-backed operations. The model chooses; your application still validates arguments and decides whether the call is safe to execute.
- Pass ambient data through closures, `ChatOptions.AdditionalProperties`, `AIFunctionArguments.Context`, or DI, depending on lifetime and ownership.
- Validate invalid tool input explicitly. Do not trust the model to always produce perfectly shaped arguments.
- Keep side effects narrow, auditable, and guarded outside the prompt.

## Data Ingestion Pipelines

- Use `Microsoft.Extensions.DataIngestion` when the document-processing side of RAG matters as much as retrieval or prompting.
- Model the flow explicitly: document reader -> document processor -> chunker -> chunk processor -> writer.
- Start from `IngestionDocument` and choose built-in readers like MarkItDown or Markdig instead of ad-hoc file-to-string helpers when document fidelity matters.
- Use `ImageAlternativeTextEnricher` at the document level and chunk enrichers like `SummaryEnricher`, `KeywordEnricher`, or `ClassificationEnricher` at the chunk level.
- Keep tokenizer, chunk size, overlap, and embedding model choices versioned together so reindexing and evaluation stay comparable.
- Treat `IngestionPipeline<T>.ProcessAsync` as partial success. Handle `IngestionResult` per document and decide explicitly whether to retry, continue, or fail the batch.

## Structured Output

- Use typed response helpers when you need enums, records, or other constrained result shapes.
- Keep requested schemas small and stable. The more ambiguous the target type, the more fragile the model output becomes.
- Log raw provider output or failure details when typed deserialization fails.

## Embeddings and Vector Search

- Use `IEmbeddingGenerator<TInput, TEmbedding>` for semantic indexing, similarity, search, and embedding-backed caches.
- Keep the embedding model fixed per collection or explicitly versioned. Mixing models in one vector space causes silent quality degradation.
- Keep vector dimensions, collection schema, and chunking rules versioned with the embedding model. If any of them change, reindex the collection instead of reusing stale vector data.
- Ensure vector-store dimensions match the embedding model output.
- Keep chunking deterministic so reindexing and evaluation remain reproducible.
- Use delegating generators or wrappers for telemetry, rate limits, and caching rather than duplicating those concerns at each call site.

## VectorData Source Compatibility

- In `dotnet/extensions` v10.5.0, `VectorStoreVectorAttribute` renamed the constructor parameter from `Dimensions` to `dimensions`. This is source-breaking only; the property name and binary compatibility are unchanged.
- Update any named-argument usage to `new VectorStoreVectorAttribute(dimensions: 1536)` and check generated samples or templates for the old casing.
- If a collection was created against an older embedding model or dimension count, treat the schema change as a reindexing event rather than a transparent package upgrade.

## Evaluation

- Use quality evaluators when answer relevance, completeness, truthfulness, or groundedness matter.
- Use agent-focused evaluators like `IntentResolutionEvaluator`, `TaskAdherenceEvaluator`, and `ToolCallAccuracyEvaluator` when workflows depend on tool use or instruction following.
- Use NLP evaluators for cheaper offline regression baselines when you already have reference answers.
- Use reporting and response caching in CI so evaluation runs are reproducible and affordable.

## Escalate to Agent Framework When

- the application needs agent threads or durable interaction state
- the control flow becomes multi-step, multi-agent, or workflow-driven
- remote hosting protocols, A2A, AG-UI, or durable execution enter the design
- the architecture needs more than model abstraction and middleware composition

`Microsoft.Extensions.AI` is the composition layer. `Microsoft Agent Framework` is the orchestration layer built on top of the abstractions.
