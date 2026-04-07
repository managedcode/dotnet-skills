# CLOC Usage Examples for .NET Repositories

## Scenario: Initial Codebase Assessment

Goal: Understand the size and composition of a .NET solution.

```bash
# Full language breakdown
cloc --vcs=git --exclude-dir=bin,obj

# Sample output:
#      Language          files     blank   comment      code
# ---------------------------------------------------------
#      C#                  142      3200      1850     18500
#      MSBuild              15       120        80       890
#      JSON                 12        50         0       420
#      XML                   8        30        10       280
#      YAML                  5        20         5       150
# ---------------------------------------------------------
#      SUM:                182      3420      1945     20240
```

## Scenario: Test vs Production Code Ratio

Goal: Compare test code to production code.

```bash
# Count production code
cloc --vcs=git --exclude-dir=bin,obj,tests,test,Tests,Test,*.Tests,*.Test --include-lang="C#"

# Count test code only
cloc --vcs=git --exclude-dir=bin,obj --include-lang="C#" tests/ src/*.Tests/
```

Analysis approach:
1. Run both commands
2. Compare code line counts
3. Typical healthy ratio is 1:1 to 2:1 (production to test)

## Scenario: PR Size Assessment

Goal: Quantify changes in a pull request.

```bash
# Compare current branch to main
cloc --git --diff origin/main HEAD --include-lang="C#,MSBuild,JSON"

# Sample output:
#                    same  modified  added  removed
# ------------------------------------------------
# C#                  150        12     45       20
# MSBuild               8         2      1        0
# JSON                  5         1      3        0
# ------------------------------------------------
# SUM:                163        15     49       20
```

Interpretation:
- `same`: files unchanged
- `modified`: files with changes
- `added`: new lines added
- `removed`: lines deleted

## Scenario: Release-to-Release Comparison

Goal: Measure growth between releases.

```bash
# Compare two tagged releases
cloc --git --diff v1.0.0 v2.0.0 --include-lang="C#" --json > release-diff.json

# Extract summary
cat release-diff.json | jq '.SUM'
```

## Scenario: Identify Largest Files

Goal: Find files that might need refactoring.

```bash
# List all C# files by size
cloc --vcs=git --by-file --include-lang="C#" --exclude-dir=bin,obj | sort -t'|' -k4 -nr | head -20

# Alternative with JSON processing
cloc --vcs=git --by-file --include-lang="C#" --json | jq '.[] | select(.language == "C#") | {file: .file, code: .code}' | sort -t':' -k2 -nr
```

## Scenario: Solution Composition Report

Goal: Document the technology mix for architecture review.

```bash
# Generate markdown report
cloc --vcs=git --exclude-dir=bin,obj,node_modules --md --report-file=codebase-composition.md

# Include in docs or PR description
cat codebase-composition.md
```

## Scenario: CI Pipeline Metrics

Goal: Track code size over time in CI.

```yaml
# GitHub Actions example
- name: Generate code metrics
  run: |
    cloc --vcs=git --json --exclude-dir=bin,obj > cloc-report.json
    echo "Total lines: $(jq '.SUM.code' cloc-report.json)"

- name: Upload metrics artifact
  uses: actions/upload-artifact@v7
  with:
    name: code-metrics
    path: cloc-report.json
```

## Scenario: Pre-Refactor Baseline

Goal: Establish metrics before a major refactoring effort.

```bash
# Create baseline snapshot
cloc --vcs=git --by-file --include-lang="C#" --json > baseline-before-refactor.json

# After refactoring, compare
cloc --vcs=git --by-file --include-lang="C#" --json > baseline-after-refactor.json

# Diff the reports
diff <(jq '.SUM' baseline-before-refactor.json) <(jq '.SUM' baseline-after-refactor.json)
```

## Scenario: Exclude Generated Code

Goal: Count only hand-written code.

```bash
# Exclude common generated file patterns
cloc --vcs=git --exclude-dir=bin,obj,Generated,Migrations \
     --not-match-f='\.Designer\.cs$|\.g\.cs$|\.generated\.cs$' \
     --include-lang="C#"
```

## Scenario: Multi-Project Solution Analysis

Goal: Compare sizes across projects in a solution.

```bash
# Count each project directory separately
for dir in src/*/; do
    echo "=== $dir ==="
    cloc --vcs=git "$dir" --include-lang="C#" --quiet
done
```

## Scenario: Documentation Coverage Assessment

Goal: Compare documentation to code volume.

```bash
# Count documentation files
cloc --vcs=git --include-lang="Markdown,XML" docs/ README.md

# Count code files
cloc --vcs=git --include-lang="C#" src/

# Calculate ratio manually or with script
```

## Scenario: Verify Cleanup Success

Goal: Confirm dead code removal reduced codebase size.

```bash
# Before cleanup (commit hash: abc1234)
git stash
git checkout abc1234
cloc --vcs=git --include-lang="C#" --json > before-cleanup.json

# After cleanup (current HEAD)
git checkout -
git stash pop
cloc --vcs=git --include-lang="C#" --json > after-cleanup.json

# Compare
echo "Before: $(jq '.SUM.code' before-cleanup.json) lines"
echo "After: $(jq '.SUM.code' after-cleanup.json) lines"
```

## Scenario: Excluding Specific Vendored Libraries

Goal: Count only first-party code.

```bash
# Exclude vendored directories
cloc --vcs=git \
     --exclude-dir=bin,obj,vendor,external,third-party,packages \
     --include-lang="C#,MSBuild"
```

## Scenario: Quick Health Check

Goal: Fast assessment of repo state for code review.

```bash
# One-liner summary
cloc --vcs=git --quiet --exclude-dir=bin,obj | tail -n 5
```
