# graphify-dotnet Source Map

This skill was built from the live upstream repository at [elbruno/graphify-dotnet](https://github.com/elbruno/graphify-dotnet). Use this map when the task needs primary-source detail instead of the shorter routing guidance in `SKILL.md`.

## Core Entry Points

| Topic | Upstream source | When to read it |
|---|---|---|
| Project overview and install | [README.md](https://github.com/elbruno/graphify-dotnet/blob/main/README.md) | First-pass understanding, supported languages, quick-start flow, package identity |
| Pipeline and project structure | [ARCHITECTURE.md](https://github.com/elbruno/graphify-dotnet/blob/main/ARCHITECTURE.md) | Pipeline stages, project boundaries, exporters, cache, MCP and SDK surfaces |
| Getting started | [docs/getting-started.md](https://github.com/elbruno/graphify-dotnet/blob/main/docs/getting-started.md) | First-run walkthrough, sample outputs, default formats |
| CLI surface | [docs/cli-reference.md](https://github.com/elbruno/graphify-dotnet/blob/main/docs/cli-reference.md) | Exact commands, options, defaults, and command shapes |
| Configuration model | [docs/configuration.md](https://github.com/elbruno/graphify-dotnet/blob/main/docs/configuration.md) | Config precedence, wizard flow, env vars, user secrets |
| Worked example | [docs/worked-example.md](https://github.com/elbruno/graphify-dotnet/blob/main/docs/worked-example.md) | Concrete interpretation of graph output from a small C# sample |
| Sample project | [samples/mini-library/README.md](https://github.com/elbruno/graphify-dotnet/blob/main/samples/mini-library/README.md) | Realistic small input project and expected relationships |

## Provider Setup

| Provider | Upstream source | Notes |
|---|---|---|
| Azure OpenAI | [docs/setup-azure-openai.md](https://github.com/elbruno/graphify-dotnet/blob/main/docs/setup-azure-openai.md) | Endpoint, API key, deployment name, CLI and env-var patterns |
| Ollama | [docs/setup-ollama.md](https://github.com/elbruno/graphify-dotnet/blob/main/docs/setup-ollama.md) | Local model workflow, endpoint defaults, model examples |
| GitHub Copilot SDK | [docs/setup-copilot-sdk.md](https://github.com/elbruno/graphify-dotnet/blob/main/docs/setup-copilot-sdk.md) | Authentication paths, model examples, zero-key flow |

## Output Formats

| Format area | Upstream source | Why it matters |
|---|---|---|
| Format overview | [docs/export-formats.md](https://github.com/elbruno/graphify-dotnet/blob/main/docs/export-formats.md) | Format-to-use-case selection and recommended combinations |
| HTML viewer | [docs/format-html.md](https://github.com/elbruno/graphify-dotnet/blob/main/docs/format-html.md) | Browser viewer expectations and interaction model |
| JSON graph | [docs/format-json.md](https://github.com/elbruno/graphify-dotnet/blob/main/docs/format-json.md) | Schema-level downstream automation and scripting |
| SVG | [docs/format-svg.md](https://github.com/elbruno/graphify-dotnet/blob/main/docs/format-svg.md) | Static-document and presentation output |
| Neo4j | [docs/format-neo4j.md](https://github.com/elbruno/graphify-dotnet/blob/main/docs/format-neo4j.md) | Cypher import shape, indexes, query examples |
| Obsidian | [docs/format-obsidian.md](https://github.com/elbruno/graphify-dotnet/blob/main/docs/format-obsidian.md) | Note-per-node vault export |
| Wiki | [docs/format-wiki.md](https://github.com/elbruno/graphify-dotnet/blob/main/docs/format-wiki.md) | Agent-crawlable documentation output |
| Report | [docs/format-report.md](https://github.com/elbruno/graphify-dotnet/blob/main/docs/format-report.md) | Human-readable analysis summary |

## Operations And Troubleshooting

| Topic | Upstream source | Why it matters |
|---|---|---|
| Watch mode | [docs/watch-mode.md](https://github.com/elbruno/graphify-dotnet/blob/main/docs/watch-mode.md) | Incremental updates, debounce model, current limitations |
| Troubleshooting | [docs/troubleshooting.md](https://github.com/elbruno/graphify-dotnet/blob/main/docs/troubleshooting.md) | PATH, provider, empty-graph, large-repo, and HTML viewer issues |
| Tool installation details | [docs/dotnet-tool-install.md](https://github.com/elbruno/graphify-dotnet/blob/main/docs/dotnet-tool-install.md) | Global-tool installation and build-from-source commands |

## Version Signals Observed During Skill Creation

- Upstream default branch: `main`
- Latest tag observed: `v0.6.0`
- CLI package id: `graphify-dotnet`
- CLI command name: `graphify`
- Target framework in published CLI project: `.NET 10`
