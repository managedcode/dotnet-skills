# Preview Status, Support, and Recurring Checks

## Public Preview Is An Engineering Constraint

The overview page still marks Microsoft Agent Framework as public preview.

Treat that as a real design input:

- package versions will churn
- docs will move
- some features are uneven across languages
- some hosting and integration packages are pre-release only

Preview does not mean "do not use". It means "do not pretend the surface is stable".

## Official Support Surfaces

| Need | Official Place |
| --- | --- |
| current docs | Microsoft Learn Agent Framework site |
| issues and releases | `microsoft/agent-framework` repository |
| questions and discussion | GitHub Discussions |
| migration signals | Learn migration and support pages |

## What To Check On Every Non-Trivial Task

- Which provider and SDK are actually in use?
- Is the feature documented for `.NET`, or only conceptually in Python?
- Is history local, service-backed, or custom-store-backed?
- Are risky tools governed by approvals or middleware?
- Is the hosting surface OpenAI-compatible HTTP, A2A, AG-UI, Azure Functions, or just local testing?
- Are prerelease packages called out explicitly in the target repo?

## Documentation Maturity Signals

The current docs already show uneven maturity:

- declarative workflows are mainly Python-first
- DevUI docs are much richer for Python than for `.NET`
- support upgrade guides are Python-heavy
- troubleshooting is still sparse and being reworked

That means you should use some pages as roadmap or concept signals rather than as proof of shipped `.NET` APIs.

## Support Page Signals That Matter

The live support pages currently reinforce these practical checks:

- FAQ confirms `.NET` and Python are the main languages
- troubleshooting currently starts with authentication and package-version checks
- upgrade guides are not strong `.NET` implementation docs right now

## Common Failure Modes

- Presenting Python-first docs as if they were guaranteed `.NET` APIs
- Assuming preview packages can be locked once and forgotten
- Ignoring provider-specific auth and endpoint requirements
- Treating DevUI as a production support answer
- Building around a support page hint rather than an actual `.NET` guide

## Minimal Troubleshooting Playbook

When something breaks, check in this order:

1. package versions and prerelease alignment
2. provider authentication
3. endpoint format and SDK mismatch
4. thread mode mismatch
5. tool support mismatch
6. protocol-hosting mismatch

That catches most real integration failures faster than diving into app code first.

## Refresh Checklist When The Framework Moves

At minimum re-check:

- overview
- agent types
- running agents
- tools
- workflows overview
- hosting overview
- protocol integrations you actually use
- migration and support pages

## Source Pages

- `references/official-docs/overview/agent-framework-overview.md`
- `references/official-docs/support/index.md`
- `references/official-docs/support/faq.md`
- `references/official-docs/support/troubleshooting.md`
- `references/official-docs/support/upgrade/index.md`
