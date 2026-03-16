using System.Text.RegularExpressions;

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
            var destinationFile = new FileInfo(Path.Combine(layout.PrimaryRoot.FullName, fileName));

            if (destinationFile.Exists && !force)
            {
                skippedExisting.Add(agent.Name);
                continue;
            }

            switch (layout.Mode)
            {
                case AgentInstallMode.ClaudeSubagents:
                    WriteClaudeSubagent(destinationFile, sourceDirectory, agent);
                    break;
                case AgentInstallMode.CopilotAgents:
                    WriteCopilotAgent(destinationFile, sourceDirectory, agent);
                    break;
                case AgentInstallMode.RawAgentPayloads:
                    WriteRawAgent(destinationFile, sourceDirectory, agent);
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
        var allSkipped = new List<string>();

        foreach (var layout in layouts)
        {
            var summary = Install(agents, layout, force);
            totalInstalled += summary.InstalledCount;
            // Don't add to skipped if installed in another layout
        }

        return new AgentInstallSummary(totalInstalled, allSkipped);
    }

    public AgentRemoveSummary Remove(IReadOnlyList<AgentEntry> agents, AgentInstallLayout layout)
    {
        var removedCount = 0;
        var missingAgents = new List<string>();

        foreach (var agent in agents)
        {
            var fileName = $"{agent.Name}{layout.FileExtension}";
            var destinationFile = new FileInfo(Path.Combine(layout.PrimaryRoot.FullName, fileName));

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
        return File.Exists(Path.Combine(layout.PrimaryRoot.FullName, fileName));
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

    private static void WriteClaudeSubagent(FileInfo destinationFile, DirectoryInfo sourceDirectory, AgentEntry agent)
    {
        var agentMarkdown = ExtractAgentMarkdown(sourceDirectory);
        var skillsList = agent.Skills.Count > 0
            ? $"\nskills:\n{string.Join("\n", agent.Skills.Select(s => $"  - {s}"))}"
            : "";

        var contents =
            $"""
            ---
            name: {agent.Name}
            description: "{EscapeYaml(agent.Description)}"
            tools: {(string.IsNullOrEmpty(agent.Tools) ? "Read, Glob, Grep, Bash" : agent.Tools)}
            model: {agent.Model}{skillsList}
            ---

            {agentMarkdown}
            """;

        File.WriteAllText(destinationFile.FullName, contents);
    }

    private static void WriteCopilotAgent(FileInfo destinationFile, DirectoryInfo sourceDirectory, AgentEntry agent)
    {
        var agentMarkdown = ExtractAgentMarkdown(sourceDirectory);

        // Copilot uses different tool format
        var tools = string.IsNullOrEmpty(agent.Tools)
            ? "- codebase\n  - terminal"
            : string.Join("\n", agent.Tools.Split(',', StringSplitOptions.TrimEntries).Select(t => $"  - {t.ToLowerInvariant()}"));

        var contents =
            $"""
            ---
            name: {agent.Name}
            description: "{EscapeYaml(agent.Description)}"
            tools:
            {tools}
            ---

            {agentMarkdown}
            """;

        File.WriteAllText(destinationFile.FullName, contents);
    }

    private static void WriteRawAgent(FileInfo destinationFile, DirectoryInfo sourceDirectory, AgentEntry agent)
    {
        // Copy the AGENT.md as-is for platforms that support raw format
        var agentFile = new FileInfo(Path.Combine(sourceDirectory.FullName, "AGENT.md"));
        if (!agentFile.Exists)
        {
            throw new InvalidOperationException($"AGENT.md not found in {sourceDirectory.FullName}");
        }

        File.Copy(agentFile.FullName, destinationFile.FullName, overwrite: true);
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

    private static string EscapeYaml(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}

internal sealed record AgentInstallSummary(int InstalledCount, IReadOnlyList<string> SkippedExisting);
internal sealed record AgentRemoveSummary(int RemovedCount, IReadOnlyList<string> MissingAgents);
internal sealed record InstalledAgentRecord(AgentEntry Agent);
