# Microsoft.Extensions.AI Practical Examples

## Quickstart-To-Task Map

| Scenario | Start with | Main packages or surfaces | Notes |
|---|---|---|---|
| Prompt a model once | `official-docs/quickstarts/prompt-model.md` | `Microsoft.Extensions.AI.OpenAI` + provider SDK | Smallest provider-agnostic entry point |
| Build a chat app | `official-docs/quickstarts/build-chat-app.md` | `IChatClient` | Good baseline for message history and follow-up turns |
| Stream responses in UI | `official-docs/ichatclient.md` | `GetStreamingResponseAsync` | Use `IAsyncEnumerable<ChatResponseUpdate>` all the way to the UI |
| Request structured output | `official-docs/quickstarts/structured-output.md` | typed `GetResponseAsync<T>` helpers | Prefer typed enums or records over manual JSON parsing |
| Execute local tools | `official-docs/quickstarts/use-function-calling.md` | `AIFunction`, `FunctionInvokingChatClient` | Add invalid-input handling from `official-docs/how-to/handle-invalid-tool-input.md` |
| Build vector search or RAG | `official-docs/quickstarts/build-vector-search-app.md` | `IEmbeddingGenerator`, `Microsoft.Extensions.VectorData.Abstractions` | Keep chunking and embedding model/version stable |
| Process data for RAG | `official-docs/quickstarts/process-data.md` | `Microsoft.Extensions.DataIngestion`, `IngestionPipeline<T>` | Use when the ingestion pipeline matters as much as inference |
| Chat with a local model | `official-docs/quickstarts/chat-local-model.md` | local provider adapter + `IChatClient` | Good for dev, lower cost, and offline workflows |
| Generate images | `official-docs/quickstarts/text-to-image.md` | experimental `IImageGenerator` or provider client | Treat image generation as a separate capability surface |
| Build an MCP client | `official-docs/quickstarts/build-mcp-client.md` | MCP client + `IChatClient` | Relevant when tools live behind MCP servers |
| Build an MCP server | `official-docs/quickstarts/build-mcp-server.md` | MCP server SDK | This leans toward `dotnet-mcp`, but often pairs with Extensions.AI clients |
| Create a minimal assistant | `official-docs/quickstarts/create-assistant.md` | provider-specific assistants SDK | This quickstart is assistant-service-centric, not the pure `IChatClient` abstraction layer |

## Recommended Composition Recipes

### Provider-Agnostic App

- Register one or more `IChatClient` implementations in DI.
- Add options configuration, logging or telemetry, caching, and function invocation in a deliberate builder order.
- Keep feature code dependent on `IChatClient`, not the vendor SDK, unless you truly need provider-specific capabilities.

### Typed Chat + Tools

- Use `GetResponseAsync<T>` or the equivalent typed helpers for structured output.
- Give the model a narrow result shape and a narrow tool surface.
- Route ambient tool data through `AdditionalProperties`, `AIFunctionArguments`, or DI instead of serializing hidden state into prompts.

### Vector Search / RAG

- Use `IEmbeddingGenerator<string, Embedding<float>>` to create embeddings for both source content and user queries.
- Store vectors in a vector store accessed through `Microsoft.Extensions.VectorData.Abstractions`.
- Keep ingestion, chunking, and retrieval policies versioned so evaluation results stay meaningful over time.

### Data Ingestion for RAG

- Start from `Microsoft.Extensions.DataIngestion` when documents must be read, normalized, enriched, chunked, and written as one pipeline instead of a pile of custom helpers.
- Reach for the official processing shape:
  - document reader such as MarkItDown or Markdig
  - optional document processor such as `ImageAlternativeTextEnricher`
  - chunker such as `HeaderChunker` or semantic chunking
  - chunk processors such as `SummaryEnricher`
  - `VectorStoreWriter<T>` and `IngestionPipeline<T>` for the final persisted flow
- Handle `ProcessAsync` results per document. A single ingestion failure should be an explicit policy decision, not an accidental crash.

### Evaluation-Backed Delivery

- Add quality and safety evaluators for important prompts and user journeys.
- Run cheap NLP evaluators for stable offline comparisons when you have reference outputs.
- Publish reports and reuse cached evaluation responses in CI so the team can compare prompt or model changes.

## Important Boundaries

- `Microsoft.Extensions.AI` is ideal for provider abstraction, middleware, embeddings, evaluation, and typed tool calling.
- Provider-hosted assistants APIs are adjacent but not identical to `IChatClient` composition.
- When the app needs threads, multi-agent orchestration, or durable workflow control, hand off to `dotnet-microsoft-agent-framework`.
