---
title: Self-host Telegram bots
description: Use the Agent Framework Telegram helpers in an application-owned bot server.
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 07/22/2026
ms.service: agent-framework
---

# Self-host Telegram bots

:::zone pivot="programming-language-csharp"

> [!NOTE]
> Self-hosting helpers for Telegram bots in .NET are coming soon.

:::zone-end

:::zone pivot="programming-language-go"

> [!NOTE]
> Self-hosting helpers for Telegram bots are not currently available for Go.

:::zone-end

:::zone pivot="programming-language-python"

`agent-framework-hosting-telegram` converts Telegram Bot API updates into Agent Framework run values and renders final or streaming runs as Bot API operations. It does not provide a bot client, polling runtime, webhook router, command registry, or delivery framework.

```bash
pip install --pre agent-framework agent-framework-foundry agent-framework-hosting agent-framework-hosting-telegram azure-identity
```

Use any Telegram client library that can supply an update payload and execute the operations returned by the helpers. The sample uses `aiogram`, but the helpers are not tied to it.

## Process an update

The `aiogram` webhook sample verifies Telegram's secret header, dispatches the update, and uses a bot-scoped session ID to preserve an agent session for each private chat or shared group chat.

:::code language="python" source="~/../agent-framework-code/python/samples/04-hosting/af-hosting/local_telegram/app.py" range="176-243":::

For polling and webhook setup, command handling, inbound media policy, streaming edits, and production deployment guidance, see the [local Telegram sample](https://github.com/microsoft/agent-framework/tree/main/python/samples/04-hosting/af-hosting/local_telegram).

> [!IMPORTANT]
> Verify Telegram webhook deliveries before processing updates. A webhook secret authenticates Telegram's delivery, but it does not authorize the Telegram user or chat to access application data. Treat chat and user IDs as untrusted until your application applies its authorization policy.

## Next steps

> [!div class="nextstepaction"]
> [Add A2A](a2a/index.md)

**Go deeper:**

- [Self-hosting overview](index.md)
- [OpenAI Responses](responses.md)
- [MCP](mcp.md)
- [Foundry Hosted Agents](../foundry-hosted-agent.md)

:::zone-end
