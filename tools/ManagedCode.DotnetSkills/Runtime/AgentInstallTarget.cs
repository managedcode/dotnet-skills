namespace ManagedCode.DotnetSkills.Runtime;

internal enum AgentInstallMode
{
    RawAgentPayloads,
    ClaudeSubagents,
    CopilotAgents,
}

internal sealed record AgentInstallLayout(
    AgentPlatform Agent,
    InstallScope Scope,
    AgentInstallMode Mode,
    DirectoryInfo PrimaryRoot,
    bool IsExplicitTarget)
{
    public string PrimaryPath => PrimaryRoot.FullName;

    public string ReloadHint => Agent switch
    {
        AgentPlatform.Auto => "Restart your agent session to pick up new agents.",
        AgentPlatform.Codex => "Restart Codex to pick up new agents.",
        AgentPlatform.Claude => "Restart Claude Code or run /agents to pick up the generated subagents.",
        AgentPlatform.Copilot => "Restart Copilot or your IDE to pick up new agents.",
        AgentPlatform.Gemini => "Run /agents reload or restart Gemini CLI to pick up new agents.",
        _ => "Restart your agent session to pick up new agents.",
    };

    public string FileExtension => Mode switch
    {
        AgentInstallMode.ClaudeSubagents => ".md",
        AgentInstallMode.CopilotAgents => ".agent.md",
        AgentInstallMode.RawAgentPayloads => ".md",
        _ => ".md",
    };
}

internal static class AgentInstallTarget
{
    public static AgentInstallLayout Resolve(
        string? explicitTargetPath,
        AgentPlatform agent,
        InstallScope scope,
        string? projectDirectory)
    {
        if (!string.IsNullOrWhiteSpace(explicitTargetPath))
        {
            return ResolveExplicit(agent, scope, explicitTargetPath);
        }

        if (agent == AgentPlatform.Auto)
        {
            return scope switch
            {
                InstallScope.Global => ResolveAutoGlobal(),
                InstallScope.Project => ResolveAutoProject(projectDirectory),
                _ => throw new InvalidOperationException($"Unsupported install scope: {scope}"),
            };
        }

        return scope switch
        {
            InstallScope.Global => ResolveGlobal(agent),
            InstallScope.Project => ResolveProject(agent, projectDirectory),
            _ => throw new InvalidOperationException($"Unsupported install scope: {scope}"),
        };
    }

    public static IReadOnlyList<AgentInstallLayout> ResolveAllDetected(string? projectDirectory, InstallScope scope)
    {
        var layouts = new List<AgentInstallLayout>();
        var rootDirectory = ResolveProjectRoot(projectDirectory);
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (scope == InstallScope.Project)
        {
            // Check for Claude
            if (Directory.Exists(Path.Combine(rootDirectory, ".claude")))
            {
                layouts.Add(new AgentInstallLayout(
                    AgentPlatform.Claude,
                    InstallScope.Project,
                    AgentInstallMode.ClaudeSubagents,
                    new DirectoryInfo(Path.Combine(rootDirectory, ".claude", "agents")),
                    IsExplicitTarget: false));
            }

            // Check for Copilot
            if (Directory.Exists(Path.Combine(rootDirectory, ".github")))
            {
                layouts.Add(new AgentInstallLayout(
                    AgentPlatform.Copilot,
                    InstallScope.Project,
                    AgentInstallMode.CopilotAgents,
                    new DirectoryInfo(Path.Combine(rootDirectory, ".github", "agents")),
                    IsExplicitTarget: false));
            }

            // Check for Gemini
            if (Directory.Exists(Path.Combine(rootDirectory, ".gemini")))
            {
                layouts.Add(new AgentInstallLayout(
                    AgentPlatform.Gemini,
                    InstallScope.Project,
                    AgentInstallMode.RawAgentPayloads,
                    new DirectoryInfo(Path.Combine(rootDirectory, ".gemini", "agents")),
                    IsExplicitTarget: false));
            }

            // Check for Codex (.agents directory)
            if (Directory.Exists(Path.Combine(rootDirectory, ".agents")) || Directory.Exists(Path.Combine(rootDirectory, ".codex")))
            {
                layouts.Add(new AgentInstallLayout(
                    AgentPlatform.Codex,
                    InstallScope.Project,
                    AgentInstallMode.RawAgentPayloads,
                    new DirectoryInfo(Path.Combine(rootDirectory, ".agents", "skills")),
                    IsExplicitTarget: false));
            }
        }
        else // Global
        {
            // Check for Claude global
            if (Directory.Exists(Path.Combine(userHome, ".claude")))
            {
                layouts.Add(new AgentInstallLayout(
                    AgentPlatform.Claude,
                    InstallScope.Global,
                    AgentInstallMode.ClaudeSubagents,
                    new DirectoryInfo(Path.Combine(userHome, ".claude", "agents")),
                    IsExplicitTarget: false));
            }

            // Check for Gemini global
            if (Directory.Exists(Path.Combine(userHome, ".gemini")))
            {
                layouts.Add(new AgentInstallLayout(
                    AgentPlatform.Gemini,
                    InstallScope.Global,
                    AgentInstallMode.RawAgentPayloads,
                    new DirectoryInfo(Path.Combine(userHome, ".gemini", "agents")),
                    IsExplicitTarget: false));
            }

            // Check for Codex global
            if (Directory.Exists(Path.Combine(userHome, ".agents")) || Directory.Exists(Path.Combine(userHome, ".codex")))
            {
                layouts.Add(new AgentInstallLayout(
                    AgentPlatform.Codex,
                    InstallScope.Global,
                    AgentInstallMode.RawAgentPayloads,
                    new DirectoryInfo(Path.Combine(userHome, ".agents", "skills")),
                    IsExplicitTarget: false));
            }
        }

        return layouts;
    }

    private static AgentInstallLayout ResolveExplicit(AgentPlatform agent, InstallScope scope, string explicitTargetPath)
    {
        var targetRoot = new DirectoryInfo(Path.GetFullPath(explicitTargetPath));
        var mode = agent switch
        {
            AgentPlatform.Claude => AgentInstallMode.ClaudeSubagents,
            AgentPlatform.Copilot => AgentInstallMode.CopilotAgents,
            _ => AgentInstallMode.RawAgentPayloads,
        };

        return new AgentInstallLayout(agent, scope, mode, targetRoot, IsExplicitTarget: true);
    }

    private static AgentInstallLayout ResolveAutoGlobal()
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Prefer Claude for global agents
        if (Directory.Exists(Path.Combine(userHome, ".claude")))
        {
            return new AgentInstallLayout(
                AgentPlatform.Claude,
                InstallScope.Global,
                AgentInstallMode.ClaudeSubagents,
                new DirectoryInfo(Path.Combine(userHome, ".claude", "agents")),
                IsExplicitTarget: false);
        }

        // Then Gemini
        if (Directory.Exists(Path.Combine(userHome, ".gemini")))
        {
            return new AgentInstallLayout(
                AgentPlatform.Gemini,
                InstallScope.Global,
                AgentInstallMode.RawAgentPayloads,
                new DirectoryInfo(Path.Combine(userHome, ".gemini", "agents")),
                IsExplicitTarget: false);
        }

        // Fallback to Codex
        return new AgentInstallLayout(
            AgentPlatform.Codex,
            InstallScope.Global,
            AgentInstallMode.RawAgentPayloads,
            new DirectoryInfo(Path.Combine(userHome, ".agents", "skills")),
            IsExplicitTarget: false);
    }

    private static AgentInstallLayout ResolveGlobal(AgentPlatform agent)
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return agent switch
        {
            AgentPlatform.Auto => ResolveAutoGlobal(),
            AgentPlatform.Claude => new AgentInstallLayout(
                agent,
                InstallScope.Global,
                AgentInstallMode.ClaudeSubagents,
                new DirectoryInfo(Path.Combine(userHome, ".claude", "agents")),
                IsExplicitTarget: false),
            AgentPlatform.Copilot => throw new InvalidOperationException("Copilot does not support global agent installation. Use project scope instead."),
            AgentPlatform.Gemini => new AgentInstallLayout(
                agent,
                InstallScope.Global,
                AgentInstallMode.RawAgentPayloads,
                new DirectoryInfo(Path.Combine(userHome, ".gemini", "agents")),
                IsExplicitTarget: false),
            AgentPlatform.Codex => new AgentInstallLayout(
                agent,
                InstallScope.Global,
                AgentInstallMode.RawAgentPayloads,
                new DirectoryInfo(Path.Combine(userHome, ".agents", "skills")),
                IsExplicitTarget: false),
            _ => throw new InvalidOperationException($"Unsupported agent: {agent}"),
        };
    }

    private static AgentInstallLayout ResolveAutoProject(string? projectDirectory)
    {
        var rootDirectory = ResolveProjectRoot(projectDirectory);

        // Prefer Claude
        if (Directory.Exists(Path.Combine(rootDirectory, ".claude")))
        {
            return new AgentInstallLayout(
                AgentPlatform.Claude,
                InstallScope.Project,
                AgentInstallMode.ClaudeSubagents,
                new DirectoryInfo(Path.Combine(rootDirectory, ".claude", "agents")),
                IsExplicitTarget: false);
        }

        // Then Copilot
        if (Directory.Exists(Path.Combine(rootDirectory, ".github")))
        {
            return new AgentInstallLayout(
                AgentPlatform.Copilot,
                InstallScope.Project,
                AgentInstallMode.CopilotAgents,
                new DirectoryInfo(Path.Combine(rootDirectory, ".github", "agents")),
                IsExplicitTarget: false);
        }

        // Then Gemini
        if (Directory.Exists(Path.Combine(rootDirectory, ".gemini")))
        {
            return new AgentInstallLayout(
                AgentPlatform.Gemini,
                InstallScope.Project,
                AgentInstallMode.RawAgentPayloads,
                new DirectoryInfo(Path.Combine(rootDirectory, ".gemini", "agents")),
                IsExplicitTarget: false);
        }

        // Codex
        if (Directory.Exists(Path.Combine(rootDirectory, ".agents")) || Directory.Exists(Path.Combine(rootDirectory, ".codex")))
        {
            return new AgentInstallLayout(
                AgentPlatform.Codex,
                InstallScope.Project,
                AgentInstallMode.RawAgentPayloads,
                new DirectoryInfo(Path.Combine(rootDirectory, ".agents", "skills")),
                IsExplicitTarget: false);
        }

        // Default fallback to Claude
        return new AgentInstallLayout(
            AgentPlatform.Claude,
            InstallScope.Project,
            AgentInstallMode.ClaudeSubagents,
            new DirectoryInfo(Path.Combine(rootDirectory, ".claude", "agents")),
            IsExplicitTarget: false);
    }

    private static AgentInstallLayout ResolveProject(AgentPlatform agent, string? projectDirectory)
    {
        var rootDirectory = ResolveProjectRoot(projectDirectory);

        return agent switch
        {
            AgentPlatform.Auto => ResolveAutoProject(rootDirectory),
            AgentPlatform.Claude => new AgentInstallLayout(
                agent,
                InstallScope.Project,
                AgentInstallMode.ClaudeSubagents,
                new DirectoryInfo(Path.Combine(rootDirectory, ".claude", "agents")),
                IsExplicitTarget: false),
            AgentPlatform.Copilot => new AgentInstallLayout(
                agent,
                InstallScope.Project,
                AgentInstallMode.CopilotAgents,
                new DirectoryInfo(Path.Combine(rootDirectory, ".github", "agents")),
                IsExplicitTarget: false),
            AgentPlatform.Gemini => new AgentInstallLayout(
                agent,
                InstallScope.Project,
                AgentInstallMode.RawAgentPayloads,
                new DirectoryInfo(Path.Combine(rootDirectory, ".gemini", "agents")),
                IsExplicitTarget: false),
            AgentPlatform.Codex => new AgentInstallLayout(
                agent,
                InstallScope.Project,
                AgentInstallMode.RawAgentPayloads,
                new DirectoryInfo(Path.Combine(rootDirectory, ".agents", "skills")),
                IsExplicitTarget: false),
            _ => throw new InvalidOperationException($"Unsupported agent: {agent}"),
        };
    }

    private static string ResolveProjectRoot(string? projectDirectory) => string.IsNullOrWhiteSpace(projectDirectory)
        ? Path.GetFullPath(Directory.GetCurrentDirectory())
        : Path.GetFullPath(projectDirectory);
}
