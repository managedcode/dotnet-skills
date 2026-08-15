---
title: Mistral
description: Generate Mistral AI embeddings with Agent Framework Python.
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 07/28/2026
ms.service: agent-framework
---

# Mistral

`MistralEmbeddingClient` generates text embeddings with Mistral AI models. Use it for vector indexing, semantic search, clustering, or other applications that need an Agent Framework embedding client.

This provider currently supplies embeddings only; it doesn't provide an Agent Framework chat client.

## Install the package

```bash
pip install agent-framework-mistral --pre
```

## Configuration

```bash
MISTRAL_API_KEY="<api-key>"
MISTRAL_EMBEDDING_MODEL="mistral-embed"
# Optional compatible endpoint:
MISTRAL_SERVER_URL="<server-url>"
```

## Generate embeddings

Create the client and call `get_embeddings()`.

:::code language="python" source="~/../agent-framework-code/python/samples/02-agents/providers/mistral/mistral_embeddings.py" range="20-55":::

Use `MistralEmbeddingOptions` to request a supported output dimension. You can also set `MISTRAL_SERVER_URL` when the application uses a custom compatible endpoint.

> [!IMPORTANT]
> Mistral AI is a third-party system. Review its service terms, data handling, regional boundaries, model licensing, and usage costs before sending application data.

## Tools

Tools aren't applicable because this package currently provides an embedding client, not an Agent Framework chat client.

## Next steps

> [!div class="nextstepaction"]
> [RAG](../../../agents/rag.md)
