---
title: Agent Skills
description: Learn how to extend agent capabilities with Agent Skills - portable packages of instructions, scripts, and resources that agents discover and load on demand.
zone_pivot_groups: programming-languages
author: SergeyMenshykh
ms.topic: article
ms.author: semenshi
ms.date: 07/08/2026
ms.service: agent-framework
---

# Agent Skills

[Agent Skills](https://agentskills.io/) are portable packages of instructions, scripts, and resources that give agents specialized capabilities and domain expertise. Skills follow an open specification and implement a progressive disclosure pattern so agents load only the context they need, when they need it.

Use Agent Skills when you want to:

- **Package domain expertise** - Capture specialized knowledge (expense policies, legal workflows, data analysis pipelines) as reusable, portable packages.
- **Extend agent capabilities** - Give agents new abilities without changing their core instructions.
- **Ensure consistency** - Turn multi-step tasks into repeatable, auditable workflows.
- **Enable interoperability** - Reuse the same skill across different Agent Skills-compatible products.

## Skill structure

A skill is a directory containing a `SKILL.md` file with optional subdirectories for resources:

```
expense-report/
├── SKILL.md                          # Required - frontmatter + instructions
├── scripts/
│   └── validate.py                   # Executable code agents can run
├── references/
│   └── POLICY_FAQ.md                 # Reference documents loaded on demand
└── assets/
    └── expense-report-template.md    # Templates and static resources
```

### SKILL.md format

The `SKILL.md` file must contain YAML frontmatter followed by markdown content:

```yaml
---
name: expense-report
description: File and validate employee expense reports according to company policy. Use when asked about expense submissions, reimbursement rules, or spending limits.
license: Apache-2.0
compatibility: Requires python3
metadata:
  author: contoso-finance
  version: "2.1"
---
```

| Field | Required | Description |
|---|---|---|
| `name` | Yes | Max 64 characters. Lowercase letters, numbers, and hyphens only. Must not start or end with a hyphen or contain consecutive hyphens. Must match the parent directory name. |
| `description` | Yes | What the skill does and when to use it. Max 1024 characters. Should include keywords that help agents identify relevant tasks. |
| `license` | No | License name or reference to a bundled license file. |
| `compatibility` | No | Max 500 characters. Indicates environment requirements (intended product, system packages, network access, etc.). |
| `metadata` | No | Arbitrary key-value mapping for additional metadata. |
| `allowed-tools` | No | Space-delimited list of pre-approved tools the skill may use. Experimental - support may vary between agent implementations. |

The markdown body after the frontmatter contains the skill instructions - step-by-step guidance, examples of inputs and outputs, common edge cases, or any content that helps the agent perform the task. Keep `SKILL.md` under 500 lines and move detailed reference material to separate files.

## Progressive disclosure

Agent Skills use a four-stage progressive disclosure pattern to minimize context usage:

1. **Advertise** (~100 tokens per skill) - Skill names and descriptions are injected into the system prompt at the start of each run, so the agent knows what skills are available.
2. **Load** (< 5000 tokens recommended) - When a task matches a skill's domain, the agent calls the `load_skill` tool to retrieve the full SKILL.md body with detailed instructions.
3. **Read resources** (as needed) - The agent calls the `read_skill_resource` tool to fetch supplementary files (references, templates, assets) only when required.
4. **Run scripts** (as needed) - The agent calls the `run_skill_script` tool to execute scripts bundled with a skill.

This pattern keeps the agent's context window lean while giving it access to deep domain knowledge on demand.

> [!NOTE]
> `load_skill` is always advertised. `read_skill_resource` is advertised only when at least one skill has resources. `run_skill_script` is advertised only when at least one skill has scripts.

## Providing skills to an agent

Working with skills involves three building blocks:

- **Provider** - `AgentSkillsProvider` (C#) or `SkillsProvider` (Python) is a context provider that exposes skills to an agent. It advertises the available skills in the system prompt and registers the tools the agent uses to load skills, read resources, and run scripts.
- **Sources** - a source supplies skills to the provider. Skills can come from several source types:
  - **File-based** - skills discovered from `SKILL.md` files in filesystem directories.
  - **Code-defined** - skills defined inline in code using `AgentInlineSkill` (C#) or `InlineSkill` (Python).
  - **Class-based** - skills encapsulated in a class deriving from `AgentClassSkill<T>` (C#) or `ClassSkill` (Python).
  - **MCP-based** - skills discovered from MCP (Model Context Protocol) servers via `UseMcpSkills` (C#) or `MCPSkillsSource` (Python).
- **Builder** - `AgentSkillsProviderBuilder` (C#) assembles multiple sources into a single provider, applying aggregation, deduplication, caching, and optional filtering. In Python, compose source classes such as `AggregatingSkillsSource`, `FilteringSkillsSource`, and `DeduplicatingSkillsSource` directly.

The following sections show how to create skills of each source type, then how to combine sources and construct a provider from them.

## Use Agent Skills with Harness Agent

With a plain agent, create a skills provider, add it to the agent's context
providers, and compose tool-approval middleware when needed. A Harness Agent
can create or include the provider as part of its standard setup.

:::zone pivot="programming-language-csharp"

`HarnessAgent` includes `AgentSkillsProvider` by default and discovers file-based
skills from `Directory.GetCurrentDirectory()`. To use a different source, set
`HarnessAgentOptions.AgentSkillsSource`:

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

AIAgent agent = chatClient.AsHarnessAgent(new HarnessAgentOptions
{
    AgentSkillsSource = new AgentFileSkillsSource(
        Path.Combine(AppContext.BaseDirectory, "skills")),
    ToolApprovalAgentOptions = new ToolApprovalAgentOptions
    {
        // Auto-approve load_skill and read_skill_resource, but not run_skill_script.
        AutoApprovalRules = [AgentSkillsProvider.ReadOnlyToolsAutoApprovalRule],
    },
    ChatOptions = new ChatOptions
    {
        Instructions = "Use the available skills when they match the task.",
    },
});
```

`DisableAgentSkillsProvider` defaults to `false`. Set it to `true` to remove the
built-in provider. `AgentSkillsSource` replaces the default current-directory
source, but it doesn't expose `AgentSkillsProviderOptions`. If you need provider
options such as `DisableLoadSkillApproval`, disable the built-in provider and
add your configured `AgentSkillsProvider` through
`HarnessAgentOptions.AIContextProviders`.

To run scripts from file-based skills, pass an `AgentFileSkillScriptRunner`
delegate as the second `AgentFileSkillsSource` constructor argument. Without a
runner, script execution fails when requested.

All three skill tools require approval by default. The harness tool-approval
middleware is enabled by default, but its default options don't auto-approve any
tool. Use `AgentSkillsProvider.ReadOnlyToolsAutoApprovalRule` or
`AgentSkillsProvider.AllToolsAutoApprovalRule` only for skill sources you trust.

:::zone-end

:::zone pivot="programming-language-python"

Agent Skills are opt-in for `create_harness_agent`. Pass `skills_paths` for
file-based discovery:

```python
from pathlib import Path

from agent_framework import SkillsProvider, create_harness_agent

agent = create_harness_agent(
    client=client,
    agent_instructions="Use the available skills when they match the task.",
    skills_paths=Path(__file__).parent / "skills",
    # Auto-approve load_skill and read_skill_resource, but not run_skill_script.
    auto_approval_rules=[SkillsProvider.read_only_tools_auto_approval_rule],
)

session = agent.create_session()
result = await agent.run("Use the appropriate skill for this task.", session=session)
```

`skills_paths` accepts one `str` or `Path`, or a sequence of them. When both
`skills_provider` and `skills_paths` are `None` (the defaults), the harness
doesn't add a `SkillsProvider`. You can combine both parameters to include
code-defined and file-based skills.

The `skills_paths` shortcut constructs `SkillsProvider.from_paths()` without a
`script_runner`. If file-based skills need to execute scripts, create the
provider yourself with
`SkillsProvider.from_paths(..., script_runner=...)` and pass it through
`skills_provider`.

All three skill tools require approval by default. Because the harness installs
`ToolApprovalMiddleware` by default, pass a session on every run and use
`auto_approval_rules` for trusted read-only or all-tool approval policies.

:::zone-end

:::zone pivot="programming-language-go"

A packaged Go harness isn't currently available. Register the Go skills provider
in `agent.Config.ContextProviders` and compose approval middleware directly.

:::zone-end

:::zone pivot="programming-language-csharp"

## File-based skills

Create an `AgentSkillsProvider` pointing to a directory containing your skills, and add it to the agent's context providers. Pass a script runner to enable execution of file-based scripts found in skill directories:

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using OpenAI.Responses;

string endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!;
string deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-4o-mini";

// Discover skills from the 'skills' directory
var skillsProvider = new AgentSkillsProvider(
    Path.Combine(AppContext.BaseDirectory, "skills"));

// Create an agent with the skills provider
AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
    .GetResponsesClient()
    .AsAIAgent(new ChatClientAgentOptions
    {
        Name = "SkillsAgent",
        ChatOptions = new()
        {
            Instructions = "You are a helpful assistant.",
        },
        AIContextProviders = [skillsProvider],
    },
    model: deploymentName);
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

### Multiple skill directories

You can point the provider to a single parent directory - each subdirectory containing a `SKILL.md` is automatically discovered as a skill:

```csharp
var skillsProvider = new AgentSkillsProvider(
    Path.Combine(AppContext.BaseDirectory, "all-skills"));
```

Or pass a list of paths to search multiple root directories:

```csharp
var skillsProvider = new AgentSkillsProvider(
    [
        Path.Combine(AppContext.BaseDirectory, "company-skills"),
        Path.Combine(AppContext.BaseDirectory, "team-skills"),
    ]);
```

The provider searches up to two levels deep.

### Customizing resource and script discovery

By default, the provider recognizes resources with extensions `.md`, `.json`, `.yaml`, `.yml`, `.csv`, `.xml`, and `.txt` and scripts with extensions `.py`, `.js`, `.sh`, `.ps1`, `.cs`, and `.csx`. It searches up to two levels deep within each skill directory. Use `AgentFileSkillsSourceOptions` to change these defaults:

```csharp
var fileOptions = new AgentFileSkillsSourceOptions
{
    AllowedResourceExtensions = [".md", ".txt"],
    AllowedScriptExtensions = [".py"],
    SearchDepth = 3, // Search up to 3 levels deep (default is 2)
    ResourceFilter = context => context.RelativeFilePath.StartsWith("references/"),
    ScriptFilter = context => context.RelativeFilePath.StartsWith("scripts/")
                           || context.RelativeFilePath.StartsWith("tools/"),
};

// Via constructor
var skillsProvider = new AgentSkillsProvider(
    Path.Combine(AppContext.BaseDirectory, "skills"),
    fileOptions: fileOptions);

// Via builder
var skillsProvider = new AgentSkillsProviderBuilder()
    .UseFileSkill(Path.Combine(AppContext.BaseDirectory, "skills"), options: fileOptions)
    .Build();
```

`ResourceFilter` and `ScriptFilter` receive an `AgentFileSkillFilterContext` with the skill name and the file's relative path, letting you restrict files by location, naming convention, or any custom logic.

### Script execution

Pass `SubprocessScriptRunner.RunAsync` as the script runner to enable execution of file-based scripts:

```csharp
var skillsProvider = new AgentSkillsProvider(
    Path.Combine(AppContext.BaseDirectory, "skills"),
    SubprocessScriptRunner.RunAsync);
```

`SubprocessScriptRunner.RunAsync` is roughly equivalent to the following:

```csharp
// Simplified equivalent of what SubprocessScriptRunner.RunAsync does internally
using System.Diagnostics;
using System.Text.Json;

static async Task<object?> RunAsync(
    AgentFileSkill skill,
    AgentFileSkillScript script,
    JsonElement? args,
    IServiceProvider? serviceProvider,
    CancellationToken cancellationToken)
{
    var psi = new ProcessStartInfo("python3")
    {
        RedirectStandardOutput = true,
        UseShellExecute = false,
    };
    psi.ArgumentList.Add(script.FullPath);
    if (args is { ValueKind: JsonValueKind.Array } json)
    {
        foreach (var element in json.EnumerateArray())
        {
            psi.ArgumentList.Add(element.GetString()!);
        }
    }
    using var process = Process.Start(psi)!;
    string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
    await process.WaitForExitAsync(cancellationToken);
    return output.Trim();
}
```

The runner runs each discovered script as a local subprocess. File-based scripts expect arguments as a JSON array of strings - each array element becomes a positional command-line argument.

> [!WARNING]
> `SubprocessScriptRunner` is provided for **demonstration purposes only**. For production use, consider adding:
>
> - Sandboxing (for example, containers or isolated execution environments)
> - Resource limits (CPU, memory, wall-clock timeout)
> - Input validation and allow-listing of executable scripts
> - Structured logging and audit trails

:::zone-end

:::zone pivot="programming-language-python"

## File-based skills

Use the `SkillsProvider.from_paths()` factory to discover skills from directories containing `SKILL.md` files, and add the provider to the agent's context providers:

```python
import os
from pathlib import Path

# Discover skills from the 'skills' directory
skills_provider = SkillsProvider.from_paths(
    skill_paths=Path(__file__).parent / "skills",
)

# Create an agent with the skills provider
endpoint = os.environ["FOUNDRY_PROJECT_ENDPOINT"]
deployment = os.environ.get("FOUNDRY_MODEL", "gpt-4o-mini")

client = FoundryChatClient(
    project_endpoint=endpoint,
    model=deployment,
    credential=AzureCliCredential(),
)

agent = Agent(
    client=client,
    instructions="You are a helpful assistant.",
    context_providers=[skills_provider],
)
```

### Multiple skill directories

You can point the provider to a single parent directory - each subdirectory containing a `SKILL.md` is automatically discovered as a skill:

```python
skills_provider = SkillsProvider.from_paths(
    skill_paths=Path(__file__).parent / "all-skills"
)
```

Or pass a list of paths to search multiple root directories:

```python
skills_provider = SkillsProvider.from_paths(
    skill_paths=[
        Path(__file__).parent / "company-skills",
        Path(__file__).parent / "team-skills",
    ]
)
```

The provider searches up to two levels deep.

### Customizing resource and script discovery

By default, resources are discovered from `references/` and `assets/` subdirectories, and scripts from `scripts/`, per the [agentskills.io specification](https://agentskills.io/specification). Recognized resource extensions are `.md`, `.json`, `.yaml`, `.yml`, `.csv`, `.xml`, and `.txt`. It searches up to two levels deep within each skill directory. Use `resource_extensions`, `script_extensions`, `search_depth`, `resource_filter`, and `script_filter` to customize discovery:

```python
skills_provider = SkillsProvider.from_paths(
    skill_paths=Path(__file__).parent / "skills",
    resource_extensions=(".md", ".txt"),
    script_extensions=(".py", ".sh"),
    search_depth=3,  # Search up to 3 levels deep (default is 2)
    resource_filter=lambda skill_name, path: path.startswith("references/"),
    script_filter=lambda skill_name, path: path.startswith("scripts/"),
)
```

The `resource_filter` and `script_filter` predicates receive the skill name and the file's relative path, letting you restrict files by location, naming convention, or any custom logic. Use `"."` to include files at the skill root level in addition to subdirectories.

### Script execution

To enable execution of file-based scripts, pass a `script_runner` to `SkillsProvider.from_paths()`. Any sync or async callable that satisfies the `SkillScriptRunner` protocol can be used:

```python
from pathlib import Path
from agent_framework import FileSkill, FileSkillScript, SkillsProvider

def my_runner(
    skill: FileSkill,
    script: FileSkillScript,
    args: dict | list[str] | None = None,
) -> str:
    """Run a file-based script as a subprocess."""
    import subprocess, sys
    script_path = Path(script.full_path)
    cmd = [sys.executable, str(script_path)]
    if isinstance(args, list):
        cmd.extend(args)
    result = subprocess.run(
        cmd, capture_output=True, text=True, timeout=30, cwd=str(script_path.parent)
    )
    return result.stdout.strip()

skills_provider = SkillsProvider.from_paths(
    skill_paths=Path(__file__).parent / "skills",
    script_runner=my_runner,
)
```

The runner receives the resolved `FileSkill`, `FileSkillScript`, and an optional `args` argument. File-based scripts expect arguments as a JSON array of strings - each array element becomes a positional command-line argument. Scripts are automatically discovered from `.py` files in the `scripts/` subdirectory of each skill directory.

> [!WARNING]
> The runner above is provided for **demonstration purposes only**. For production use, consider adding:
>
> - Sandboxing (for example, containers, `seccomp`, or `firejail`)
> - Resource limits (CPU, memory, wall-clock timeout)
> - Input validation and allow-listing of executable scripts
> - Structured logging and audit trails

> [!NOTE]
> If file-based skills with scripts are provided but no `script_runner` is set, `SkillsProvider` raises an error when script execution is attempted.

:::zone-end

:::zone pivot="programming-language-go"

## File-based skills

Go agents support skills through the `agent/skills` package. Skills follow the same progressive disclosure pattern: advertise -> load -> read resources -> run scripts.

Discover skills from `SKILL.md` files on disk and register the skills provider as an agent context provider:

```go
import (
    "os"

    "github.com/microsoft/agent-framework-go/agent"
    "github.com/microsoft/agent-framework-go/provider/foundryprovider"
    "github.com/microsoft/agent-framework-go/agent/skills"
    "github.com/microsoft/agent-framework-go/agent/skills/fsskills"
)

skillsRoot, _ := os.OpenRoot("skills")
defer skillsRoot.Close()

skillsProvider := skills.NewContextProvider(skills.ContextProviderOptions{
    Sources: []skills.Source{
        fsskills.NewSourceOptions(fsskills.SourceOptions{}, skillsRoot.FS()),
    },
})

a := foundryprovider.NewAgent(endpoint, token, foundryprovider.ModelDeployment(model), foundryprovider.AgentConfig{
    Instructions: "You are a helpful assistant.",
    Config: agent.Config{
        ContextProviders: []agent.ContextProvider{skillsProvider},
    },
})
```

:::zone-end

## Code-defined skills

:::zone pivot="programming-language-csharp"

In addition to file-based skills discovered from `SKILL.md` files, you can define skills entirely in code using `AgentInlineSkill`. Code-defined skills are useful when:

- Skill content is generated dynamically (for example, reading from a database or environment).
- You want to keep skill definitions alongside the application code that uses them.
- You need resources that execute logic at read time rather than serving static files.
- Skill definitions need to be **constructed at runtime from data** - for example, creating a personalized skill for each user session based on their role or permissions.
- A skill needs to **close over call-site state** (local variables, closures) rather than resolve services from a DI container.

### Basic code skill

Create an `AgentInlineSkill` with a name, description, and instructions. Attach resources using `.AddResource()`:

```csharp
using Microsoft.Agents.AI;

var codeStyleSkill = new AgentInlineSkill(
    name: "code-style",
    description: "Coding style guidelines and conventions for the team",
    instructions: """
        Use this skill when answering questions about coding style, conventions, or best practices for the team.
        1. Read the style-guide resource for the full set of rules.
        2. Answer based on those rules, quoting the relevant guideline where helpful.
        """)
    .AddResource(
        "style-guide",
        """
        # Team Coding Style Guide
        - Use 4-space indentation (no tabs)
        - Maximum line length: 120 characters
        - Use type annotations on all public methods
        """);

var skillsProvider = new AgentSkillsProvider(codeStyleSkill);
```

### Dynamic resources

Pass a factory delegate to `.AddResource()` to compute the content at runtime. The delegate is invoked each time the agent reads the resource:

```csharp
var projectInfoSkill = new AgentInlineSkill(
    name: "project-info",
    description: "Project status and configuration information",
    instructions: """
        Use this skill for questions about the current project.
        1. Read the environment resource for deployment configuration details.
        2. Read the team-roster resource for information about team members.
        """)
    .AddResource("environment", () =>
    {
        string env = Environment.GetEnvironmentVariable("APP_ENV") ?? "development";
        string region = Environment.GetEnvironmentVariable("APP_REGION") ?? "us-east-1";
        return $"Environment: {env}, Region: {region}";
    })
    .AddResource(
        "team-roster",
        "Alice Chen (Tech Lead), Bob Smith (Backend Engineer)");
```

### Code-defined scripts

Use `.AddScript()` to register a delegate as an executable script. Code-defined scripts run **in-process** as direct delegate calls. No script runner is needed. The delegate's typed parameters are automatically converted into a JSON Schema that the agent uses to pass arguments:

```csharp
using System.Text.Json;

var unitConverterSkill = new AgentInlineSkill(
    name: "unit-converter",
    description: "Convert between common units using a conversion factor",
    instructions: """
        Use this skill when the user asks to convert between units.
        1. Review the conversion-table resource to find the correct factor.
        2. Use the convert script, passing the value and factor from the table.
        3. Present the result clearly with both units.
        """)
    .AddResource(
        "conversion-table",
        """
        # Conversion Tables
        Formula: **result = value × factor**
        | From       | To         | Factor   |
        |------------|------------|----------|
        | miles      | kilometers | 1.60934  |
        | kilometers | miles      | 0.621371 |
        | pounds     | kilograms  | 0.453592 |
        | kilograms  | pounds     | 2.20462  |
        """)
    .AddScript("convert", (double value, double factor) =>
    {
        double result = Math.Round(value * factor, 4);
        return JsonSerializer.Serialize(new { value, factor, result });
    });

var skillsProvider = new AgentSkillsProvider(unitConverterSkill);
```

> [!NOTE]
> To combine code-defined skills with file-based or class-based skills in a single provider, use `AgentSkillsProviderBuilder` - see [Provider construction](#provider-construction).

:::zone-end

:::zone pivot="programming-language-python"

In addition to file-based skills discovered from `SKILL.md` files, you can define skills entirely in Python code using `InlineSkill`. Code-defined skills are useful when:

- Skill content is generated dynamically (for example, reading from a database or environment).
- You want to keep skill definitions alongside the application code that uses them.
- You need resources that execute logic at read time rather than serving static files.
- Skill definitions need to be **constructed at runtime from data** - for example, creating a personalized skill for each user session based on their role or permissions.
- A skill needs to **close over call-site state** (local variables, closures) rather than resolve services through `**kwargs`.

### Basic code skill

Create an `InlineSkill` instance with a `SkillFrontmatter` (containing the name and description) and instruction content. Optionally attach `InlineSkillResource` instances with static content:

```python
from textwrap import dedent
from agent_framework import InlineSkill, InlineSkillResource, SkillFrontmatter, SkillsProvider

code_style_skill = InlineSkill(
    frontmatter=SkillFrontmatter(
        name="code-style",
        description="Coding style guidelines and conventions for the team",
    ),
    instructions=dedent("""\
        Use this skill when answering questions about coding style,
        conventions, or best practices for the team.
    """),
    resources=[
        InlineSkillResource(
            name="style-guide",
            content=dedent("""\
                # Team Coding Style Guide
                - Use 4-space indentation (no tabs)
                - Maximum line length: 120 characters
                - Use type annotations on all public functions
            """),
        ),
    ],
)

skills_provider = SkillsProvider(code_style_skill)
```

### Dynamic resources

Use the `@skill.resource` decorator to register a function as a resource. The function is called each time the agent reads the resource, so it can return up-to-date data. Both sync and async functions are supported:

```python
import os
from agent_framework import InlineSkill, SkillFrontmatter

project_info_skill = InlineSkill(
    frontmatter=SkillFrontmatter(
        name="project-info",
        description="Project status and configuration information",
    ),
    instructions="Use this skill for questions about the current project.",
)

@project_info_skill.resource
def environment() -> str:
    """Get current environment configuration."""
    env = os.environ.get("APP_ENV", "development")
    region = os.environ.get("APP_REGION", "us-east-1")
    return f"Environment: {env}, Region: {region}"

@project_info_skill.resource(name="team-roster", description="Current team members")
def get_team_roster() -> str:
    """Return the team roster."""
    return "Alice Chen (Tech Lead), Bob Smith (Backend Engineer)"
```

When the decorator is used without arguments (`@skill.resource`), the function name becomes the resource name and the docstring becomes the description. Use `@skill.resource(name="...", description="...")` to set them explicitly.

### Code-defined scripts

Use the `@skill.script` decorator to register a function as an executable script on a skill. Code-defined scripts run **in-process** and do not require a script runner. Both sync and async functions are supported:

```python
from agent_framework import InlineSkill, SkillFrontmatter

unit_converter_skill = InlineSkill(
    frontmatter=SkillFrontmatter(
        name="unit-converter",
        description="Convert between common units using a conversion factor",
    ),
    instructions="Use the convert script to perform unit conversions.",
)

@unit_converter_skill.script(name="convert", description="Convert a value: result = value × factor")
def convert_units(value: float, factor: float) -> str:
    """Convert a value using a multiplication factor."""
    import json
    result = round(value * factor, 4)
    return json.dumps({"value": value, "factor": factor, "result": result})
```

When the decorator is used without arguments (`@skill.script`), the function name becomes the script name and the docstring becomes the description. The function's typed parameters are automatically converted into a JSON Schema that the agent uses to pass arguments.

:::zone-end

:::zone pivot="programming-language-go"

In addition to file-based skills discovered from `SKILL.md` files, you can define skills entirely in Go code:

```go
skill := &skills.Skill{
    Frontmatter: skills.Frontmatter{
        Name:        "unit-converter",
        Description: "Convert between common units using a multiplication factor.",
    },
    GetContent: func(context.Context) (string, error) {
        return "Use this skill when the user asks to convert between units.", nil
    },
    Resources: []skills.Resource{
        {
            Name:        "conversion-table",
            Description: "Lookup table of multiplication factors.",
            Read: func(context.Context) (any, error) {
                return conversionTable, nil
            },
        },
    },
    Scripts: []skills.Script{
        {
            Name:        "convert",
            Description: "Multiplies a value by a conversion factor. Pass value and factor as positional string arguments: [\"<value>\", \"<factor>\"].",
            Run: func(_ context.Context, _ *skills.Skill, args []string) (any, error) {
                if len(args) != 2 {
                    return nil, fmt.Errorf("expected value and factor")
                }
                value, err := strconv.ParseFloat(args[0], 64)
                if err != nil {
                    return nil, err
                }
                factor, err := strconv.ParseFloat(args[1], 64)
                if err != nil {
                    return nil, err
                }
                return map[string]any{
                    "value":  value,
                    "factor": factor,
                    "result": value * factor,
                }, nil
            },
        },
    },
}

provider := skills.NewContextProvider(skills.ContextProviderOptions{
    Skills: []*skills.Skill{skill},
})
```

`GetContent` loads the skill instructions only when the agent calls `load_skill`. Scripts receive positional CLI-style string arguments, for example `["26.2", "1.60934"]`, and can parse those arguments however the script requires.

> [!TIP]
> See the [skills examples](https://github.com/microsoft/agent-framework-go/tree/main/examples/02-agents/skills) for complete runnable samples.

:::zone-end

:::zone pivot="programming-language-csharp"

## Class-based skills

Class-based skills let you bundle all skill components - name, description, instructions, resources, and scripts - into a single C# class. This makes them easy to package and distribute as NuGet packages - teams can author and ship skills independently, and consumers add them with `dotnet add package` and a single `.UseSkill()` call. Derive from `AgentClassSkill<T>` (where `T` is your class), then annotate properties with `[AgentSkillResource]` and methods with `[AgentSkillScript]` for automatic discovery:

```csharp
using System.ComponentModel;
using System.Text.Json;
using Microsoft.Agents.AI;

internal sealed class UnitConverterSkill : AgentClassSkill<UnitConverterSkill>
{
    public override AgentSkillFrontmatter Frontmatter { get; } = new(
        "unit-converter",
        "Convert between common units using a multiplication factor. Use when asked to convert miles, kilometers, pounds, or kilograms.");

    protected override string Instructions => """
        Use this skill when the user asks to convert between units.

        1. Review the conversion-table resource to find the correct factor.
        2. Use the convert script, passing the value and factor from the table.
        3. Present the result clearly with both units.
        """;

    [AgentSkillResource("conversion-table")]
    [Description("Lookup table of multiplication factors for common unit conversions.")]
    public string ConversionTable => """
        # Conversion Tables
        Formula: **result = value × factor**
        | From       | To         | Factor   |
        |------------|------------|----------|
        | miles      | kilometers | 1.60934  |
        | kilometers | miles      | 0.621371 |
        | pounds     | kilograms  | 0.453592 |
        | kilograms  | pounds     | 2.20462  |
        """;

    [AgentSkillScript("convert")]
    [Description("Multiplies a value by a conversion factor and returns the result as JSON.")]
    private static string ConvertUnits(double value, double factor)
    {
        double result = Math.Round(value * factor, 4);
        return JsonSerializer.Serialize(new { value, factor, result });
    }
}
```

Register the class-based skill with `AgentSkillsProvider`:

```csharp
var skill = new UnitConverterSkill();
var skillsProvider = new AgentSkillsProvider(skill);
```

When the `[AgentSkillResource]` attribute is applied to a property or method, its return value is used as the resource content when the agent reads the resource - use a method when the content needs to be computed at read time. When `[AgentSkillScript]` is applied to a method, the method is invoked when the agent calls the script. Use `[Description]` from `System.ComponentModel` to describe each resource and script for the agent.

> [!NOTE]
> `AgentClassSkill<T>` also supports overriding `Resources` and `Scripts` as collections for scenarios where attribute-based discovery does not fit.

:::zone-end

:::zone pivot="programming-language-python"

## Class-based skills

Class-based skills let you bundle all skill components - name, description, instructions, resources, and scripts - into a single Python class. This makes them easy to package and distribute as PyPI packages - teams can author and ship skills independently, and consumers add them with `pip install` and a single `SkillsProvider()` call. Subclass `ClassSkill`, then use the `@ClassSkill.resource` and `@ClassSkill.script` decorators for automatic discovery:

```python
import json
from textwrap import dedent
from agent_framework import ClassSkill, SkillFrontmatter

class UnitConverterSkill(ClassSkill):
    """A unit-converter skill defined as a Python class."""

    def __init__(self) -> None:
        super().__init__(
            frontmatter=SkillFrontmatter(
                name="unit-converter",
                description=(
                    "Convert between common units using a multiplication factor. "
                    "Use when asked to convert miles, kilometers, pounds, or kilograms."
                ),
            ),
        )

    @property
    def instructions(self) -> str:
        return dedent("""\
            Use this skill when the user asks to convert between units.

            1. Review the conversion-table resource to find the correct factor.
            2. Use the convert script, passing the value and factor from the table.
            3. Present the result clearly with both units.
        """)

    @property
    @ClassSkill.resource
    def conversion_table(self) -> str:
        """Lookup table of multiplication factors for common unit conversions."""
        return dedent("""\
            # Conversion Tables
            Formula: **result = value × factor**
            | From       | To         | Factor   |
            |------------|------------|----------|
            | miles      | kilometers | 1.60934  |
            | kilometers | miles      | 0.621371 |
            | pounds     | kilograms  | 0.453592 |
            | kilograms  | pounds     | 2.20462  |
        """)

    @ClassSkill.script(name="convert", description="Multiplies a value by a conversion factor.")
    def convert_units(self, value: float, factor: float) -> str:
        """Convert a value using a multiplication factor."""
        result = round(value * factor, 4)
        return json.dumps({"value": value, "factor": factor, "result": result})
```

Register the class-based skill with `SkillsProvider`:

```python
from agent_framework import SkillsProvider

skill = UnitConverterSkill()
skills_provider = SkillsProvider(skill)
```

When `@ClassSkill.resource` is applied as a bare decorator (no arguments), the method name becomes the resource name (with underscores converted to hyphens) and the docstring becomes the description. Use `@ClassSkill.resource(name="...", description="...")` to set them explicitly. The same pattern applies to `@ClassSkill.script`.

Resources can be defined as either regular methods or `@property` descriptors. When using `@property`, place `@property` first and `@ClassSkill.resource` second. Resource return values are cached after first access.

> [!NOTE]
> `ClassSkill` also supports explicitly overriding the `resources` and `scripts` properties to return `InlineSkillResource` and `InlineSkillScript` instances directly, for scenarios where decorator-based discovery does not fit.

:::zone-end

:::zone pivot="programming-language-csharp"

## MCP-based skills

> [!NOTE]
> MCP-based skills require the `Microsoft.Agents.AI.Mcp` NuGet package. The MCP skills API is experimental and may change in future releases.

Skills can be discovered from MCP (Model Context Protocol) servers that expose skill resources under the `skill://` URI scheme. The MCP server advertises skills via a `skill://index.json` discovery document, and the framework fetches skill content on demand.

MCP-based skills support two index entry types:

- **`skill-md`** - The skill's `SKILL.md` and sibling resources are fetched on demand from the MCP server.
- **`archive`** - The skill is distributed as a single packaged archive (ZIP, TAR, or gzip-compressed TAR) that is downloaded and unpacked locally.

### Basic usage

Use the `UseMcpSkills` extension method on `AgentSkillsProviderBuilder` to add an MCP skills source:

```csharp
using Microsoft.Agents.AI;
using ModelContextProtocol.Client;

// Connect to the MCP server
await using McpClient client = await McpClient.CreateAsync(
    new StdioClientTransport(new()
    {
        Name = "skills-server",
        Command = "dotnet",
        Arguments = [skillsServerPath, "--server"],
    }));

// Build a skills provider that discovers skills over MCP
var skillsProvider = new AgentSkillsProviderBuilder()
    .UseMcpSkills(client)
    .Build();

// Create an agent with the MCP skills
AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
    .GetResponsesClient()
    .AsAIAgent(new ChatClientAgentOptions
    {
        Name = "SkillsAgent",
        ChatOptions = new()
        {
            Instructions = "You are a helpful assistant. Use available skills to answer the user.",
        },
        AIContextProviders = [skillsProvider],
    },
    model: deploymentName);
```

### Archive-type skills

For archive-type skills, use `AgentMcpSkillsSourceOptions` (from the `Microsoft.Agents.AI.Mcp` package) to configure extraction behavior:

```csharp
var skillsProvider = new AgentSkillsProviderBuilder()
    .UseMcpSkills(client, new AgentMcpSkillsSourceOptions
    {
        ArchiveSkillsDirectory = Path.Combine(AppContext.BaseDirectory, "extracted-skills"),
        ArchiveMaxFileCount = 50,
        ArchiveMaxSizeBytes = 2 * 1024 * 1024, // 2 MB
    })
    .Build();
```

`AgentMcpSkillsSourceOptions` exposes the following properties to control archive extraction:

- `ArchiveSkillsDirectory` - Base directory for extracted archives. Defaults to a unique subdirectory under the current working directory, generated per source instance to prevent collisions between multiple sources.
- `ArchiveResourceExtensions` - Allowed extensions for resources in extracted archives. Defaults to `.md`, `.json`, `.yaml`, `.yml`, `.csv`, `.xml`, `.txt`.
- `ArchiveResourceSearchDepth` - How deep to search for resources within each extracted skill directory. Defaults to `2`.
- `ArchiveMaxFileCount` - Maximum files per archive. Archives exceeding this limit are skipped. Defaults to `20`.
- `ArchiveMaxSizeBytes` - Maximum download size per archive. Defaults to `1 MB`.
- `ArchiveMaxUncompressedSizeBytes` - Maximum total uncompressed size per archive. Defaults to `1 MB`.

> [!IMPORTANT]
> Scripts bundled in archive-type skills are **never executed**. This is a deliberate security measure - executable content from remote MCP servers requires explicit trust.

:::zone-end

:::zone pivot="programming-language-python"

## MCP-based skills

> [!NOTE]
> MCP-based skills are experimental and may change in future releases. Using `MCPSkillsSource` emits a `FutureWarning` under the `MCP_SKILLS` feature flag.

Skills can be discovered from MCP (Model Context Protocol) servers that expose skill resources under the `skill://` URI scheme. The MCP server advertises skills via a `skill://index.json` discovery document, and the framework fetches each skill's `SKILL.md` body on demand via `resources/read`.

Wrap an MCP `ClientSession` in `MCPSkillsSource` and pass it to `SkillsProvider`:

```python
import os
from agent_framework import Agent, MCPSkillsSource, SkillsProvider, ToolApprovalMiddleware
from agent_framework.foundry import FoundryChatClient
from azure.identity import AzureCliCredential
from mcp.client.session import ClientSession
from mcp.client.streamable_http import streamable_http_client

mcp_url = os.environ["MCP_SKILLS_SERVER_URL"]

# Connect to the MCP server over streamable HTTP
async with streamable_http_client(url=mcp_url) as (read, write, _), ClientSession(read, write) as session:
    await session.initialize()

    # MCPSkillsSource reads skill://index.json and creates one skill per
    # skill-md entry; SKILL.md bodies are fetched on demand.
    skills_provider = SkillsProvider(MCPSkillsSource(client=session))

    client = FoundryChatClient(
        project_endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
        model=os.environ.get("FOUNDRY_MODEL", "gpt-4o-mini"),
        credential=AzureCliCredential(),
    )

    async with Agent(
        client=client,
        instructions="You are a helpful assistant. Use available skills to answer the user.",
        context_providers=[skills_provider],
        middleware=[ToolApprovalMiddleware(auto_approval_rules=[SkillsProvider.all_tools_auto_approval_rule])],
    ) as agent:
        response = await agent.run("...")
```

> [!NOTE]
> The Python `MCPSkillsSource` supports only `skill-md` index entries (index entries of any other type are silently skipped). Unlike the .NET implementation, it does **not** support archive-type skills. If `skill://index.json` is absent, unreadable, empty, or fails to parse, the source returns an empty list.

> [!IMPORTANT]
> An external MCP server controls what skill content - including instructions and scripts the agent may run - reaches the agent. Only connect `MCPSkillsSource` to servers you have vetted and trust, and treat their responses as untrusted input.

:::zone-end

:::zone pivot="programming-language-csharp"

## Skill sources

An `AgentSkillsProvider` retrieves skills from one or more **sources** - objects that implement `AgentSkillsSource`. Sources fall into two categories: **leaf sources** that discover or hold skills (such as `AgentFileSkillsSource` for file-based skills), and **decorators** that transform the output of another source (aggregation, deduplication, caching, and filtering). You can also create a [custom source](#custom-sources).

Every source implements a single method - `GetSkillsAsync(AgentSkillsSourceContext context, CancellationToken cancellationToken = default)`. The `AgentSkillsSourceContext` carries information about the current request:

- `Agent` - the `AIAgent` instance requesting skills.
- `Session` - the `AgentSession` associated with the invocation, or `null` when there is no session.

This context is available throughout the source pipeline, so a `FilteringAgentSkillsSource` predicate or a custom source can base its logic on it - for example, returning a different set of skills depending on the requesting agent.

### Leaf sources

#### `AgentFileSkillsSource`

Discovers skills from `SKILL.md` files on disk. Accepts one or more directory paths, an optional script runner, and optional `AgentFileSkillsSourceOptions` (documented in [File-based skills](#file-based-skills)).

```csharp
var source = new AgentFileSkillsSource(
    [Path.Combine(AppContext.BaseDirectory, "skills")],
    scriptRunner: SubprocessScriptRunner.RunAsync,
    options: new AgentFileSkillsSourceOptions { SearchDepth = 3 });
```

#### `AgentInMemorySkillsSource`

Wraps `AgentSkill` instances (code-defined or class-based) in memory.

```csharp
var source = new AgentInMemorySkillsSource([volumeConverterSkill, temperatureConverter]);
```

### Combinators

#### `AggregatingAgentSkillsSource`

Combines multiple sources into one. Skills are returned in registration order with no deduplication or filtering applied.

```csharp
var aggregated = new AggregatingAgentSkillsSource([fileSource, inMemorySource]);
```

### Decorators

Decorators wrap an inner source and transform its output. They can be chained to build a pipeline.

#### `DeduplicatingAgentSkillsSource`

Removes duplicate skill names (case-insensitive, first occurrence wins). Duplicates are logged at warning level.

```csharp
var deduplicated = new DeduplicatingAgentSkillsSource(innerSource);
```

#### `CachingAgentSkillsSource`

Caches the skill list returned by the inner source. Concurrent callers are serialized per cache key so only one fetch runs at a time. Accepts optional `CachingAgentSkillsSourceOptions`:

- `RefreshInterval` (`TimeSpan?`) - when set, cached results expire after this interval and the inner source is re-invoked. When `null` (the default), cached results never expire.
- `CacheIsolationKeySelector` (`Func<AgentSkillsSourceContext, string?>?`) - returns a cache key to isolate cached results by context (for example, per tenant). When `null`, all callers share a single cache bucket.

```csharp
var cached = new CachingAgentSkillsSource(innerSource, new CachingAgentSkillsSourceOptions
{
    RefreshInterval = TimeSpan.FromMinutes(5)
});
```

#### `FilteringAgentSkillsSource`

Applies a predicate to include or exclude skills. The predicate receives the skill and an `AgentSkillsSourceContext`.

```csharp
var filtered = new FilteringAgentSkillsSource(
    innerSource,
    (skill, context) => skill.Frontmatter.Name != "experimental-skill");
```

### Custom sources

When the built-in sources do not cover your scenario, implement your own. Subclass `AgentSkillsSource` for a leaf source (one that produces skills from a new origin such as a database or remote service), or subclass `DelegatingAgentSkillsSource` for a decorator that transforms another source's output.

#### Leaf source

Derive from `AgentSkillsSource` and implement `GetSkillsAsync`. The `AgentSkillsSourceContext` argument lets the source tailor its result to the current request - for example, returning a different set of skills depending on the requesting agent. Override `Dispose(bool)` if the source owns resources such as a client or connection.

```csharp
public sealed class TenantSkillsSource : AgentSkillsSource
{
    private readonly ISkillStore _store;

    public TenantSkillsSource(ISkillStore store)
    {
        _store = store;
    }

    public override async Task<IList<AgentSkill>> GetSkillsAsync(
        AgentSkillsSourceContext context,
        CancellationToken cancellationToken = default)
    {
        // Use the requesting agent to decide which skills to load.
        var tenantId = context.Agent.Name ?? "default";
        return await _store.GetSkillsForTenantAsync(tenantId, cancellationToken);
    }
}
```

#### Custom decorator

Derive from `DelegatingAgentSkillsSource`, call `InnerSource.GetSkillsAsync`, and transform or observe the result. This is the same pattern the built-in caching, deduplication, and filtering decorators use. For example, a decorator that records how many skills were returned per request without changing the result:

```csharp
public sealed class MetricsAgentSkillsSource : DelegatingAgentSkillsSource
{
    private readonly ILogger<MetricsAgentSkillsSource> _logger;

    public MetricsAgentSkillsSource(
        AgentSkillsSource innerSource,
        ILogger<MetricsAgentSkillsSource> logger)
        : base(innerSource)
    {
        _logger = logger;
    }

    public override async Task<IList<AgentSkill>> GetSkillsAsync(
        AgentSkillsSourceContext context,
        CancellationToken cancellationToken = default)
    {
        var skills = await base.GetSkillsAsync(context, cancellationToken);
        _logger.LogInformation(
            "Returned {SkillCount} skills to agent {AgentName}.",
            skills.Count,
            context.Agent.Name);
        return skills;
    }
}
```

Both custom sources can be passed to `AgentSkillsProvider` directly or nested inside a larger pipeline, just like the built-in sources.

:::zone-end

:::zone pivot="programming-language-csharp"

## Provider construction

`AgentSkillsProvider` is the component that exposes skills to an agent. It wraps one or more sources and registers the `load_skill`, `read_skill_resource`, and `run_skill_script` tools. There are three ways to create one:

1. **`AgentSkillsProviderBuilder`** - composes multiple skill types into one provider with automatic aggregation, deduplication, caching, and optional filtering. Best for scenarios that combine file-based, code-defined, class-based, and MCP-based skills.
2. **Direct source composition** - construct the source pipeline yourself using the public `AgentSkillsSource` classes. No automatic caching or deduplication is applied - you control the full pipeline. Best when you need control over ordering, conditional logic, or custom decorator behavior.
3. **Convenience constructors** - create a provider from a file path or skill instance(s) directly. Automatically applies deduplication and caching. Best for single-source scenarios.

### Using AgentSkillsProviderBuilder

Use `AgentSkillsProviderBuilder` when you need any of the following:

- **Mixed skill types** - combine file-based, code-defined (`AgentInlineSkill`), class-based (`AgentClassSkill`), and MCP-based skills in a single provider.
- **Skill filtering** - include or exclude skills using a predicate.

#### Mixed skill types

Combine multiple skill types in one provider by chaining `UseFileSkill`, `UseSkill`, `UseMcpSkills`, and `UseFileScriptRunner`:

```csharp
var skillsProvider = new AgentSkillsProviderBuilder()
    .UseFileSkill(Path.Combine(AppContext.BaseDirectory, "skills"))  // file-based skills
    .UseSkill(volumeConverterSkill)                                  // AgentInlineSkill
    .UseSkill(temperatureConverter)                                  // AgentClassSkill
    .UseMcpSkills(mcpClient)                                         // MCP-based skills
    .UseFileScriptRunner(SubprocessScriptRunner.RunAsync)            // runner for file scripts
    .Build();
```

#### Skill filtering

Use `UseFilter` to include only the skills that meet your criteria - for example, to load skills from a shared directory but exclude experimental ones:

```csharp
var approvedSkillNames = new HashSet<string> { "expense-report", "code-style" };

var skillsProvider = new AgentSkillsProviderBuilder()
    .UseFileSkill(Path.Combine(AppContext.BaseDirectory, "skills"))
    .UseFilter((skill, context) => approvedSkillNames.Contains(skill.Frontmatter.Name))
    .Build();
```

### Composing sources directly

When the builder does not offer the control you need, compose source classes yourself and pass the resulting pipeline to `AgentSkillsProvider`. See [Skill sources](#skill-sources) for the full list of available sources and their options.

The following example builds a comparable multi-source pipeline, but gives you explicit control over each decorator:

```csharp
// 1. Create the leaf sources
var fileSource = new AgentFileSkillsSource(
    [Path.Combine(AppContext.BaseDirectory, "skills")],
    SubprocessScriptRunner.RunAsync);

var inMemorySource = new AgentInMemorySkillsSource(
    [volumeConverterSkill, temperatureConverter]);

// 2. Aggregate them into one source
var aggregated = new AggregatingAgentSkillsSource([fileSource, inMemorySource]);

// 3. Add deduplication and caching decorators
var deduplicated = new DeduplicatingAgentSkillsSource(aggregated);
var cached = new CachingAgentSkillsSource(deduplicated);

// 4. Create the provider, transferring source ownership
var skillsProvider = new AgentSkillsProvider(
    cached,
    options: new AgentSkillsProviderOptions(),
    ownsSource: true);
```

> [!NOTE]
> When `ownsSource` is `true`, disposing the provider also disposes the entire source pipeline. Set it to `false` if you manage the source lifecycle yourself.

### Convenience constructors

For single-source scenarios, use the `AgentSkillsProvider` constructors directly. These automatically apply deduplication and caching without requiring a builder or manual source composition.

From a file path:

```csharp
var skillsProvider = new AgentSkillsProvider(
    Path.Combine(AppContext.BaseDirectory, "skills"),
    scriptRunner: SubprocessScriptRunner.RunAsync);
```

From skill instances:

```csharp
var skillsProvider = new AgentSkillsProvider(volumeConverterSkill, temperatureConverter);
```

:::zone-end

:::zone pivot="programming-language-python"

## Skill sources

A `SkillsProvider` retrieves skills from one or more **sources** - objects that derive from `SkillsSource`. Sources fall into two categories: **leaf sources** that discover or hold skills (such as `FileSkillsSource` for file-based skills), and **decorators** that transform the output of another source (aggregation, deduplication, caching, and filtering). You can also create a [custom source](#custom-sources).

Every source implements a single method - `async def get_skills(self, context: SkillsSourceContext) -> list[Skill]`. The `SkillsSourceContext` carries information about the current request:

- `agent` - the agent (`SupportsAgentRun`) requesting skills.
- `session` - the `AgentSession` associated with the invocation, or `None` when there is no session.

This context flows through the whole source pipeline, so a `FilteringSkillsSource` predicate or a custom source can base its logic on it - for example, returning a different set of skills depending on the requesting agent.

### Leaf sources

- **`FileSkillsSource`** - discovers skills from `SKILL.md` files on disk. Accepts one or more directory paths, an optional `script_runner`, and discovery options (`resource_extensions`, `script_extensions`, `search_depth`, `resource_filter`, `script_filter`) documented in [File-based skills](#file-based-skills).
- **`InMemorySkillsSource`** - wraps `Skill` instances (code-defined or class-based) in memory.
- **`MCPSkillsSource`** - discovers skills from an MCP server (see [MCP-based skills](#mcp-based-skills)).

```python
from pathlib import Path
from agent_framework import FileSkillsSource, InMemorySkillsSource

file_source = FileSkillsSource(Path(__file__).parent / "skills", script_runner=my_runner)
in_memory_source = InMemorySkillsSource([volume_converter_skill, temperature_converter_skill])
```

### Combinator

**`AggregatingSkillsSource`** combines multiple sources into one. Skills are returned in registration order with no deduplication or filtering applied.

```python
from agent_framework import AggregatingSkillsSource

aggregated = AggregatingSkillsSource([file_source, in_memory_source])
```

### Decorators

Decorators wrap an inner source and transform its output. They can be chained to build a pipeline.

- **`DeduplicatingSkillsSource`** - removes duplicate skill names (case-insensitive, first occurrence wins). Duplicates are logged at warning level.
- **`CachingSkillsSource`** - caches the skill list returned by the inner source. Concurrent callers for the same cache key share a single in-flight fetch, so the inner source is queried at most once per key. Accepts two optional keyword arguments:
  - `refresh_interval` (`timedelta | None`) - when set, a cached list is treated as stale once it is older than the interval, so the next call re-queries the inner source. When `None` (the default), cached results never expire. Useful for inner sources whose skills change over the process lifetime, such as `MCPSkillsSource`.
  - `cache_isolation_key_selector` (`Callable[[SkillsSourceContext], str | None]`) - derives a cache key from the context to isolate cached results (for example, per agent or tenant). Keys should be low-cardinality and stable. Returning `None` (or leaving it `None`) uses a single shared cache bucket.
- **`FilteringSkillsSource`** - applies a predicate to include or exclude skills. The predicate receives the skill **and** a `SkillsSourceContext`: `Callable[[Skill, SkillsSourceContext], bool]`.

```python
from datetime import timedelta
from agent_framework import (
    CachingSkillsSource,
    DeduplicatingSkillsSource,
    FilteringSkillsSource,
)

deduplicated = DeduplicatingSkillsSource(aggregated)

cached = CachingSkillsSource(
    deduplicated,
    refresh_interval=timedelta(minutes=5),
    cache_isolation_key_selector=lambda context: context.agent.name,
)

filtered = FilteringSkillsSource(
    cached,
    predicate=lambda skill, context: skill.frontmatter.name != "experimental-skill",
)
```

### Custom sources

When the built-in sources do not cover your scenario, implement your own. Subclass `SkillsSource` for a leaf source (one that produces skills from a new origin such as a database or remote service), or subclass `DelegatingSkillsSource` for a decorator that transforms another source's output.

#### Leaf source

Derive from `SkillsSource` and implement `get_skills`. The `SkillsSourceContext` argument lets the source tailor its result to the current request - for example, returning a different set of skills depending on the requesting agent:

```python
from agent_framework import Skill, SkillsSource, SkillsSourceContext

class TenantSkillsSource(SkillsSource):
    def __init__(self, store: "SkillStore") -> None:
        self._store = store

    async def get_skills(self, context: SkillsSourceContext) -> list[Skill]:
        # Use the requesting agent to decide which skills to load.
        tenant_id = context.agent.name or "default"
        return await self._store.get_skills_for_tenant(tenant_id)
```

#### Custom decorator

Derive from `DelegatingSkillsSource`, call `self.inner_source.get_skills(context)`, and transform or observe the result. This is the same pattern the built-in caching, deduplication, and filtering decorators use. For example, a decorator that logs how many skills were returned per request without changing the result:

```python
import logging
from agent_framework import DelegatingSkillsSource, Skill, SkillsSourceContext

logger = logging.getLogger(__name__)

class MetricsSkillsSource(DelegatingSkillsSource):
    async def get_skills(self, context: SkillsSourceContext) -> list[Skill]:
        skills = await self.inner_source.get_skills(context)
        logger.info("Returned %d skills to agent %s.", len(skills), context.agent.name)
        return skills
```

Both custom sources can be passed to `SkillsProvider` directly or nested inside a larger pipeline, just like the built-in sources.

:::zone-end

:::zone pivot="programming-language-python"

## Provider construction

`SkillsProvider` is the component that exposes skills to an agent. It wraps one or more sources and registers the `load_skill`, `read_skill_resource`, and `run_skill_script` tools. There are three ways to create one:

1. **From skill instances** - pass a single `Skill` or a sequence of skills to the constructor. Best for code-defined and class-based skills. Automatically applies deduplication and caching.
2. **From file paths** - use the `SkillsProvider.from_paths()` factory. Best for single-source file-based skills. Automatically applies deduplication and caching.
3. **Direct source composition** - construct the source pipeline yourself using the public `SkillsSource` classes and pass it to the constructor. You control the full pipeline. Best when you need control over ordering, conditional logic, caching keys, or custom decorator behavior.

### From skill instances

```python
from agent_framework import SkillsProvider

# Single skill or a list of skills - deduplicated and cached automatically.
skills_provider = SkillsProvider(volume_converter_skill)
skills_provider = SkillsProvider([volume_converter_skill, temperature_converter_skill])
```

### From file paths

```python
from pathlib import Path
from agent_framework import SkillsProvider

skills_provider = SkillsProvider.from_paths(
    skill_paths=Path(__file__).parent / "skills",
    script_runner=my_runner,
)
```

### Composing sources directly

When you need full control, compose source classes yourself and pass the resulting pipeline to `SkillsProvider`. See [Skill sources](#skill-sources) for the full list of available sources and their options.

The example below builds a multi-source pipeline with explicit control over each decorator. The example uses placeholder objects:

- `volume_converter_skill` - any `InlineSkill` instance, built as shown in [Code-defined skills](#code-defined-skills).
- `temperature_converter_skill` - any `ClassSkill` instance, built as shown in [Class-based skills](#class-based-skills).
- `my_runner` - a `SkillScriptRunner` callable, defined as shown in [Script execution](#script-execution).

```python
from pathlib import Path
from agent_framework import (
    AggregatingSkillsSource,
    CachingSkillsSource,
    DeduplicatingSkillsSource,
    FileSkillsSource,
    InMemorySkillsSource,
    SkillsProvider,
)

# 1. Create the leaf sources
file_source = FileSkillsSource(Path(__file__).parent / "skills", script_runner=my_runner)
in_memory_source = InMemorySkillsSource([volume_converter_skill, temperature_converter_skill])

# 2. Aggregate them, then add deduplication and caching decorators
aggregated = AggregatingSkillsSource([file_source, in_memory_source])
deduplicated = DeduplicatingSkillsSource(aggregated)
cached = CachingSkillsSource(deduplicated)

# 3. Create the provider from the composed pipeline
skills_provider = SkillsProvider(cached)
```

> [!IMPORTANT]
> A caller-supplied `SkillsSource` is used **as-is**: it is *not* automatically deduplicated or wrapped in a `CachingSkillsSource`. Auto-caching a context-aware source in a single shared bucket could replay one agent's or tenant's skills for another. Compose `DeduplicatingSkillsSource` and `CachingSkillsSource` (optionally with a `cache_isolation_key_selector`) yourself when you need them. The automatic deduplication and caching applies only when you pass skills or file paths directly (options 1 and 2 above).

### Mixed skill types

Combine file-based, code-defined, and class-based skills in one provider using `AggregatingSkillsSource`:

```python
from pathlib import Path
from agent_framework import (
    AggregatingSkillsSource,
    DeduplicatingSkillsSource,
    FileSkillsSource,
    InMemorySkillsSource,
    SkillsProvider,
)

temperature_converter_skill = TemperatureConverterSkill()

skills_provider = SkillsProvider(
    DeduplicatingSkillsSource(
        AggregatingSkillsSource([
            FileSkillsSource(
                Path(__file__).parent / "skills",
                script_runner=my_runner,
            ),
            InMemorySkillsSource([volume_converter_skill, temperature_converter_skill]),
        ])
    )
)
```

### Skill filtering

Use `FilteringSkillsSource` to control which skills the agent sees. The predicate receives each `Skill` and the `SkillsSourceContext`, and returns `True` to include the skill. For example, to load skills from a shared directory but hide an experimental one:

```python
from pathlib import Path
from agent_framework import (
    DeduplicatingSkillsSource,
    FileSkillsSource,
    FilteringSkillsSource,
    SkillsProvider,
)

skills_provider = SkillsProvider(
    DeduplicatingSkillsSource(
        FilteringSkillsSource(
            FileSkillsSource(Path(__file__).parent / "skills"),
            predicate=lambda skill, context: skill.frontmatter.name != "experimental-tools",
        )
    )
)
```

:::zone-end

:::zone pivot="programming-language-csharp"

## Caching behavior

By default, the builder wraps the source pipeline with a `CachingAgentSkillsSource` that caches the list of skills returned by the underlying sources. Once the skills are resolved on the first request, subsequent requests reuse the cached list without re-querying the sources. To disable caching (for example, during development when skill definitions change frequently), use `DisableCaching()` on the builder:

```csharp
var skillsProvider = new AgentSkillsProviderBuilder()
    .UseFileSkill(Path.Combine(AppContext.BaseDirectory, "skills"))
    .UseFileScriptRunner(SubprocessScriptRunner.RunAsync)
    .DisableCaching()
    .Build();
```

> [!NOTE]
> Disabling caching is useful during development when skill content changes frequently. In production, leave caching enabled (the default) for better performance.

:::zone-end

:::zone pivot="programming-language-python"

## Caching behavior

By default, skill tools and instructions are cached after the first build. Set `disable_caching=True` to force a rebuild on every invocation:

```python
skills_provider = SkillsProvider.from_paths(
    skill_paths=Path(__file__).parent / "skills",
    disable_caching=True,
)
```

`disable_caching` is also available on the `SkillsProvider` constructor for code-defined and class-based skills.

To keep caching enabled but re-discover skills periodically (for example, when a file-based or MCP source changes over the process lifetime), pass `cache_refresh_interval`. The built-in cache is treated as stale once it is older than the interval, so the next run re-queries the source:

```python
from datetime import timedelta

skills_provider = SkillsProvider.from_paths(
    skill_paths=Path(__file__).parent / "skills",
    cache_refresh_interval=timedelta(minutes=5),
)
```

`cache_refresh_interval` affects only the cache the provider builds internally (from skills or file paths); it is ignored when `disable_caching=True` and has no effect on a caller-supplied `SkillsSource` (compose your own `CachingSkillsSource` with a `refresh_interval` for those).

> [!NOTE]
> Disabling caching is useful during development when skill content changes frequently. In production, leave caching enabled (the default) for better performance.

:::zone-end

## Tool approval

:::zone pivot="programming-language-csharp"

All tools exposed by `AgentSkillsProvider` (`load_skill`, `read_skill_resource`, `run_skill_script`) require approval by default. When a tool call requires approval, the agent pauses and returns a `ToolApprovalRequestContent` instead of executing immediately. Use `UseToolApproval` middleware with auto-approval rules to selectively bypass prompts for trusted operations:

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

var skillsProvider = new AgentSkillsProvider(
    Path.Combine(AppContext.BaseDirectory, "skills"),
    SubprocessScriptRunner.RunAsync);

AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
    .GetResponsesClient()
    .AsAIAgent(new ChatClientAgentOptions
    {
        Name = "SkillsAgent",
        ChatOptions = new() { Instructions = "You are a helpful assistant." },
        AIContextProviders = [skillsProvider],
    },
    model: deploymentName)
    .AsBuilder()
    .UseToolApproval(new ToolApprovalAgentOptions
    {
        // Auto-approve read-only skill tools (load_skill, read_skill_resource).
        // run_skill_script still requires explicit user approval.
        AutoApprovalRules = [AgentSkillsProvider.ReadOnlyToolsAutoApprovalRule],
    })
    .Build();
```

To auto-approve all skill tools including script execution:

```csharp
.UseToolApproval(new ToolApprovalAgentOptions
{
    AutoApprovalRules = [AgentSkillsProvider.AllToolsAutoApprovalRule],
})
```

### Disabling approval for specific tools

Use `AgentSkillsProviderOptions` to disable approval for individual tools, removing them from the approval flow entirely:

```csharp
var skillsProvider = new AgentSkillsProvider(
    Path.Combine(AppContext.BaseDirectory, "skills"),
    SubprocessScriptRunner.RunAsync,
    options: new AgentSkillsProviderOptions
    {
        DisableLoadSkillApproval = true,
        DisableReadSkillResourceApproval = true,
        // DisableRunSkillScriptApproval remains false - scripts still require approval
    });
```

When some tools require approval and others do not in the same response, the model may call both types simultaneously. Set `EnableNonApprovalRequiredFunctionBypassing` so that approval-free tools execute immediately while the user is prompted only for the remaining ones:

```csharp
AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
    .GetResponsesClient()
    .AsAIAgent(new ChatClientAgentOptions
    {
        Name = "SkillsAgent",
        ChatOptions = new() { Instructions = "You are a helpful assistant." },
        AIContextProviders = [skillsProvider],
        EnableNonApprovalRequiredFunctionBypassing = true,
    },
    model: deploymentName)
    .AsBuilder()
    .UseToolApproval()
    .Build();
```

### Handling approval requests

When tools require approval (and no auto-approval rule matches), the agent returns `ToolApprovalRequestContent` items that must be approved or rejected before continuing:

```csharp
AgentSession session = await agent.CreateSessionAsync();
AgentResponse response = await agent.RunAsync("Convert 26.2 miles to kilometers", session);

List<ToolApprovalRequestContent> approvalRequests = response.Messages
    .SelectMany(m => m.Contents)
    .OfType<ToolApprovalRequestContent>()
    .ToList();

while (approvalRequests.Count > 0)
{
    List<ChatMessage> userInputResponses = approvalRequests
        .ConvertAll(request =>
        {
            var toolCall = (FunctionCallContent)request.ToolCall;
            Console.WriteLine($"Approve {toolCall.Name}? (Y/N)");
            bool approved = Console.ReadLine()?.Equals("Y", StringComparison.OrdinalIgnoreCase) ?? false;
            return new ChatMessage(ChatRole.User, [request.CreateResponse(approved)]);
        });

    response = await agent.RunAsync(userInputResponses, session);
    approvalRequests = response.Messages
        .SelectMany(m => m.Contents)
        .OfType<ToolApprovalRequestContent>()
        .ToList();
}
```

### Script error details

By default, when a skill script execution fails, the exception propagates to the underlying `FunctionInvokingChatClient`. If its `IncludeDetailedErrors` property is set to `true`, the exception message is forwarded to the model, enabling it to self-correct by retrying with different arguments:

```csharp
AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
    .GetResponsesClient()
    .AsAIAgent(
        options: new ChatClientAgentOptions
        {
            Name = "SkillsAgent",
            ChatOptions = new()
            {
                Instructions = "You are a helpful assistant.",
            },
            AIContextProviders = [skillsProvider],
        },
        model: deploymentName,
        clientFactory: client => client
            .AsBuilder()
            .UseFunctionInvocation(configure: (c) => c.IncludeDetailedErrors = true)
            .Build());
```

If you cannot configure `FunctionInvokingChatClient` directly, set `AgentSkillsProviderOptions.IncludeDetailedErrors` instead. This catches the exception at the skills provider level and returns the error message directly to the model:

```csharp
var skillsProvider = new AgentSkillsProvider(
    Path.Combine(AppContext.BaseDirectory, "skills"),
    SubprocessScriptRunner.RunAsync,
    options: new AgentSkillsProviderOptions
    {
        IncludeDetailedErrors = true,
    });
```

> [!WARNING]
> Either approach may disclose raw exception details to the model. Exception messages can contain sensitive information such as connection strings, file paths, or internal service names. Additionally, if skills or scripts originate from untrusted sources, a maliciously crafted script could throw an exception whose message embeds a prompt-injection payload.

:::zone-end

:::zone pivot="programming-language-python"

All tools exposed by `SkillsProvider` (`load_skill`, `read_skill_resource`, and `run_skill_script`) require approval by default. When a tool call requires approval, the agent pauses and returns approval requests via `result.user_input_requests` instead of executing immediately. You approve or reject each request with `request.to_function_approval_response(approved=...)` and send the responses back:

```python
from textwrap import dedent
from agent_framework import Agent, Content, InlineSkill, Message, SkillFrontmatter, SkillsProvider

deployment_skill = InlineSkill(
    frontmatter=SkillFrontmatter(
        name="deployment",
        description="Tools for deploying application versions to production",
    ),
    instructions=dedent("""\
        Use this skill when the user asks to deploy an application.
        Run the deploy script with the version and environment parameters.
    """),
)

@deployment_skill.script
def deploy(version: str, environment: str = "staging") -> str:
    """Deploy the application to the specified environment."""
    return f"Deployed version {version} to {environment}"

# All skill tools require approval by default.
skills_provider = SkillsProvider(deployment_skill)

async with Agent(
    client=client,
    instructions="You are a deployment assistant.",
    context_providers=[skills_provider],
) as agent:
    # Use a session so the agent retains context across approval round-trips
    session = agent.create_session()

    result = await agent.run("Deploy version 2.5.0 to production", session=session)

    # Collect a response for every request and send them in one run so the
    # loop always makes progress.
    while result.user_input_requests:
        approval_responses: list[Content] = []
        for request in result.user_input_requests:
            if request.function_call is None:
                approval_responses.append(request.to_function_approval_response(approved=False))
                continue
            print(f"Approve {request.function_call.name}? Args: {request.function_call.arguments}")
            # In a real application, prompt the user here.
            approval_responses.append(request.to_function_approval_response(approved=True))

        result = await agent.run(Message(role="user", contents=approval_responses), session=session)

    print(result)
```

When a tool call is rejected (`approved=False`), the agent is informed that the user declined and can respond accordingly.

### Auto-approving trusted tools

Rather than prompting for every call, install `ToolApprovalMiddleware` with one of the static auto-approval rules exposed by `SkillsProvider`. This lets the read-only tools run automatically while still prompting for script execution:

```python
from agent_framework import Agent, SkillsProvider, ToolApprovalMiddleware

skills_provider = SkillsProvider(deployment_skill)

# Auto-approve read-only skill tools (load_skill, read_skill_resource).
# run_skill_script still requires explicit approval via result.user_input_requests.
approval_middleware = ToolApprovalMiddleware(
    auto_approval_rules=[SkillsProvider.read_only_tools_auto_approval_rule],
)

agent = Agent(
    client=client,
    instructions="You are a deployment assistant.",
    context_providers=[skills_provider],
    middleware=[approval_middleware],
)
```

Two rules are available:

- `SkillsProvider.read_only_tools_auto_approval_rule` - approves only the read-only tools (`load_skill`, `read_skill_resource`) while still prompting for `run_skill_script`.
- `SkillsProvider.all_tools_auto_approval_rule` - approves every skill tool, including `run_skill_script` (no manual approval loop needed).

Both rules reject any call carrying a `server_label`, so they stay scoped to this provider's local tools and never auto-approve a same-named hosted tool. The rules only apply to tools that still require approval - tools opted out via the `disable_*_approval` arguments below run without approval regardless.

### Disabling approval for specific tools

For trusted skills, pass `disable_load_skill_approval`, `disable_read_skill_resource_approval`, and/or `disable_run_skill_script_approval` to opt individual tools out of the approval flow entirely (they are registered with `approval_mode="never_require"`):

```python
skills_provider = SkillsProvider(
    deployment_skill,
    disable_load_skill_approval=True,
    disable_read_skill_resource_approval=True,
    # disable_run_skill_script_approval remains False - scripts still require approval
)
```

These arguments are also available on `SkillsProvider.from_paths()`.

> [!WARNING]
> Only disable approval, or auto-approve script execution, for skills and scripts from sources you trust. Skill instructions are injected into the agent's context, and `run_skill_script` executes code supplied by the source.

:::zone-end

## Custom system prompt

By default, the skills provider injects a system prompt that lists available skills and instructs the agent to use `load_skill` and `read_skill_resource`. You can customize this prompt:

:::zone pivot="programming-language-csharp"

```csharp
var skillsProvider = new AgentSkillsProvider(
    skillPath: Path.Combine(AppContext.BaseDirectory, "skills"),
    options: new AgentSkillsProviderOptions
    {
        SkillsInstructionPrompt = """
            You have skills available. Here they are:
            {skills}
            When a task matches a skill, use load_skill to retrieve instructions,
            then read_skill_resource for referenced resources, and run_skill_script for scripts.
            """
    });
```

> [!NOTE]
> The custom template must contain `{skills}` as the placeholder for the generated skills list. Literal braces must be escaped as `{{` and `}}`.

:::zone-end

:::zone pivot="programming-language-python"

```python
skills_provider = SkillsProvider.from_paths(
    skill_paths=Path(__file__).parent / "skills",
    instruction_template=(
        "You have skills available. Here they are:\n{skills}\n"
        "{resource_instructions}\n"
        "{runner_instructions}"
    ),
)
```

> [!NOTE]
> The custom template must contain the `{skills}` placeholder for the generated skills list. It may optionally contain `{resource_instructions}` (resource tool hint) and `{runner_instructions}` (script tool hint) placeholders; when present, they are filled with built-in guidance, and when omitted they are simply not rendered (the corresponding tools are still registered). Literal braces must be escaped as `{{` and `}}`.

:::zone-end

## Injecting services and runtime arguments

Skill resource and script functions can receive external application context supplied at runtime.

:::zone pivot="programming-language-csharp"

Skill resource and script delegates can declare an `IServiceProvider` parameter that the Agent Framework injects automatically. This lets skills resolve registered application services on demand.

### Setup

Register your application services and pass the built `IServiceProvider` to the agent via the `services` parameter:

```csharp
using Microsoft.Extensions.DependencyInjection;

// Register application services
ServiceCollection services = new();
services.AddSingleton<ConversionService>();
IServiceProvider serviceProvider = services.BuildServiceProvider();

// Create the agent and pass the service provider
AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
    .GetResponsesClient()
    .AsAIAgent(
        options: new ChatClientAgentOptions
        {
            Name = "ConverterAgent",
            ChatOptions = new() { Instructions = "You are a helpful assistant." },
            AIContextProviders = [skillsProvider],
        },
        model: deploymentName,
        services: serviceProvider);
```

### Code-defined skills with DI

Declare `IServiceProvider` as a parameter in `AddResource` or `AddScript` delegates - the framework resolves and injects it automatically when the agent reads a resource or runs a script:

```csharp
var distanceSkill = new AgentInlineSkill(
    name: "distance-converter",
    description: "Convert between distance units (miles and kilometers).",
    instructions: """
        Use this skill when the user asks to convert between miles and kilometers.
        1. Read the distance-table resource for conversion factors.
        2. Use the convert script to compute the result.
        """)
    .AddResource("distance-table", (IServiceProvider sp) =>
    {
        return sp.GetRequiredService<ConversionService>().GetDistanceTable();
    })
    .AddScript("convert", (double value, double factor, IServiceProvider sp) =>
    {
        return sp.GetRequiredService<ConversionService>().Convert(value, factor);
    });
```

### Class-based skills with DI

Annotate methods with `[AgentSkillResource]` or `[AgentSkillScript]` and declare an `IServiceProvider` parameter - the framework discovers these members via reflection and injects the service provider automatically:

```csharp
internal sealed class WeightConverterSkill : AgentClassSkill<WeightConverterSkill>
{
    public override AgentSkillFrontmatter Frontmatter { get; } = new(
        "weight-converter",
        "Convert between weight units (pounds and kilograms).");

    protected override string Instructions => """
        Use this skill when the user asks to convert between pounds and kilograms.
        1. Read the weight-table resource for conversion factors.
        2. Use the convert script to compute the result.
        """;

    [AgentSkillResource("weight-table")]
    [Description("Lookup table of multiplication factors for weight conversions.")]
    private static string GetWeightTable(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<ConversionService>().GetWeightTable();
    }

    [AgentSkillScript("convert")]
    [Description("Multiplies a value by a conversion factor and returns the result as JSON.")]
    private static string Convert(double value, double factor, IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<ConversionService>().Convert(value, factor);
    }
}
```

> [!TIP]
> Class-based skills can also resolve dependencies through their **constructor**. Register the skill class in the `ServiceCollection` and resolve it from the container instead of calling `new` directly:
>
> ```csharp
> services.AddSingleton<WeightConverterSkill>();
> var weightSkill = serviceProvider.GetRequiredService<WeightConverterSkill>();
> ```
>
> This is useful when the skill class itself needs injected services beyond what the resource and script delegates use.

:::zone-end

:::zone pivot="programming-language-python"

Resource and script functions that accept `**kwargs` automatically receive runtime keyword arguments passed to `agent.run()`. This lets skill functions access application context - such as configuration, user identity, or service clients - without hard-coding them into the skill definition.

### Passing runtime arguments

Pass `function_invocation_kwargs` to `agent.run()` to supply keyword arguments that the framework forwards to resource and script functions:

```python
response = await agent.run(
    "How many kilometers is 26.2 miles?",
    function_invocation_kwargs={"precision": 2, "user_id": "alice"},
)
```

### Code-defined skills with kwargs

When a resource function declares `**kwargs`, the framework forwards the runtime keyword arguments each time the agent reads the resource:

```python
import os
from typing import Any
from agent_framework import InlineSkill, SkillFrontmatter

project_info_skill = InlineSkill(
    frontmatter=SkillFrontmatter(
        name="project-info",
        description="Project status and configuration information",
    ),
    instructions="Use this skill for questions about the current project.",
)

@project_info_skill.resource(name="environment", description="Current environment configuration")
def environment(**kwargs: Any) -> str:
    """Return environment config, optionally scoped to a user."""
    user_id = kwargs.get("user_id", "anonymous")
    env = os.environ.get("APP_ENV", "development")
    return f"Environment: {env}, Caller: {user_id}"
```

Resource functions without `**kwargs` are called with no arguments and do not receive runtime context.

When a script function declares `**kwargs`, the framework forwards the runtime keyword arguments alongside the `args` provided by the agent:

```python
import json
from typing import Any
from agent_framework import InlineSkill, SkillFrontmatter

converter_skill = InlineSkill(
    frontmatter=SkillFrontmatter(
        name="unit-converter",
        description="Convert between common units using a conversion factor",
    ),
    instructions="Use the convert script to perform unit conversions.",
)

@converter_skill.script(name="convert", description="Convert a value: result = value × factor")
def convert_units(value: float, factor: float, **kwargs: Any) -> str:
    """Convert a value using a multiplication factor.

    Args:
        value: The numeric value to convert (provided by the agent).
        factor: Conversion factor (provided by the agent).
        **kwargs: Runtime keyword arguments from agent.run().
    """
    precision = kwargs.get("precision", 4)
    result = round(value * factor, precision)
    return json.dumps({"value": value, "factor": factor, "result": result})
```

The agent provides `value` and `factor` through the tool call `args`; the application provides `precision` through `function_invocation_kwargs`. Script functions without `**kwargs` receive only the agent-provided arguments.

### Class-based skills with kwargs

Class-based skill methods can also accept `**kwargs` to receive runtime arguments. The pattern works the same way - declare `**kwargs` on resource methods or script methods:

```python
from typing import Any
from agent_framework import ClassSkill, SkillFrontmatter

class WeightConverterSkill(ClassSkill):
    def __init__(self) -> None:
        super().__init__(
            frontmatter=SkillFrontmatter(
                name="weight-converter",
                description="Convert between weight units (pounds and kilograms).",
            ),
        )

    @property
    def instructions(self) -> str:
        return "Use this skill to convert between pounds and kilograms."

    @ClassSkill.resource(name="weight-table")
    def get_weight_table(self, **kwargs: Any) -> str:
        """Weight conversion factors, scoped to caller context."""
        user_id = kwargs.get("user_id", "anonymous")
        return f"Weight table for {user_id}: | lbs | kg | 0.453592 |"

    @ClassSkill.script(name="convert")
    def convert(self, value: float, factor: float, **kwargs: Any) -> str:
        """Convert a weight value."""
        import json
        precision = kwargs.get("precision", 4)
        result = round(value * factor, precision)
        return json.dumps({"value": value, "factor": factor, "result": result})
```

:::zone-end

## Security best practices

Agent Skills should be treated like any third-party code you bring into your project.Because skill instructions are injected into the agent's context - and skills can include scripts - applying the same level of review and governance you would to an open-source dependency is essential.

- **Review before use** - Read all skill content (`SKILL.md`, scripts, and resources) before deploying. Verify that a script's actual behavior matches its stated intent. Check for adversarial instructions that attempt to bypass safety guidelines, exfiltrate data, or modify agent configuration files.
- **Source trust** - Only install skills from trusted authors or vetted internal contributors. Prefer skills with clear provenance, version control, and active maintenance. Watch for typosquatted skill names that mimic popular packages.
- **Sandboxing** - Run skills that include executable scripts in isolated environments. Limit filesystem, network, and system-level access to only what the skill requires. Require explicit user confirmation before executing potentially sensitive operations.
- **Audit and logging** - Record which skills are loaded, which resources are read, and which scripts are executed. This gives you an audit trail to trace agent behavior back to specific skill content if something goes wrong.

## When to use skills vs. workflows

Agent Skills and [Agent Framework Workflows](../concepts/workflows/index.md) both extend what agents can do, but they work in fundamentally different ways. Choose the approach that best matches your requirements:

- **Control** - With a skill, the AI decides how to execute the instructions. This is ideal when you want the agent to be creative or adaptive. With a workflow, you explicitly define the execution path. Use workflows when you need deterministic, predictable behavior.
- **Resilience** - A skill runs within a single agent turn. If something fails, the entire operation must be retried. Workflows support [checkpointing](../workflows/checkpoints.md), so they can resume from the last successful step after a failure. Choose workflows when the cost of re-executing the entire process is high.
- **Side effects** - Skills are suitable when operations are idempotent or low-risk. Prefer workflows when steps produce side effects (sending emails, charging payments) that should not be repeated on retry.
- **Complexity** - Skills are best for focused, single-domain tasks that one agent can handle. Workflows are better suited for multi-step business processes that coordinate multiple agents, human approvals, or external system integrations.

> [!TIP]
> As a rule of thumb: if you want the AI to figure out _how_ to accomplish a task, use a skill. If you need to guarantee _what_ steps execute and in what order, use a workflow.

## Next steps

> [!div class="nextstepaction"]
> [Agent Harness](../concepts/harness.md)

### Related content

- [Agent Skills specification](https://agentskills.io/)
- [Agent Harness](../concepts/harness.md)
- [Context Providers](../concepts/agents/conversations/context-providers.md)
- [Running Agents](../concepts/agents/running-agents.md)
- [Tools Overview](./tools/index.md)
