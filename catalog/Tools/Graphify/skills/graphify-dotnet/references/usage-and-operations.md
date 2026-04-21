# graphify-dotnet Usage And Operations

## Tool Identity

- Published global tool package: `graphify-dotnet`
- Installed command: `graphify`
- Primary upstream entry point: `src/Graphify.Cli/Graphify.Cli.csproj`
- Default CLI outputs: `graph.json`, `graph.html`, `GRAPH_REPORT.md`
- Full pipeline stages: detect files -> extract features -> build graph -> cluster with Louvain -> analyze -> report -> export

The upstream repo also contains `Graphify`, `Graphify.Sdk`, and `Graphify.Mcp` projects, but the public quick-start and documentation center the CLI tool first.

## Install And Verify

```bash
dotnet --version
dotnet tool install -g graphify-dotnet
graphify --version
```

Build from source only when you intentionally need the upstream repository checkout:

```bash
git clone https://github.com/elbruno/graphify-dotnet.git
cd graphify-dotnet
dotnet build graphify-dotnet.slnx
dotnet run --project src/Graphify.Cli -- run .
```

## Core Command Surface

| Command | Purpose | Default behavior |
|---|---|---|
| `graphify run [path]` | Full extraction pipeline | Writes `json,html,report` into `graphify-out/` |
| `graphify watch [path]` | Incremental rebuild loop | Runs one full build, then watches for changes |
| `graphify benchmark [graph.json]` | Estimate token reduction | Uses an existing graph file |
| `graphify config` | Interactive configuration wizard | Configure provider, folder, and default formats |

Useful options on `run` and `watch`:

- `--output` or `-o`: output directory, default `graphify-out`
- `--format` or `-f`: comma-separated formats
- `--verbose` or `-v`: stage-level progress details
- `--provider`: `azureopenai`, `ollama`, `copilotsdk`, or effectively no provider / AST-only
- `--endpoint`, `--api-key`, `--model`, `--deployment`: provider-specific overrides
- `--config` on `run`: opens the wizard first, then runs the pipeline

## Practical Command Patterns

### Fast structural first pass

```bash
graphify run ./src --format json,html,report --provider none --verbose
```

This is the safest first run when you want deterministic structure before adding semantic inference.

### Full documentation and graph-db export

```bash
graphify run . --format json,html,svg,neo4j,obsidian,wiki,report
```

The upstream docs state there is no meaningful export-stage penalty in generating multiple formats in one run; most cost lives in detection and extraction, especially when AI enrichment is enabled.

### Local privacy-first semantic extraction

```bash
graphify run . --provider ollama --model llama3.2 --format html,report
```

### Azure-governed semantic extraction

```bash
graphify run . \
  --provider azureopenai \
  --endpoint https://myresource.openai.azure.com/ \
  --api-key ... \
  --deployment gpt-4o
```

### Copilot-subscriber semantic extraction

```bash
graphify run . --provider copilotsdk --model gpt-4.1 --verbose
```

### Benchmark an existing graph

```bash
graphify benchmark ./graphify-out/graph.json
```

The benchmark is useful for comparing graph-based context size against raw corpus size, but it is not a provider-authored token invoice. The upstream source uses heuristic token estimation in `BenchmarkRunner`.

## Provider Setup Summary

### AST-only / no provider

- zero setup
- structural facts only
- best default for first runs and CI smoke checks

### Ollama

- endpoint default: `http://localhost:11434`
- common models in docs: `llama3.2`, `codellama`, `deepseek-coder`
- best when privacy, offline work, or low marginal cost matter

### Azure OpenAI

- requires endpoint, API key, deployment name
- best when Azure governance, quota control, or enterprise networking matter
- docs recommend `gpt-4o` or `gpt-4o-mini` style deployments

### GitHub Copilot SDK

- no separate API key flow in the quick-start
- relies on existing GitHub Copilot authentication
- best when the team already uses Copilot and wants the lightest setup

## Configuration Precedence

The docs define this priority order:

1. CLI arguments
2. user secrets
3. environment variables
4. `appsettings.local.json`
5. `appsettings.json`

Representative environment-variable patterns:

```bash
export GRAPHIFY__Provider=Ollama
export GRAPHIFY__Ollama__Endpoint=http://localhost:11434
export GRAPHIFY__Ollama__ModelId=llama3.2
```

```bash
export GRAPHIFY__Provider=AzureOpenAI
export GRAPHIFY__AzureOpenAI__Endpoint=https://myresource.openai.azure.com/
export GRAPHIFY__AzureOpenAI__ApiKey=...
export GRAPHIFY__AzureOpenAI__DeploymentName=gpt-4o
```

```bash
export GRAPHIFY__Provider=CopilotSdk
export GRAPHIFY__CopilotSdk__ModelId=gpt-4.1
```

Use `graphify config show` to inspect the resolved effective configuration.

## Output Selection

| Output | Best use | Notes |
|---|---|---|
| `graph.html` | interactive exploration | Best quick visual check; can hit browser `file://` restrictions |
| `GRAPH_REPORT.md` | human-readable summary | Good first artifact to read |
| `graph.json` | scripting, CI, custom tooling | Best automation handoff |
| `graph.svg` | docs, PRs, slides | Static artifact |
| `graph.cypher` | Neo4j | One-way import into a graph database |
| `obsidian/` | personal knowledge vault | Markdown note-per-node export |
| `wiki/` | team docs or agent-readable docs | Flat, crawlable markdown structure |

Recommended combinations from the upstream docs:

- quick start: `html,report`
- documentation: `html,svg,report`
- knowledge base: `obsidian,wiki,report`
- analysis and automation: `json,neo4j`
- everything: `json,html,svg,neo4j,obsidian,wiki,report`

## Watch Mode Behavior

The upstream watch loop:

1. runs an initial full pipeline
2. monitors files with `FileSystemWatcher`
3. debounces for 500ms
4. verifies content changes with SHA256 hashes
5. re-extracts only changed files
6. merges into the existing graph
7. re-clusters and re-exports

Current limitations called out by upstream docs:

- deleted files can leave stale nodes until a clean rebuild
- renames may temporarily appear as delete + create
- network drives and some containerized mounts can break watcher fidelity

## Troubleshooting Signals

### `graphify: command not found`

- verify installation with `dotnet tool list -g`
- restart the terminal if PATH has not refreshed yet

### Empty graph

- run with `--verbose`
- confirm the path contains supported files
- check whether `.gitignore` or wrong path selection is excluding input files

### Ollama connection problems

- start or restart `ollama serve`
- verify with `curl http://localhost:11434/api/tags`

### Azure OpenAI auth problems

- inspect effective settings with `graphify config show`
- re-run `graphify config set`

### Blank `graph.html`

- serve the output directory with a local HTTP server instead of opening `file://.../graph.html`
- expect slower render times for very large graphs

## Constraints Worth Remembering

- Requires a `.NET 10` SDK.
- AST-only mode is fully supported and useful even without any model provider.
- AI semantic extraction is the expensive stage on large repositories; scope the target path deliberately.
- `benchmark` numbers are heuristic.
- Neo4j export is one-way; changes in Neo4j do not sync back into source or graphify state.
