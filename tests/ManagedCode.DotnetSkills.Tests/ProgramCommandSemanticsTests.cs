namespace ManagedCode.DotnetSkills.Tests;

public sealed class ProgramCommandSemanticsTests
{
    [Theory]
    [InlineData("help")]
    [InlineData("--help")]
    [InlineData("-h")]
    public void IsUsageStartup_ReturnsTrue_ForHelpPaths(params string[] args)
    {
        Assert.True(Program.IsUsageStartup(args));
    }

    [Theory]
    [InlineData()]
    public void IsInteractiveStartup_ReturnsTrue_ForNoArgs(params string[] args)
    {
        Assert.True(Program.IsInteractiveStartup(args));
    }

    [Theory]
    [InlineData("version")]
    [InlineData("--version")]
    [InlineData("list")]
    [InlineData("package")]
    [InlineData("recommend")]
    [InlineData("install")]
    [InlineData("update")]
    public void IsUsageStartup_ReturnsFalse_ForNonUsageCommands(params string[] args)
    {
        Assert.False(Program.IsUsageStartup(args));
    }

    [Theory]
    [InlineData("help")]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("list")]
    public void IsInteractiveStartup_ReturnsFalse_WhenArgsExist(params string[] args)
    {
        Assert.False(Program.IsInteractiveStartup(args));
    }

    [Fact]
    public void ParseInstallOptions_RecognizesAutoMode()
    {
        var options = Program.ParseInstallOptions(["--auto"]);

        Assert.True(options.AutoInstall);
        Assert.False(options.PackageMode);
        Assert.Empty(options.RequestedSkills);
    }

    [Fact]
    public void ParseInstallOptions_RecognizesPackageMode()
    {
        var options = Program.ParseInstallOptions(["package", "data"]);

        Assert.True(options.PackageMode);
        Assert.False(options.AutoInstall);
        Assert.Equal(["data"], options.RequestedSkills);
    }

    [Fact]
    public void ParseInstallOptions_RecognizesBundleMode()
    {
        var options = Program.ParseInstallOptions(["bundle", "data"]);

        Assert.True(options.PackageMode);
        Assert.False(options.AutoInstall);
        Assert.Equal(["data"], options.RequestedSkills);
    }
}
