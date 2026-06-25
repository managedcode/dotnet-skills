namespace ManagedCode.DotnetSkills.Runtime;

/// <summary>
/// Structured key binding (carried for future direct dispatch; not wired to global keys yet).
/// </summary>
public record KeyBinding(ConsoleKey Key, ConsoleModifiers Modifiers = 0);

/// <summary>
/// A single command-palette command: a labelled, categorised action with an optional keybinding hint.
/// </summary>
public sealed class SkillCommand
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public string Category { get; init; } = "";
    public string? Keybinding { get; init; }
    public KeyBinding? KeyCombo { get; init; }
    public string Icon { get; init; } = "  ";
    public Action Execute { get; init; } = () => { };
    public int Priority { get; init; } = 50;
}

/// <summary>
/// Holds the command-palette commands and provides search + key lookup. Commands are registered once
/// at shell startup; the palette renders and filters them.
/// </summary>
public sealed class CommandRegistry
{
    private readonly List<SkillCommand> _commands = new();
    private readonly Dictionary<(ConsoleKey, ConsoleModifiers), SkillCommand> _keyMap = new();

    public void Register(SkillCommand command)
    {
        _commands.Add(command);
        if (command.KeyCombo != null)
            _keyMap[(command.KeyCombo.Key, command.KeyCombo.Modifiers)] = command;
    }

    public IReadOnlyList<SkillCommand> All => _commands;

    /// <summary>Look up a command by key combo, or null if none is bound.</summary>
    public SkillCommand? FindByKey(ConsoleKey key, ConsoleModifiers modifiers) =>
        _keyMap.TryGetValue((key, modifiers), out var cmd) ? cmd : null;

    /// <summary>
    /// Filters by case-insensitive substring on label/category/keybinding, then orders by
    /// label-prefix-match first, then by descending priority.
    /// </summary>
    public List<SkillCommand> Search(string query)
    {
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _commands.ToList()
            : _commands.Where(c =>
                c.Label.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (c.Keybinding != null && c.Keybinding.Contains(query, StringComparison.OrdinalIgnoreCase))
            ).ToList();

        return filtered
            .OrderByDescending(c => !string.IsNullOrWhiteSpace(query)
                && c.Label.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenByDescending(c => c.Priority)
            .ToList();
    }
}
