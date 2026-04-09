using ManagedCode.DotnetSkills.Runtime;

namespace ManagedCode.DotnetSkills.Tests;

public sealed class SkillCatalogPackageTests
{
    [Fact]
    public void LoadFromDirectory_ScansCatalogTree()
    {
        using var tempDirectory = new TemporaryDirectory();

        static void WritePackageManifest(string root, string type, string package, string json)
        {
            var packageDirectory = Directory.CreateDirectory(Path.Combine(root, "catalog", type, package));
            File.WriteAllText(Path.Combine(packageDirectory.FullName, "manifest.json"), json);
        }

        static void WriteSkill(string root, string type, string package, string skillName, string title)
        {
            var skillDirectory = Directory.CreateDirectory(Path.Combine(root, "catalog", type, package, "skills", skillName));
            File.WriteAllText(
                Path.Combine(skillDirectory.FullName, "SKILL.md"),
                $"""
                ---
                name: {skillName}
                description: "{title}"
                compatibility: "codex,claude,copilot,gemini"
                ---

                # {title}
                """);
        }

        static void WriteSkillManifest(string root, string type, string package, string skillName, string json)
        {
            var skillDirectory = Directory.CreateDirectory(Path.Combine(root, "catalog", type, package, "skills", skillName));
            File.WriteAllText(Path.Combine(skillDirectory.FullName, "manifest.json"), json);
        }

        WritePackageManifest(
            tempDirectory.Path,
            "Frameworks",
            "Aspire",
            """
            {
              "name": "Aspire",
              "title": ".NET Aspire",
              "description": "Cloud-native app host for distributed .NET apps",
              "links": {
                "repository": "https://github.com/dotnet/aspire",
                "docs": "https://learn.microsoft.com/en-us/dotnet/aspire/",
                "nuget": "https://www.nuget.org/packages/Aspire.Hosting"
              }
            }
            """);
        WriteSkill(tempDirectory.Path, "Frameworks", "Aspire", "dotnet-aspire", ".NET Aspire");
        WriteSkillManifest(tempDirectory.Path, "Frameworks", "Aspire", "dotnet-aspire", """{"version":"1.0.0","category":"Cloud","package_prefix":"Aspire"}""");

        WritePackageManifest(tempDirectory.Path, "Platform", "MCAF", """{"name":"MCAF","title":"MCAF","description":"","links":{}}""");
        foreach (var skillName in new[]
                 {
                     "dotnet-mcaf",
                     "dotnet-mcaf-agile-delivery",
                     "dotnet-mcaf-devex",
                     "dotnet-mcaf-documentation",
                     "dotnet-mcaf-feature-spec",
                     "dotnet-mcaf-human-review-planning",
                     "dotnet-mcaf-ml-ai-delivery",
                     "dotnet-mcaf-nfr",
                     "dotnet-mcaf-source-control",
                     "dotnet-mcaf-ui-ux",
                 })
        {
            WriteSkill(tempDirectory.Path, "Platform", "MCAF", skillName, skillName);
            WriteSkillManifest(tempDirectory.Path, "Platform", "MCAF", skillName, """{"version":"1.0.0","category":"Architecture"}""");
        }

        WritePackageManifest(tempDirectory.Path, "Frameworks", "Orleans", """{"name":"Orleans","title":"Orleans","description":"","links":{}}""");
        WriteSkill(tempDirectory.Path, "Frameworks", "Orleans", "dotnet-orleans", "Orleans");
        WriteSkillManifest(tempDirectory.Path, "Frameworks", "Orleans", "dotnet-orleans", """{"version":"1.0.0","category":"Distributed"}""");

        WritePackageManifest(tempDirectory.Path, "Libraries", "ManagedCode-Orleans-Graph", """{"name":"ManagedCode-Orleans-Graph","title":"ManagedCode Orleans Graph","description":"","links":{}}""");
        WriteSkill(tempDirectory.Path, "Libraries", "ManagedCode-Orleans-Graph", "dotnet-managedcode-orleans-graph", "ManagedCode Orleans Graph");
        WriteSkillManifest(tempDirectory.Path, "Libraries", "ManagedCode-Orleans-Graph", "dotnet-managedcode-orleans-graph", """{"version":"1.0.0","category":"Distributed"}""");

        WritePackageManifest(tempDirectory.Path, "Libraries", "ManagedCode-Orleans-SignalR", """{"name":"ManagedCode-Orleans-SignalR","title":"ManagedCode Orleans SignalR","description":"","links":{}}""");
        WriteSkill(tempDirectory.Path, "Libraries", "ManagedCode-Orleans-SignalR", "dotnet-managedcode-orleans-signalr", "ManagedCode Orleans SignalR");
        WriteSkillManifest(tempDirectory.Path, "Libraries", "ManagedCode-Orleans-SignalR", "dotnet-managedcode-orleans-signalr", """{"version":"1.0.0","category":"Distributed"}""");

        WritePackageManifest(tempDirectory.Path, "Frameworks", "Worker-Services", """{"name":"Worker-Services","title":"Worker Services","description":"","links":{}}""");
        WriteSkill(tempDirectory.Path, "Frameworks", "Worker-Services", "dotnet-worker-services", "Worker Services");
        WriteSkillManifest(tempDirectory.Path, "Frameworks", "Worker-Services", "dotnet-worker-services", """{"version":"1.0.0","category":"Cloud"}""");

        WritePackageManifest(tempDirectory.Path, "Frameworks", "SignalR", """{"name":"SignalR","title":"SignalR","description":"","links":{}}""");
        WriteSkill(tempDirectory.Path, "Frameworks", "SignalR", "dotnet-signalr", "SignalR");
        WriteSkillManifest(tempDirectory.Path, "Frameworks", "SignalR", "dotnet-signalr", """{"version":"1.0.0","category":"Web"}""");

        var package = SkillCatalogPackage.LoadFromDirectory(
            new DirectoryInfo(tempDirectory.Path),
            "test payload",
            "test");

        var aspire = Assert.Single(package.Skills, skill => skill.Name == "dotnet-aspire");
        Assert.Equal("Aspire", aspire.PackagePrefix);
        Assert.Equal("https://github.com/dotnet/aspire", aspire.Links.Repository);
        Assert.Equal("https://learn.microsoft.com/en-us/dotnet/aspire/", aspire.Links.Docs);
        Assert.Equal("https://www.nuget.org/packages/Aspire.Hosting", aspire.Links.NuGet);
        Assert.Contains(package.Packages, bundle => bundle.Name == "cloud");
        Assert.Equal("dotnet-aspire", package.ResolveSkillSource("dotnet-aspire").Name);
    }

    [Fact]
    public void LoadFromDirectory_RejectsInlineSkillVersionAndCategory()
    {
        using var tempDirectory = new TemporaryDirectory();
        var skillDirectory = Directory.CreateDirectory(Path.Combine(tempDirectory.Path, "catalog", "Frameworks", "Aspire", "skills", "dotnet-aspire"));

        File.WriteAllText(
            Path.Combine(tempDirectory.Path, "catalog", "Frameworks", "Aspire", "manifest.json"),
            """{"name":"Aspire","title":"Aspire","description":"","links":{}}""");
        File.WriteAllText(
            Path.Combine(skillDirectory.FullName, "SKILL.md"),
            """
            ---
            name: dotnet-aspire
            version: "1.0.0"
            category: "Cloud"
            description: "Aspire"
            compatibility: "codex"
            ---

            # Aspire
            """);

        var exception = Assert.Throws<InvalidOperationException>(
            () => SkillCatalogPackage.LoadFromDirectory(new DirectoryInfo(tempDirectory.Path), "test payload", "test"));

        Assert.Contains("must not declare", exception.Message, StringComparison.Ordinal);
        Assert.Contains("version", exception.Message, StringComparison.Ordinal);
        Assert.Contains("category", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadFromDirectory_DiscoversCustomCategoryAndTypeWithoutCodeConstants()
    {
        using var tempDirectory = new TemporaryDirectory();

        static void WritePackageManifest(string root, string type, string package, string json)
        {
            var targetDirectory = Directory.CreateDirectory(Path.Combine(root, "catalog", type, package));
            File.WriteAllText(Path.Combine(targetDirectory.FullName, "manifest.json"), json);
        }

        static void WriteSkill(string root, string type, string package, string skillName, string title)
        {
            var targetDirectory = Directory.CreateDirectory(Path.Combine(root, "catalog", type, package, "skills", skillName));
            File.WriteAllText(
                Path.Combine(targetDirectory.FullName, "SKILL.md"),
                $"""
                ---
                name: {skillName}
                description: "{title}"
                compatibility: "codex"
                ---

                # {title}
                """);
        }

        static void WriteSkillManifest(string root, string type, string package, string skillName, string json)
        {
            var targetDirectory = Directory.CreateDirectory(Path.Combine(root, "catalog", type, package, "skills", skillName));
            File.WriteAllText(Path.Combine(targetDirectory.FullName, "manifest.json"), json);
        }

        WritePackageManifest(tempDirectory.Path, "Platform", "MCAF", """{"name":"MCAF","title":"MCAF","description":"","links":{}}""");
        foreach (var skillName in new[]
                 {
                     "dotnet-mcaf",
                     "dotnet-mcaf-agile-delivery",
                     "dotnet-mcaf-devex",
                     "dotnet-mcaf-documentation",
                     "dotnet-mcaf-feature-spec",
                     "dotnet-mcaf-human-review-planning",
                     "dotnet-mcaf-ml-ai-delivery",
                     "dotnet-mcaf-nfr",
                     "dotnet-mcaf-source-control",
                     "dotnet-mcaf-ui-ux",
                 })
        {
            WriteSkill(tempDirectory.Path, "Platform", "MCAF", skillName, skillName);
            WriteSkillManifest(tempDirectory.Path, "Platform", "MCAF", skillName, """{"version":"1.0.0","category":"Architecture"}""");
        }

        WritePackageManifest(tempDirectory.Path, "Frameworks", "Orleans", """{"name":"Orleans","title":"Orleans","description":"","links":{}}""");
        WriteSkill(tempDirectory.Path, "Frameworks", "Orleans", "dotnet-orleans", "Orleans");
        WriteSkillManifest(tempDirectory.Path, "Frameworks", "Orleans", "dotnet-orleans", """{"version":"1.0.0","category":"Distributed"}""");

        WritePackageManifest(tempDirectory.Path, "Libraries", "ManagedCode-Orleans-Graph", """{"name":"ManagedCode-Orleans-Graph","title":"ManagedCode Orleans Graph","description":"","links":{}}""");
        WriteSkill(tempDirectory.Path, "Libraries", "ManagedCode-Orleans-Graph", "dotnet-managedcode-orleans-graph", "ManagedCode Orleans Graph");
        WriteSkillManifest(tempDirectory.Path, "Libraries", "ManagedCode-Orleans-Graph", "dotnet-managedcode-orleans-graph", """{"version":"1.0.0","category":"Distributed"}""");

        WritePackageManifest(tempDirectory.Path, "Libraries", "ManagedCode-Orleans-SignalR", """{"name":"ManagedCode-Orleans-SignalR","title":"ManagedCode Orleans SignalR","description":"","links":{}}""");
        WriteSkill(tempDirectory.Path, "Libraries", "ManagedCode-Orleans-SignalR", "dotnet-managedcode-orleans-signalr", "ManagedCode Orleans SignalR");
        WriteSkillManifest(tempDirectory.Path, "Libraries", "ManagedCode-Orleans-SignalR", "dotnet-managedcode-orleans-signalr", """{"version":"1.0.0","category":"Distributed"}""");

        WritePackageManifest(tempDirectory.Path, "Frameworks", "Worker-Services", """{"name":"Worker-Services","title":"Worker Services","description":"","links":{}}""");
        WriteSkill(tempDirectory.Path, "Frameworks", "Worker-Services", "dotnet-worker-services", "Worker Services");
        WriteSkillManifest(tempDirectory.Path, "Frameworks", "Worker-Services", "dotnet-worker-services", """{"version":"1.0.0","category":"Cloud"}""");

        WritePackageManifest(tempDirectory.Path, "Frameworks", "Aspire", """{"name":"Aspire","title":"Aspire","description":"","links":{}}""");
        WriteSkill(tempDirectory.Path, "Frameworks", "Aspire", "dotnet-aspire", "Aspire");
        WriteSkillManifest(tempDirectory.Path, "Frameworks", "Aspire", "dotnet-aspire", """{"version":"1.0.0","category":"Cloud"}""");

        WritePackageManifest(tempDirectory.Path, "Frameworks", "SignalR", """{"name":"SignalR","title":"SignalR","description":"","links":{}}""");
        WriteSkill(tempDirectory.Path, "Frameworks", "SignalR", "dotnet-signalr", "SignalR");
        WriteSkillManifest(tempDirectory.Path, "Frameworks", "SignalR", "dotnet-signalr", """{"version":"1.0.0","category":"Web"}""");

        var packageDirectory = Directory.CreateDirectory(Path.Combine(tempDirectory.Path, "catalog", "Analyzers", "Observability"));
        var skillDirectory = Directory.CreateDirectory(Path.Combine(packageDirectory.FullName, "skills", "dotnet-observability-analyzer"));

        File.WriteAllText(
            Path.Combine(packageDirectory.FullName, "manifest.json"),
            """{"name":"Observability","title":"Observability","description":"","links":{}}""");
        File.WriteAllText(
            Path.Combine(skillDirectory.FullName, "SKILL.md"),
            """
            ---
            name: dotnet-observability-analyzer
            description: "Observability analyzer"
            compatibility: "codex"
            ---

            # Observability analyzer
            """);
        File.WriteAllText(
            Path.Combine(skillDirectory.FullName, "manifest.json"),
            """{"version":"1.0.0","category":"Observability"}""");

        var package = SkillCatalogPackage.LoadFromDirectory(
            new DirectoryInfo(tempDirectory.Path),
            "test payload",
            "test");

        var skill = Assert.Single(package.Skills, candidate => candidate.Name == "dotnet-observability-analyzer");
        Assert.Equal("Analyzer", skill.Type);
        Assert.Contains(package.Packages, bundle => bundle.Name == "observability" && bundle.SourceCategory == "Observability");
    }

    [Fact]
    public void LoadFromDirectory_AllowsUpstreamSkillMarkdownWithoutInlineCompatibility()
    {
        using var tempDirectory = new TemporaryDirectory();

        static void WritePackageManifest(string root, string type, string package, string json)
        {
            var targetDirectory = Directory.CreateDirectory(Path.Combine(root, "catalog", type, package));
            File.WriteAllText(Path.Combine(targetDirectory.FullName, "manifest.json"), json);
        }

        static void WriteSkill(string root, string type, string package, string skillName, string title)
        {
            var targetDirectory = Directory.CreateDirectory(Path.Combine(root, "catalog", type, package, "skills", skillName));
            File.WriteAllText(
                Path.Combine(targetDirectory.FullName, "SKILL.md"),
                $"""
                ---
                name: {skillName}
                description: "{title}"
                compatibility: "codex"
                ---

                # {title}
                """);
        }

        static void WriteSkillManifest(string root, string type, string package, string skillName, string json)
        {
            var targetDirectory = Directory.CreateDirectory(Path.Combine(root, "catalog", type, package, "skills", skillName));
            File.WriteAllText(Path.Combine(targetDirectory.FullName, "manifest.json"), json);
        }

        WritePackageManifest(tempDirectory.Path, "Platform", "MCAF", """{"name":"MCAF","title":"MCAF","description":"","links":{}}""");
        foreach (var skillName in new[]
                 {
                     "dotnet-mcaf",
                     "dotnet-mcaf-agile-delivery",
                     "dotnet-mcaf-devex",
                     "dotnet-mcaf-documentation",
                     "dotnet-mcaf-feature-spec",
                     "dotnet-mcaf-human-review-planning",
                     "dotnet-mcaf-ml-ai-delivery",
                     "dotnet-mcaf-nfr",
                     "dotnet-mcaf-source-control",
                     "dotnet-mcaf-ui-ux",
                 })
        {
            WriteSkill(tempDirectory.Path, "Platform", "MCAF", skillName, skillName);
            WriteSkillManifest(tempDirectory.Path, "Platform", "MCAF", skillName, """{"version":"1.0.0","category":"Architecture"}""");
        }

        WritePackageManifest(tempDirectory.Path, "Frameworks", "Orleans", """{"name":"Orleans","title":"Orleans","description":"","links":{}}""");
        WriteSkill(tempDirectory.Path, "Frameworks", "Orleans", "dotnet-orleans", "Orleans");
        WriteSkillManifest(tempDirectory.Path, "Frameworks", "Orleans", "dotnet-orleans", """{"version":"1.0.0","category":"Distributed"}""");

        WritePackageManifest(tempDirectory.Path, "Libraries", "ManagedCode-Orleans-Graph", """{"name":"ManagedCode-Orleans-Graph","title":"ManagedCode Orleans Graph","description":"","links":{}}""");
        WriteSkill(tempDirectory.Path, "Libraries", "ManagedCode-Orleans-Graph", "dotnet-managedcode-orleans-graph", "ManagedCode Orleans Graph");
        WriteSkillManifest(tempDirectory.Path, "Libraries", "ManagedCode-Orleans-Graph", "dotnet-managedcode-orleans-graph", """{"version":"1.0.0","category":"Distributed"}""");

        WritePackageManifest(tempDirectory.Path, "Libraries", "ManagedCode-Orleans-SignalR", """{"name":"ManagedCode-Orleans-SignalR","title":"ManagedCode Orleans SignalR","description":"","links":{}}""");
        WriteSkill(tempDirectory.Path, "Libraries", "ManagedCode-Orleans-SignalR", "dotnet-managedcode-orleans-signalr", "ManagedCode Orleans SignalR");
        WriteSkillManifest(tempDirectory.Path, "Libraries", "ManagedCode-Orleans-SignalR", "dotnet-managedcode-orleans-signalr", """{"version":"1.0.0","category":"Distributed"}""");

        WritePackageManifest(tempDirectory.Path, "Frameworks", "Worker-Services", """{"name":"Worker-Services","title":"Worker Services","description":"","links":{}}""");
        WriteSkill(tempDirectory.Path, "Frameworks", "Worker-Services", "dotnet-worker-services", "Worker Services");
        WriteSkillManifest(tempDirectory.Path, "Frameworks", "Worker-Services", "dotnet-worker-services", """{"version":"1.0.0","category":"Cloud"}""");

        WritePackageManifest(tempDirectory.Path, "Frameworks", "Aspire", """{"name":"Aspire","title":"Aspire","description":"","links":{}}""");
        WriteSkill(tempDirectory.Path, "Frameworks", "Aspire", "dotnet-aspire", "Aspire");
        WriteSkillManifest(tempDirectory.Path, "Frameworks", "Aspire", "dotnet-aspire", """{"version":"1.0.0","category":"Cloud"}""");

        WritePackageManifest(tempDirectory.Path, "Frameworks", "SignalR", """{"name":"SignalR","title":"SignalR","description":"","links":{}}""");
        WriteSkill(tempDirectory.Path, "Frameworks", "SignalR", "dotnet-signalr", "SignalR");
        WriteSkillManifest(tempDirectory.Path, "Frameworks", "SignalR", "dotnet-signalr", """{"version":"1.0.0","category":"Web"}""");

        var packageDirectory = Directory.CreateDirectory(Path.Combine(tempDirectory.Path, "catalog", "Platform", "Official-DotNet-AI"));
        var skillDirectory = Directory.CreateDirectory(Path.Combine(packageDirectory.FullName, "skills", "mcp-csharp-create"));

        File.WriteAllText(
            Path.Combine(packageDirectory.FullName, "manifest.json"),
            """{"name":"dotnet-ai","title":"Official .NET skills: dotnet-ai","description":"","links":{}}""");
        File.WriteAllText(
            Path.Combine(skillDirectory.FullName, "SKILL.md"),
            """
            ---
            name: mcp-csharp-create
            description: >
              Create MCP servers using the C# SDK and .NET project templates. Covers scaffolding,
              tool implementation, and transport configuration.
            ---

            # C# MCP Server Creation
            """);
        File.WriteAllText(
            Path.Combine(skillDirectory.FullName, "manifest.json"),
            """
            {
              "version": "0.1.0",
              "category": "AI",
              "compatibility": "Requires a .NET repository working with AI, ML, or MCP workloads."
            }
            """);

        var package = SkillCatalogPackage.LoadFromDirectory(
            new DirectoryInfo(tempDirectory.Path),
            "test payload",
            "test");

        var skill = Assert.Single(package.Skills, candidate => candidate.Name == "mcp-csharp-create");
        Assert.Equal("AI", skill.Category);
        Assert.Equal("Requires a .NET repository working with AI, ML, or MCP workloads.", skill.Compatibility);
        Assert.Equal(
            "Create MCP servers using the C# SDK and .NET project templates. Covers scaffolding, tool implementation, and transport configuration.",
            skill.Description);
    }

    [Fact]
    public void ResolveSkillSource_RejectsEscapingManifestPath()
    {
        using var tempDirectory = new TemporaryDirectory();

        var manifest = new SkillManifest
        {
            Skills =
            [
                new SkillEntry
                {
                    Name = "dotnet-escape",
                    Title = "Escape",
                    Version = "1.0.0",
                    Category = "Test",
                    Type = "Library",
                    Package = "Escape",
                    Description = "Escape test",
                    Compatibility = "codex",
                    Path = "../outside"
                },
            ],
        };

        var package = SkillCatalogPackage.LoadFromManifest(new DirectoryInfo(tempDirectory.Path), manifest, "test payload", "test");

        var exception = Assert.Throws<InvalidOperationException>(() => package.ResolveSkillSource("dotnet-escape"));

        Assert.Contains("must stay within", exception.Message, StringComparison.Ordinal);
    }
}
