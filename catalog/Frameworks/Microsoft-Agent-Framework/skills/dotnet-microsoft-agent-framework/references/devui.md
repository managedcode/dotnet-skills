# DevUI

## What DevUI Actually Is

DevUI is a sample app for development-time testing of agents and workflows.

It gives you:

- a local web UI
- an OpenAI-compatible local API surface
- trace viewing
- directory discovery for sample entities
- a quick way to exercise inputs without building your real frontend

It is not a production hosting surface.

## `.NET` Caveat

The current docs are explicit that `.NET` DevUI documentation is still limited and mostly "coming soon", while Python has the richer published guidance.

So for `.NET` work:

- treat DevUI docs as conceptual guidance
- do not invent `.NET` APIs that the docs do not actually publish
- do not anchor production architecture on DevUI behavior

## Good Uses

- smoke-testing prompts and tools locally
- checking whether a workflow input shape is usable
- tracing runs during early development
- trying sample entities before you wire real hosting

## Bad Uses

- production chat surfaces
- public internet endpoints
- security boundaries
- long-lived integration contracts

## DevUI Versus Real Hosting

| Need | Use DevUI? | Real Answer |
| --- | --- | --- |
| Local debugging | Yes | DevUI is good here |
| Human-facing production UI | No | AG-UI or your own app |
| OpenAI-compatible production endpoint | No | Hosting.OpenAI |
| Agent-to-agent interoperability | No | A2A |
| Secure public service boundary | No | ASP.NET Core hosting with your own auth and policies |

## Safe Usage Rules

- Keep it on localhost by default.
- If you expose it to a network, add auth and still treat it as non-production.
- Be careful with side-effecting tools even in local demos.
- Do not mistake "it works in DevUI" for "the production contract is done".

## Source Pages

- `references/official-docs/user-guide/devui/index.md`
- `references/official-docs/user-guide/devui/security.md`
- `references/official-docs/user-guide/devui/tracing.md`
- `references/official-docs/user-guide/devui/directory-discovery.md`
