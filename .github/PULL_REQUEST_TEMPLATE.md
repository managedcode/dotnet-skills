## Summary

Describe the catalog change and the user-facing outcome.

## Package Scope

- Catalog package path:
- Skills touched:
- Agents touched:

## Manifest Notes

- package `manifest.json` updates:
- `links.repository`:
- `links.docs`:
- `links.nuget`:
- sibling `skills/<skill>/manifest.json` or `agents/<agent>/manifest.json` changes:

## Validation

- [ ] The change stays inside `catalog/<type>/<package>/` for package-owned content
- [ ] `SKILL.md` frontmatter does not declare `version`, `category`, `packages`, or `package_prefix`
- [ ] Package metadata lives in the package `manifest.json`
- [ ] Skill- or agent-specific metadata lives in the nearest sibling `manifest.json`
- [ ] Agents, if any, live in `catalog/<type>/<package>/agents/<agent>/AGENT.md`
- [ ] I ran `python3 scripts/generate_catalog.py --validate-only`
- [ ] I ran `python3 scripts/generate_agent_catalog.py --validate-only` if agents changed
- [ ] I ran `python3 scripts/generate_pages.py` if contributor-facing site content changed
- [ ] I ran `dotnet build dotnet-skills.slnx -c Release`
- [ ] I ran `dotnet test dotnet-skills.slnx -c Release`

## Contributor Checklist

- [ ] I updated `README.md` or `CONTRIBUTING.md` if the contributor workflow or catalog structure changed
- [ ] I added or updated upstream watch entries if the package needs release or docs monitoring
