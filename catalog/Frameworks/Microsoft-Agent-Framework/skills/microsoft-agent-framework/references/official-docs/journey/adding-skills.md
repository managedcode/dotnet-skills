---
title: Adding Skills
description: Understand why and when to package agent capabilities into skills, how skills differ from tools, and when to reach for skills vs. other patterns.
author: TaoChenOSU
ms.topic: article
ms.author: taochen
ms.date: 04/03/2026
ms.service: agent-framework
---

# Adding Skills

The [previous page](adding-tools.md) showed how tools let agents act — calling functions, querying APIs, searching the web. But as you build more agents, a pattern emerges: the same cluster of tools, instructions, and reference material keeps showing up together. A "file an expense report" capability isn't just one tool — it's a validation script, a set of policy documents, step-by-step instructions on how to fill out the form, and knowledge about spending limits. You end up copy-pasting this bundle from agent to agent, and it drifts out of sync.

**Skills** solve this problem. A skill is a portable package that bundles instructions, reference material, and optional scripts into a single unit that any agent can discover and load on demand. Skills follow an [open specification](https://agentskills.io/) so they're reusable across agents, teams, and even products.

## When to use this

Add skills to your agent when:

- You have a **cluster of related knowledge** — instructions, reference documents, and scripts — that logically belong together (for example, "expense reporting" or "code review guidelines").
- **Multiple agents** need the same domain expertise and you want a single source of truth rather than duplicated instructions.
- You want to **share and distribute** agent capabilities across teams, projects, or organizations as self-contained packages.
- You need to **manage context efficiently** — skills use progressive disclosure so agents only load the detail they need, when they need it.

## Considerations

| Consideration | Details |
|---------------|---------|
| **Reusability** | A skill is a self-contained package. Once created, any agent can pick it up — no copy-paste, no drift between copies. |
| **Context efficiency** | Skills use progressive disclosure: the agent sees a brief description (~100 tokens) upfront and loads full instructions only when relevant. This keeps the context window lean when the skill isn't needed. |
| **Abstraction cost** | Skills add an abstraction layer on top of tools. For a single, standalone function tool, adding a skill wrapper is unnecessary overhead. |
| **Design effort** | You need to think about skill boundaries upfront: what belongs inside the skill and what stays outside. Poor boundaries lead to skills that are too broad (wasting context) or too narrow (losing the bundling benefit). |

## How skills differ from tools

Tools and skills are complementary, not competing. Understanding the distinction helps you decide when to reach for each.

A **tool** is a single callable action — one function with a name, description, and parameter schema. When the model decides a tool is needed, it generates a structured call, Agent Framework executes it, and the result goes back to the model. Tools are the atoms of agent behavior.

A **skill** is a package of domain expertise. It can include:

- **Instructions** — step-by-step guidance, decision rules, and examples that tell the agent *how* to approach a domain.
- **Reference material** — policy documents, FAQs, templates, and other knowledge the agent can consult on demand.
- **Scripts** — executable code the agent can run to perform specific operations (for example, a validation script that checks expense data against policy rules).

The key difference is one of scope: a tool gives the agent the ability to perform **one action**; a skill gives the agent the knowledge and resources to handle **an entire domain**.

| | Tool | Skill |
|---|------|-------|
| **What it provides** | A single callable action | Instructions + reference material + optional scripts |
| **How the agent uses it** | Calls it when it needs to act | Loads it when it encounters a relevant task, reads instructions, and may call scripts or consult resources |
| **Context cost** | Tool schema is always in the prompt | Only the skill name and description (~100 tokens) are in the prompt; full content is loaded on demand |
| **Portability** | Tied to the agent that registers it | Self-contained package that any compatible agent can discover |
| **Best for** | Individual actions (query a database, send an email) | Domain expertise (expense policies, code review guidelines, onboarding procedures) |

> [!TIP]
> Think of tools as **verbs** (search, book, validate) and skills as **expertise** (travel booking knowledge, expense policy knowledge). An agent uses tools to act and skills to know how to act.

## How skills work: progressive disclosure

Skills are designed to be context-efficient. Instead of injecting everything into the prompt upfront, skills use a three-stage pattern:

```
┌──────────────────────────────────────────────────────────────────┐
│  Stage 1: Advertise                                              │
│  Agent sees skill names and descriptions (~100 tokens each)      │
│  in its system prompt at the start of every run.                 │
└──────────────┬───────────────────────────────────────────────────┘
               ▼ (task matches a skill's domain)
┌──────────────────────────────────────────────────────────────────┐
│  Stage 2: Load                                                   │
│  Agent calls load_skill to get the full instructions             │
│  (< 5000 tokens recommended).                                   │
└──────────────┬───────────────────────────────────────────────────┘
               ▼ (agent needs more detail)
┌──────────────────────────────────────────────────────────────────┐
│  Stage 3: Read resources                                         │
│  Agent calls read_skill_resource to fetch supplementary files    │
│  (FAQs, templates, reference docs) only when needed.            │
└──────────────────────────────────────────────────────────────────┘
```

This pattern means an agent with 10 registered skills pays roughly 1,000 tokens of context overhead — not 50,000. The agent only deepens its knowledge when the current task demands it.

In addition, skills are built on top of the tool infrastructure. Agent Framework advertises available skills in the agent's system prompt, then exposes `load_skill` and `read_skill_resource` as tool calls that the agent invokes to progressively load content.

> [!TIP]
> For the full details on skill structure, setup, and code examples, see the [Agent Skills](../agents/skills.md) reference.

## When to use skills vs. other patterns

As your agent grows more capable, you have several ways to organize its behavior. Here's how skills compare to tools:

| Pattern | Best for | Example |
|---------|----------|---------|
| **Individual tools** | One-off actions that don't need shared context | A `get_weather` function tool |
| **Skills** | Domain expertise with instructions, references, and optional scripts | An "expense-report" skill with policy docs, validation scripts, and step-by-step filing instructions |

## Common pitfalls

| Pitfall | Guidance |
|---------|----------|
| **Overly broad skills** | A skill called "everything-about-finance" that tries to cover accounting, taxes, expense reports, and payroll will have instructions too long and unfocused. Keep skills focused on one domain. |
| **Skipping security review** | Skill instructions are injected into the agent's context and scripts execute code. Treat skills like third-party dependencies — review them before deploying. See the [security best practices](../agents/skills.md#security-best-practices) in the skills reference. |
| **Ignoring progressive disclosure** | If your `SKILL.md` is 2,000 lines long, the agent pays a heavy context cost when it loads the skill. Keep instructions concise and move detailed reference material to separate resource files to take full advantage of progressive disclosure. |

## Next steps

Once your agent has tools and skills, the next step is to add **middleware** — cross-cutting behaviors like guardrails, logging, and content filtering that apply to every interaction without modifying your agent's core logic.

> [!div class="nextstepaction"]
> [Adding Middleware](adding-middleware.md)

**Go deeper:**

- [Agent Skills](../agents/skills.md) — full reference with setup, code examples, scripts, and security guidance
- [Agent Skills specification](https://agentskills.io/) — the open standard behind skills
- [Tools Overview](../agents/tools/index.md) — all tool types and provider support matrix
