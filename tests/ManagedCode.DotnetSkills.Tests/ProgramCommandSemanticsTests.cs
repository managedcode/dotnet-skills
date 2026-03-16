namespace ManagedCode.DotnetSkills.Tests;

public sealed class ProgramCommandSemanticsTests
{
    [Theory]
    [InlineData()]
    [InlineData("help")]
    [InlineData("--help")]
    [InlineData("-h")]
    public void IsUsageStartup_ReturnsTrue_ForUsageAndHelpPaths(params string[] args)
    {
        Assert.True(Program.IsUsageStartup(args));
    }

    [Theory]
    [InlineData("version")]
    [InlineData("--version")]
    [InlineData("list")]
    [InlineData("recommend")]
    [InlineData("install")]
    [InlineData("update")]
    public void IsUsageStartup_ReturnsFalse_ForNonUsageCommands(params string[] args)
    {
        Assert.False(Program.IsUsageStartup(args));
    }
}
