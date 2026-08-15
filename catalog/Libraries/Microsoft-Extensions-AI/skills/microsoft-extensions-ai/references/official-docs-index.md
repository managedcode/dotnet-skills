# Official Docs Index

This skill keeps a slim, markdown-only snapshot of the official `.NET AI` docs tree from `dotnet/docs` under `docs/ai`.

## Snapshot Summary

- Local root: `references/official-docs/`
- Coverage: `64` current markdown pages
- Scope: `Microsoft.Extensions.AI`, adjacent `VectorData` and `DataIngestion` guidance, evaluation libraries, MCP quickstarts, RAG guidance, and the surrounding `.NET AI` concept pages
- Boundary: Microsoft Agent Framework is linked from this docs tree, but its dedicated authored snapshot and deeper routing guidance live in the separate `microsoft-agent-framework` skill
- Intentional exclusions: project files, TOC scaffolding, DocFX support files, JSON helpers, media folders, and non-markdown source assets are not mirrored. Markdown include and example fragments referenced by the primary pages are retained.

## Start Here

- [`official-docs/overview.md`](official-docs/overview.md) - Root `.NET AI` landing page
- [`official-docs/dotnet-ai-ecosystem.md`](official-docs/dotnet-ai-ecosystem.md) - Ecosystem map and the official boundary between `Microsoft.Extensions.AI` and Agent Framework
- [`official-docs/microsoft-extensions-ai.md`](official-docs/microsoft-extensions-ai.md) - Package split and core API overview
- [`official-docs/ichatclient.md`](official-docs/ichatclient.md) - Chat, streaming, tools, caching, telemetry, DI, and state handling
- [`official-docs/iembeddinggenerator.md`](official-docs/iembeddinggenerator.md) - Embeddings, delegating generators, and implementation guidance

## Section Map

- Root pages: `overview.md`, `dotnet-ai-ecosystem.md`, `microsoft-extensions-ai.md`, `ichatclient.md`, `iembeddinggenerator.md`, `get-started-mcp.md`, `get-started-app-chat-template.md`, and `azure-ai-services-authentication.md`
- Concepts: [`official-docs/conceptual/`](official-docs/conceptual/) with `12` pages covering agents, tool calling, tokens, embeddings, DataIngestion, VectorData, ingestion, prompt engineering, zero-shot, chain-of-thought, and RAG
- Quickstarts: [`official-docs/quickstarts/`](official-docs/quickstarts/) with `13` primary pages plus `11` markdown include/example fragments covering prompting, chat apps, structured output, function calling, local models, assistants, MCP client and server, templates, text-to-image, and data processing
- How-to: [`official-docs/how-to/`](official-docs/how-to/) with `5` pages covering function data access, invalid tool input, content filtering, Azure-hosted auth, and tokenizers
- Evaluation: [`official-docs/evaluation/`](official-docs/evaluation/) with `5` pages covering responsible AI, libraries, response quality, reporting, and safety evaluation
- Resources: [`official-docs/resources/`](official-docs/resources/) with `2` pages for Azure AI and MCP resource lists
- Vector stores: [`official-docs/vector-stores/`](official-docs/vector-stores/) with `8` pages covering models, data management, search, ingestion, implementation, and the end-to-end tutorial

## Complete Local File Map

### Root Pages

- [`official-docs/azure-ai-services-authentication.md`](official-docs/azure-ai-services-authentication.md)
- [`official-docs/dotnet-ai-ecosystem.md`](official-docs/dotnet-ai-ecosystem.md)
- [`official-docs/get-started-app-chat-template.md`](official-docs/get-started-app-chat-template.md)
- [`official-docs/get-started-mcp.md`](official-docs/get-started-mcp.md)
- [`official-docs/ichatclient.md`](official-docs/ichatclient.md)
- [`official-docs/iembeddinggenerator.md`](official-docs/iembeddinggenerator.md)
- [`official-docs/microsoft-extensions-ai.md`](official-docs/microsoft-extensions-ai.md)
- [`official-docs/overview.md`](official-docs/overview.md)

### Conceptual

- [`official-docs/conceptual/agents.md`](official-docs/conceptual/agents.md)
- [`official-docs/conceptual/calling-tools.md`](official-docs/conceptual/calling-tools.md)
- [`official-docs/conceptual/chain-of-thought-prompting.md`](official-docs/conceptual/chain-of-thought-prompting.md)
- [`official-docs/conceptual/data-ingestion.md`](official-docs/conceptual/data-ingestion.md)
- [`official-docs/conceptual/embeddings.md`](official-docs/conceptual/embeddings.md)
- [`official-docs/conceptual/how-genai-and-llms-work.md`](official-docs/conceptual/how-genai-and-llms-work.md)
- [`official-docs/conceptual/medi-library.md`](official-docs/conceptual/medi-library.md)
- [`official-docs/conceptual/mevd-library.md`](official-docs/conceptual/mevd-library.md)
- [`official-docs/conceptual/prompt-engineering-dotnet.md`](official-docs/conceptual/prompt-engineering-dotnet.md)
- [`official-docs/conceptual/rag.md`](official-docs/conceptual/rag.md)
- [`official-docs/conceptual/understanding-tokens.md`](official-docs/conceptual/understanding-tokens.md)
- [`official-docs/conceptual/zero-shot-learning.md`](official-docs/conceptual/zero-shot-learning.md)

### How-To

- [`official-docs/how-to/access-data-in-functions.md`](official-docs/how-to/access-data-in-functions.md)
- [`official-docs/how-to/app-service-aoai-auth.md`](official-docs/how-to/app-service-aoai-auth.md)
- [`official-docs/how-to/content-filtering.md`](official-docs/how-to/content-filtering.md)
- [`official-docs/how-to/handle-invalid-tool-input.md`](official-docs/how-to/handle-invalid-tool-input.md)
- [`official-docs/how-to/use-tokenizers.md`](official-docs/how-to/use-tokenizers.md)

### Quickstarts

- [`official-docs/quickstarts/ai-templates.md`](official-docs/quickstarts/ai-templates.md)
- [`official-docs/quickstarts/build-chat-app.md`](official-docs/quickstarts/build-chat-app.md)
- [`official-docs/quickstarts/build-mcp-client.md`](official-docs/quickstarts/build-mcp-client.md)
- [`official-docs/quickstarts/build-mcp-server.md`](official-docs/quickstarts/build-mcp-server.md)
- [`official-docs/quickstarts/chat-local-model.md`](official-docs/quickstarts/chat-local-model.md)
- [`official-docs/quickstarts/create-assistant.md`](official-docs/quickstarts/create-assistant.md)
- [`official-docs/quickstarts/generate-images.md`](official-docs/quickstarts/generate-images.md)
- [`official-docs/quickstarts/process-data.md`](official-docs/quickstarts/process-data.md)
- [`official-docs/quickstarts/prompt-model.md`](official-docs/quickstarts/prompt-model.md)
- [`official-docs/quickstarts/publish-mcp-registry.md`](official-docs/quickstarts/publish-mcp-registry.md)
- [`official-docs/quickstarts/structured-output.md`](official-docs/quickstarts/structured-output.md)
- [`official-docs/quickstarts/text-to-image.md`](official-docs/quickstarts/text-to-image.md)
- [`official-docs/quickstarts/use-function-calling.md`](official-docs/quickstarts/use-function-calling.md)

### Evaluation

- [`official-docs/evaluation/evaluate-ai-response.md`](official-docs/evaluation/evaluate-ai-response.md)
- [`official-docs/evaluation/evaluate-safety.md`](official-docs/evaluation/evaluate-safety.md)
- [`official-docs/evaluation/evaluate-with-reporting.md`](official-docs/evaluation/evaluate-with-reporting.md)
- [`official-docs/evaluation/libraries.md`](official-docs/evaluation/libraries.md)
- [`official-docs/evaluation/responsible-ai.md`](official-docs/evaluation/responsible-ai.md)

### Resources

- [`official-docs/resources/azure-ai.md`](official-docs/resources/azure-ai.md)
- [`official-docs/resources/mcp-servers.md`](official-docs/resources/mcp-servers.md)

### Quickstart Includes And Examples

- [`official-docs/quickstarts/includes/ai-templates-azure-openai.md`](official-docs/quickstarts/includes/ai-templates-azure-openai.md)
- [`official-docs/quickstarts/includes/ai-templates-explore-app.md`](official-docs/quickstarts/includes/ai-templates-explore-app.md)
- [`official-docs/quickstarts/includes/ai-templates-ollama.md`](official-docs/quickstarts/includes/ai-templates-ollama.md)
- [`official-docs/quickstarts/includes/ai-templates-openai.md`](official-docs/quickstarts/includes/ai-templates-openai.md)
- [`official-docs/quickstarts/includes/create-ai-service.md`](official-docs/quickstarts/includes/create-ai-service.md)
- [`official-docs/quickstarts/includes/prerequisites-azure-openai.md`](official-docs/quickstarts/includes/prerequisites-azure-openai.md)
- [`official-docs/quickstarts/includes/prerequisites-openai.md`](official-docs/quickstarts/includes/prerequisites-openai.md)
- [`official-docs/quickstarts/snippets/mcp-server/README.md`](official-docs/quickstarts/snippets/mcp-server/README.md)
- [`official-docs/quickstarts/snippets/process-data/data/sample.md`](official-docs/quickstarts/snippets/process-data/data/sample.md)
- [`official-docs/quickstarts/snippets/prompt-completion/azure-openai/benefits.md`](official-docs/quickstarts/snippets/prompt-completion/azure-openai/benefits.md)
- [`official-docs/quickstarts/snippets/prompt-completion/openai/benefits.md`](official-docs/quickstarts/snippets/prompt-completion/openai/benefits.md)

### Vector Stores

- [`official-docs/vector-stores/define-your-data-model.md`](official-docs/vector-stores/define-your-data-model.md)
- [`official-docs/vector-stores/how-to/build-vector-search-app.md`](official-docs/vector-stores/how-to/build-vector-search-app.md)
- [`official-docs/vector-stores/how-to/use-vector-stores.md`](official-docs/vector-stores/how-to/use-vector-stores.md)
- [`official-docs/vector-stores/how-to/vector-store-data-ingestion.md`](official-docs/vector-stores/how-to/vector-store-data-ingestion.md)
- [`official-docs/vector-stores/manage-data.md`](official-docs/vector-stores/manage-data.md)
- [`official-docs/vector-stores/overview.md`](official-docs/vector-stores/overview.md)
- [`official-docs/vector-stores/tutorial-vector-search.md`](official-docs/vector-stores/tutorial-vector-search.md)
- [`official-docs/vector-stores/vector-search.md`](official-docs/vector-stores/vector-search.md)

## API Reference Landing Pages

- `https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai`
- `https://learn.microsoft.com/dotnet/api/microsoft.extensions.vectordata`
- `https://learn.microsoft.com/dotnet/api/microsoft.extensions.dataingestion`

## Reading Strategy

- Use the local snapshot when exact wording, package names, or Learn-page structure matters.
- Start with the authored overview pages before diving into provider-specific quickstarts.
- Raw Learn `:::code` and `:::image` source-asset directives are stripped from the local snapshot to keep it prose-first and avoid broken local references.
- For orchestration, threads, workflows, or hosted-agent protocols, switch to the `microsoft-agent-framework` skill rather than assuming the answer lives in the `Microsoft.Extensions.AI` layer.
