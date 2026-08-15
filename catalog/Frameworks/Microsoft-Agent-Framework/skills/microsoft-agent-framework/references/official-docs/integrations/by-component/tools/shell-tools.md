---
title: Shell tools
description: Run local or containerized shell commands with the Agent Framework tools package.
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 07/29/2026
ms.service: agent-framework
---

<!--
  Language parity table - keep in sync when adding/removing sections.

  | Section                    | C# | Python | Go | Notes                         |
  |----------------------------|:--:|:------:|:--:|:------------------------------|
  | Tool selection             | ✅ |   ✅   | ✅ | Shared                        |
  | Local shell                | ✅ |   ✅   | ✅ |                               |
  | Docker shell               | ✅ |   ✅   | ❌ | No dedicated .NET sample      |
  | Environment provider       | ✅ |   ✅   | ✅ |                               |
  | Harness Agent setup      | ✅ |   ✅   | ❌ | Go status guidance            |
  | Language availability      | ✅ |   ✅   | ✅ |                               |
-->

# Shell tools

The beta `agent-framework-tools` Python package provides shell execution and environment-awareness tools through the `agent_framework.tools` namespace.

| Tool | Use it when |
|---|---|
| `LocalShellTool` | Commands are trusted or individually approved and should run in the agent process's host environment. |
| `DockerShellTool` | Model-generated shell commands need OCI-container isolation. |
| `ShellEnvironmentProvider` | The model needs the active shell family, operating system, working directory, and installed CLI versions. |
| `ShellPolicy` | You want an allow-list or deny-list pre-filter before approval or execution. |

> [!WARNING]
> Shell execution can modify files, launch processes, access credentials, and communicate with external systems. Use the least-privileged execution tier that supports the task.

:::zone pivot="programming-language-csharp"

## Install the package

```bash
dotnet add package Microsoft.Agents.AI.Tools.Shell --prerelease
```

## Use local shell and environment awareness

`LocalShellExecutor` supports stateless and persistent modes. `ShellEnvironmentProvider` probes the active environment and adds authoritative shell guidance to the agent context.

:::code language="csharp" source="~/../agent-framework-code/dotnet/samples/02-agents/Agents/Agent_Step21_ShellWithEnvironment/Program.cs" range="33-69,86-120":::

`ShellPolicy` is also available for command pre-filtering. A dedicated runnable `DockerShellExecutor` sample isn't currently published.

:::zone-end

:::zone pivot="programming-language-python"

## Install the package

```bash
pip install agent-framework-tools --pre
```

The package installs `psutil` to terminate child process trees when an execution times out.

## Use `LocalShellTool`

`LocalShellTool` runs commands directly on the host. It defaults to a persistent shell, a 30-second timeout, 64-KiB output truncation, working-directory confinement, and approval for every command.

:::code language="python" source="~/../agent-framework-code/python/samples/02-agents/providers/openai/client_with_local_shell.py" range="3-12,33-97":::

Use `mode="stateless"` when each call should run in a fresh process. Use the `AGENT_FRAMEWORK_SHELL` environment variable or the `shell` constructor argument to override the resolved shell.

> [!IMPORTANT]
> `LocalShellTool` isn't a sandbox. Approval is the primary security boundary. Disabling approval requires `acknowledge_unsafe=True`.

## Restrict commands with `ShellPolicy`

`ShellPolicy` applies regular-expression allow and deny lists before execution. Deny rules take precedence.

:::code language="python" source="~/../agent-framework-code/python/samples/02-agents/tools/local_shell_with_allowlist.py" range="3-8,19,22-53":::

> [!WARNING]
> A command policy is a usability pre-filter, not a security boundary. Shell syntax, aliases, variables, interpreters, and encoded payloads can bypass simple pattern matching.

## Add `ShellEnvironmentProvider`

`ShellEnvironmentProvider` probes the shell family, version, operating system, working directory, and selected CLI versions, then injects that information before the agent runs. The default probe list is `git`, `node`, `python`, and `docker`.

:::code language="python" source="~/../agent-framework-code/python/samples/02-agents/tools/local_shell_with_environment_provider.py" range="3-12,34,37-99":::

## Use `DockerShellTool`

`DockerShellTool` requires Docker or Podman on `PATH`. The defaults disable networking, run as a non-root user, use a read-only root filesystem, drop capabilities, limit memory to 512 MiB, and cap the container at 256 processes.

```python
from agent_framework.tools import DockerShellTool

async with DockerShellTool(
    image="mcr.microsoft.com/azurelinux/base/core:3.0",
    approval_mode="never_require",
) as shell:
    result = await shell.run("uname -a && id")
    print(result.stdout)
```

The default image is `mcr.microsoft.com/azurelinux/base/core:3.0`. Pass `docker_binary="podman"` to use Podman. A dedicated runnable `DockerShellTool` sample isn't currently published.

## Choose an execution tier

| Scenario | Tool | Isolation boundary |
|---|---|---|
| Trusted development commands | `LocalShellTool` | Approval in the host process |
| Untrusted shell commands | `DockerShellTool` | OCI container with default isolation flags |
| Untrusted generated code without a shell | [Hyperlight CodeAct](../context-providers/hyperlight.md) | Hyperlight microVM |

:::zone-end

:::zone pivot="programming-language-go"

Go provides local shell execution and environment probing through `tool/shelltool`. See [Use the local shell tool](../../../agents/tools/function-tools.md#use-the-local-shell-tool).

`DockerShellTool` guidance isn't currently available for Go.

:::zone-end

<a id="use-shell-tools-with-harnessed-agent"></a>

## Use shell tools with Harness Agent

:::zone pivot="programming-language-csharp"

Plain agents and `HarnessAgent` use the same two-part shell setup: register the executor's function as a tool, and add `ShellEnvironmentProvider` when the model should receive shell, operating-system, working-directory, and CLI-version context. `HarnessAgent` doesn't create or own a shell executor:

```csharp
using System.IO;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Tools.Shell;
using Microsoft.Extensions.AI;

await using var shell = new LocalShellExecutor(new LocalShellExecutorOptions
{
    WorkingDirectory = Directory.GetCurrentDirectory(),
    Timeout = LocalShellExecutor.DefaultTimeout,
});

AIAgent agent = chatClient.AsHarnessAgent(new HarnessAgentOptions
{
    AIContextProviders = [new ShellEnvironmentProvider(shell)],
    ChatOptions = new ChatOptions
    {
        Tools = [shell.AsAIFunction(requireApproval: true)],
    },
});
```

`AsAIFunction` defaults to the name `run_shell` and `requireApproval: true`. `LocalShellExecutor` defaults to persistent mode, a 64-KiB cap per output stream, and no timeout; the example explicitly uses the recommended 30-second `LocalShellExecutor.DefaultTimeout`. `ShellEnvironmentProviderOptions` defaults to probing `git`, `dotnet`, `node`, `python`, and `docker`, with a five-second timeout per probe.

Create one persistent executor per user session and dispose it when the session ends. Don't share it across users or concurrent conversations because working directory, environment, shell history, background jobs, and the command queue are shared. `ShellPolicy` is only a pre-filter; keep approval enabled, use least-privileged credentials, and prefer `DockerShellExecutor` when commands require a stronger isolation boundary.

Shell tools are available from the prerelease `Microsoft.Agents.AI.Tools.Shell` package. `HarnessAgent` is available from `Microsoft.Agents.AI.Harness`.

:::zone-end

:::zone pivot="programming-language-python"

For a plain agent, create the shell function with `client.get_shell_tool(func=shell.as_function())` and add `ShellEnvironmentProvider` separately. `create_harness_agent` performs both steps when you pass `shell_executor`:

```python
from agent_framework import create_harness_agent
from agent_framework.tools import LocalShellTool, ShellEnvironmentProviderOptions

async with LocalShellTool() as shell:
    agent = create_harness_agent(
        client=client,
        shell_executor=shell,
        shell_environment_provider_options=ShellEnvironmentProviderOptions(
            probe_tools=("git", "python"),
        ),
    )

    session = agent.create_session()
    response = await agent.run("Inspect the current repository.", session=session)
```

`shell_executor` is opt-in and must expose `as_function()`. The factory adds the shell tool and `ShellEnvironmentProvider` only when the client implements `SupportsShellTool`; otherwise it logs a warning and skips both. `shell_environment_provider_options` is optional and is used only with `shell_executor`.

`LocalShellTool` defaults to persistent mode, a 30-second timeout, 64-KiB combined output, working-directory re-anchoring, and `approval_mode="always_require"`. Because Harness tool approval is enabled by default, pass an `AgentSession` to `run`. The caller owns the executor lifecycle; use `async with` or call `close()`, and create one persistent tool per user session. Don't share mutable shell state across users or concurrent conversations.

The host shell isn't a sandbox. Keep approval enabled, use least-privileged credentials, and use `DockerShellTool` for container isolation. Disabling approval requires `approval_mode="never_require"` and `acknowledge_unsafe=True`; `ShellPolicy` alone isn't a security boundary.

`create_harness_agent` is released in `agent-framework-core`. Shell integration is provided by the pre-release `agent-framework-tools` package and emits an `ExperimentalWarning` when enabled.

:::zone-end

:::zone pivot="programming-language-go"

A packaged Go Harness isn't currently available. Compose the local shell tool and environment provider directly on a plain Go agent.

:::zone-end

## Related guidance

- [Tool approval](../../../agents/tools/tool-approval.md)
- [Agent Harness](../../../concepts/harness.md)
- [Hyperlight](../context-providers/hyperlight.md)
