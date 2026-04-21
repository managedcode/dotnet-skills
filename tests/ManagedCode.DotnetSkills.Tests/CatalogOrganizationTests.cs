using ManagedCode.DotnetSkills.Runtime;

namespace ManagedCode.DotnetSkills.Tests;

public sealed class CatalogOrganizationTests
{
    [Fact]
    public void Load_AssignsExpectedStacksAndLanes()
    {
        var catalog = TestCatalog.Load();

        AssertSkill(catalog, "format", ".NET Quality", "Code Quality");
        AssertSkill(catalog, "modern-csharp", ".NET Foundations", "Foundations");
        AssertSkill(catalog, "project-setup", ".NET Foundations", "Foundations");
        AssertSkill(catalog, "eslint", "Frontend Quality", "Code Quality");
        AssertSkill(catalog, "aspnet-core", "Web", "Frameworks");
        AssertSkill(catalog, "aspire", "Aspire", "Frameworks");
        AssertSkill(catalog, "azure-functions", "Azure Functions", "Frameworks");
        AssertSkill(catalog, "worker-services", "Background Workers", "Frameworks");
        AssertSkill(catalog, "maui", "Mobile & Device", "Frameworks");
        AssertSkill(catalog, "maui-doctor", "Mobile & Device", "Tooling");
        AssertSkill(catalog, "android-tombstone-symbolication", "Diagnostics & Metrics", "Crash Analysis");
        AssertSkill(catalog, "dump-collect", "Diagnostics & Metrics", "Crash Analysis");
        AssertSkill(catalog, "mixed-reality", "XR & Spatial", "Frameworks");
        AssertSkill(catalog, "uno-platform", "Desktop & UI", "Frameworks");
        AssertSkill(catalog, "xunit", "Testing", "Frameworks");
        AssertSkill(catalog, "code-testing-agent", "Testing Research", "Automation");
        AssertSkill(catalog, "stryker", "Testing Research", "Mutation");
        AssertSkill(catalog, "exp-test-gap-analysis", "Testing Research", "Experimental");
        AssertSkill(catalog, "csharp-scripts", ".NET Foundations", "Tooling");
        AssertSkill(catalog, "msbuild-modernization", "MSBuild", "Build Pipelines");
        AssertSkill(catalog, "convert-to-cpm", "NuGet & Publishing", "Package Management");
        AssertSkill(catalog, "template-authoring", "Templates & Scaffolding", "Project & Templates");
        AssertSkill(catalog, "analyzing-dotnet-performance", "Diagnostics & Metrics", "Performance");
        AssertSkill(catalog, "cloc", "Diagnostics & Metrics", "Observability");
        AssertSkill(catalog, "complexity", ".NET Quality", "Code Quality");
        AssertSkill(catalog, "codeql", "Diagnostics & Metrics", "Static Analysis");
        AssertSkill(catalog, "mtp-hot-reload", "Upgrades & Migration", "Testing migrations");
        AssertSkill(catalog, "migrate-xunit-to-xunit-v3", "Upgrades & Migration", "Testing migrations");
        AssertSkill(catalog, "legacy-aspnet", "Legacy", "Legacy frameworks");
        AssertSkill(catalog, "architecture", "Architecture", "Architecture");
        AssertSkill(catalog, "mcaf", "Governance & Delivery", "Governance");
        AssertSkill(catalog, "code-review", "Governance & Delivery", "Review");
        var aspire = catalog.Skills.Single(entry => string.Equals(entry.Name, "aspire", StringComparison.Ordinal));
        Assert.True(aspire.TokenCount > 0);
    }

    [Fact]
    public void Load_ExposesFocusedBundles_WithoutMixingQualityAndMigrationFlows()
    {
        var catalog = TestCatalog.Load();

        var dotnetQuality = catalog.Packages.Single(package => string.Equals(package.Name, "quality", StringComparison.Ordinal));
        var frontendQuality = catalog.Packages.Single(package => string.Equals(package.Name, "frontend-quality", StringComparison.Ordinal));
        var testingBase = catalog.Packages.Single(package => string.Equals(package.Name, "testing-base", StringComparison.Ordinal));
        var testingMigrations = catalog.Packages.Single(package => string.Equals(package.Name, "testing-migrations", StringComparison.Ordinal));
        var mcaf = catalog.Packages.Single(package => string.Equals(package.Name, "mcaf", StringComparison.Ordinal));
        var dotnetBase = catalog.Packages.Single(package => string.Equals(package.Name, "foundations", StringComparison.Ordinal));

        Assert.Equal(".NET Quality", dotnetQuality.Stack);
        Assert.Equal("Code Quality", dotnetQuality.Lane);
        Assert.DoesNotContain(dotnetQuality.Skills, skill => string.Equals(skill, "eslint", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dotnetQuality.Skills, skill => string.Equals(skill, "complexity", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dotnetQuality.Skills, skill => string.Equals(skill, "crap-score", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(".NET Foundations", dotnetBase.Stack);
        Assert.Equal("Foundations", dotnetBase.Lane);

        Assert.Equal("Frontend Quality", frontendQuality.Stack);
        Assert.Equal("Code Quality", frontendQuality.Lane);
        Assert.DoesNotContain(frontendQuality.Skills, skill => string.Equals(skill, "format", StringComparison.OrdinalIgnoreCase));

        Assert.All(
            testingBase.Skills,
            skill => Assert.DoesNotContain("migrate-", skill, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(testingBase.Skills, skill => skill.Contains("mtp", StringComparison.OrdinalIgnoreCase));

        Assert.All(
            testingMigrations.Skills,
            skill => Assert.True(
                skill.Contains("migrate-", StringComparison.OrdinalIgnoreCase)
                || skill.Contains("mtp", StringComparison.OrdinalIgnoreCase),
                $"Expected only migration-oriented testing skills, but found {skill}."));

        Assert.True(CatalogOrganization.IsPrimaryBundle(dotnetQuality));
        Assert.Equal("Upgrades & Migration", testingMigrations.Stack);
        Assert.Equal("Governance & Delivery", mcaf.Stack);
        var orleans = catalog.Packages.Single(package => string.Equals(package.Name, "orleans", StringComparison.Ordinal));
        Assert.DoesNotContain(orleans.Skills, skill => string.Equals(skill, "worker-services", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(orleans.Skills, skill => string.Equals(skill, "aspire", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(orleans.Skills, skill => string.Equals(skill, "signalr", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(catalog.Packages, package => string.Equals(package.Kind, "category", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertSkill(SkillCatalogPackage catalog, string name, string expectedStack, string expectedLane)
    {
        var skill = catalog.Skills.Single(entry => string.Equals(entry.Name, name, StringComparison.Ordinal));

        Assert.Equal(expectedStack, skill.Stack);
        Assert.Equal(expectedLane, skill.Lane);
    }
}
