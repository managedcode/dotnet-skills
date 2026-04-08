# GitHub Copilot Instructions

Use `AGENTS.md` as the repository-wide source of truth for workflow, catalog structure, release policy, and skill maintenance rules.

This repository's human-maintained catalog source lives under `catalog/<type>/<package>/` with package `manifest.json`, nested `skills/*/SKILL.md`, and nested `agents/*/AGENT.md`. When project-local Copilot skills are installed, they should be placed in `.github/skills/`.
