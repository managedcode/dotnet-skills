namespace ManagedCode.DotnetSkills.Runtime;

internal sealed record StackCatalogView(
    string Stack,
    IReadOnlyList<LaneCatalogView> Lanes,
    int SkillCount,
    int InstalledCount,
    int TokenCount);

internal sealed record LaneCatalogView(
    string Stack,
    string Lane,
    IReadOnlyList<SkillEntry> Skills,
    int InstalledCount,
    int TokenCount);
