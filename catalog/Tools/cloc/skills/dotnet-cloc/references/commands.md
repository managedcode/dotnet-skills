# CLOC CLI Commands Reference

## Basic Counting

```bash
# Count all files in current directory
cloc .

# Count with Git awareness (respects .gitignore)
cloc --vcs=git

# Count specific directory or file
cloc src/
cloc src/MyProject.cs
```

## Language Filtering

```bash
# .NET-focused counting
cloc --vcs=git --include-lang="C#,MSBuild,JSON,XML,YAML"

# C# only
cloc --vcs=git --include-lang="C#"

# Razor and web files
cloc --vcs=git --include-lang="C#,Razor,CSS,JavaScript,TypeScript"

# Exclude specific languages
cloc --vcs=git --exclude-lang="Markdown,Text"
```

## Directory and File Exclusions

```bash
# Exclude build output directories
cloc --vcs=git --exclude-dir=bin,obj

# Exclude vendored or generated folders
cloc --vcs=git --exclude-dir=bin,obj,node_modules,packages,.nuget

# Exclude test directories
cloc --vcs=git --exclude-dir=bin,obj,tests,test

# Exclude by file extension
cloc --vcs=git --exclude-ext=Designer.cs,g.cs
```

## Output Formats

```bash
# JSON output
cloc --vcs=git --json

# JSON to file
cloc --vcs=git --json --report-file=cloc-report.json

# CSV output
cloc --vcs=git --csv

# YAML output
cloc --vcs=git --yaml

# Markdown output
cloc --vcs=git --md

# SQL output for database import
cloc --vcs=git --sql=1 --sql-project=MyProject
```

## Detailed File Reports

```bash
# Report by file
cloc --vcs=git --by-file

# Report by file with percentages
cloc --vcs=git --by-file --by-percent c

# Sort by code lines descending
cloc --vcs=git --by-file --sort=code

# Show only top N files
cloc --vcs=git --by-file | head -50
```

## Git Diff Counting

```bash
# Diff between branches
cloc --git --diff origin/main HEAD

# Diff with language filter
cloc --git --diff origin/main HEAD --include-lang="C#"

# Diff between specific commits
cloc --git --diff abc1234 def5678

# Diff between tags
cloc --git --diff v1.0.0 v2.0.0

# Diff output as JSON
cloc --git --diff origin/main HEAD --json
```

## Solution and Project Scope

```bash
# Count a specific solution
cloc MySolution.sln

# Count multiple projects
cloc src/Project1/ src/Project2/

# Count with solution file and exclusions
cloc --vcs=git --exclude-dir=bin,obj,tests src/
```

## Advanced Options

```bash
# Skip duplicate files
cloc --vcs=git --skip-uniqueness

# Force language detection
cloc --vcs=git --force-lang="C#",cs

# Show processing progress
cloc --vcs=git --progress-rate=10

# Count blank lines and comments separately
cloc --vcs=git --by-file-by-lang

# Ignore whitespace differences in diff
cloc --git --diff origin/main HEAD --ignore-whitespace

# Use multiple cores for faster counting
cloc --vcs=git --processes=4
```

## Verification Commands

```bash
# Check cloc installation
command -v cloc

# Show version
cloc --version

# Show help
cloc --help

# List recognized languages
cloc --show-lang

# Show language extensions
cloc --show-ext
```

## CI/CD Integration

```bash
# JSON output for parsing in CI
cloc --vcs=git --json --quiet > cloc-results.json

# Quiet mode (suppress header)
cloc --vcs=git --quiet

# Exit with error if threshold exceeded
cloc --vcs=git --json | jq '.SUM.code' | xargs -I {} test {} -lt 100000

# Save to artifact
cloc --vcs=git --md --report-file=code-metrics.md
```
