---
title: Local (.NET)
description: Run Agent Framework CodeAct in a local Python subprocess from .NET.
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 07/28/2026
ms.service: agent-framework
---

# Local (.NET)

`Microsoft.Agents.AI.LocalCodeAct` runs generated Python in a child process in the agent's environment. It provides the CodeAct provider pattern without requiring a Hyperlight guest runtime.

This integration uses the CodeAct pattern and relies on the host environment for isolation.

> [!WARNING]
> Local CodeAct is **not a security sandbox**. Run it only where an external container, virtual machine, or managed hosting environment provides process, filesystem, network, and credential isolation.

## Install the package

```bash
dotnet add package Microsoft.Agents.AI.LocalCodeAct --prerelease
```

The package requires an explicit Python executable path.

## Configure the provider

Register host tools through `LocalCodeActProviderOptions`. Generated code can call only those tools through `await call_tool(...)`. Apply execution limits to bound subprocess runtime and captured output.

:::code language="csharp" source="~/../agent-framework-code/dotnet/samples/04-hosting/FoundryHostedAgents/responses/Hosted-LocalCodeAct/Program.cs" range="74-106":::

## Defense-in-depth controls

Local CodeAct provides:

- AST validation with configurable allowed and blocked imports and built-ins.
- Direct Python subprocess execution without invoking a shell.
- Time, output, result, and captured-file size limits.
- Explicit host-tool registration.
- Read-only and read-write file mounts.
- Configurable working directory and subprocess environment.

These controls reduce risk but don't provide containment. Keep validation enabled, pass a restricted environment dictionary, expose narrow host tools, and run the process inside a strong external sandbox.

## Choose a CodeAct runtime

| Runtime | Choose it when |
|---|---|
| [Hyperlight](hyperlight.md) | You need an isolated sandbox with filesystem and network controls. |
| Local CodeAct | Your .NET agent already runs inside an externally sandboxed environment. |
| [Monty](monty.md) | You need a cross-platform restricted interpreter for Python agents. |

## Next steps

> [!div class="nextstepaction"]
> [Monty](monty.md)
