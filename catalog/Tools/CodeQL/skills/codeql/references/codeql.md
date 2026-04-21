# CodeQL

## Open/Free Status

- open-source query packs and tooling exist
- usable on open-source codebases
- important caveat: GitHub-hosted scanning for private repositories is not universally free and may require GitHub Advanced Security

## Install

For GitHub Actions, use the official action:

- `github/codeql-action/init`
- `github/codeql-action/analyze`

For CLI and query work on open-source codebases, use the CodeQL bundle and CLI from the official CodeQL docs and releases.

## Verify First

Before proposing install steps, check whether the repo already has CodeQL configured:

```bash
rg -n "codeql-action|security-events|CodeQL" .github/workflows
command -v codeql
```

## Common Usage

Typical GitHub Actions flow:

1. initialize CodeQL
2. build the .NET project in `manual` or `autobuild` mode
3. analyze and upload results

## CI Fit

- strong fit for security scanning
- best used with explicit build mode for compiled .NET repos
- document the private-repo licensing caveat before standardizing on it

## When Not To Use

- when the team requires a tool that is unambiguously open/free for private repos without platform caveats

## Sources

- [CodeQL tools](https://codeql.github.com/docs/codeql-overview/codeql-tools/)
- [CodeQL Action](https://github.com/github/codeql-action)
- [About code scanning with CodeQL](https://docs.github.com/en/code-security/code-scanning/introduction-to-code-scanning/about-code-scanning-with-codeql)
