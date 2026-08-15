---
title: Azure Content Understanding
description: Analyze documents, images, audio, and video with Azure Content Understanding in Agent Framework.
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 07/28/2026
ms.service: agent-framework
---

# Azure Content Understanding

`ContentUnderstandingContextProvider` analyzes file attachments with Azure Content Understanding and injects structured results into the agent context. It supports documents, images, audio, and video, including OCR, tables, structured fields, transcription, diarization, and segment summaries.

This integration uses the pre-processing pattern: it transforms incoming content before model invocation and can retain processed state for later turns.

For large documents, the provider can upload extracted markdown to a file-search vector store instead of placing the entire result in the model context.

## Prerequisites

- An Azure subscription.
- Azure Content Understanding in a supported region.
- The service's required model deployments.
- Azure identity access to the resource.

## Install the package

```bash
pip install agent-framework-azure-contentunderstanding --pre
```

## Analyze a document

Attach `ContentUnderstandingContextProvider` to the agent and send a supported binary attachment. The provider removes the binary input after analysis and supplies the extracted content to the model.

:::code language="python" source="~/../agent-framework-code/python/samples/02-agents/context_providers/azure_content_understanding/01_document_qa.py" range="42-100":::

## Processing options

- Leave `analyzer_id` unset to select a document, audio, or video search analyzer from the media type.
- Set `max_wait=None` when the run must wait for analysis to complete.
- Use `FileSearchConfig` for token-efficient retrieval over large extracted documents.
- Reuse an `AgentSession` to preserve analyzed-document state across turns.

## Next steps

> [!div class="nextstepaction"]
> [Mistral](../model-providers/mistral.md)

**Go deeper:**

- [Azure Content Understanding samples](https://github.com/microsoft/agent-framework/tree/main/python/samples/02-agents/context_providers/azure_content_understanding)
- [Context providers](../../../concepts/agents/conversations/context-providers.md)
- [Azure Content Understanding documentation](/azure/ai-services/content-understanding/)
