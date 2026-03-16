using ManagedCode.DotnetSkills.Runtime;

namespace ManagedCode.DotnetSkills.Tests;

public sealed class SkillInstallTargetTests
{
    [Fact]
    public void ResolveAutoProject_PrefersCodexRootBeforeOtherAgentFolders()
    {
        using var tempDirectory = new TemporaryDirectory();
        // When .codex or .agents exists, Codex uses .agents/skills path per official Codex docs
        Directory.CreateDirectory(System.IO.Path.Combine(tempDirectory.Path, ".codex"));
        Directory.CreateDirectory(System.IO.Path.Combine(tempDirectory.Path, ".claude"));
        Directory.CreateDirectory(System.IO.Path.Combine(tempDirectory.Path, ".github"));

        var layout = SkillInstallTarget.Resolve(
            explicitTargetPath: null,
            agent: AgentPlatform.Auto,
            scope: InstallScope.Project,
            projectDirectory: tempDirectory.Path);

        Assert.Equal(AgentPlatform.Codex, layout.Agent);
        Assert.Equal(SkillInstallMode.RawSkillPayloads, layout.Mode);
        // Codex uses .agents/skills for project-level skills (per official docs)
        Assert.Equal(System.IO.Path.Combine(tempDirectory.Path, ".agents", "skills"), layout.PrimaryRoot.FullName);
    }
}
