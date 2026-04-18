namespace ManagedCode.DotnetSkills.Runtime;

internal static class CatalogOrganization
{
    private static readonly string[] StackOrder =
    [
        ".NET Foundations",
        ".NET Quality",
        "MSBuild",
        "NuGet & Publishing",
        "Templates & Scaffolding",
        "Diagnostics & Metrics",
        "Web",
        "Aspire",
        "Azure Functions",
        "Background Workers",
        "Mobile & Device",
        "XR & Spatial",
        "Desktop & UI",
        "Frontend Quality",
        "Testing",
        "Testing Research",
        "Architecture",
        "Governance & Delivery",
        "Data",
        "AI & Agents",
        "Distributed",
        "Legacy",
        "Upgrades & Migration",
    ];

    private static readonly string[] LaneOrder =
    [
        "Foundations",
        "Code Quality",
        "Frameworks",
        "Libraries",
        "Interop",
        "Quality",
        "Mutation",
        "Experimental",
        "Review",
        "Governance",
        "Delivery Workflow",
        "Project & Templates",
        "Package Management",
        "Package Publishing",
        "Automation",
        "Build Pipelines",
        "Crash Analysis",
        "Performance",
        "Observability",
        "Static Analysis",
        "Runtime upgrades",
        "Testing migrations",
        "Legacy frameworks",
        "Migration",
        "Architecture",
        "Analysis",
        "Tooling",
    ];

    private static readonly HashSet<string> FrontendQualityPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "Biome",
        "ESLint",
        "Stylelint",
        "HTMLHint",
        "webhint",
        "SonarJS",
    };

    private static readonly HashSet<string> DotnetQualityPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "Analyzer-Config",
        "Chous",
        "Code-Analysis",
        "CSharpier",
        "Format",
        "Meziantou-Analyzer",
        "Metalint",
        "Quality-CI",
        "ReSharper-CLT",
        "Roslynator",
        "StyleCop-Analyzers",
    };

    private static readonly HashSet<string> MsBuildPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "Official-DotNet-MSBuild",
    };

    private static readonly HashSet<string> DiagnosticsPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "Asynkron-Profiler",
        "cloc",
        "CodeQL",
        "Complexity",
        "Official-DotNet-Diagnostics",
        "Profiling",
        "QuickDup",
    };

    private static readonly HashSet<string> ArchitecturePackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "ArchUnitNET",
        "Architecture",
        "Graphify",
        "NetArchTest",
    };

    private static readonly HashSet<string> GovernancePackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "Code-Review",
        "MCAF",
    };

    private static readonly HashSet<string> TestingFrameworkPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "MSTest",
        "NUnit",
        "TUnit",
        "xUnit",
    };

    private static readonly HashSet<string> TestingQualityPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "Coverlet",
        "ReportGenerator",
        "Stryker",
    };

    private static readonly HashSet<string> LegacyPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "Entity-Framework-6",
        "Legacy-ASP.NET",
        "Official-DotNet-Upgrade",
        "WCF",
        "Workflow-Foundation",
    };

    private static readonly HashSet<string> DistributedPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "ManagedCode-Orleans-Graph",
        "ManagedCode-Orleans-SignalR",
        "Orleans",
    };

    private static readonly HashSet<string> MobileDevicePackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "MAUI",
        "Official-DotNet-MAUI",
    };

    public static CatalogSkillGrouping Classify(SkillEntry skill)
    {
        return Classify(skill.Type, skill.Package, skill.Category, skill.Name);
    }

    public static CatalogSkillGrouping Classify(string type, string package, string category, string name)
    {
        if (IsGovernanceSkill(package, name))
        {
            return new("Governance & Delivery", ResolveGovernanceLane(package, name));
        }

        if (IsMigrationSkill(package, name))
        {
            return new("Upgrades & Migration", ResolveMigrationLane(package, name));
        }

        if (IsLegacySkill(package, category))
        {
            return new("Legacy", ResolveLegacyLane(type, package, category));
        }

        if (IsArchitectureSkill(package, category))
        {
            return new("Architecture", ResolveArchitectureLane(type, package));
        }

        if (IsTestingResearchSkill(package, category, name))
        {
            return new("Testing Research", ResolveTestingResearchLane(package, name));
        }

        if (IsTestingSkill(type, category))
        {
            return new("Testing", ResolveTestingLane(package, name));
        }

        if (IsDataSkill(package, category))
        {
            return new("Data", ResolveEntityLane(type));
        }

        if (IsXrSpatialSkill(package))
        {
            return new("XR & Spatial", ResolveXrSpatialLane(type, package));
        }

        if (IsMobileDeviceSkill(package, name))
        {
            return new("Mobile & Device", ResolveMobileDeviceLane(type, package, name));
        }

        if (IsAiSkill(package, category))
        {
            return new("AI & Agents", ResolveAiLane(type, package));
        }

        if (IsAspireSkill(package))
        {
            return new("Aspire", ResolveFrameworkLane(type));
        }

        if (IsAzureFunctionsSkill(package))
        {
            return new("Azure Functions", ResolveFrameworkLane(type));
        }

        if (IsBackgroundWorkerSkill(package))
        {
            return new("Background Workers", ResolveFrameworkLane(type));
        }

        if (IsDistributedSkill(package, category))
        {
            return new("Distributed", ResolveEntityLane(type));
        }

        if (IsFrontendQualitySkill(package))
        {
            return new("Frontend Quality", "Code Quality");
        }

        if (IsWebSkill(type, package, category))
        {
            return new("Web", ResolveWebLane(type, package));
        }

        if (IsDesktopSkill(type, package, category))
        {
            return new("Desktop & UI", ResolveDesktopLane(type, package));
        }

        if (IsDotnetQualitySkill(package, category))
        {
            return new(".NET Quality", "Code Quality");
        }

        if (IsMsBuildSkill(package))
        {
            return new("MSBuild", "Build Pipelines");
        }

        if (IsNuGetPublishingSkill(package, name))
        {
            return new("NuGet & Publishing", ResolveNuGetPublishingLane(package, name));
        }

        if (IsTemplateSkill(package))
        {
            return new("Templates & Scaffolding", "Project & Templates");
        }

        if (IsDiagnosticsSkill(package, category, name))
        {
            return new("Diagnostics & Metrics", ResolveDiagnosticsLane(package, category, name));
        }

        return new(".NET Foundations", ResolveDotnetLane(type, package, category, name));
    }

    public static int GetStackRank(string stack)
    {
        for (var index = 0; index < StackOrder.Length; index++)
        {
            if (string.Equals(StackOrder[index], stack, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return StackOrder.Length;
    }

    public static int GetLaneRank(string lane)
    {
        for (var index = 0; index < LaneOrder.Length; index++)
        {
            if (string.Equals(LaneOrder[index], lane, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return LaneOrder.Length;
    }

    public static bool IsPrimaryBundle(SkillPackageEntry package)
    {
        return true;
    }

    public static string FormatBundleSortKey(SkillPackageEntry package)
    {
        var kindRank = package.Kind.ToLowerInvariant() switch
        {
            "stack" => "0",
            "workflow" => "1",
            "curated" => "2",
            _ => "5",
        };

        return $"{kindRank}:{GetStackRank(package.Stack):D2}:{package.Name}";
    }

    public static string ResolveBundleAreaLabel(SkillPackageEntry package)
    {
        if (!string.IsNullOrWhiteSpace(package.Stack) && !string.IsNullOrWhiteSpace(package.Lane))
        {
            return $"{package.Stack} / {package.Lane}";
        }

        if (!string.IsNullOrWhiteSpace(package.Stack))
        {
            return package.Stack;
        }

        if (!string.IsNullOrWhiteSpace(package.SourceCategory))
        {
            return $"Category: {package.SourceCategory}";
        }

        return package.Kind;
    }

    private static bool IsGovernanceSkill(string package, string name)
    {
        return GovernancePackages.Contains(package)
            || string.Equals(name, "dotnet-code-review", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMigrationSkill(string package, string name)
    {
        return string.Equals(package, "Official-DotNet-Upgrade", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "dotnet-aot-compat", StringComparison.OrdinalIgnoreCase)
            || name.Contains("migrate-", StringComparison.OrdinalIgnoreCase)
            || name.Contains("migration", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "mtp-hot-reload", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLegacySkill(string package, string category)
    {
        return LegacyPackages.Contains(package)
            || string.Equals(category, "Legacy", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsArchitectureSkill(string package, string category)
    {
        return ArchitecturePackages.Contains(package)
            || string.Equals(category, "Architecture", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTestingSkill(string type, string category)
    {
        return string.Equals(type, "Testing", StringComparison.OrdinalIgnoreCase)
            || string.Equals(category, "Testing", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTestingResearchSkill(string package, string category, string name)
    {
        return string.Equals(name, "code-testing-agent", StringComparison.OrdinalIgnoreCase)
            || string.Equals(package, "Stryker", StringComparison.OrdinalIgnoreCase)
            || string.Equals(package, "Official-DotNet-Experimental", StringComparison.OrdinalIgnoreCase)
               && string.Equals(category, "Testing", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDataSkill(string package, string category)
    {
        return string.Equals(category, "Data", StringComparison.OrdinalIgnoreCase)
            || package is "Official-DotNet-Data" or "Sep";
    }

    private static bool IsAiSkill(string package, string category)
    {
        return string.Equals(category, "AI", StringComparison.OrdinalIgnoreCase)
            || package is "MCP" or "Microsoft-Extensions-AI" or "Official-DotNet-AI" or "Semantic-Kernel";
    }

    private static bool IsMobileDeviceSkill(string package, string name)
    {
        return MobileDevicePackages.Contains(package);
    }

    private static bool IsXrSpatialSkill(string package)
    {
        return string.Equals(package, "Mixed-Reality", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAspireSkill(string package)
    {
        return string.Equals(package, "Aspire", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAzureFunctionsSkill(string package)
    {
        return string.Equals(package, "Azure-Functions", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBackgroundWorkerSkill(string package)
    {
        return string.Equals(package, "Worker-Services", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDistributedSkill(string package, string category)
    {
        return DistributedPackages.Contains(package)
            || string.Equals(category, "Distributed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFrontendQualitySkill(string package)
    {
        return FrontendQualityPackages.Contains(package);
    }

    private static bool IsWebSkill(string type, string package, string category)
    {
        return string.Equals(category, "Web", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "Framework", StringComparison.OrdinalIgnoreCase)
               && (package.Contains("ASP", StringComparison.OrdinalIgnoreCase)
                   || package.Contains("Blazor", StringComparison.OrdinalIgnoreCase)
                   || package.Contains("Web", StringComparison.OrdinalIgnoreCase)
                   || package.Contains("SignalR", StringComparison.OrdinalIgnoreCase)
                   || package.Contains("gRPC", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDesktopSkill(string type, string package, string category)
    {
        return string.Equals(category, "Cross-Platform UI", StringComparison.OrdinalIgnoreCase)
            || string.Equals(category, "Desktop", StringComparison.OrdinalIgnoreCase)
            || string.Equals(package, "MVVM-Toolkit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(package, "LibVLC", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "Framework", StringComparison.OrdinalIgnoreCase)
               && (package.Contains("MAUI", StringComparison.OrdinalIgnoreCase)
                   || package.Contains("Uno", StringComparison.OrdinalIgnoreCase)
                   || package.Contains("Win", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMsBuildSkill(string package)
    {
        return MsBuildPackages.Contains(package);
    }

    private static bool IsNuGetPublishingSkill(string package, string name)
    {
        return string.Equals(package, "Official-DotNet-NuGet", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "nuget-trusted-publishing", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTemplateSkill(string package)
    {
        return string.Equals(package, "Official-DotNet-Template-Engine", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDotnetQualitySkill(string package, string category)
    {
        if (string.Equals(package, "Modern-CSharp", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return DotnetQualityPackages.Contains(package)
            || string.Equals(category, "Code Quality", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDiagnosticsSkill(string package, string category, string name)
    {
        return DiagnosticsPackages.Contains(package)
            || string.Equals(category, "Metrics", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "exp-simd-vectorization", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveDotnetLane(string type, string package, string category, string name)
    {
        if (string.Equals(name, "csharp-scripts", StringComparison.OrdinalIgnoreCase))
        {
            return "Tooling";
        }

        if (string.Equals(name, "dotnet-pinvoke", StringComparison.OrdinalIgnoreCase))
        {
            return "Interop";
        }

        return ResolveEntityLane(type) switch
        {
            "Libraries" => "Libraries",
            _ => "Foundations",
        };
    }

    private static string ResolveWebLane(string type, string package)
    {
        if (string.Equals(type, "Tool", StringComparison.OrdinalIgnoreCase))
        {
            return "Foundations";
        }

        return ResolveEntityLane(type);
    }

    private static string ResolveFrameworkLane(string type)
    {
        if (string.Equals(type, "Tool", StringComparison.OrdinalIgnoreCase))
        {
            return "Foundations";
        }

        return ResolveEntityLane(type);
    }

    private static string ResolveMobileDeviceLane(string type, string package, string name)
    {
        if (name.Contains("doctor", StringComparison.OrdinalIgnoreCase))
        {
            return "Tooling";
        }

        return ResolveEntityLane(type);
    }

    private static string ResolveXrSpatialLane(string type, string package)
    {
        if (string.Equals(package, "Mixed-Reality", StringComparison.OrdinalIgnoreCase))
        {
            return "Frameworks";
        }

        return ResolveEntityLane(type);
    }

    private static string ResolveDesktopLane(string type, string package)
    {
        if (string.Equals(package, "MVVM-Toolkit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(package, "LibVLC", StringComparison.OrdinalIgnoreCase))
        {
            return "Libraries";
        }

        return ResolveEntityLane(type);
    }

    private static string ResolveTestingLane(string package, string name)
    {
        if (TestingFrameworkPackages.Contains(package)
            || name is "writing-mstest-tests" or "dotnet-mstest" or "dotnet-nunit" or "dotnet-tunit" or "dotnet-xunit")
        {
            return "Frameworks";
        }

        if (TestingQualityPackages.Contains(package)
            || name is "coverage-analysis" or "crap-score" or "test-anti-patterns")
        {
            return "Quality";
        }

        return "Foundations";
    }

    private static string ResolveTestingResearchLane(string package, string name)
    {
        if (string.Equals(name, "code-testing-agent", StringComparison.OrdinalIgnoreCase))
        {
            return "Automation";
        }

        if (string.Equals(package, "Stryker", StringComparison.OrdinalIgnoreCase))
        {
            return "Mutation";
        }

        return "Experimental";
    }

    private static string ResolveMigrationLane(string package, string name)
    {
        if (name.Contains("mstest", StringComparison.OrdinalIgnoreCase)
            || name.Contains("xunit", StringComparison.OrdinalIgnoreCase)
            || name.Contains("vstest", StringComparison.OrdinalIgnoreCase)
            || name.Contains("mtp", StringComparison.OrdinalIgnoreCase))
        {
            return "Testing migrations";
        }

        if (string.Equals(package, "Official-DotNet-Upgrade", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "dotnet-aot-compat", StringComparison.OrdinalIgnoreCase))
        {
            return "Runtime upgrades";
        }

        return "Migration";
    }

    private static string ResolveLegacyLane(string type, string package, string category)
    {
        if (string.Equals(category, "Legacy", StringComparison.OrdinalIgnoreCase) || LegacyPackages.Contains(package))
        {
            return "Legacy frameworks";
        }

        return ResolveEntityLane(type);
    }

    private static string ResolveArchitectureLane(string type, string package)
    {
        if (string.Equals(package, "MCAF", StringComparison.OrdinalIgnoreCase))
        {
            return "Governance";
        }

        if (string.Equals(type, "Tool", StringComparison.OrdinalIgnoreCase))
        {
            return "Analysis";
        }

        return ResolveEntityLane(type) switch
        {
            "Frameworks" or "Foundations" => "Architecture",
            var lane => lane,
        };
    }

    private static string ResolveGovernanceLane(string package, string name)
    {
        if (string.Equals(name, "dotnet-code-review", StringComparison.OrdinalIgnoreCase))
        {
            return "Review";
        }

        if (name.Contains("delivery", StringComparison.OrdinalIgnoreCase)
            || name.Contains("devex", StringComparison.OrdinalIgnoreCase)
            || name.Contains("ui-ux", StringComparison.OrdinalIgnoreCase)
            || name.Contains("ml-ai", StringComparison.OrdinalIgnoreCase))
        {
            return "Delivery Workflow";
        }

        return "Governance";
    }

    private static string ResolveAiLane(string type, string package)
    {
        if (string.Equals(type, "Tool", StringComparison.OrdinalIgnoreCase)
            || string.Equals(package, "Official-DotNet-AI", StringComparison.OrdinalIgnoreCase))
        {
            return "Tooling";
        }

        return ResolveEntityLane(type);
    }

    private static string ResolveNuGetPublishingLane(string package, string name)
    {
        if (string.Equals(package, "Official-DotNet-NuGet", StringComparison.OrdinalIgnoreCase))
        {
            return "Package Management";
        }

        if (string.Equals(name, "nuget-trusted-publishing", StringComparison.OrdinalIgnoreCase))
        {
            return "Package Publishing";
        }

        return "Package Management";
    }

    private static string ResolveDiagnosticsLane(string package, string category, string name)
    {
        if (string.Equals(package, "CodeQL", StringComparison.OrdinalIgnoreCase))
        {
            return "Static Analysis";
        }

        if (name.Contains("tombstone", StringComparison.OrdinalIgnoreCase)
            || name.Contains("dump", StringComparison.OrdinalIgnoreCase)
            || name.Contains("crash", StringComparison.OrdinalIgnoreCase))
        {
            return "Crash Analysis";
        }

        if (string.Equals(name, "exp-simd-vectorization", StringComparison.OrdinalIgnoreCase))
        {
            return "Performance";
        }

        if (package is "cloc" or "Complexity" or "QuickDup")
        {
            return "Observability";
        }

        if (string.Equals(category, "Metrics", StringComparison.OrdinalIgnoreCase)
            || package is "Asynkron-Profiler" or "Official-DotNet-Diagnostics" or "Profiling")
        {
            return "Performance";
        }

        return "Static Analysis";
    }

    private static string ResolveEntityLane(string type)
    {
        return type switch
        {
            "Library" => "Libraries",
            "Framework" => "Frameworks",
            _ => "Foundations",
        };
    }
}

internal sealed record CatalogSkillGrouping(string Stack, string Lane);
