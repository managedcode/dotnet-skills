# Official Docs Index

This skill keeps a slim, markdown-only snapshot of the official `.NET AI` docs tree from `dotnet/docs` under `docs/ai`.

## Snapshot Summary

- Local root: `references/official-docs/`
- Coverage: `48` useful markdown pages
- Scope: `Microsoft.Extensions.AI`, adjacent `VectorData` and `DataIngestion` guidance, evaluation libraries, MCP quickstarts, RAG guidance, and the surrounding `.NET AI` concept pages
- Boundary: Microsoft Agent Framework is linked from this docs tree, but its dedicated authored snapshot and deeper routing guidance live in the separate `microsoft-agent-framework` skill
- Intentional exclusions: snippet trees, project files, TOC scaffolding, DocFX support files, JSON helpers, media folders, and other low-signal assets are not mirrored into the skill

## Start Here

- [`official-docs/overview.md`](official-docs/overview.md) - Root `.NET AI` landing page
- [`official-docs/dotnet-ai-ecosystem.md`](official-docs/dotnet-ai-ecosystem.md) - Ecosystem map and the official boundary between `Microsoft.Extensions.AI` and Agent Framework
- [`official-docs/microsoft-extensions-ai.md`](official-docs/microsoft-extensions-ai.md) - Package split and core API overview
- [`official-docs/ichatclient.md`](official-docs/ichatclient.md) - Chat, streaming, tools, caching, telemetry, DI, and state handling
- [`official-docs/iembeddinggenerator.md`](official-docs/iembeddinggenerator.md) - Embeddings, delegating generators, and implementation guidance

## Section Map

- Root pages: `overview.md`, `dotnet-ai-ecosystem.md`, `microsoft-extensions-ai.md`, `ichatclient.md`, `iembeddinggenerator.md`, `get-started-mcp.md`, `get-started-app-chat-template.md`, `get-started-app-chat-scaling-with-azure-container-apps.md`, `azure-ai-services-authentication.md`
- Concepts: [`official-docs/conceptual/`](official-docs/conceptual/) with `11` pages covering agents, tools, tokens, embeddings, vector databases, ingestion, prompt engineering, zero-shot and few-shot, chain-of-thought, and RAG
- Quickstarts: [`official-docs/quickstarts/`](official-docs/quickstarts/) with `14` pages covering prompting, chat apps, structured output, vector search, function calling, local models, assistants, MCP client and server, templates, text-to-image, and data processing
- How-to: [`official-docs/how-to/`](official-docs/how-to/) with `5` pages covering function data access, invalid tool input, content filtering, Azure-hosted auth, and tokenizers
- Evaluation: [`official-docs/evaluation/`](official-docs/evaluation/) with `5` pages covering responsible AI, libraries, response quality, reporting, and safety evaluation
- Resources: [`official-docs/resources/`](official-docs/resources/) with `3` pages for general `.NET AI`, Azure AI, and MCP resource lists
- Tutorial: [`official-docs/tutorials/tutorial-ai-vector-search.md`](official-docs/tutorials/tutorial-ai-vector-search.md) for the deeper vector-search walkthrough

## Complete Local File Map

### Root Pages

- [`official-docs/azure-ai-services-authentication.md`](official-docs/azure-ai-services-authentication.md)
- [`official-docs/dotnet-ai-ecosystem.md`](official-docs/dotnet-ai-ecosystem.md)
- [`official-docs/get-started-app-chat-scaling-with-azure-container-apps.md`](official-docs/get-started-app-chat-scaling-with-azure-container-apps.md)
- [`official-docs/get-started-app-chat-template.md`](official-docs/get-started-app-chat-template.md)
- [`official-docs/get-started-mcp.md`](official-docs/get-started-mcp.md)
- [`official-docs/ichatclient.md`](official-docs/ichatclient.md)
- [`official-docs/iembeddinggenerator.md`](official-docs/iembeddinggenerator.md)
- [`official-docs/microsoft-extensions-ai.md`](official-docs/microsoft-extensions-ai.md)
- [`official-docs/overview.md`](official-docs/overview.md)

### Conceptual

- [`official-docs/conceptual/agents.md`](official-docs/conceptual/agents.md)
- [`official-docs/conceptual/ai-tools.md`](official-docs/conceptual/ai-tools.md)
- [`official-docs/conceptual/chain-of-thought-prompting.md`](official-docs/conceptual/chain-of-thought-prompting.md)
- [`official-docs/conceptual/data-ingestion.md`](official-docs/conceptual/data-ingestion.md)
- [`official-docs/conceptual/embeddings.md`](official-docs/conceptual/embeddings.md)
- [`official-docs/conceptual/how-genai-and-llms-work.md`](official-docs/conceptual/how-genai-and-llms-work.md)
- [`official-docs/conceptual/prompt-engineering-dotnet.md`](official-docs/conceptual/prompt-engineering-dotnet.md)
- [`official-docs/conceptual/rag.md`](official-docs/conceptual/rag.md)
- [`official-docs/conceptual/understanding-tokens.md`](official-docs/conceptual/understanding-tokens.md)
- [`official-docs/conceptual/vector-databases.md`](official-docs/conceptual/vector-databases.md)
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
- [`official-docs/quickstarts/build-vector-search-app.md`](official-docs/quickstarts/build-vector-search-app.md)
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
- [`official-docs/resources/get-started.md`](official-docs/resources/get-started.md)
- [`official-docs/resources/mcp-servers.md`](official-docs/resources/mcp-servers.md)

### Tutorials

- [`official-docs/tutorials/tutorial-ai-vector-search.md`](official-docs/tutorials/tutorial-ai-vector-search.md)

## API Reference Landing Pages

- `https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai`
- `https://learn.microsoft.com/dotnet/api/microsoft.extensions.vectordata`
- `https://learn.microsoft.com/dotnet/api/microsoft.extensions.dataingestion`

## Reading Strategy

- Use the local snapshot when exact wording, package names, or Learn-page structure matters.
- Start with the authored overview pages before diving into provider-specific quickstarts.
- Raw Learn `:::code` and `:::image` source-asset directives are stripped from the local snapshot to keep it prose-first and avoid broken local references.
- For orchestration, threads, workflows, or hosted-agent protocols, switch to the `microsoft-agent-framework` skill rather than assuming the answer lives in the `Microsoft.Extensions.AI` layer.
