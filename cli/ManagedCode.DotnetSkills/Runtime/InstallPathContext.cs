namespace ManagedCode.DotnetSkills.Runtime;

internal sealed class InstallPathContext
{
    private InstallPathContext(DirectoryInfo projectRoot, DirectoryInfo userHome, DirectoryInfo codexHome)
    {
        ProjectRoot = projectRoot;
        UserHome = userHome;
        CodexHome = codexHome;
    }

    public DirectoryInfo ProjectRoot { get; }

    public DirectoryInfo UserHome { get; }

    public DirectoryInfo CodexHome { get; }

    public static InstallPathContext Create(string? projectDirectory)
    {
        var projectRoot = new DirectoryInfo(ResolveProjectRoot(projectDirectory));
        var userHome = new DirectoryInfo(ResolveUserHomePath());
        var codexHome = new DirectoryInfo(ResolveCodexHomePath(userHome.FullName));
        return new InstallPathContext(projectRoot, userHome, codexHome);
    }

    public static DirectoryInfo ResolveExplicitRoot(string explicitTargetPath)
    {
        return new DirectoryInfo(Path.GetFullPath(explicitTargetPath));
    }

    private static string ResolveProjectRoot(string? projectDirectory) => string.IsNullOrWhiteSpace(projectDirectory)
        ? Path.GetFullPath(Directory.GetCurrentDirectory())
        : Path.GetFullPath(projectDirectory);

    private static string ResolveUserHomePath()
    {
        var home = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrWhiteSpace(home))
        {
            return Path.GetFullPath(home);
        }

        home = Environment.GetEnvironmentVariable("USERPROFILE");
        if (!string.IsNullOrWhiteSpace(home))
        {
            return Path.GetFullPath(home);
        }

        return Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    private static string ResolveCodexHomePath(string userHome)
    {
        var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (!string.IsNullOrWhiteSpace(codexHome))
        {
            return Path.GetFullPath(codexHome);
        }

        return Path.Combine(userHome, ".codex");
    }
}
