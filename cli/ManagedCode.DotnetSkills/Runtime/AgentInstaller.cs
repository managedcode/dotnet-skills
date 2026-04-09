namespace ManagedCode.DotnetSkills.Runtime;

internal sealed class AgentInstaller(AgentCatalogPackage catalog)
{
    public IReadOnlyList<AgentEntry> SelectAgents(IReadOnlyList<string> requestedAgents, bool installAll)
    {
        if (installAll || requestedAgents.Count == 0)
        {
            return catalog.Agents.OrderBy(agent => agent.Name, StringComparer.Ordinal).ToArray();
        }

        var available = catalog.Agents.ToDictionary(agent => agent.Name, StringComparer.OrdinalIgnoreCase);
        var selected = new List<AgentEntry>();

        foreach (var agentName in requestedAgents)
        {
            if (!TryResolveAgent(available, agentName, out var agent))
            {
                throw new InvalidOperationException($"Unknown agent: {agentName}");
            }

            selected.Add(agent);
        }

        return selected;
    }

    public AgentInstallSummary Install(IReadOnlyList<AgentEntry> agents, AgentInstallLayout layout, bool force)
    {
        layout.PrimaryRoot.Create();

        var installedCount = 0;
        var skippedExisting = new List<string>();

        foreach (var agent in agents)
        {
            var sourceDirectory = catalog.ResolveAgentSource(agent.Name);
            var fileName = $"{agent.Name}{layout.FileExtension}";
            var destinationFile = ResolveInstalledAgentFile(layout, agent, fileName);

            if (destinationFile.Exists && !force)
            {
                skippedExisting.Add(agent.Name);
                continue;
            }

            switch (layout.Mode)
            {
                case AgentInstallMode.MarkdownAgentFiles:
                    WriteMarkdownAgent(destinationFile, sourceDirectory);
                    break;
                case AgentInstallMode.CopilotAgentFiles:
                    WriteCopilotAgent(destinationFile, sourceDirectory, agent);
                    break;
                case AgentInstallMode.CodexRoleFiles:
                    WriteCodexRole(destinationFile, sourceDirectory, agent);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported install mode: {layout.Mode}");
            }

            installedCount++;
        }

        return new AgentInstallSummary(installedCount, skippedExisting);
    }

    public AgentInstallSummary InstallToMultiple(IReadOnlyList<AgentEntry> agents, IReadOnlyList<AgentInstallLayout> layouts, bool force)
    {
        var totalInstalled = 0;
        var allSkipped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var layout in layouts)
        {
            var summary = Install(agents, layout, force);
            totalInstalled += summary.InstalledCount;

            foreach (var skipped in summary.SkippedExisting)
            {
                allSkipped.Add(skipped);
            }
        }

        return new AgentInstallSummary(totalInstalled, allSkipped.OrderBy(name => name, StringComparer.Ordinal).ToArray());
    }

    public AgentRemoveSummary Remove(IReadOnlyList<AgentEntry> agents, AgentInstallLayout layout)
    {
        var removedCount = 0;
        var missingAgents = new List<string>();

        foreach (var agent in agents)
        {
            var fileName = $"{agent.Name}{layout.FileExtension}";
            var destinationFile = ResolveInstalledAgentFile(layout, agent, fileName);

            if (!destinationFile.Exists)
            {
                missingAgents.Add(agent.Name);
                continue;
            }

            destinationFile.Delete();
            removedCount++;
        }

        return new AgentRemoveSummary(removedCount, missingAgents);
    }

    public bool IsInstalled(AgentEntry agent, AgentInstallLayout layout)
    {
        var fileName = $"{agent.Name}{layout.FileExtension}";
        return ResolveInstalledAgentFile(layout, agent, fileName).Exists;
    }

    public IReadOnlyList<InstalledAgentRecord> GetInstalledAgents(AgentInstallLayout layout)
    {
        return catalog.Agents
            .Where(agent => IsInstalled(agent, layout))
            .Select(agent => new InstalledAgentRecord(agent))
            .OrderBy(record => record.Agent.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool TryResolveAgent(
        IReadOnlyDictionary<string, AgentEntry> available,
        string requestedAgent,
        out AgentEntry agent)
    {
        if (available.TryGetValue(requestedAgent, out agent!))
        {
            return true;
        }

        if (!requestedAgent.StartsWith("dotnet-", StringComparison.OrdinalIgnoreCase))
        {
            return available.TryGetValue($"dotnet-{requestedAgent}", out agent!);
        }

        return false;
    }

    private static FileInfo ResolveInstalledAgentFile(AgentInstallLayout layout, AgentEntry agent, string fileName)
    {
        return PathSafety.ResolveFileWithinRoot(
            layout.PrimaryRoot,
            fileName,
            $"Installed agent path for {agent.Name}");
    }

    private static void WriteMarkdownAgent(FileInfo destinationFile, DirectoryInfo sourceDirectory)
    {
        var agentFile = new FileInfo(Path.Combine(sourceDirectory.FullName, "AGENT.md"));
        if (!agentFile.Exists)
        {
            throw new InvalidOperationException($"AGENT.md not found in {sourceDirectory.FullName}");
        }

        destinationFile.Directory?.Create();
        File.Copy(agentFile.FullName, destinationFile.FullName, overwrite: true);
    }

    private static void WriteCopilotAgent(FileInfo destinationFile, DirectoryInfo sourceDirectory, AgentEntry agent)
    {
        var agentMarkdown = ExtractAgentMarkdown(sourceDirectory);
        var tools = ParseTools(agent.Tools);
        var toolLines = string.Join(Environment.NewLine, tools.Select(tool => $"  - {tool}"));

        var contents =
            $"""
            ---
            name: {agent.Name}
            description: "{EscapeYaml(agent.Description)}"
            tools:
            {toolLines}
            ---

            {agentMarkdown}
            """;

        destinationFile.Directory?.Create();
        File.WriteAllText(destinationFile.FullName, contents);
    }

    private static void WriteCodexRole(FileInfo destinationFile, DirectoryInfo sourceDirectory, AgentEntry agent)
    {
        var agentMarkdown = ExtractAgentMarkdown(sourceDirectory);
        var lines = new List<string>
        {
            $"name = {ToTomlString(agent.Name)}",
            $"description = {ToTomlString(agent.Description)}",
        };

        if (!string.IsNullOrWhiteSpace(agent.Model) &&
            !string.Equals(agent.Model, "inherit", StringComparison.OrdinalIgnoreCase))
        {
            lines.Add($"model = {ToTomlString(agent.Model)}");
        }

        lines.Add($"developer_instructions = {ToTomlString(agentMarkdown)}");

        destinationFile.Directory?.Create();
        File.WriteAllText(destinationFile.FullName, string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    private static IReadOnlyList<string> ParseTools(string tools)
    {
        if (string.IsNullOrWhiteSpace(tools))
        {
            return ["codebase", "terminal"];
        }

        return tools
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(tool => tool.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string ExtractAgentMarkdown(DirectoryInfo sourceDirectory)
    {
        var agentFile = new FileInfo(Path.Combine(sourceDirectory.FullName, "AGENT.md"));
        if (!agentFile.Exists)
        {
            throw new InvalidOperationException($"Agent file not found: {agentFile.FullName}");
        }

        var text = File.ReadAllText(agentFile.FullName);
        if (!text.StartsWith("---\n", StringComparison.Ordinal))
        {
            return text.Trim();
        }

        var marker = "\n---\n";
        var markerIndex = text.IndexOf(marker, startIndex: 4, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return text.Trim();
        }

        return text[(markerIndex + marker.Length)..].Trim();
    }

    private static string EscapeYaml(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string ToTomlString(string value)
    {
        return "\"" + value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t") + "\"";
    }
}

internal sealed record AgentInstallSummary(int InstalledCount, IReadOnlyList<string> SkippedExisting);
internal sealed record AgentRemoveSummary(int RemovedCount, IReadOnlyList<string> MissingAgents);
internal sealed record InstalledAgentRecord(AgentEntry Agent);
