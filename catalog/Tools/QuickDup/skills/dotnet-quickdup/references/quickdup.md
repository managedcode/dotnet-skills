# QuickDup for .NET Repositories

## What It Is

`QuickDup` is a fast clone detector that surfaces structural duplication candidates. Treat its output as a review queue, not as proof that two blocks should always be merged.

## Installation Paths

Use the official upstream install paths and keep the chosen command in `AGENTS.md`:

- preferred when Go is available:

```bash
go install github.com/asynkron/Asynkron.QuickDup/cmd/quickdup@latest
```

- official macOS/Linux fallback:

```bash
curl -sSL https://raw.githubusercontent.com/asynkron/Asynkron.QuickDup/main/install.sh | bash
```

- official Windows fallback:

```powershell
iwr -useb https://raw.githubusercontent.com/asynkron/Asynkron.QuickDup/main/install.ps1 | iex
```

Verify the CLI after installation:

```bash
quickdup -h
```

## Good Default Scan Commands

Scan the whole repository:

```bash
quickdup -path . -ext .cs -exclude "bin/*,obj/*,*.g.cs,*.generated.cs,*.Designer.cs"
```

Show the top 20 patterns:

```bash
quickdup -path . -ext .cs -exclude "bin/*,obj/*,*.g.cs,*.generated.cs,*.Designer.cs" -top 20
```

Inspect a bounded set of patterns:

```bash
quickdup -path . -ext .cs -exclude "bin/*,obj/*,*.g.cs,*.generated.cs,*.Designer.cs" -select 0..5
```

Compare a feature branch against main:

```bash
quickdup -path . -ext .cs -exclude "bin/*,obj/*,*.g.cs,*.generated.cs,*.Designer.cs" -compare origin/main..HEAD
```

## Common Excludes for C#

Start with these when the repo has a lot of generated files:

- `bin/*`
- `obj/*`
- `*.g.cs`
- `*.generated.cs`
- `*.Designer.cs`
- `Migrations/*` when migration scaffolding dominates

Only exclude real source folders when the team consciously accepts that duplication there is not actionable.

## How to Review Findings

For each candidate:

1. Confirm the code is hand-written and still active.
2. Confirm the duplication is not documenting domain shape on purpose.
3. Check whether the repeated block differs only by injected behavior, parameter bundles, or tiny wrappers.
4. Decide whether the fix belongs in:
   - a helper method
   - a value object
   - a shared service
   - a small generic abstraction
   - an ignore rule because the duplication is intentional
5. Re-run the relevant tests after each small cleanup.

## Refactoring Patterns That Usually Fit

### Structural Duplication with Small Variations

Signal:

- same control flow
- same loop or branch shape
- only the inner behavior changes

Typical fix:

- extract the shared structure
- pass the varying behavior explicitly

Avoid:

- giant helper methods controlled by flags

### Repeated Parameter Bundles

Signal:

- the same 4+ values travel together through multiple calls

Typical fix:

- create a named context object or value type

### Repeated Argument Unpacking

Signal:

- many methods repeat the same guards and argument extraction logic

Typical fix:

- create a small parsing or validation helper

### Structural Switch Duplication

Signal:

- repeated `switch` or pattern matching on the same domain type

Keep it when:

- the switch documents the domain shape clearly
- each case is trivial

Refactor it when:

- the same switch grows across several sites
- behavior starts drifting between copies

## Suppression

If a pattern is intentionally duplicated, suppress it in `.quickdup/ignore.json`:

```json
{
  "description": "Intentional duplication that should not reappear in review output",
  "ignored": ["56c2f5f9b27ed5a0"]
}
```

Do not suppress findings just to make a report look cleaner. Suppress only when the team has reviewed and accepted the duplication.

## CI Guidance

`QuickDup` is best used as:

- an advisory report in CI
- a scheduled maintainability check
- a local cleanup aid before or during refactors

It is usually a poor primary gate unless the team already trusts the excludes, thresholds, and suppression set.

## Recommended Verification After Cleanup

After refactoring from `QuickDup` findings:

1. run the relevant automated tests
2. run the repo's formatter and analyzer pass
3. re-run the same `QuickDup` command
4. compare findings and ensure the cleanup actually reduced meaningful duplication
