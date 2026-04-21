# Orchestration Agents

This repository maintains two parallel content layers:

- [`skills/`](../skills): narrow, reusable `dotnet-*` instruction packs
- [`agents/`](../agents): broader orchestration agents that triage work and route into the right skills

Use these placement rules:

- Put broad routing or review agents in `agents/<agent>/AGENT.md`.
- Put tightly coupled specialist agents in `skills/<skill>/agents/<agent>/AGENT.md` when they only make sense next to one skill or one framework surface.
- Use a top-level agent when it orchestrates a group of related skills.
- Use a skill-scoped agent when it should travel with one specific skill and rely on that skill for detailed implementation guidance.
- Keep one folder per agent so each agent can carry references, assets, scripts, and future installer metadata.
- Keep `AGENT.md` thin and routing-oriented. Put bulk documentation, protocol notes, and long decision support material in `references/` or in the paired skill so the agent entrypoint stays token-efficient.

## Starter Agents

- [`dotnet-router/AGENT.md`](../agents/dotnet-router/AGENT.md): first-stop router for broad or ambiguous `.NET` work
- [`dotnet-build/AGENT.md`](../agents/dotnet-build/AGENT.md): build, restore, diagnostics, and CI-focused triage
- [`dotnet-data/AGENT.md`](../agents/dotnet-data/AGENT.md): EF Core, EF6, migrations, and query issues
- [`dotnet-frontend/AGENT.md`](../agents/dotnet-frontend/AGENT.md): frontend-focused triage for Blazor, Node-based web assets, linting, site audits, and file-structure drift inside `.NET` repos
- [`dotnet-ai/AGENT.md`](../agents/dotnet-ai/AGENT.md): Semantic Kernel, Microsoft Agent Framework, Microsoft.Extensions.AI, MCP, and ML.NET
- [`dotnet-modernization/AGENT.md`](../agents/dotnet-modernization/AGENT.md): upgrade, migration, and legacy modernization
- [`dotnet-review/AGENT.md`](../agents/dotnet-review/AGENT.md): review orchestration across quality, testing, and architecture

## Skill-Scoped Specialists

- [`skills/orleans/agents/dotnet-orleans-specialist/AGENT.md`](../skills/orleans/agents/dotnet-orleans-specialist/AGENT.md): Orleans-specific triage for grain boundaries, persistence, streams, reminders, placement, Aspire wiring, and cluster validation
- [`skills/aspire/agents/dotnet-aspire-orchestrator/AGENT.md`](../skills/aspire/agents/dotnet-aspire-orchestrator/AGENT.md): framework-specific routing for AppHost, first-party versus CommunityToolkit/Aspire integrations, dashboard and testing, and deployment choices inside Aspire
- [`skills/microsoft-agent-framework/agents/agent-framework-router/AGENT.md`](../skills/microsoft-agent-framework/agents/agent-framework-router/AGENT.md): Agent Framework-only triage for agent-vs-workflow choice, `AgentThread`, tools, hosting, MCP/A2A/AG-UI, durable agents, and migration

## Layout

```text
agents/
├── README.md
└── <agent-name>/
    ├── AGENT.md
    ├── scripts/       # optional
    ├── references/    # optional
    └── assets/        # optional

skills/<skill-slug>/
├── SKILL.md
├── agents/             # optional skill-scoped agents
│   └── <agent-name>/
│       ├── AGENT.md
│       ├── scripts/    # optional
│       ├── references/ # optional
│       └── assets/     # optional
├── scripts/
├── references/
└── assets/
```

These agent folders are repository-owned orchestration assets. The current `dotnet-skills` CLI remains focused on the skill catalog; agent packaging and distribution can evolve separately. Runtime-specific `.agent.md` or native Claude files should be generated from these canonical folders rather than treated as the source of truth.
