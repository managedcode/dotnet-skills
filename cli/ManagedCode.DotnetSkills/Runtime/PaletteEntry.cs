namespace ManagedCode.DotnetSkills;

/// <summary>
/// A searchable content entry in the command palette's content mode (a skill, bundle, or agent).
/// Carries its own display fields and an activation action (open its detail modal). Distinct from a
/// <see cref="Runtime.SkillCommand"/>, which is a curated verb in commands mode.
/// </summary>
internal sealed record PaletteEntry(
    string IconLabel,
    string AccentMarkup,
    string Label,
    string Detail,
    string SearchHaystack,
    Action Activate);
