---
title: Amazon Bedrock
description: Use Amazon Bedrock model inference with Agent Framework C# and Python agents.
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 07/28/2026
ms.service: agent-framework
---

<!--
  Language parity table - keep in sync when adding/removing sections.

  | Section              | C# | Python | Go | Notes                  |
  |----------------------|:--:|:------:|:--:|------------------------|
  | Configuration        | ✅ |   ✅   | ❌ |                        |
  | Bedrock client setup | ✅ |   ✅   | ❌ |                        |
  | Tools                | ✅ |   ✅   | ❌ |                        |
  | Embeddings           | ❌ |   ✅   | ❌ | No published sample    |
  | Go availability      | ✅ |   ✅   | ✅ | Go zone is status only |
-->

# Amazon Bedrock

Amazon Bedrock provides managed inference for foundation models through AWS. Agent Framework can wrap a Bedrock `IChatClient` or use the Python `BedrockChatClient` while keeping the standard agent, session, middleware, and tool APIs.

> [!IMPORTANT]
> Amazon Bedrock is a third-party system. Review AWS service terms, data handling, regional availability, model access, and usage costs before sending application data.

:::zone pivot="programming-language-csharp"

## Install the packages

```bash
dotnet add package AWSSDK.Extensions.Bedrock.MEAI
dotnet add package Microsoft.Agents.AI --prerelease
```

## Configuration

```bash
AWS_REGION="us-east-1"
BEDROCK_MODEL_ID="anthropic.claude-3-5-sonnet-20241022-v2:0"
```

Authentication uses the standard AWS credential chain, including environment variables, shared profiles, workload identity, and IAM roles.

Create the AWS Bedrock runtime client, convert it to `IChatClient`, and then create an Agent Framework agent.

:::code language="csharp" source="~/../agent-framework-code/dotnet/samples/02-agents/AgentWithMemory/AgentWithMemory_Step03_MemoryUsingValkey_Bedrock/Program.cs" range="19-20,23-25":::

```csharp
AIAgent agent = chatClient.AsAIAgent(
    instructions: "You are a helpful assistant.",
    name: "BedrockAgent");
```

AWS credentials follow the standard AWS credential chain. Grant only the Bedrock model actions the application needs.

:::zone-end

:::zone pivot="programming-language-python"

## Install the package

```bash
pip install agent-framework-bedrock --pre
```

## Configuration

```bash
BEDROCK_REGION="us-east-1"
BEDROCK_CHAT_MODEL="anthropic.claude-3-5-sonnet-20241022-v2:0"
AWS_ACCESS_KEY_ID="<access-key>"
AWS_SECRET_ACCESS_KEY="<secret-key>"
# Optional temporary credentials:
AWS_SESSION_TOKEN="<session-token>"
# Optional shared profile:
AWS_PROFILE="<profile>"
```

`BedrockChatClient` reads the model, region, and AWS credentials from its settings or explicit constructor values.

:::code language="python" source="~/../agent-framework-code/python/samples/02-agents/providers/amazon/bedrock_chat_client.py" range="39-54":::

Use `BedrockChatOptions` for Bedrock-specific request options and `BedrockGuardrailConfig` when your deployment uses Bedrock guardrails.

## Generate embeddings

`BedrockEmbeddingClient` generates embeddings with Amazon Titan embedding models. Configure `BEDROCK_EMBEDDING_MODEL` and `BEDROCK_REGION`, then use the same AWS credential chain as `BedrockChatClient`.

No runnable Agent Framework embedding sample is currently published for this client.

:::zone-end

:::zone pivot="programming-language-go"

> [!NOTE]
> Amazon Bedrock integration isn't currently available for Agent Framework Go. See the [Agent Framework Go repository](https://github.com/microsoft/agent-framework-go) for the latest status.

:::zone-end

## Tools

Bedrock supports locally invoked Agent Framework tools but doesn't expose provider-hosted tool factories.

| Tool | C# | Python | Notes |
|---|:---:|:---:|---|
| [Function Tools](../../../agents/tools/function-tools.md) | ✅ | ✅ | Model support varies by the selected Bedrock model. |
| [Tool Approval](../../../agents/tools/tool-approval.md) | ✅ | ✅ | Applied by the Agent Framework function-invocation loop. |
| [Code Interpreter](../../../agents/tools/code-interpreter.md) | ❌ | ❌ | No Bedrock-hosted code interpreter integration. |
| [File Search](../../../agents/tools/file-search.md) | ❌ | ❌ | No Bedrock-hosted file-search integration. |
| [Web Search](../../../agents/tools/web-search.md) | ❌ | ❌ | No Bedrock-hosted web-search integration. |
| [Hosted MCP Tools](../../../agents/tools/hosted-mcp-tools.md) | ❌ | ❌ | No Bedrock-hosted MCP integration. |
| [Local MCP Tools](../../../agents/tools/local-mcp-tools.md) | ✅ | ✅ | Runs in the application process. |

## Next steps

> [!div class="nextstepaction"]
> [Google Gemini](google-gemini.md)
