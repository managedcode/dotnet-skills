---
title: Hyperlight
description: Use the Hyperlight connector to add CodeAct and sandboxed Python execution to Agent Framework.
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 07/28/2026
ms.service: agent-framework
---
<!--
  Language parity table - keep in sync when adding/removing sections.

    | Section                         | C# | Python | Go | Notes                             |
    |---------------------------------|:--:|:------:|:--:|-----------------------------------|
    | Why Hyperlight CodeAct          | ✅ |   ✅   | ❌ | Shared integration overview       |
    | Getting started                 | ✅ |   ✅   | ❌ | C#/Python documented              |
    | Package installation            | ✅ |   ✅   | ❌ | C#/Python preview                 |
    | HyperlightCodeActProvider       | ✅ |   ✅   | ❌ | No Go Hyperlight package          |
    | Approvals and host tools        | ✅ |   ✅   | ❌ | C#/Python documented              |
    | Manual execute_code wiring      | ✅ |   ✅   | ❌ | C# uses HyperlightExecuteCodeFunction; Python uses HyperlightExecuteCodeTool |
    | Filesystem and network settings | ✅ |   ✅   | ❌ | C#/Python documented              |
    | Output guidance                 | ✅ |   ✅   | ❌ | C#/Python documented              |
    | Benchmark framing               | ❌ |   ✅   | ❌ | Python sample only today          |
    | Current limitations             | ✅ |   ✅   | ❌ | C#/Python documented              |
-->

# Hyperlight

Hyperlight is the currently documented backend for CodeAct in Agent Framework. It exposes an `execute_code` tool backed by an isolated sandbox runtime and can call provider-owned host tools through `call_tool(...)`.

This integration uses the CodeAct pattern: the provider contributes a code-execution tool and manages the execution environment around each run.

For the pattern-level overview, see [CodeAct](../../../agents/code_act.md).

## Why Hyperlight CodeAct

Modern agents are often limited more by tool-calling overhead than by the model itself. A task that reads data, performs light computation, and assembles a result can easily turn into a chain of model -> tool -> model -> tool interactions, even when each individual step is simple.

Hyperlight-backed CodeAct collapses that loop. The model writes one short Python program, the sandbox executes it once, and provider-owned tools are reached from inside the sandbox with `call_tool(...)`. In representative tool-heavy workloads, that shift can cut latency roughly in half and token usage by more than 60%, while keeping the execution isolated and auditable.

::: zone pivot="programming-language-csharp"

## Install the package

```bash
dotnet add package Microsoft.Agents.AI.Hyperlight --prerelease
```

`Microsoft.Agents.AI.Hyperlight` ships separately from the core abstractions, so you only take on the sandbox runtime when you need it.

> [!IMPORTANT]
> The .NET package is in preview. It depends on the `Hyperlight.HyperlightSandbox.Api` NuGet package from [hyperlight-dev/hyperlight-sandbox](https://github.com/hyperlight-dev/hyperlight-sandbox); until that dependency is published to nuget.org the project will fail to restore. Track the upstream sandbox repository for availability.

> [!NOTE]
> Hyperlight requires hardware virtualization on the host: KVM on Linux or the Windows Hypervisor Platform (WHP) on Windows. The `Wasm` backend additionally requires a Hyperlight Python guest module — set `HYPERLIGHT_PYTHON_GUEST_PATH` to its absolute path before running.

## Use `HyperlightCodeActProvider`

`HyperlightCodeActProvider` is the recommended entry point when you want CodeAct added automatically for each run. It is an `AIContextProvider` that injects run-scoped CodeAct instructions plus the `execute_code` tool, while keeping provider-owned tools off the direct agent tool surface. The provider applies snapshot/restore per run so the guest starts from a known clean state every invocation.

Use the `HyperlightCodeActProviderOptions.CreateForWasm(modulePath)` factory to target the Wasm-based Python guest used by the samples; `CreateForJavaScript()` is also available for the JavaScript backend.

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hyperlight;
using OpenAI.Chat;

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-5.4-mini";
var guestPath = Environment.GetEnvironmentVariable("HYPERLIGHT_PYTHON_GUEST_PATH")
    ?? throw new InvalidOperationException("HYPERLIGHT_PYTHON_GUEST_PATH is not set.");

using var codeAct = new HyperlightCodeActProvider(
    HyperlightCodeActProviderOptions.CreateForWasm(guestPath));

AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
    .GetChatClient(deploymentName)
    .AsAIAgent(new ChatClientAgentOptions()
    {
        ChatOptions = new()
        {
            Instructions = "You are a helpful assistant. When the user asks something quantitative, "
                + "write Python and call `execute_code` instead of guessing.",
        },
        AIContextProviders = [codeAct],
    });

Console.WriteLine(await agent.RunAsync("What is the 20th Fibonacci number?"));
```

> [!NOTE]
> Only one `HyperlightCodeActProvider` may be attached to a given agent. The provider uses a fixed state key so `ChatClientAgent`'s state-key uniqueness validation rejects duplicate registrations. `HyperlightCodeActProvider` implements `IDisposable`; use a `using` declaration so the underlying sandbox is released when the agent is no longer needed.

Tools, file mounts, and outbound allow-list entries can be supplied up front via `HyperlightCodeActProviderOptions` (`Tools`, `FileMounts`, `AllowedDomains`, `HostInputDirectory`) or managed at runtime via the provider's `AddTools(...)`, `RemoveTools(...)`, `ClearTools()`, `AddFileMounts(...)`, `AddAllowedDomains(...)`, and matching `Get*` accessors.

## How approvals and host tools work

Agent Framework tools carry approval metadata that controls whether they can be auto-invoked or must pause for user approval. In .NET, approval is opt-in by wrapping an `AIFunction` in `ApprovalRequiredAIFunction`.

The main difference between registering a tool on `HyperlightCodeActProvider` and registering it directly on the agent is **how the tool is invoked**, not where the function ultimately runs:

- Tools registered on `HyperlightCodeActProviderOptions.Tools` are hidden from the model as direct tools. The model reaches them by writing code that calls `call_tool("name", ...)` inside `execute_code`.
- Tools registered directly on the agent (for example via `AsAIAgent(tools: [...])`) are surfaced to the model as first-class tools, and each direct call honors that tool's own approval metadata.

`call_tool(...)` is a bridge back to host callbacks; it is not an in-sandbox reimplementation of the tool. That means provider-owned tools still execute in the host process, with whatever filesystem, network, and credentials the host process itself can access.

The `CodeActApprovalMode` enum controls how the `execute_code` tool itself is approved:

- `CodeActApprovalMode.NeverRequire` (default): approval propagates from the registered tools. If any tool in the registry is wrapped in `ApprovalRequiredAIFunction`, `execute_code` also requires approval; otherwise it does not.
- `CodeActApprovalMode.AlwaysRequire`: `execute_code` always requires user approval before invocation.

As a rule of thumb:

- Put cheap, deterministic, safe-to-chain tools on the provider so the model can compose many calls inside one `execute_code` turn.
- Wrap side-effecting or sensitive operations in `ApprovalRequiredAIFunction` (and consider keeping them as direct agent tools instead) so each invocation stays individually visible and approvable.

The next sample registers two safe tools (`fetch_docs`, `query_data`) plus a sensitive `send_email` tool wrapped in `ApprovalRequiredAIFunction`. Because at least one registered tool requires approval, the default `NeverRequire` mode causes `execute_code` itself to require approval whenever it is invoked.

```csharp
AIFunction fetchDocs = AIFunctionFactory.Create(
    (string topic) => $"Docs for {topic}: (...)",
    name: "fetch_docs",
    description: "Fetch documentation for a given topic.");

AIFunction queryData = AIFunctionFactory.Create(
    (string query) => $"Rows for `{query}`: []",
    name: "query_data",
    description: "Run a read-only SQL-like query against the sample store.");

AIFunction sendEmail = new ApprovalRequiredAIFunction(
    AIFunctionFactory.Create(
        (string to, string subject) => $"Sent '{subject}' to {to}.",
        name: "send_email",
        description: "Send an email on behalf of the user."));

var options = HyperlightCodeActProviderOptions.CreateForWasm(guestPath);
options.Tools = [fetchDocs, queryData, sendEmail];

using var codeAct = new HyperlightCodeActProvider(options);

AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
    .GetChatClient(deploymentName)
    .AsAIAgent(new ChatClientAgentOptions()
    {
        ChatOptions = new()
        {
            Instructions = "You are a helpful assistant. Prefer orchestrating your work in a single "
                + "`execute_code` block using `call_tool(...)` over issuing many direct tool calls.",
        },
        AIContextProviders = [codeAct],
    });
```

Because host tools run outside the sandbox, `FileMounts` and `AllowedDomains` constrain the sandboxed code itself, not the host callback behind `call_tool(...)`. When you need controlled access to a sensitive resource, prefer a narrow host tool over broadening sandbox permissions.

## Use `HyperlightExecuteCodeFunction` for direct wiring

When you need to mix `execute_code` with direct-only tools on the same agent, or the sandbox configuration is fixed for the agent's lifetime, use `HyperlightExecuteCodeFunction` instead of the provider. It is a standalone `AIFunction` that captures a single snapshot of the supplied options at construction time and reuses it for every invocation.

Unlike `HyperlightCodeActProvider`, the standalone function does not inject prompt guidance automatically, so you are responsible for adding the `BuildInstructions(...)` output to the agent instructions yourself. Pass `toolsVisibleToModel: false` when the registered tools are reachable only through `call_tool(...)`, and `true` when the same tools are also exposed directly to the model.

```csharp
AIFunction calculate = AIFunctionFactory.Create(
    (double a, double b) => a * b,
    name: "multiply",
    description: "Multiply two numbers.");

var options = HyperlightCodeActProviderOptions.CreateForWasm(guestPath);
options.Tools = [calculate];

using var executeCode = new HyperlightExecuteCodeFunction(options);

var instructions =
    "You are a helpful assistant. When math is involved, solve it by writing Python "
    + "and calling `execute_code` instead of computing values yourself.\n\n"
    + executeCode.BuildInstructions(toolsVisibleToModel: false);

AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
    .GetChatClient(deploymentName)
    .AsAIAgent(instructions: instructions, tools: [executeCode]);
```

`HyperlightExecuteCodeFunction` also implements `IDisposable`. When the configuration requires approval (per `ApprovalMode` or because a configured tool is itself wrapped in `ApprovalRequiredAIFunction`), the instance surfaces an `ApprovalRequiredAIFunction` proxy via `AITool.GetService(...)`, which is how the rest of the framework discovers approval requirements.

## Configure files and outbound access

Hyperlight can expose a read-only `/input` tree plus a writable `/output` area for generated artifacts.

- Use `HostInputDirectory` to make a host directory available under `/input/`.
- Use `FileMounts` to map specific host paths into the sandbox via `new FileMount(hostPath, mountPath)`.
- Use `AllowedDomains` to enable outbound access only for specific targets or methods via `new AllowedDomain(target, methods)`.

```csharp
var options = HyperlightCodeActProviderOptions.CreateForWasm(guestPath);
options.Tools = [compute];
options.FileMounts =
[
    new FileMount("/host/data", "/input/data"),
    new FileMount("/host/models", "/sandbox/models"),
];
options.AllowedDomains =
[
    new AllowedDomain("https://api.github.com"),
    new AllowedDomain("https://internal.api.example.com", ["GET"]),
];

using var codeAct = new HyperlightCodeActProvider(options);
```

The same `FileMounts` and `AllowedDomains` collections, plus tools, can also be modified at runtime through `AddFileMounts(...)`, `RemoveFileMounts(...)`, `AddAllowedDomains(...)`, and `RemoveAllowedDomains(...)` on `HyperlightCodeActProvider`.

## Output guidance

To surface text from `execute_code`, end the guest code with `print(...)`; Hyperlight does not return the value of the last expression automatically.

When filesystem access is enabled, write larger artifacts to `/output/<filename>` instead. Returned files are attached to the tool result, while files under `/input` are available for reading inside the sandbox.

## Current limitations

This package is still preview, and a few constraints are worth planning around:

1. The package depends on `Hyperlight.HyperlightSandbox.Api`, which is not yet published on nuget.org. Until that ships, project restore will fail.
2. Platform support follows the published Hyperlight backend packages: supported Linux (KVM) and Windows (WHP) environments. Unsupported platforms or missing virtualization back ends will fail when creating the sandbox.
3. The current Wasm backend executes a Python guest module specified by `HYPERLIGHT_PYTHON_GUEST_PATH`. The JavaScript backend (`CreateForJavaScript()`) is available for guest code in JavaScript.
4. In-memory interpreter state does not persist across separate `execute_code` calls. Use mounted files and `/output` artifacts when data needs to survive across calls.
5. Approval applies to the `execute_code` invocation as a whole, not to each individual `call_tool(...)` inside the same code block.
6. Tool descriptions, parameter annotations, and return shapes matter more here because the model is writing code against that contract rather than choosing isolated direct tool calls.
7. There is no .NET equivalent of the Python benchmark sample yet — see the Python tab for the published comparison harness.

::: zone-end

::: zone pivot="programming-language-python"

## Install the package

```bash
pip install agent-framework-hyperlight --pre
```

`agent-framework-hyperlight` ships separately from `agent-framework-core`, so you only take on the sandbox runtime when you need it.

> [!NOTE]
> The package depends on Hyperlight sandbox components. If the backend is not published for your current platform yet, `execute_code` fails when it tries to create the sandbox.

## Use `HyperlightCodeActProvider`

`HyperlightCodeActProvider` is the recommended entry point when you want CodeAct added automatically for each run. It injects run-scoped CodeAct instructions plus the `execute_code` tool, while keeping provider-owned tools off the direct agent tool surface.

```python
import os

from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient
from agent_framework.hyperlight import HyperlightCodeActProvider
from azure.identity import AzureCliCredential

# 1. Create the Hyperlight-backed provider and register sandbox tools on it.
codeact = HyperlightCodeActProvider(
    tools=[compute, fetch_data],
    approval_mode="never_require",
)

# 2. Create the client and the agent.
agent = Agent(
    client=FoundryChatClient(
        project_endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
        model=os.environ["FOUNDRY_MODEL"],
        credential=AzureCliCredential(),
    ),
    name="HyperlightCodeActProviderAgent",
    instructions="You are a helpful assistant.",
    context_providers=[codeact],
)

# 3. Run a request that should use execute_code plus provider-owned tools.
query = (
    "Fetch all users, find admins, multiply 7*(3*2), and print the users, "
    "admins, and multiplication result. Use execute_code and call_tool(...) "
    "inside the sandbox."
)
result = await agent.run(query)
print(result.text)
```

Tools registered on the provider are available inside the sandbox through `call_tool(...)`, but they are not exposed as direct agent tools. The provider also exposes CRUD-style management for tools, file mounts, and outbound allow-list entries through methods such as `add_tools(...)`, `remove_tool(...)`, `add_file_mounts(...)`, and `add_allowed_domains(...)`.

## How approvals and host tools work

Agent Framework tools carry an `approval_mode` that controls whether they can be auto-invoked or must pause for user approval.

The main difference between registering a tool on `HyperlightCodeActProvider` and registering it directly on `Agent(tools=...)` is **how the tool is invoked**, not where the Python function ultimately runs:

- Tools registered on `HyperlightCodeActProvider(tools=...)` are hidden from the model as direct tools. The model reaches them by writing code that calls `call_tool("name", ...)` inside `execute_code`.
- Tools registered on `Agent(tools=...)` are surfaced to the model as first-class tools, and each direct call honors that tool's own `approval_mode`.

`call_tool(...)` is a bridge back to host callbacks; it is not an in-sandbox reimplementation of the tool. That means provider-owned tools still execute in the host process, with whatever filesystem, network, and credentials the host process itself can access.

As a rule of thumb:

- Put cheap, deterministic, safe-to-chain tools on the provider so the model can compose many calls inside one `execute_code` turn.
- Keep side-effecting or approval-gated operations as direct agent tools, often with `approval_mode="always_require"`, so each invocation stays individually visible and approvable.

Because host tools run outside the sandbox, `file_mounts` and `allowed_domains` constrain the sandboxed code itself, not the host callback behind `call_tool(...)`. When you need controlled access to a sensitive resource, prefer a narrow host tool over broadening sandbox permissions.

> [!NOTE]
> Tools invoked through `call_tool(...)` return their native Python value (`dict`, `list`, primitive, or custom object) directly to the guest. Any `result_parser` configured on a `FunctionTool` is intended for LLM-facing consumers and does **not** run on the sandbox path — apply formatting inside the tool function itself if you need it for in-sandbox consumers.

## Use `HyperlightExecuteCodeTool` for direct wiring

When you need to mix `execute_code` with direct-only tools on the same agent, use `HyperlightExecuteCodeTool` instead of the provider. For fixed configurations, you can build the CodeAct instructions once and wire the tool directly:

```python
from agent_framework.hyperlight import HyperlightExecuteCodeTool

execute_code = HyperlightExecuteCodeTool(
    tools=[compute],
    approval_mode="never_require",
)

codeact_instructions = execute_code.build_instructions(tools_visible_to_model=False)
```

This pattern is useful when the CodeAct surface is fixed and you do not need the provider lifecycle on every run. Unlike `HyperlightCodeActProvider`, the standalone tool does not inject prompt guidance automatically, so you are responsible for adding the `build_instructions(...)` output to the agent instructions yourself.

## Configure files and outbound access

Hyperlight can expose a read-only `/input` tree plus a writable `/output` area for generated artifacts.

- Use `workspace_root` to make a workspace available under `/input/`.
- Use `file_mounts` to map specific host paths into the sandbox.
- Use `allowed_domains` to enable outbound access only for specific targets or methods.

`file_mounts` accepts a shorthand string, an explicit `(host_path, mount_path)` pair, or a `FileMount` named tuple. `allowed_domains` accepts a string target, an explicit `(target, method-or-methods)` pair, or an `AllowedDomain` named tuple.

```python
from agent_framework.hyperlight import HyperlightCodeActProvider

codeact = HyperlightCodeActProvider(
    tools=[compute],
    file_mounts=[
        "/host/data",
        ("/host/models", "/sandbox/models"),
    ],
    allowed_domains=[
        "api.github.com",
        ("internal.api.example.com", "GET"),
    ],
)
```

## Output guidance

To surface text from `execute_code`, end the code with `print(...)`; Hyperlight does not return the value of the last expression automatically.

When filesystem access is enabled, write larger artifacts to `/output/<filename>` instead. Returned files are attached to the tool result, while files under `/input` are available for reading inside the sandbox.

## Compare CodeAct and direct tool calling

The conceptual comparison is the same as for any CodeAct backend: the same client, model, tools, prompt, and structured output schema can be wired either through traditional tool calling or through Hyperlight-backed CodeAct. The only difference is the tool surface — direct tools versus a single `execute_code` tool backed by `HyperlightCodeActProvider`:

```python
from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient
from agent_framework.hyperlight import HyperlightCodeActProvider

# Direct tool calling: the model picks one tool at a time per turn.
direct = Agent(
    client=FoundryChatClient(...),
    instructions="...",
    tools=[fetch_data, compute],
)

# Hyperlight-backed CodeAct: the model writes one program per turn that
# orchestrates the same tools through call_tool(...).
codeact = Agent(
    client=FoundryChatClient(...),
    instructions="...",
    context_providers=[
        HyperlightCodeActProvider(
            tools=[fetch_data, compute],
            approval_mode="never_require",
        ),
    ],
)
```

For workloads that compute totals across a dataset by repeatedly looking up data and performing light computation — many small, chainable steps — CodeAct can remove orchestration overhead. Wrap both runs with a stopwatch and inspect the returned `ChatResponse.usage` to compare elapsed time and token usage in your own environment.

## Current limitations

This package is still alpha, and a few constraints are worth planning around:

1. Platform support follows the published Hyperlight backend packages. Today that means supported Linux and Windows environments; unsupported platforms will fail when creating the sandbox.
2. The current integration executes Python guest code.
3. In-memory interpreter state does not persist across separate `execute_code` calls. Use mounted files and `/output` artifacts when data needs to survive across calls.
4. Approval applies to the `execute_code` invocation as a whole, not to each individual `call_tool(...)` inside the same code block.
5. Tool descriptions, parameter annotations, and return shapes matter more here because the model is writing code against that contract rather than choosing isolated direct tool calls.

::: zone-end


::: zone pivot="programming-language-go"

> [!NOTE]
> Go support for this feature is coming soon. See the [Agent Framework Go repository](https://github.com/microsoft/agent-framework-go) for the latest status.

::: zone-end
## Next steps

> [!div class="nextstepaction"]
> [Local (.NET)](local.md)

### Related content

- [CodeAct](../../../agents/code_act.md)
- [CodeAct paper](https://arxiv.org/abs/2402.01030)
- [Context Providers](../../../concepts/agents/conversations/context-providers.md)
- [Tool Approval](../../../agents/tools/tool-approval.md)
- [Hyperlight provider sample (Python)](https://github.com/microsoft/agent-framework/blob/main/python/samples/02-agents/context_providers/code_act/code_act.py)
- [Hyperlight CodeAct samples (.NET)](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/AgentWithCodeAct)
