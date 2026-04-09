namespace ManagedCode.DotnetSkills.Runtime;

internal static class PathSafety
{
    public static DirectoryInfo ResolveDirectoryWithinRoot(DirectoryInfo root, string relativePath, string description)
    {
        return new DirectoryInfo(ResolvePathWithinRoot(root, relativePath, description));
    }

    public static FileInfo ResolveFileWithinRoot(DirectoryInfo root, string relativePath, string description)
    {
        return new FileInfo(ResolvePathWithinRoot(root, relativePath, description));
    }

    internal static string ResolvePathWithinRoot(DirectoryInfo root, string relativePath, string description)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidOperationException($"{description} must not use an empty path.");
        }

        var normalizedRelativePath = relativePath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        var rootPath = EnsureTrailingSeparator(Path.GetFullPath(root.FullName));
        var candidatePath = Path.GetFullPath(Path.Combine(root.FullName, normalizedRelativePath));

        if (!candidatePath.StartsWith(rootPath, PathComparison)
            || string.Equals(candidatePath, rootPath.TrimEnd(Path.DirectorySeparatorChar), PathComparison))
        {
            throw new InvalidOperationException($"{description} must stay within {root.FullName}.");
        }

        return candidatePath;
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
