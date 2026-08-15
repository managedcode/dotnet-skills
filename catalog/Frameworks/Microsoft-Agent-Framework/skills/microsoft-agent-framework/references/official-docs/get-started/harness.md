---
title: "Step 6: Agent Harness"
description: "Create a harness agent that plans, tracks todos, and runs multi-step tasks."
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: tutorial
ms.author: westey
ms.date: 07/08/2026
ms.service: agent-framework
---

# Step 6: Agent Harness

A *harness* wraps a chat client with the scaffolding an agent needs to work through long, multi-step tasks — planning / execution modes, a todo list to plan against, context compaction, file memory, file access, and don't-ask-again tool approval. Instead of assembling those pieces yourself, you create a harness agent and get them out of the box.

:::zone pivot="programming-language-csharp"

Create a harness agent from any `IChatClient` with the `AsHarnessAgent` extension method. Because a harness works through tasks interactively over many steps, you typically drive it from a conversation loop: keep an `AgentSession` so the harness state (plan, todos, and history) persists across turns, read the user's next instruction, and stream the agent's output as it's produced.

```csharp
using System;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

// chatClient is any IChatClient implementation (Foundry, Azure OpenAI, OpenAI, Anthropic, ...).
AIAgent agent = chatClient.AsHarnessAgent();

// A session carries the harness state (plan, todos, history) across turns.
AgentSession session = await agent.CreateSessionAsync();

Console.WriteLine("Harness agent ready. Type 'exit' to quit.");
while (true)
{
    Console.Write("> ");
    string? input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    // Stream this turn's output as the harness plans and works through the request.
    await foreach (var update in agent.RunStreamingAsync(input, session))
    {
        Console.Write(update);
    }

    Console.WriteLine();
}
```

The harness handles planning, todo tracking, and history persistence for you across the whole conversation. For a full-featured console — with tool-approval prompts, todo/mode rendering, and slash commands — see the [sample terminal UX](../concepts/harness.md#sample-terminal-ux).

> [!TIP]
> See the [.NET harness samples](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Harness) for full runnable applications.

:::zone-end

:::zone pivot="programming-language-python"

Create a harness agent with the `create_harness_agent` factory. Because a harness works through tasks interactively over many steps, you typically drive it from a conversation loop: keep a session so the harness state (plan, todos, and history) persists across turns, read the user's next instruction, and stream the agent's output as it's produced.

```python
from agent_framework import create_harness_agent
from agent_framework.openai import OpenAIChatClient

agent = create_harness_agent(
    OpenAIChatClient(model="gpt-4o"),
)

# A session carries the harness state (plan, todos, history) across turns.
session = agent.create_session()

print("Harness agent ready. Type 'exit' to quit.")
while True:
    user_input = input("> ")
    if user_input.strip().lower() in {"exit", "quit"}:
        break

    # Stream this turn's output as the harness plans and works through the request.
    async for chunk in agent.run(user_input, session=session, stream=True):
        if chunk.text:
            print(chunk.text, end="", flush=True)
    print()
```

The harness handles planning, todo tracking, and history persistence for you across the whole conversation. For a full-featured console — with tool-approval prompts, todo/mode rendering, and slash commands — see the [sample terminal UX](../concepts/harness.md#sample-terminal-ux).

> [!TIP]
> See the [Python harness samples](https://github.com/microsoft/agent-framework/tree/main/python/samples/02-agents/harness) for full runnable applications.

:::zone-end

:::zone pivot="programming-language-go"

> [!NOTE]
> Go support for agent harnesses is coming soon. See the [Agent Framework Go repository](https://github.com/microsoft/agent-framework-go) for the latest status.

:::zone-end

## Next steps

> [!div class="nextstepaction"]
> [Step 7: Host Your Agent](./hosting.md)

**Go deeper:**

- [Agent Harnesses](../concepts/harness.md) — compaction, looping, shell, and the sample terminal UX
- [Agent Skills](../agents/skills.md) — progressively load skills from the file system
