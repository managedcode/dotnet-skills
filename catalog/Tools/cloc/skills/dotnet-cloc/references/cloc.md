# cloc for .NET Repositories

## What It Is

`cloc` counts blank lines, comment lines, and physical lines of code across many languages. In a .NET repository, it is useful for footprint reporting, solution composition, branch diffs, and bounded-scope comparisons.

Use it to answer specific questions, not to rate developer output.

## Installation Paths

Use the official upstream install paths and keep the chosen command in `AGENTS.md`:

- macOS with Homebrew:

```bash
brew install cloc
```

- Debian or Ubuntu:

```bash
sudo apt install cloc
```

- Red Hat or older Fedora family:

```bash
sudo yum install cloc
```

- Fedora or newer Red Hat family:

```bash
sudo dnf install cloc
```

- npm fallback:

```bash
npm install -g cloc
```

- Windows with Chocolatey:

```powershell
choco install cloc
```

- Windows with Scoop:

```powershell
scoop install cloc
```

- Docker fallback:

```bash
docker run --rm -v $PWD:/tmp aldanial/cloc .
```

If package-manager builds are not acceptable, use the latest upstream release from `AlDanial/cloc` and verify:

```bash
cloc --version
```

## Good Default Commands

Count tracked files in the current repository:

```bash
cloc --vcs=git
```

Count common .NET repo languages only:

```bash
cloc --vcs=git --include-lang="C#,MSBuild,JSON,XML,YAML"
```

Count only C# by file:

```bash
cloc --by-file --vcs=git --include-lang="C#"
```

Compare branch delta for C#:

```bash
cloc --git --diff origin/main HEAD --include-lang="C#"
```

Count a bounded subtree:

```bash
cloc src --include-lang="C#,MSBuild,JSON,XML,YAML"
```

## Excludes That Usually Matter

Start with repo-respecting or explicit excludes so the numbers are not polluted by build artifacts:

- `bin`
- `obj`
- `.git`
- vendored folders
- generated folders when they are not part of the question

Example:

```bash
cloc . --exclude-dir=bin,obj,.git
```

## Output Modes

Use machine-readable output when the numbers feed docs or automation:

- `--json`
- `--csv`
- `--yaml`
- `--md`
- `--xml`

Example:

```bash
cloc --vcs=git --include-lang="C#" --json
```

## When It Helps

Use `cloc` when you need:

- a quick footprint of production versus test code
- a branch-to-branch size diff after a refactor
- a codebase language mix for docs or governance
- a stable command that humans and CI can rerun

## When It Does Not Help

Do not use `cloc` to conclude that:

- a larger change is better or worse by itself
- a smaller file is automatically simpler
- test quality is good because test LOC is high

Pair `cloc` with the repo's real verification flow: tests, analyzers, architecture checks, and maintainability review.
