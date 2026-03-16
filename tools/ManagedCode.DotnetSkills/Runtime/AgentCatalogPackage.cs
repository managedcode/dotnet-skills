using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManagedCode.DotnetSkills.Runtime;

internal sealed class AgentCatalogPackage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private AgentCatalogPackage(DirectoryInfo catalogRoot, DirectoryInfo agentsRoot, IReadOnlyList<AgentEntry> agents, string sourceLabel)
    {
        CatalogRoot = catalogRoot;
        AgentsRoot = agentsRoot;
        Agents = agents;
        SourceLabel = sourceLabel;
    }

    public DirectoryInfo CatalogRoot { get; }

    public DirectoryInfo AgentsRoot { get; }

    public IReadOnlyList<AgentEntry> Agents { get; }

    public string SourceLabel { get; }

    public static AgentCatalogPackage LoadBundled()
    {
        var baseDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        return LoadFromDirectory(baseDirectory, "bundled tool payload");
    }

    public static AgentCatalogPackage LoadFromDirectory(DirectoryInfo rootDirectory, string sourceLabel)
    {
        var agentsRoot = new DirectoryInfo(Path.Combine(rootDirectory.FullName, "agents"));
        var manifestPath = new FileInfo(Path.Combine(rootDirectory.FullName, "catalog", "agents.json"));

        if (!agentsRoot.Exists)
        {
            // Return empty catalog if agents directory doesn't exist
            return new AgentCatalogPackage(rootDirectory, agentsRoot, [], sourceLabel);
        }

        if (!manifestPath.Exists)
        {
            // Return empty catalog if manifest doesn't exist
            return new AgentCatalogPackage(rootDirectory, agentsRoot, [], sourceLabel);
        }

        var manifest = JsonSerializer.Deserialize<AgentManifest>(File.ReadAllText(manifestPath.FullName), JsonOptions)
            ?? throw new InvalidOperationException($"Could not parse {manifestPath.FullName}");

        return new AgentCatalogPackage(rootDirectory, agentsRoot, manifest.Agents, sourceLabel);
    }

    public DirectoryInfo ResolveAgentSource(string agentName)
    {
        var directory = new DirectoryInfo(Path.Combine(AgentsRoot.FullName, agentName));
        if (!directory.Exists)
        {
            throw new InvalidOperationException($"Agent payload is missing for {agentName} in {SourceLabel}");
        }

        return directory;
    }
}

internal sealed class AgentManifest
{
    [JsonPropertyName("agents")]
    public List<AgentEntry> Agents { get; init; } = [];
}

internal sealed class AgentEntry
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("skills")]
    public List<string> Skills { get; init; } = [];

    [JsonPropertyName("tools")]
    public string Tools { get; init; } = string.Empty;

    [JsonPropertyName("model")]
    public string Model { get; init; } = "inherit";

    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;
}
