using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManagedCode.DotnetSkills.Runtime;

internal sealed class AgentCatalogPackage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private AgentCatalogPackage(DirectoryInfo catalogRoot, IReadOnlyList<AgentEntry> agents, string sourceLabel)
    {
        CatalogRoot = catalogRoot;
        Agents = agents;
        SourceLabel = sourceLabel;
    }

    public DirectoryInfo CatalogRoot { get; }

    public IReadOnlyList<AgentEntry> Agents { get; }

    public string SourceLabel { get; }

    public static AgentCatalogPackage LoadBundled()
    {
        var baseDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        return LoadFromDirectory(baseDirectory, "bundled tool payload");
    }

    public static AgentCatalogPackage LoadFromDirectory(DirectoryInfo rootDirectory, string sourceLabel)
    {
        var agents = CatalogScanner.ScanAgents(rootDirectory);
        return new AgentCatalogPackage(rootDirectory, agents, sourceLabel);
    }

    public DirectoryInfo ResolveAgentSource(string agentName)
    {
        var agent = Agents.FirstOrDefault(candidate => string.Equals(candidate.Name, agentName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Agent metadata is missing for {agentName} in {SourceLabel}");
        var directory = new DirectoryInfo(Path.Combine(CatalogRoot.FullName, agent.Path.Replace('/', Path.DirectorySeparatorChar)));
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

    [JsonPropertyName("package")]
    public string Package { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("packages")]
    public List<string> Packages { get; init; } = [];

    [JsonPropertyName("package_prefix")]
    public string PackagePrefix { get; init; } = string.Empty;

    [JsonPropertyName("links")]
    public CatalogLinks Links { get; init; } = CatalogLinks.Empty;
}
