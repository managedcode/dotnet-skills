namespace ManagedCode.DotnetSkills.Runtime;

internal sealed record CollectionCatalogView(
    string Collection,
    IReadOnlyList<CollectionLaneView> Lanes,
    int SkillCount,
    int InstalledCount,
    int TokenCount);

internal sealed record CollectionLaneView(
    string Collection,
    string Lane,
    IReadOnlyList<SkillEntry> Skills,
    int InstalledCount,
    int TokenCount);
