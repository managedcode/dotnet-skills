---
title: Monty
description: Add cross-platform CodeAct execution to Agent Framework Python agents with Monty.
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 07/28/2026
ms.service: agent-framework
---

# Monty

Monty is a Rust-based interpreter for a restricted Python subset. `MontyCodeActProvider` gives an Agent Framework agent one `execute_code` tool and lets generated code call provider-owned tools as typed async functions or through `call_tool(...)`.

This integration uses the CodeAct pattern with a restricted interpreter rather than a hardware-isolated sandbox.

Use Monty when you need a cross-platform CodeAct runtime without Hyperlight's hypervisor or WASM guest dependency.

> [!NOTE]
> `agent-framework-monty` is a beta package. Monty restricts operating-system, subprocess, and direct network access, but it isn't a hardware-isolated virtual machine.

## Install the packages

```bash
pip install agent-framework-monty agent-framework-foundry --pre
```

## Add `MontyCodeActProvider`

Register host tools on the provider rather than directly on the agent. The model sees `execute_code` and calls those tools from generated code.

:::code language="python" source="~/../agent-framework-code/python/samples/02-agents/context_providers/code_act/monty_code_act.py" range="137-171":::

## Configure capabilities

`MontyCodeActProvider` and `MontyExecuteCodeTool` support:

- host tools and runtime tool management
- `never_require` or `always_require` approval for `execute_code`
- a workspace root and explicit file mounts
- Monty resource limits
- files returned from read-write mounts as Agent Framework content

Monty doesn't provide an outbound URL allow list. Provide network access through a narrow host tool that validates destinations and inputs.

## Choose Monty or Hyperlight

| Runtime | Choose it when |
|---|---|
| Monty | Cross-platform execution and a restricted interpreter are sufficient. |
| [Hyperlight](hyperlight.md) | You need a hardened sandbox, filesystem controls, or outbound-domain allow lists. |

## Next steps

> [!div class="nextstepaction"]
> [Review the CodeAct pattern](../../../agents/code_act.md)
