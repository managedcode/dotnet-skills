namespace ManagedCode.DotnetSkills.Runtime;

internal enum AgentPlatform
{
    Auto,
    Codex,
    Claude,
    Copilot,
    Gemini,
}

internal enum InstallScope
{
    Global,
    Project,
}

internal enum SkillInstallMode
{
    RawSkillPayloads,
    ClaudeSubagents,
}

internal sealed record SkillInstallLayout(
    AgentPlatform Agent,
    InstallScope Scope,
    SkillInstallMode Mode,
    DirectoryInfo PrimaryRoot,
    bool IsExplicitTarget)
{
    public string PrimaryPath => PrimaryRoot.FullName;

    public string ReloadHint => Agent switch
    {
        AgentPlatform.Auto => "Restart your agent session to pick up new skills.",
        AgentPlatform.Codex => "Restart Codex to pick up new skills.",
        AgentPlatform.Claude => "Restart Claude Code or start a new session to pick up the generated subagents.",
        AgentPlatform.Copilot => "Restart Copilot CLI or your IDE agent session to pick up new skills.",
        AgentPlatform.Gemini => "Run /skills reload or restart Gemini CLI to pick up new skills.",
        _ => "Restart your agent session to pick up new skills.",
    };
}

internal static class SkillInstallTarget
{
    public static SkillInstallLayout Resolve(
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

    public static AgentPlatform ParseAgent(string value) => value.ToLowerInvariant() switch
    {
        "auto" => AgentPlatform.Auto,
        "codex" => AgentPlatform.Codex,
        "openai" => AgentPlatform.Codex,
        "claude" => AgentPlatform.Claude,
        "anthropic" => AgentPlatform.Claude,
        "copilot" => AgentPlatform.Copilot,
        "github" => AgentPlatform.Copilot,
        "github-copilot" => AgentPlatform.Copilot,
        "gemini" => AgentPlatform.Gemini,
        "google" => AgentPlatform.Gemini,
        "google-gemini" => AgentPlatform.Gemini,
        _ => throw new InvalidOperationException("Unsupported agent: " + value + ". Expected auto, codex, openai, claude, anthropic, copilot, github-copilot, gemini, or google-gemini."),
    };

    public static InstallScope ParseScope(string value) => value.ToLowerInvariant() switch
    {
        "global" => InstallScope.Global,
        "project" => InstallScope.Project,
        _ => throw new InvalidOperationException($"Unsupported scope: {value}. Expected global or project."),
    };

    private static SkillInstallLayout ResolveExplicit(AgentPlatform agent, InstallScope scope, string explicitTargetPath)
    {
        var targetRoot = new DirectoryInfo(Path.GetFullPath(explicitTargetPath));
        var mode = agent == AgentPlatform.Claude
            ? SkillInstallMode.ClaudeSubagents
            : SkillInstallMode.RawSkillPayloads;

        return new SkillInstallLayout(agent, scope, mode, targetRoot, IsExplicitTarget: true);
    }

    private static SkillInstallLayout ResolveAutoGlobal()
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var codexRoot = ResolveCodexGlobal(userHome);
        if (Directory.Exists(codexRoot.Parent?.FullName))
        {
            return new SkillInstallLayout(AgentPlatform.Codex, InstallScope.Global, SkillInstallMode.RawSkillPayloads, codexRoot, IsExplicitTarget: false);
        }

        var claudeRoot = new DirectoryInfo(Path.Combine(userHome, ".claude", "agents"));
        if (Directory.Exists(Path.Combine(userHome, ".claude")))
        {
            return new SkillInstallLayout(AgentPlatform.Claude, InstallScope.Global, SkillInstallMode.ClaudeSubagents, claudeRoot, IsExplicitTarget: false);
        }

        var copilotRoot = new DirectoryInfo(Path.Combine(userHome, ".copilot", "skills"));
        if (Directory.Exists(Path.Combine(userHome, ".copilot")))
        {
            return new SkillInstallLayout(AgentPlatform.Copilot, InstallScope.Global, SkillInstallMode.RawSkillPayloads, copilotRoot, IsExplicitTarget: false);
        }

        var geminiRoot = ResolveGeminiGlobal(userHome);
        if (Directory.Exists(Path.Combine(userHome, ".gemini")) || Directory.Exists(Path.Combine(userHome, ".agents")))
        {
            return new SkillInstallLayout(AgentPlatform.Gemini, InstallScope.Global, SkillInstallMode.RawSkillPayloads, geminiRoot, IsExplicitTarget: false);
        }

        return new SkillInstallLayout(
            AgentPlatform.Auto,
            InstallScope.Global,
            SkillInstallMode.RawSkillPayloads,
            new DirectoryInfo(Path.Combine(userHome, "skills")),
            IsExplicitTarget: false);
    }

    private static SkillInstallLayout ResolveGlobal(AgentPlatform agent)
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return agent switch
        {
            AgentPlatform.Auto => ResolveAutoGlobal(),
            AgentPlatform.Codex => new SkillInstallLayout(agent, InstallScope.Global, SkillInstallMode.RawSkillPayloads, ResolveCodexGlobal(userHome), IsExplicitTarget: false),
            AgentPlatform.Claude => new SkillInstallLayout(
                agent,
                InstallScope.Global,
                SkillInstallMode.ClaudeSubagents,
                new DirectoryInfo(Path.Combine(userHome, ".claude", "agents")),
                IsExplicitTarget: false),
            AgentPlatform.Copilot => new SkillInstallLayout(agent, InstallScope.Global, SkillInstallMode.RawSkillPayloads, new DirectoryInfo(Path.Combine(userHome, ".copilot", "skills")), IsExplicitTarget: false),
            AgentPlatform.Gemini => new SkillInstallLayout(agent, InstallScope.Global, SkillInstallMode.RawSkillPayloads, new DirectoryInfo(Path.Combine(userHome, ".gemini", "skills")), IsExplicitTarget: false),
            _ => throw new InvalidOperationException($"Unsupported agent: {agent}"),
        };
    }

    private static SkillInstallLayout ResolveAutoProject(string? projectDirectory)
    {
        var rootDirectory = ResolveProjectRoot(projectDirectory);

        // Codex uses .agents/skills/ for project-level skills
        if (Directory.Exists(Path.Combine(rootDirectory, ".agents")) || Directory.Exists(Path.Combine(rootDirectory, ".codex")))
        {
            return new SkillInstallLayout(
                AgentPlatform.Codex,
                InstallScope.Project,
                SkillInstallMode.RawSkillPayloads,
                new DirectoryInfo(Path.Combine(rootDirectory, ".agents", "skills")),
                IsExplicitTarget: false);
        }

        if (Directory.Exists(Path.Combine(rootDirectory, ".claude")))
        {
            return new SkillInstallLayout(
                AgentPlatform.Claude,
                InstallScope.Project,
                SkillInstallMode.ClaudeSubagents,
                new DirectoryInfo(Path.Combine(rootDirectory, ".claude", "agents")),
                IsExplicitTarget: false);
        }

        if (Directory.Exists(Path.Combine(rootDirectory, ".github")))
        {
            return new SkillInstallLayout(
                AgentPlatform.Copilot,
                InstallScope.Project,
                SkillInstallMode.RawSkillPayloads,
                new DirectoryInfo(Path.Combine(rootDirectory, ".github", "skills")),
                IsExplicitTarget: false);
        }

        if (Directory.Exists(Path.Combine(rootDirectory, ".gemini")) || Directory.Exists(Path.Combine(rootDirectory, ".agents")))
        {
            return new SkillInstallLayout(
                AgentPlatform.Gemini,
                InstallScope.Project,
                SkillInstallMode.RawSkillPayloads,
                ResolveGeminiProject(rootDirectory),
                IsExplicitTarget: false);
        }

        return new SkillInstallLayout(
            AgentPlatform.Auto,
            InstallScope.Project,
            SkillInstallMode.RawSkillPayloads,
            new DirectoryInfo(Path.Combine(rootDirectory, "skills")),
            IsExplicitTarget: false);
    }

    private static SkillInstallLayout ResolveProject(AgentPlatform agent, string? projectDirectory)
    {
        var rootDirectory = ResolveProjectRoot(projectDirectory);

        return agent switch
        {
            AgentPlatform.Auto => ResolveAutoProject(rootDirectory),
            AgentPlatform.Codex => new SkillInstallLayout(agent, InstallScope.Project, SkillInstallMode.RawSkillPayloads, new DirectoryInfo(Path.Combine(rootDirectory, ".agents", "skills")), IsExplicitTarget: false),
            AgentPlatform.Claude => new SkillInstallLayout(
                agent,
                InstallScope.Project,
                SkillInstallMode.ClaudeSubagents,
                new DirectoryInfo(Path.Combine(rootDirectory, ".claude", "agents")),
                IsExplicitTarget: false),
            AgentPlatform.Copilot => new SkillInstallLayout(agent, InstallScope.Project, SkillInstallMode.RawSkillPayloads, new DirectoryInfo(Path.Combine(rootDirectory, ".github", "skills")), IsExplicitTarget: false),
            AgentPlatform.Gemini => new SkillInstallLayout(agent, InstallScope.Project, SkillInstallMode.RawSkillPayloads, new DirectoryInfo(Path.Combine(rootDirectory, ".gemini", "skills")), IsExplicitTarget: false),
            _ => throw new InvalidOperationException($"Unsupported agent: {agent}"),
        };
    }

    private static string ResolveProjectRoot(string? projectDirectory) => string.IsNullOrWhiteSpace(projectDirectory)
        ? Path.GetFullPath(Directory.GetCurrentDirectory())
        : Path.GetFullPath(projectDirectory);

    private static DirectoryInfo ResolveCodexGlobal(string userHome)
    {
        var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (!string.IsNullOrWhiteSpace(codexHome))
        {
            return new DirectoryInfo(Path.Combine(codexHome, "skills"));
        }

        // Codex uses ~/.agents/skills/ for global skills
        return new DirectoryInfo(Path.Combine(userHome, ".agents", "skills"));
    }

    private static DirectoryInfo ResolveGeminiGlobal(string userHome)
    {
        var geminiSkills = Path.Combine(userHome, ".gemini", "skills");
        if (Directory.Exists(Path.Combine(userHome, ".gemini")))
        {
            return new DirectoryInfo(geminiSkills);
        }

        return new DirectoryInfo(Path.Combine(userHome, ".agents", "skills"));
    }

    private static DirectoryInfo ResolveGeminiProject(string rootDirectory)
    {
        if (Directory.Exists(Path.Combine(rootDirectory, ".gemini")))
        {
            return new DirectoryInfo(Path.Combine(rootDirectory, ".gemini", "skills"));
        }

        return new DirectoryInfo(Path.Combine(rootDirectory, ".agents", "skills"));
    }
}
