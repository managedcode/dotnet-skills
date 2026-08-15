---
title: ONNX
description: Run a local ONNX Runtime GenAI model behind an Agent Framework .NET agent.
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 07/28/2026
ms.service: agent-framework
---

# ONNX

ONNX Runtime GenAI lets a .NET Agent Framework application run a compatible model locally. Use it for offline development, on-device inference, or deployments where model execution must stay on the host.

> [!NOTE]
> The current ONNX client doesn't support function calling. Function tools passed to the agent are ignored.

## Prerequisites

- .NET 8 or later.
- A model exported for ONNX Runtime GenAI.
- Sufficient local memory and a compatible execution provider for the selected model.

## Install the packages

```bash
dotnet add package Microsoft.ML.OnnxRuntimeGenAI
dotnet add package Microsoft.Agents.AI --prerelease
```

## Configuration

```bash
ONNX_MODEL_PATH="<path-to-onnx-runtime-genai-model-directory>"
```

## Create an ONNX-backed agent

Download a model exported for ONNX Runtime GenAI and point `ONNX_MODEL_PATH` to the model directory.

:::code language="csharp" source="~/../agent-framework-code/dotnet/samples/02-agents/AgentProviders/onnx/Agent_With_ONNX/Program.cs" range="10-18":::

The model files, execution provider, quantization, and available memory determine hardware compatibility and performance. Review the model license before redistributing it.

## Tools

The current ONNX client doesn't support function calling or provider-hosted tools.

## Next steps

> [!div class="nextstepaction"]
> [Dapr](dapr.md)
