#!/usr/bin/env python3
"""Generate a multi-page GitHub Pages site from the skill and agent catalogs."""

from __future__ import annotations

from datetime import date
import html
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
from urllib.parse import urljoin, urlparse

REPO_ROOT = Path(__file__).resolve().parent.parent
CATALOG_PATH = REPO_ROOT / "catalog" / "skills.json"
AGENTS_CATALOG_PATH = REPO_ROOT / "catalog" / "agents.json"
README_PATH = REPO_ROOT / "README.md"
TEMPLATE_PATH = REPO_ROOT / "github-pages" / "index.html"
OUTPUT_DIR = REPO_ROOT / "artifacts" / "github-pages"
SITEMAP_PATH = OUTPUT_DIR / "sitemap.xml"
ROBOTS_PATH = OUTPUT_DIR / "robots.txt"
CNAME_PATH = OUTPUT_DIR / "CNAME"

DEFAULT_SITE_URL = "https://skills.managed-code.com/"
DEFAULT_RELEASES_URL = "https://github.com/managedcode/dotnet-skills/releases/tag/"
COPYRIGHT_START_YEAR = 2024
SOCIAL_IMAGE_PATH = "assets/social-card.svg"
GITHUB_REPOSITORY_URL = "https://github.com/managedcode/dotnet-skills"
NUGET_PACKAGE_URL = "https://www.nuget.org/packages/dotnet-skills"
MANAGEDCODE_WEBSITE_URL = "https://www.managed-code.com/"

PLACEHOLDERS = {
    "page_title": "PAGE_TITLE_PLACEHOLDER",
    "page_description": "PAGE_DESCRIPTION_PLACEHOLDER",
    "page_keywords": "PAGE_KEYWORDS_PLACEHOLDER",
    "canonical_url": "CANONICAL_URL_PLACEHOLDER",
    "og_type": "OG_TYPE_PLACEHOLDER",
    "social_image": "SOCIAL_IMAGE_URL_PLACEHOLDER",
    "page_json_ld": "PAGE_JSON_LD_PLACEHOLDER",
    "page_extra_head": "PAGE_EXTRA_HEAD_PLACEHOLDER",
    "body_class": "BODY_CLASS_PLACEHOLDER",
    "root_prefix": "ROOT_PREFIX_PLACEHOLDER",
    "root_href": "ROOT_HREF_PLACEHOLDER",
    "site_url": "SITE_URL_PLACEHOLDER",
    "release_tag": "RELEASE_TAG_PLACEHOLDER",
    "release_url": "RELEASE_URL_PLACEHOLDER",
    "page_main_content": "PAGE_MAIN_CONTENT_PLACEHOLDER",
    "page_data": "PAGE_DATA_PLACEHOLDER",
    "copyright": "COPYRIGHT_YEAR_RANGE_PLACEHOLDER",
}

CATEGORY_DESCRIPTIONS = {
    "AI": "Agent frameworks, provider abstractions, MCP, Semantic Kernel, ML.NET, and other modern .NET AI building blocks.",
    "Architecture": "Solution-level direction for boundaries, layering, modernization, and long-lived .NET design decisions.",
    "Cloud": "Cloud-native .NET app hosts, Aspire topologies, deployment workflows, and service-to-service integration patterns.",
    "Code Quality": "Analyzers, formatters, review guidance, mutation testing, and maintainability tooling for healthy .NET repos.",
    "Core": "Foundational .NET topics such as modern C#, SDK setup, project structure, and broad platform guidance.",
    "Cross-Platform UI": "Uno Platform, MAUI-adjacent patterns, and cross-device UI guidance for teams shipping beyond one OS.",
    "Data": "Entity Framework, storage libraries, ingestion pipelines, and persistence patterns for production .NET applications.",
    "Desktop": "Desktop-focused application development, Windows-oriented UX, media, and client app support libraries.",
    "Distributed": "Orleans, distributed coordination, messaging, clusters, and runtime patterns for stateful .NET systems.",
    "Legacy": "Migration and maintenance help for older ASP.NET, WCF, Workflow Foundation, and other legacy .NET surfaces.",
    "Metrics": "Profilers, code metrics, duplication detection, and observability tooling that improve engineering feedback loops.",
    "Testing": "Unit, integration, browser, and distributed-system testing patterns for current .NET projects.",
    "Web": "ASP.NET Core, Blazor, frontends, APIs, auth, and service hosting for modern web-facing .NET stacks.",
}


def escape_html(text: str | None) -> str:
    """Escape HTML text safely."""
    return html.escape(text or "", quote=True)


def slugify(text: str) -> str:
    """Create stable URL slugs for pages."""
    slug = re.sub(r"[^a-z0-9]+", "-", text.lower()).strip("-")
    return slug or "item"


def normalize_site_url(raw_url: str) -> str:
    """Normalize the configured public site URL."""
    return raw_url.rstrip("/") + "/"


def relative_root_prefix(path: str) -> str:
    """Return the relative path prefix from a page path back to the site root."""
    if not path:
        return ""

    depth = len([segment for segment in path.strip("/").split("/") if segment])
    return "../" * depth


def build_absolute_url(site_url: str, path: str) -> str:
    """Build an absolute public URL for a generated page path."""
    return urljoin(site_url, path)


def output_file_for(path: str) -> Path:
    """Resolve the output file for a page path."""
    if not path:
        return OUTPUT_DIR / "index.html"

    return OUTPUT_DIR / path / "index.html"


def render_copyright_year_range() -> str:
    """Render the footer copyright year span."""
    current_year = date.today().year
    if current_year <= COPYRIGHT_START_YEAR:
        return str(COPYRIGHT_START_YEAR)

    return f"{COPYRIGHT_START_YEAR}-{current_year}"


def resolve_release_version() -> str:
    """Resolve the current release version for the public site."""
    if os.environ.get("DOTNET_SKILLS_RELEASE_VERSION"):
        return os.environ["DOTNET_SKILLS_RELEASE_VERSION"]

    try:
        result = subprocess.run(
            ["git", "tag", "--list", "catalog-v*", "--sort=-v:refname"],
            capture_output=True,
            text=True,
            cwd=REPO_ROOT,
            timeout=5,
        )
        if result.returncode == 0:
            latest_tag = next((line.strip() for line in result.stdout.splitlines() if line.strip()), "")
            if latest_tag.startswith("catalog-v"):
                return latest_tag.removeprefix("catalog-v")
    except (FileNotFoundError, subprocess.TimeoutExpired):
        pass

    return "0.0.0"


def resolve_release_tag(release_version: str) -> str:
    """Resolve the current release tag for the public site."""
    return os.environ.get("DOTNET_SKILLS_RELEASE_TAG") or f"catalog-v{release_version}"


def resolve_release_url(release_tag: str) -> str:
    """Resolve the current release URL for the public site."""
    return (os.environ.get("DOTNET_SKILLS_RELEASE_URL") or f"{DEFAULT_RELEASES_URL}{release_tag}").strip()


def get_git_last_modified(paths: list[str]) -> str:
    """Get the latest git commit date for the given repository-relative paths."""
    valid_paths = [path for path in paths if path]
    if not valid_paths:
        return date.today().isoformat()

    try:
        result = subprocess.run(
            ["git", "log", "-1", "--date=short", "--format=%cd", "--", *valid_paths],
            capture_output=True,
            text=True,
            cwd=REPO_ROOT,
            timeout=8,
        )
        if result.returncode == 0 and result.stdout.strip():
            return result.stdout.strip()
    except (FileNotFoundError, subprocess.TimeoutExpired):
        pass

    return date.today().isoformat()


def parse_frontmatter_value(raw_value: str) -> object:
    """Parse a simple YAML scalar used by repository frontmatter."""
    value = raw_value.strip()
    if not value:
        return ""

    if len(value) >= 2 and value[0] == value[-1] and value[0] in {'"', "'"}:
        return value[1:-1]

    lowered = value.lower()
    if lowered == "true":
        return True
    if lowered == "false":
        return False

    if re.fullmatch(r"-?\d+", value):
        return int(value)

    if value.startswith("[") and value.endswith("]"):
        inner = value[1:-1].strip()
        if not inner:
            return []
        return [parse_frontmatter_value(part) for part in inner.split(",")]

    return value


def parse_frontmatter(frontmatter_text: str) -> dict:
    """Parse the small frontmatter subset used in SKILL.md and AGENT.md files."""
    metadata: dict[str, object] = {}
    current_list_key: str | None = None

    for raw_line in frontmatter_text.splitlines():
        line = raw_line.rstrip()
        stripped = line.strip()

        if not stripped or stripped.startswith("#"):
            continue

        list_match = re.match(r"^\s*-\s+(.*)$", line)
        if list_match and current_list_key:
            list_value = metadata.setdefault(current_list_key, [])
            if isinstance(list_value, list):
                list_value.append(parse_frontmatter_value(list_match.group(1)))
            continue

        current_list_key = None
        key, separator, remainder = line.partition(":")
        if not separator:
            continue

        key = key.strip()
        if not key:
            continue

        value = remainder.strip()
        if value:
            metadata[key] = parse_frontmatter_value(value)
            continue

        metadata[key] = []
        current_list_key = key

    return metadata


def split_frontmatter(markdown_text: str) -> tuple[dict, str]:
    """Split YAML frontmatter from markdown content."""
    if not markdown_text.startswith("---\n"):
        return {}, markdown_text

    match = re.match(r"^---\n(.*?)\n---\n(.*)$", markdown_text, re.DOTALL)
    if not match:
        return {}, markdown_text

    metadata = parse_frontmatter(match.group(1))
    body = match.group(2)
    return metadata, body


def parse_markdown_sections(markdown_text: str) -> dict[str, list[str]]:
    """Parse second-level markdown sections into line arrays."""
    sections: dict[str, list[str]] = {}
    current: str | None = None

    for raw_line in markdown_text.splitlines():
        line = raw_line.rstrip()
        if line.startswith("## "):
            current = line[3:].strip()
            sections[current] = []
            continue

        if current is not None:
            sections[current].append(line)

    return sections


INLINE_TOKEN_RE = re.compile(r"`([^`]+)`|\[([^\]]+)\]\(([^)]+)\)|\*\*([^*]+)\*\*")


def render_inline_markdown(text: str) -> str:
    """Render a narrow markdown inline subset to HTML."""
    chunks: list[str] = []
    last_index = 0

    for match in INLINE_TOKEN_RE.finditer(text):
        chunks.append(escape_html(text[last_index:match.start()]))

        if match.group(1):
            chunks.append(f"<code>{escape_html(match.group(1))}</code>")
        elif match.group(2) and match.group(3):
            chunks.append(
                f'<a href="{escape_html(match.group(3))}" target="_blank" rel="noopener noreferrer">'
                f"{escape_html(match.group(2))}</a>"
            )
        elif match.group(4):
            chunks.append(f"<strong>{escape_html(match.group(4))}</strong>")

        last_index = match.end()

    chunks.append(escape_html(text[last_index:]))
    return "".join(chunks)


def render_markdown_lines(lines: list[str]) -> str:
    """Render a limited markdown subset for SKILL.md and AGENT.md sections."""
    output: list[str] = []
    index = 0

    while index < len(lines):
        line = lines[index].rstrip()

        if not line.strip():
            index += 1
            continue

        if line.startswith("```"):
            language = escape_html(line[3:].strip())
            code_lines: list[str] = []
            index += 1
            while index < len(lines) and not lines[index].startswith("```"):
                code_lines.append(lines[index])
                index += 1
            output.append(
                f'<pre class="doc-code"><code class="{language}">{escape_html(chr(10).join(code_lines))}</code></pre>'
            )
            index += 1
            continue

        if line.startswith("### "):
            output.append(f"<h3>{render_inline_markdown(line[4:].strip())}</h3>")
            index += 1
            continue

        ordered_match = re.match(r"^\d+\.\s+(.*)$", line)
        if ordered_match:
            items: list[str] = []
            while index < len(lines):
                candidate = lines[index].rstrip()
                numbered = re.match(r"^\d+\.\s+(.*)$", candidate)
                if not numbered:
                    break
                items.append(numbered.group(1))
                index += 1
            output.append(
                "<ol>" + "".join(f"<li>{render_inline_markdown(item)}</li>" for item in items) + "</ol>"
            )
            continue

        if line.startswith("- "):
            items = []
            while index < len(lines) and lines[index].startswith("- "):
                items.append(lines[index][2:].rstrip())
                index += 1
            output.append(
                "<ul>" + "".join(f"<li>{render_inline_markdown(item)}</li>" for item in items) + "</ul>"
            )
            continue

        paragraph_lines = [line]
        index += 1
        while index < len(lines):
            candidate = lines[index].rstrip()
            if not candidate.strip():
                break
            if candidate.startswith(("## ", "### ", "- ", "```")) or re.match(r"^\d+\.\s+", candidate):
                break
            paragraph_lines.append(candidate)
            index += 1

        paragraph = " ".join(part.strip() for part in paragraph_lines)
        output.append(f"<p>{render_inline_markdown(paragraph)}</p>")

    return "\n".join(output)


def render_reference_links(lines: list[str]) -> str:
    """Render reference lists as link-rich HTML when possible."""
    items: list[str] = []
    for raw_line in lines:
        line = raw_line.strip()
        if not line or not line.startswith("- "):
            continue
        items.append(f"<li>{render_inline_markdown(line[2:])}</li>")

    if not items:
        return render_markdown_lines(lines)

    return '<div class="doc-link-list"><ul>' + "".join(items) + "</ul></div>"


def trim_text(value: str, limit: int) -> str:
    """Trim text for card summaries without cutting words harshly."""
    if len(value) <= limit:
        return value

    truncated = value[: limit - 1].rsplit(" ", 1)[0].rstrip(" ,.;:")
    return truncated + "…"


def preview_text(value: str, limit: int = 180) -> str:
    """Create a calmer card preview without dumping full descriptions into list pages."""
    first_sentence = re.split(r"(?<=[.!?])\s+", value.strip(), maxsplit=1)[0].strip()
    if first_sentence and len(first_sentence) <= limit:
        return first_sentence

    return trim_text(value.strip(), limit)


def dedupe_strings(values: list[str]) -> list[str]:
    """Preserve order while removing duplicates."""
    seen: set[str] = set()
    result: list[str] = []
    for value in values:
        if value not in seen and value:
            seen.add(value)
            result.append(value)
    return result


def read_text(path: Path) -> str:
    """Read a UTF-8 text file."""
    return path.read_text(encoding="utf-8")


def load_skill_documents(skills: list[dict], site_url: str) -> list[dict]:
    """Enrich skills with content extracted from their markdown source."""
    enriched: list[dict] = []

    for skill in skills:
        skill_slug = skill["name"]
        skill_short_name = skill_slug.replace("dotnet-", "")
        skill_path = REPO_ROOT / skill["path"] / "SKILL.md"
        raw_markdown = read_text(skill_path)
        _, body = split_frontmatter(raw_markdown)
        sections = parse_markdown_sections(body)
        category_slug = slugify(skill["category"])
        detail_path = f"skills/{skill_slug}/"
        source_url = f"{GITHUB_REPOSITORY_URL}/tree/main/{skill['path']}"

        enriched.append(
            {
                **skill,
                "slug": skill_slug,
                "short_name": skill_short_name,
                "category_slug": category_slug,
                "detail_path": detail_path,
                "detail_url": build_absolute_url(site_url, detail_path),
                "source_url": source_url,
                "source_file": f"{skill['path']}/SKILL.md",
                "sections": sections,
                "lastmod": get_git_last_modified([f"{skill['path']}/SKILL.md", skill["path"]]),
            }
        )

    return enriched


def load_agent_documents(agents: list[dict], site_url: str) -> list[dict]:
    """Enrich agents with parsed markdown content and URLs."""
    enriched: list[dict] = []

    for agent in agents:
        agent_slug = agent["name"]
        agent_path = REPO_ROOT / agent["path"] / "AGENT.md"
        raw_markdown = read_text(agent_path)
        _, body = split_frontmatter(raw_markdown)
        sections = parse_markdown_sections(body)
        detail_path = f"agents/{agent_slug}/"
        source_url = f"{GITHUB_REPOSITORY_URL}/tree/main/{agent['path']}"

        enriched.append(
            {
                **agent,
                "slug": agent_slug,
                "short_name": agent_slug.replace("dotnet-", ""),
                "detail_path": detail_path,
                "detail_url": build_absolute_url(site_url, detail_path),
                "source_url": source_url,
                "source_file": f"{agent['path']}/AGENT.md",
                "sections": sections,
                "lastmod": get_git_last_modified([f"{agent['path']}/AGENT.md", agent["path"]]),
            }
        )

    return enriched


def load_package_documents(packages: list[dict], skills_by_name: dict[str, dict], site_url: str) -> list[dict]:
    """Enrich packages with linked skill data and generated URLs."""
    enriched: list[dict] = []

    for package in packages:
        package_name = package["name"]
        raw_skill_names = package.get("skills", [])
        missing_skills = [skill_name for skill_name in raw_skill_names if skill_name not in skills_by_name]
        if missing_skills:
            raise ValueError(
                f"Package {package_name} references unknown skills: {', '.join(sorted(missing_skills))}"
            )

        linked_skills = [skills_by_name[skill_name] for skill_name in raw_skill_names]
        detail_path = f"packages/{package_name}/"
        source_category = package.get("sourceCategory", "")
        kind = package.get("kind", "curated")

        enriched.append(
            {
                **package,
                "slug": package_name,
                "short_name": package_name,
                "kind": kind,
                "kind_label": "Category package" if kind == "category" else "Curated package",
                "source_category_slug": slugify(source_category) if source_category else "",
                "detail_path": detail_path,
                "detail_url": build_absolute_url(site_url, detail_path),
                "skill_names": raw_skill_names,
                "skills": linked_skills,
                "install_command": f"dotnet skills install package {package_name}",
                "lastmod": get_git_last_modified(
                    [skill["source_file"] for skill in linked_skills]
                    + ["scripts/generate_catalog.py", "scripts/generate_pages.py"]
                ),
            }
        )

    return enriched


def parse_credits_from_readme() -> list[dict]:
    """Parse the README credits section into structured tables."""
    readme_text = read_text(README_PATH)
    match = re.search(r"^## Credits\n(.*)$", readme_text, re.DOTALL | re.MULTILINE)
    if not match:
        return []

    credits_text = match.group(1)
    blocks: list[dict] = []
    current_title = ""
    current_rows: list[list[str]] = []

    for line in credits_text.splitlines():
        if line.startswith("### "):
            if current_title and current_rows:
                blocks.append({"title": current_title, "rows": current_rows})
            current_title = line[4:].strip()
            current_rows = []
            continue

        if line.startswith("|") and not line.startswith("|---"):
            cells = [cell.strip() for cell in line.strip("|").split("|")]
            if len(cells) == 3 and cells[0] != "Tool/Library":
                current_rows.append(cells)

    if current_title and current_rows:
        blocks.append({"title": current_title, "rows": current_rows})

    return blocks


def render_breadcrumb(items: list[tuple[str, str | None]]) -> str:
    """Render breadcrumb navigation."""
    parts: list[str] = ['<nav class="breadcrumb" aria-label="Breadcrumb">']
    for index, (label, href) in enumerate(items):
        if href:
            parts.append(f'<a href="{escape_html(href)}">{escape_html(label)}</a>')
        else:
            parts.append(f'<span>{escape_html(label)}</span>')
        if index < len(items) - 1:
            parts.append("<span>/</span>")
    parts.append("</nav>")
    return "".join(parts)


def render_metric_card(value: str, label: str) -> str:
    """Render a metric card."""
    return (
        '<div class="metric-card">'
        f'<div class="metric-value">{escape_html(value)}</div>'
        f'<div class="metric-label">{escape_html(label)}</div>'
        "</div>"
    )


def render_button(label: str, href: str, variant: str = "ghost", external: bool = False) -> str:
    """Render a CTA button link."""
    attrs = ' target="_blank" rel="noopener noreferrer"' if external else ""
    return f'<a class="button button-{variant}" href="{escape_html(href)}"{attrs}>{escape_html(label)}</a>'


def render_skill_card(skill: dict, root_prefix: str, quick_view: bool = True) -> str:
    """Render a skill card with both quick view and crawlable page links."""
    detail_href = f"{root_prefix}{skill['detail_path']}"
    category_href = f"{root_prefix}categories/{skill['category_slug']}/"
    install_command = f"dotnet skills install {skill['short_name']}"
    summary = preview_text(skill["description"])
    filter_text = " ".join(
        dedupe_strings(
            [
                skill["title"],
                skill["name"],
                skill["description"],
                skill["category"],
                skill.get("compatibility", ""),
            ]
        )
    )
    actions = [
        f'<a class="card-inline-link" href="{escape_html(detail_href)}">Open skill page'
        '<span class="card-inline-arrow">→</span></a>',
    ]
    if quick_view:
        actions.insert(0, f'<button type="button" class="button button-ghost" data-open-skill="{escape_html(skill["name"])}">Quick view</button>')

    return f"""
      <article class="directory-card skill-card is-clickable js-filter-card" data-category="{escape_html(skill['category'])}" data-filtertext="{escape_html(filter_text)}" data-card-href="{escape_html(detail_href)}" tabindex="0" role="link" aria-label="Open {escape_html(skill['title'])} skill">
        <div class="card-top">
          <div>
            <div class="card-kicker">Skill</div>
            <h3><a href="{escape_html(detail_href)}">{escape_html(skill['title'])}</a></h3>
          </div>
          <span class="card-version">v{escape_html(skill['version'])}</span>
        </div>
        <p class="card-summary">{escape_html(summary)}</p>
        <div class="chip-row skill-meta-row">
          <a class="chip" href="{escape_html(category_href)}">{escape_html(skill['category'])}</a>
        </div>
        <div class="command-row skill-command-row">
          <code>{escape_html(install_command)}</code>
        </div>
        <div class="card-actions card-actions-inline">
          {"".join(actions)}
        </div>
      </article>
    """.strip()


def render_agent_card(agent: dict, root_prefix: str, linked_skills: dict[str, dict]) -> str:
    """Render an orchestration agent card."""
    detail_href = f"{root_prefix}{agent['detail_path']}"
    install_command = f"agents install {agent['short_name']}"
    alternate_install_command = f"dotnet agents install {agent['short_name']}"
    summary = preview_text(agent["description"], limit=150)
    related_skill_chips = []
    visible_linked_skills = agent.get("skills", [])[:2]
    for skill_name in visible_linked_skills:
        skill = linked_skills.get(skill_name)
        if not skill:
            continue
        related_skill_chips.append(
            f'<a class="chip agent-skill-chip" href="{escape_html(root_prefix + skill["detail_path"])}">{escape_html(skill["short_name"])}</a>'
        )
    hidden_linked_skills = max(0, len(agent.get("skills", [])) - len(visible_linked_skills))
    if hidden_linked_skills:
        related_skill_chips.append(
            f'<span class="chip agent-skill-chip agent-skill-chip-more">+{hidden_linked_skills} more</span>'
        )

    filter_text = " ".join(
        dedupe_strings(
            [
                agent["title"],
                agent["name"],
                agent["description"],
                *agent.get("skills", []),
            ]
        )
    )

    return f"""
      <article class="directory-card agent-card is-clickable js-filter-card" data-category="agents" data-filtertext="{escape_html(filter_text)}" data-card-href="{escape_html(detail_href)}" tabindex="0" role="link" aria-label="Open {escape_html(agent['title'])} agent">
        <div class="agent-card-meta">
          <div class="card-kicker">Orchestration agent</div>
          <span class="card-version">{len(agent.get("skills", []))} linked skills</span>
        </div>
        <h3><a href="{escape_html(detail_href)}">{escape_html(agent['title'])}</a></h3>
        <p class="card-summary">{escape_html(summary)}</p>
        <div class="agent-install-row">
          <span class="chip command-chip">{escape_html(install_command)}</span>
        </div>
        <p class="card-summary">Also works: <code>{escape_html(alternate_install_command)}</code></p>
        <div class="agent-links-block">
          <div class="card-kicker agent-links-label">Routes to</div>
          <div class="chip-row agent-chip-row">
            {"".join(related_skill_chips)}
          </div>
        </div>
        <div class="card-actions card-actions-inline">
          {render_button("Source", agent["source_url"], "ghost", external=True)}
          <a class="card-inline-link" href="{escape_html(detail_href)}">Open agent page<span class="card-inline-arrow">→</span></a>
        </div>
      </article>
    """.strip()


def render_category_card(category_name: str, category_info: dict, root_prefix: str) -> str:
    """Render a category directory card."""
    detail_href = f"{root_prefix}categories/{category_info['slug']}/"
    sample_skills = ", ".join(skill["short_name"] for skill in category_info["skills"][:3])
    summary = preview_text(category_info["description"], limit=150)
    filter_text = " ".join(
        dedupe_strings(
            [
                category_name,
                category_info["description"],
                sample_skills,
                " ".join(skill["name"] for skill in category_info["skills"]),
            ]
        )
    )

    return f"""
      <article class="directory-card category-card is-clickable js-filter-card" data-category="category" data-filtertext="{escape_html(filter_text)}" data-card-href="{escape_html(detail_href)}" tabindex="0" role="link" aria-label="Open {escape_html(category_name)} category">
        <div class="card-top">
          <div>
            <div class="card-kicker">Category</div>
            <h3><a href="{escape_html(detail_href)}">{escape_html(category_name)}</a></h3>
          </div>
          <span class="card-version">{len(category_info['skills'])} skills</span>
        </div>
        <p class="card-summary">{escape_html(summary)}</p>
        <div class="chip-row">
          <span class="chip">{len(category_info['related_agents'])} related agents</span>
          <span class="chip">{escape_html(sample_skills or 'catalog overview')}</span>
        </div>
        <div class="card-inline-link" aria-hidden="true">
          <span>Browse skills</span>
          <span class="card-inline-arrow">→</span>
        </div>
      </article>
    """.strip()


def render_package_card(package: dict, root_prefix: str) -> str:
    """Render a package directory card."""
    detail_href = f"{root_prefix}{package['detail_path']}"
    summary = preview_text(package["description"], limit=150)
    install_command = package["install_command"]
    filter_text = " ".join(
        dedupe_strings(
            [
                package["title"],
                package["name"],
                package["description"],
                package["kind_label"],
                package.get("sourceCategory", ""),
                " ".join(skill["name"] for skill in package["skills"]),
            ]
        )
    )

    chips = [
        f'<span class="chip">{escape_html(package["kind_label"])}</span>',
        f'<span class="chip command-chip">{escape_html(install_command)}</span>',
    ]
    if package.get("sourceCategory") and package.get("source_category_slug"):
        chips.insert(
            1,
            f'<a class="chip" href="{escape_html(root_prefix + "categories/" + package["source_category_slug"] + "/")}">'
            f'{escape_html(package["sourceCategory"])} category</a>',
        )

    return f"""
      <article class="directory-card package-card is-clickable js-filter-card" data-category="{escape_html(package['kind'])}" data-filtertext="{escape_html(filter_text)}" data-card-href="{escape_html(detail_href)}" tabindex="0" role="link" aria-label="Open {escape_html(package['title'])} package">
        <div class="card-top">
          <div>
            <div class="card-kicker">{escape_html(package["kind_label"])}</div>
            <h3><a href="{escape_html(detail_href)}">{escape_html(package['title'])}</a></h3>
          </div>
          <span class="card-version">{len(package['skills'])} skills</span>
        </div>
        <p class="card-summary">{escape_html(summary)}</p>
        <div class="chip-row">
          {"".join(chips)}
        </div>
        <div class="command-row">
          <code>{escape_html(install_command)}</code>
        </div>
        <div class="card-actions card-actions-inline">
          <a class="card-inline-link" href="{escape_html(detail_href)}">Open package<span class="card-inline-arrow">→</span></a>
        </div>
      </article>
    """.strip()


def render_filter_tabs(category_infos: dict[str, dict], current_category: str = "all") -> str:
    """Render category filter tabs."""
    buttons = [
        f'<button class="filter-tab {"is-active" if current_category == "all" else ""}" type="button" data-filter="all">All skills</button>'
    ]

    for category_name in sorted(category_infos):
        category_info = category_infos[category_name]
        is_active = current_category == category_name
        buttons.append(
            f'<button class="filter-tab {"is-active" if is_active else ""}" type="button" '
            f'data-filter="{escape_html(category_name)}">{escape_html(category_name)} '
            f'({len(category_info["skills"])})</button>'
        )

    return "".join(buttons)


def render_package_filter_tabs(current_kind: str = "all") -> str:
    """Render package-kind filter tabs."""
    options = [
        ("all", "All packages"),
        ("curated", "Curated"),
        ("category", "Category packs"),
    ]
    return "".join(
        f'<button class="filter-tab {"is-active" if current_kind == value else ""}" type="button" data-filter="{value}">{label}</button>'
        for value, label in options
    )


def render_empty_state(message: str) -> str:
    """Render a reusable empty state block."""
    return f'<div class="empty-state is-hidden" id="listing-empty"><p>{escape_html(message)}</p></div>'


def render_panel_links(links: list[tuple[str, str]]) -> str:
    """Render a small row of panel links."""
    if not links:
        return ""

    html_links = "".join(
        f'<a class="pill-link" href="{escape_html(href)}">{escape_html(label)}</a>' for label, href in links
    )
    return f'<div class="section-links">{html_links}</div>'


def render_skill_listing_section(
    skills: list[dict],
    category_infos: dict[str, dict],
    root_prefix: str,
    title: str,
    description: str,
    *,
    include_tabs: bool,
    empty_message: str,
    show_controls: bool = True,
    show_index_link: bool = False,
) -> str:
    """Render a searchable skill directory section."""
    tabs_html = render_filter_tabs(category_infos) if include_tabs else ""
    cards_html = "\n".join(render_skill_card(skill, root_prefix) for skill in skills)
    section_links = render_panel_links([("Skill directory", f"{root_prefix}skills/")]) if show_index_link else ""

    toolbar_html = ""
    empty_state_html = ""
    if show_controls:
        toolbar_html = f"""
          <div class="listing-toolbar">
            <input class="search-input" id="search-input" type="search" placeholder="Search by name, category, or topic" autocomplete="off">
            {'<div class="filter-tabs">' + tabs_html + '</div>' if tabs_html else ''}
          </div>
        """.strip()
        empty_state_html = render_empty_state(empty_message)

    return f"""
      <section class="section-stack">
        <div class="section-header">
          <div>
            <h2>{escape_html(title)}</h2>
            <p>{escape_html(description)}</p>
          </div>
          {section_links}
        </div>
        <div class="panel">
          {toolbar_html}
          <div class="directory-grid" id="listing-grid">
            {cards_html}
          </div>
          {empty_state_html}
        </div>
      </section>
    """.strip()


def render_agent_listing_section(
    agents: list[dict], root_prefix: str, linked_skills: dict[str, dict], *, show_index_link: bool = True
) -> str:
    """Render an agent directory section."""
    cards_html = "\n".join(render_agent_card(agent, root_prefix, linked_skills) for agent in agents)
    panel_links = render_panel_links([("See all agents", f"{root_prefix}agents/")]) if show_index_link else ""
    return f"""
      <section class="section-stack">
        <div class="section-header">
          <div>
            <h2>Orchestration agents</h2>
            <p>Top-level routing agents that sit above the skill catalog and hand work to the right .NET guidance.</p>
          </div>
          {panel_links}
        </div>
        <div class="directory-grid agent-grid">
          {cards_html}
        </div>
      </section>
    """.strip()


def render_category_listing_section(category_infos: dict[str, dict], root_prefix: str) -> str:
    """Render the category directory section."""
    cards_html = "\n".join(
        render_category_card(category_name, category_infos[category_name], root_prefix)
        for category_name in sorted(category_infos)
    )
    return f"""
      <section class="section-stack">
        <div class="section-header">
          <div>
            <h2>Browse the catalog by category</h2>
            <p>Each category has its own page with related skills, linked agents, and direct paths into the catalog.</p>
          </div>
          {render_panel_links([("Category hub", f"{root_prefix}categories/"), ("Skill directory", f"{root_prefix}skills/")])}
        </div>
        <div class="directory-grid">
          {cards_html}
        </div>
      </section>
    """.strip()


def render_package_listing_section(
    packages: list[dict],
    root_prefix: str,
    title: str,
    description: str,
    include_tabs: bool,
    empty_message: str,
    show_index_link: bool = True,
) -> str:
    """Render a package directory section."""
    if not packages:
        return ""

    cards_html = "\n".join(render_package_card(package, root_prefix) for package in packages)
    tabs_html = render_package_filter_tabs() if include_tabs else ""
    links = [("Packages", f"{root_prefix}packages/")] if show_index_link else []
    if any(package.get("sourceCategory") for package in packages):
        links.append(("Categories", f"{root_prefix}categories/"))

    toolbar_html = ""
    empty_state_html = ""
    if include_tabs:
        toolbar_html = f"""
        <div class="listing-toolbar">
          <input class="search-input" id="search-input" type="search" placeholder="Search packages by name, category, or included skill" autocomplete="off">
          <div class="filter-tabs">
            {tabs_html}
          </div>
        </div>
        """.strip()
        empty_state_html = render_empty_state(empty_message)

    return f"""
      <section class="section-stack">
        <div class="section-header">
          <div>
            <h2>{escape_html(title)}</h2>
            <p>{escape_html(description)}</p>
          </div>
          {render_panel_links(links)}
        </div>
        {toolbar_html}
        {empty_state_html}
        <div class="directory-grid">
          {cards_html}
        </div>
      </section>
    """.strip()


def render_quickstart_panel() -> str:
    """Render the home-page quickstart steps."""
    steps = [
        ("Install the CLI", "dotnet tool install --global dotnet-skills", "Get the catalog onto your machine."),
        ("Browse packages", "dotnet skills package list", "See the curated and category-based bundles that install multiple skills at once."),
        ("Install a package", "dotnet skills install package ai", "Add a ready-made skill bundle when you want broad coverage quickly."),
        ("Install exact skills", "dotnet skills install aspire blazor", "Add individual topics when you want a smaller custom setup."),
    ]
    cards = []
    for index, (title, command, description) in enumerate(steps, start=1):
        cards.append(
            f"""
              <div class="directory-card">
                <div class="card-kicker">Step {index}</div>
                <h3>{escape_html(title)}</h3>
                <p>{escape_html(description)}</p>
                <div class="command-row">
                  <code>{escape_html(command)}</code>
                  <button type="button" class="button button-ghost" data-copy="{escape_html(command)}">Copy</button>
                </div>
              </div>
            """.strip()
        )

    return f"""
      <section class="section-stack">
        <div class="section-header">
          <div>
            <h2>Quick start</h2>
            <p>Keep the install flow simple, then jump into category, skill, and agent pages when you need more detail.</p>
          </div>
          {render_panel_links([("NuGet package", NUGET_PACKAGE_URL), ("GitHub repository", GITHUB_REPOSITORY_URL)])}
        </div>
        <div class="quickstart-grid">
          {"".join(cards)}
        </div>
      </section>
    """.strip()


def render_support_panel(root_prefix: str) -> str:
    """Render a compact supported-platforms panel."""
    platforms = [
        ("Claude Code", "CC", "Native personal and project folders for skills and agents."),
        ("GitHub Copilot", "GH", "Repository-friendly skill layouts for team workflows and check-ins."),
        ("Gemini", "GM", "Consistent directory conventions for personal and repo-local installs."),
        ("Codex", "CX", "Native `.codex` roots plus auto-detect support in the CLI."),
        ("Junie", "JN", "JetBrains-native `.junie` roots for project and personal skill and agent installs."),
    ]

    cards = []
    for name, mark, description in platforms:
        cards.append(
            f"""
              <div class="directory-card support-card">
                <div class="support-mark" aria-hidden="true">{escape_html(mark)}</div>
                <div class="card-kicker">Supported platform</div>
                <h3>{escape_html(name)}</h3>
                <p>{escape_html(description)}</p>
              </div>
            """.strip()
        )

    return f"""
      <section class="section-stack">
        <div class="section-header">
          <div>
            <h2>One catalog, multiple coding platforms</h2>
            <p>The same installable catalog lands in Claude Code, GitHub Copilot, Gemini, Codex, and Junie without inventing a different setup flow for each one.</p>
          </div>
          {render_panel_links([("About the catalog", f"{root_prefix}about/")])}
        </div>
        <div class="panel-grid">
          {"".join(cards)}
        </div>
      </section>
    """.strip()


def render_home_page(
    skills: list[dict],
    packages: list[dict],
    agents: list[dict],
    category_infos: dict[str, dict],
    release_tag: str,
    root_prefix: str,
) -> tuple[str, dict]:
    """Render the root landing page."""
    hero = f"""
      <section class="page-hero">
        <div class="eyebrow-row">
          <span class="eyebrow">Open source</span>
          <span class="tag tag-accent">Curated for modern .NET</span>
          <span class="tag">{escape_html(release_tag)}</span>
        </div>
        <h1 class="page-title">.NET skills with a calmer, <span class="accent">structured install flow</span></h1>
        <p class="page-lead">Start with a package when you need broad coverage fast. Drop into categories, skills, and orchestration agents when you want more exact .NET guidance for the coding platform you already use.</p>
        <div class="hero-actions">
          {render_button("Browse packages", f"{root_prefix}packages/", "primary")}
          {render_button("Browse categories", f"{root_prefix}categories/", "ghost")}
          {render_button("Read about the catalog", f"{root_prefix}about/", "ghost")}
        </div>
        <div class="metric-grid">
          {render_metric_card(str(len(skills)), "Skills")}
          {render_metric_card(str(len(packages)), "Packages")}
          {render_metric_card(str(len(agents)), "Agents")}
          {render_metric_card(str(len(category_infos)), "Categories")}
          {render_metric_card("5", "Platforms")}
        </div>
      </section>
    """

    sections = [
        hero,
        render_quickstart_panel(),
        render_package_listing_section(
            packages[:6],
            root_prefix,
            "Install ready-made packages",
            "Use package installs when you want one command to lay down a broader, already-grouped skill set.",
            include_tabs=False,
            empty_message="",
            show_index_link=True,
        ),
        render_category_listing_section(category_infos, root_prefix),
        render_agent_listing_section(agents[:6], root_prefix, {skill["name"]: skill for skill in skills}),
        render_support_panel(root_prefix),
        render_skill_listing_section(
            skills[:6],
            category_infos,
            root_prefix,
            "Featured skills",
            "A few representative entries from the catalog. Use the full directory when you want the searchable complete list.",
            include_tabs=False,
            empty_message="No skills match this combination yet.",
            show_controls=False,
            show_index_link=True,
        ),
    ]

    page_data = {
        "querySyncPath": "",
        "skills": build_skill_payload(skills, root_prefix),
    }
    return "\n".join(sections), page_data


def build_skill_payload(skills: list[dict], root_prefix: str) -> list[dict]:
    """Build lightweight payloads used by the front-end modal."""
    return [
        {
            "name": skill["name"],
            "title": skill["title"],
            "description": skill["description"],
            "version": skill["version"],
            "category": skill["category"],
            "compatibility": skill.get("compatibility", ""),
            "detailUrl": f"{root_prefix}{skill['detail_path']}",
            "sourceUrl": skill["source_url"],
            "installCommand": f"dotnet skills install {skill['short_name']}",
        }
        for skill in skills
    ]


def build_category_infos(skills: list[dict], agents: list[dict]) -> dict[str, dict]:
    """Build per-category metadata used across multiple pages."""
    infos: dict[str, dict] = {}
    for skill in skills:
        category_name = skill["category"]
        category_info = infos.setdefault(
            category_name,
            {
                "slug": skill["category_slug"],
                "description": CATEGORY_DESCRIPTIONS.get(category_name, f"{category_name} guidance for current .NET projects."),
                "skills": [],
                "related_agents": [],
                "lastmod_paths": [],
            },
        )
        category_info["skills"].append(skill)
        category_info["lastmod_paths"].append(skill["source_file"])

    for category_name, category_info in infos.items():
        category_skill_names = {skill["name"] for skill in category_info["skills"]}
        for agent in agents:
            if category_skill_names.intersection(agent.get("skills", [])):
                category_info["related_agents"].append(agent)

    return infos


def select_skill_sections(skill: dict) -> list[tuple[str, str]]:
    """Pick the most useful sections for a skill detail page."""
    priority = [
        "Trigger On",
        "Workflow",
        "Current Guidance",
        "Selection Rules",
        "Deliver",
        "Validate",
        "Anti-Patterns",
        "References",
        "Load References",
    ]
    rendered: list[tuple[str, str]] = []

    for section_name in priority:
        lines = skill["sections"].get(section_name)
        if not lines:
            continue
        renderer = render_reference_links if section_name in {"References", "Load References"} else render_markdown_lines
        rendered.append((section_name, renderer(lines)))

    return rendered


def select_agent_sections(agent: dict) -> list[tuple[str, str]]:
    """Pick the most useful sections for an agent detail page."""
    priority = [
        "Role",
        "Trigger On",
        "Workflow",
        "Skill Routing",
        "Deliver",
        "Boundaries",
    ]
    rendered: list[tuple[str, str]] = []

    for section_name in priority:
        lines = agent["sections"].get(section_name)
        if not lines:
            continue
        rendered.append((section_name, render_markdown_lines(lines)))

    return rendered


def render_skill_detail_page(skill: dict, related_skills: list[dict], related_agents: list[dict], root_prefix: str) -> str:
    """Render a full skill page."""
    sections_html = "".join(
        f'<section><h2>{escape_html(title)}</h2>{content}</section>' for title, content in select_skill_sections(skill)
    )
    related_skill_cards = "\n".join(render_skill_card(candidate, root_prefix, quick_view=False) for candidate in related_skills[:3])
    related_agent_cards = "\n".join(
        render_agent_card(agent, root_prefix, {skill["name"]: skill for skill in related_skills + [skill]})
        for agent in related_agents[:2]
    )

    return f"""
      {render_breadcrumb([("Home", root_prefix or "./"), ("Skills", f"{root_prefix}skills/"), (skill["title"], None)])}
      <section class="page-hero">
        <div class="eyebrow-row">
          <span class="eyebrow">Skill page</span>
          <a class="tag" href="{escape_html(root_prefix + 'categories/' + skill['category_slug'] + '/')}">{escape_html(skill['category'])}</a>
          <span class="tag tag-accent">v{escape_html(skill['version'])}</span>
        </div>
        <h1 class="page-title">{escape_html(skill['title'])}</h1>
        <p class="page-lead">{escape_html(skill['description'])}</p>
      </section>

      <div class="detail-layout">
        <div class="article-copy">
          {sections_html}
          {'<section><h2>Related skills</h2><div class="directory-grid">' + related_skill_cards + '</div></section>' if related_skill_cards else ''}
          {'<section><h2>Related agents</h2><div class="directory-grid">' + related_agent_cards + '</div></section>' if related_agent_cards else ''}
        </div>

        <aside class="sidebar-stack">
          <div class="sidebar-card">
            <div class="detail-card-label">Install command</div>
            <div class="command-row">
              <code>dotnet skills install {escape_html(skill['short_name'])}</code>
              <button type="button" class="button button-ghost" data-copy="dotnet skills install {escape_html(skill['short_name'])}">Copy</button>
            </div>
          </div>

          <div class="sidebar-card">
            <div class="detail-card-label">Compatibility</div>
            <p>{escape_html(skill.get('compatibility', 'Works with current .NET projects.'))}</p>
          </div>

          <div class="sidebar-card">
            <div class="detail-card-label">Explore next</div>
            <div class="sidebar-links">
              <a class="pill-link" href="{escape_html(root_prefix + 'skills/')}">All skills</a>
              <a class="pill-link" href="{escape_html(root_prefix + 'categories/' + skill['category_slug'] + '/')}">{escape_html(skill['category'])} category</a>
              <a class="pill-link" href="{escape_html(skill['source_url'])}" target="_blank" rel="noopener noreferrer">Source on GitHub</a>
            </div>
          </div>
        </aside>
      </div>
    """.strip()


def render_agent_detail_page(agent: dict, linked_skills: list[dict], root_prefix: str) -> str:
    """Render a full orchestration agent page."""
    sections_html = "".join(
        f'<section><h2>{escape_html(title)}</h2>{content}</section>' for title, content in select_agent_sections(agent)
    )
    skill_cards = "\n".join(render_skill_card(skill, root_prefix, quick_view=False) for skill in linked_skills)

    return f"""
      {render_breadcrumb([("Home", root_prefix or "./"), ("Agents", f"{root_prefix}agents/"), (agent["title"], None)])}
      <section class="page-hero">
        <div class="eyebrow-row">
          <span class="eyebrow">Agent page</span>
          <span class="tag tag-accent">{len(linked_skills)} linked skills</span>
        </div>
        <h1 class="page-title">{escape_html(agent['title'])}</h1>
        <p class="page-lead">{escape_html(agent['description'])}</p>
      </section>

      <div class="detail-layout">
        <div class="article-copy">
          {sections_html}
          <section>
            <h2>Linked skills</h2>
            <div class="directory-grid">
              {skill_cards}
            </div>
          </section>
        </div>

        <aside class="sidebar-stack">
          <div class="sidebar-card">
            <div class="detail-card-label">Install command</div>
            <div class="command-row">
              <code>agents install {escape_html(agent['short_name'])}</code>
              <button type="button" class="button button-ghost" data-copy="agents install {escape_html(agent['short_name'])}">Copy</button>
            </div>
            <p class="card-summary">Alternate command: <code>dotnet agents install {escape_html(agent['short_name'])}</code></p>
          </div>

          <div class="sidebar-card">
            <div class="detail-card-label">Source</div>
            <div class="sidebar-links">
              <a class="pill-link" href="{escape_html(agent['source_url'])}" target="_blank" rel="noopener noreferrer">Open on GitHub</a>
              <a class="pill-link" href="{escape_html(root_prefix + 'agents/')}">All agents</a>
            </div>
          </div>
        </aside>
      </div>
    """.strip()


def render_category_detail_page(category_name: str, category_info: dict, root_prefix: str) -> tuple[str, dict]:
    """Render a category landing page."""
    hero = f"""
      {render_breadcrumb([("Home", root_prefix or "./"), ("Categories", f"{root_prefix}categories/"), (category_name, None)])}
      <section class="page-hero">
        <div class="eyebrow-row">
          <span class="eyebrow">Category page</span>
          <span class="tag tag-accent">{len(category_info['skills'])} skills</span>
          <span class="tag">{len(category_info['related_agents'])} related agents</span>
        </div>
        <h1 class="page-title">{escape_html(category_name)} <span class="accent">.NET Skills</span></h1>
        <p class="page-lead">{escape_html(category_info['description'])}</p>
      </section>
    """

    related_agents_section = ""
    if category_info["related_agents"]:
        related_agents_section = render_agent_listing_section(
            category_info["related_agents"],
            root_prefix,
            {skill["name"]: skill for skill in category_info["skills"]},
        )

    body = "\n".join(
        [
            hero,
            related_agents_section,
            render_skill_listing_section(
                category_info["skills"],
                {category_name: category_info},
                root_prefix,
                f"{category_name} skills",
                f"Browse every catalog entry tagged under {category_name}. These cards link to dedicated skill pages and still support quick-view popups.",
                include_tabs=False,
                empty_message=f"No {category_name} skills match this search.",
            ),
        ]
    )

    page_data = {
        "querySyncPath": "",
        "skills": build_skill_payload(category_info["skills"], root_prefix),
    }
    return body, page_data


def render_categories_index_page(category_infos: dict[str, dict], root_prefix: str) -> str:
    """Render the category hub page."""
    return f"""
      {render_breadcrumb([("Home", root_prefix or "./"), ("Categories", None)])}
      <section class="page-hero">
        <div class="eyebrow-row">
          <span class="eyebrow">Category hub</span>
          <span class="tag tag-accent">{len(category_infos)} categories</span>
        </div>
        <h1 class="page-title">Browse the catalog by <span class="accent">category</span></h1>
        <p class="page-lead">Each category groups related skills and agents so it is easier to browse the catalog by topic.</p>
      </section>
      {render_category_listing_section(category_infos, root_prefix)}
    """.strip()


def render_skills_index_page(skills: list[dict], category_infos: dict[str, dict], root_prefix: str) -> tuple[str, dict]:
    """Render the dedicated skill directory page."""
    body = f"""
      {render_breadcrumb([("Home", root_prefix or "./"), ("Skills", None)])}
      <section class="page-hero">
        <div class="eyebrow-row">
          <span class="eyebrow">Skill directory</span>
          <span class="tag tag-accent">{len(skills)} skills</span>
        </div>
        <h1 class="page-title">Search every <span class="accent">.NET skill</span></h1>
        <p class="page-lead">This is the full skill directory, with search, category filters, and a dedicated page for every skill.</p>
      </section>
      {render_skill_listing_section(
          skills,
          category_infos,
          root_prefix,
          "All skills",
          "Use category filters or a direct search query to narrow the catalog. Every card links to a dedicated skill page.",
          include_tabs=True,
          empty_message="No skills match this search yet."
      )}
    """.strip()

    page_data = {
        "querySyncPath": "skills/",
        "skills": build_skill_payload(skills, root_prefix),
    }
    return body, page_data


def render_packages_index_page(packages: list[dict], root_prefix: str) -> tuple[str, dict]:
    """Render the package directory page."""
    body = f"""
      {render_breadcrumb([("Home", root_prefix or "./"), ("Packages", None)])}
      <section class="page-hero">
        <div class="eyebrow-row">
          <span class="eyebrow">Package directory</span>
          <span class="tag tag-accent">{len(packages)} packages</span>
        </div>
        <h1 class="page-title">Install grouped <span class="accent">skill packages</span></h1>
        <p class="page-lead">Packages expand one command into multiple related skills. Use curated packs for common stacks such as Orleans, or category packs when you want every skill under one topic.</p>
      </section>
      {render_package_listing_section(
          packages,
          root_prefix,
          "All packages",
          "Search package bundles, inspect the exact install command, and jump to detail pages for the included skills.",
          include_tabs=True,
          empty_message="No packages match this search yet.",
          show_index_link=False,
      )}
    """.strip()

    page_data = {
        "querySyncPath": "packages/",
    }
    return body, page_data


def render_package_detail_page(package: dict, root_prefix: str) -> str:
    """Render a package detail page."""
    install_command = package["install_command"]
    category_link = ""
    if package.get("sourceCategory") and package.get("source_category_slug"):
        category_link = (
            f'<a class="pill-link" href="{escape_html(root_prefix + "categories/" + package["source_category_slug"] + "/")}">'
            f'{escape_html(package["sourceCategory"])} category</a>'
        )

    related_skill_cards = render_skill_listing_section(
        package["skills"],
        build_category_infos(package["skills"], []),
        root_prefix,
        "Included skills",
        "These are the exact skills installed by this package command.",
        include_tabs=False,
        empty_message="No skills are currently attached to this package.",
    )

    return f"""
      {render_breadcrumb([("Home", root_prefix or "./"), ("Packages", f"{root_prefix}packages/"), (package["title"], None)])}
      <section class="page-hero">
        <div class="eyebrow-row">
          <span class="eyebrow">{escape_html(package["kind_label"])}</span>
          <span class="tag tag-accent">{len(package["skills"])} skills</span>
        </div>
        <h1 class="page-title">{escape_html(package["title"])}</h1>
        <p class="page-lead">{escape_html(package["description"])}</p>
        <div class="hero-actions">
          {render_button("Browse all packages", f"{root_prefix}packages/", "ghost")}
          {render_button("See all skills", f"{root_prefix}skills/", "ghost")}
        </div>
      </section>

      <div class="detail-layout">
        <div class="article-copy">
          <section>
            <h2>Install command</h2>
            <p>Run this command when you want the package to install every linked skill in one pass.</p>
            <div class="command-row">
              <code>{escape_html(install_command)}</code>
              <button type="button" class="button button-ghost" data-copy="{escape_html(install_command)}">Copy</button>
            </div>
          </section>
          <section>
            <h2>What this package covers</h2>
            <p>{escape_html(package["description"])}</p>
          </section>
        </div>
        <aside class="sidebar-stack">
          <div class="sidebar-card">
            <div class="detail-card-label">Package info</div>
            <div class="sidebar-links">
              <span class="pill-link">{escape_html(package["kind_label"])}</span>
              <span class="pill-link">{len(package["skills"])} skills</span>
              {category_link}
            </div>
          </div>
        </aside>
      </div>

      {related_skill_cards}
    """.strip()


def render_agents_index_page(agents: list[dict], skills: list[dict], root_prefix: str) -> str:
    """Render the orchestration agent hub page."""
    return f"""
      {render_breadcrumb([("Home", root_prefix or "./"), ("Agents", None)])}
      <section class="page-hero">
        <div class="eyebrow-row">
          <span class="eyebrow">Agent hub</span>
          <span class="tag tag-accent">{len(agents)} agents</span>
        </div>
        <h1 class="page-title">Orchestration agents for <span class="accent">broader .NET routing</span></h1>
        <p class="page-lead">Agent pages sit above individual skills and help route architecture, review, modernization, AI, data, and build work into the right detailed guidance.</p>
      </section>
      {render_agent_listing_section(agents, root_prefix, {skill["name"]: skill for skill in skills}, show_index_link=False)}
    """.strip()


def render_credits_blocks(credits: list[dict]) -> str:
    """Render README credits as HTML tables."""
    blocks_html = []
    for block in credits:
        rows_html = "".join(
            "<tr>"
            f'<td data-label="Project">{render_inline_markdown(row[0])}</td>'
            f'<td data-label="Authors">{escape_html(row[1])}</td>'
            f'<td data-label="License">{escape_html(row[2])}</td>'
            "</tr>"
            for row in block["rows"]
        )
        blocks_html.append(
            f"""
              <div class="credits-block panel">
                <div class="section-header">
                  <div>
                    <h2>{escape_html(block['title'])}</h2>
                  </div>
                </div>
                <div class="credits-table-wrap">
                  <table class="credits-table">
                    <thead>
                      <tr>
                        <th>Project</th>
                        <th>Authors</th>
                        <th>License</th>
                      </tr>
                    </thead>
                    <tbody>
                      {rows_html}
                    </tbody>
                  </table>
                </div>
              </div>
            """.strip()
        )
    return "\n".join(blocks_html)


def render_about_page(
    skills: list[dict],
    packages: list[dict],
    agents: list[dict],
    category_infos: dict[str, dict],
    credits: list[dict],
    release_tag: str,
    root_prefix: str,
) -> str:
    """Render the about page with credits and release context."""
    hero = f"""
      {render_breadcrumb([("Home", root_prefix or "./"), ("About", None)])}
      <section class="page-hero">
        <div class="eyebrow-row">
          <span class="eyebrow">About</span>
          <span class="tag tag-accent">{escape_html(release_tag)}</span>
        </div>
        <h1 class="page-title">About the <span class="accent">dotnet-skills</span> catalog</h1>
        <p class="page-lead">A structured .NET catalog, not a marketplace. The site is organized around categories, skill pages, agent pages, and project credits.</p>
        <div class="metric-grid">
          {render_metric_card(str(len(skills)), "Skills")}
          {render_metric_card(str(len(packages)), "Packages")}
          {render_metric_card(str(len(agents)), "Agents")}
          {render_metric_card(str(len(category_infos)), "Categories")}
          {render_metric_card("MIT", "License")}
        </div>
      </section>
    """

    overview = f"""
      <div class="detail-layout">
        <div class="article-copy">
          <section>
            <h2>What this catalog is for</h2>
            <p>dotnet-skills packages modern .NET knowledge into installable skills and routing agents that can be consumed by Claude Code, GitHub Copilot, Gemini, Codex, and Junie. The public site mirrors that structure with dedicated pages that are easier to browse, share, and revisit.</p>
          </section>
          <section>
            <h2>How updates land</h2>
            <p>The catalog is published through a unified release workflow that produces GitHub releases, NuGet packages, and GitHub Pages output together. Upstream-watch automation monitors official releases and docs so the catalog can be refreshed when major framework guidance changes.</p>
          </section>
          <section>
            <h2>Why there are multiple page types now</h2>
            <p>Home still carries the full catalog and quick-view modal. Package pages provide one-command bundle installs, category pages group related work, skill pages expose real content from <code>SKILL.md</code>, agent pages explain routing behavior, and this about page captures the project context and credits.</p>
          </section>
        </div>
        <aside class="sidebar-stack">
          <div class="sidebar-card">
            <div class="detail-card-label">Explore</div>
            <div class="sidebar-links">
              <a class="pill-link" href="{escape_html(root_prefix + 'packages/')}">Packages</a>
              <a class="pill-link" href="{escape_html(root_prefix + 'categories/')}">Categories</a>
              <a class="pill-link" href="{escape_html(root_prefix + 'skills/')}">Skills</a>
              <a class="pill-link" href="{escape_html(root_prefix + 'agents/')}">Agents</a>
              <a class="pill-link" href="{escape_html(MANAGEDCODE_WEBSITE_URL)}" target="_blank" rel="noopener noreferrer">ManagedCode</a>
              <a class="pill-link" href="{escape_html(GITHUB_REPOSITORY_URL)}" target="_blank" rel="noopener noreferrer">GitHub</a>
            </div>
          </div>
          <div class="sidebar-card">
            <div class="detail-card-label">Contribute</div>
            <p>Want your project credited or your library represented? Add or improve a skill, then send a pull request against the catalog.</p>
            <div class="sidebar-links">
              <a class="pill-link" href="{escape_html(GITHUB_REPOSITORY_URL + '/blob/main/CONTRIBUTING.md')}" target="_blank" rel="noopener noreferrer">Contribution guide</a>
            </div>
          </div>
        </aside>
      </div>
    """

    return "\n".join([hero, overview, render_credits_blocks(credits)])


def build_breadcrumb_json_ld(site_url: str, items: list[tuple[str, str]]) -> dict:
    """Build schema.org breadcrumb structured data."""
    return {
        "@type": "BreadcrumbList",
        "itemListElement": [
            {
                "@type": "ListItem",
                "position": index,
                "name": label,
                "item": url,
            }
            for index, (label, url) in enumerate(items, start=1)
        ],
    }


def build_root_json_ld(site_url: str, canonical_url: str, release_version: str) -> list[dict]:
    """Build JSON-LD for the home page."""
    return [
        {
            "@context": "https://schema.org",
            "@type": "WebSite",
            "@id": f"{site_url}#website",
            "url": site_url,
            "name": "dotnet-skills",
            "description": "Shared .NET skill catalog for modern coding platforms",
            "publisher": {"@id": f"{site_url}#organization"},
            "potentialAction": {
                "@type": "SearchAction",
                "target": f"{site_url}skills/?q={{search_term_string}}",
                "query-input": "required name=search_term_string",
            },
            "inLanguage": "en-US",
        },
        {
            "@context": "https://schema.org",
            "@type": "Organization",
            "@id": f"{site_url}#organization",
            "name": "ManagedCode",
            "url": MANAGEDCODE_WEBSITE_URL,
            "logo": f"{site_url}assets/logo.svg",
            "sameAs": ["https://github.com/managedcode"],
        },
        {
            "@context": "https://schema.org",
            "@type": "CollectionPage",
            "@id": f"{canonical_url}#page",
            "url": canonical_url,
            "name": "dotnet-skills home",
            "description": "The home page for the dotnet-skills catalog, with package installs, the full .NET skill directory, and links to category, skill, agent, and about pages.",
            "isPartOf": {"@id": f"{site_url}#website"},
        },
        {
            "@context": "https://schema.org",
            "@type": "SoftwareApplication",
            "@id": f"{site_url}#cli",
            "name": "dotnet-skills",
            "applicationCategory": "DeveloperApplication",
            "operatingSystem": "Windows, macOS, Linux",
            "softwareVersion": release_version,
            "downloadUrl": NUGET_PACKAGE_URL,
            "offers": {"@type": "Offer", "price": "0", "priceCurrency": "USD"},
            "author": {"@id": f"{site_url}#organization"},
        },
        {
            "@context": "https://schema.org",
            "@type": "FAQPage",
            "mainEntity": [
                {
                    "@type": "Question",
                    "name": "What is dotnet-skills?",
                    "acceptedAnswer": {
                        "@type": "Answer",
                        "text": "dotnet-skills is a shared .NET skill catalog for coding assistants such as Claude Code, GitHub Copilot, Gemini, Codex, and Junie.",
                    },
                },
                {
                    "@type": "Question",
                    "name": "What changed on the public site?",
                    "acceptedAnswer": {
                        "@type": "Answer",
                        "text": "The public site now includes dedicated pages for packages, categories, skills, agents, and an about section so the catalog is easier to crawl and share.",
                    },
                },
            ],
        },
        build_breadcrumb_json_ld(site_url, [("Home", site_url)]),
    ]


def build_collection_json_ld(
    site_url: str,
    canonical_url: str,
    name: str,
    description: str,
    items: list[tuple[str, str]],
    breadcrumbs: list[tuple[str, str]],
) -> list[dict]:
    """Build JSON-LD for collection pages."""
    return [
        {
            "@context": "https://schema.org",
            "@type": "CollectionPage",
            "@id": f"{canonical_url}#page",
            "url": canonical_url,
            "name": name,
            "description": description,
            "isPartOf": {"@id": f"{site_url}#website"},
            "mainEntity": {
                "@type": "ItemList",
                "itemListElement": [
                    {
                        "@type": "ListItem",
                        "position": index,
                        "name": label,
                        "url": url,
                    }
                    for index, (label, url) in enumerate(items, start=1)
                ],
            },
        },
        build_breadcrumb_json_ld(site_url, breadcrumbs),
    ]


def build_article_json_ld(
    site_url: str,
    canonical_url: str,
    title: str,
    description: str,
    lastmod: str,
    breadcrumbs: list[tuple[str, str]],
) -> list[dict]:
    """Build JSON-LD for detail pages."""
    return [
        {
            "@context": "https://schema.org",
            "@type": "TechArticle",
            "@id": f"{canonical_url}#article",
            "headline": title,
            "description": description,
            "url": canonical_url,
            "dateModified": lastmod,
            "inLanguage": "en-US",
            "publisher": {"@id": f"{site_url}#organization"},
            "isPartOf": {"@id": f"{site_url}#website"},
        },
        build_breadcrumb_json_ld(site_url, breadcrumbs),
    ]


def build_about_json_ld(site_url: str, canonical_url: str, breadcrumbs: list[tuple[str, str]]) -> list[dict]:
    """Build JSON-LD for the about page."""
    return [
        {
            "@context": "https://schema.org",
            "@type": "AboutPage",
            "@id": f"{canonical_url}#page",
            "url": canonical_url,
            "name": "About dotnet-skills",
            "description": "About the dotnet-skills catalog, its credits, and how the public site is organized.",
            "isPartOf": {"@id": f"{site_url}#website"},
        },
        build_breadcrumb_json_ld(site_url, breadcrumbs),
    ]


def render_page(template: str, context: dict) -> str:
    """Render a page from the shared HTML template."""
    output = template
    for placeholder_key, placeholder in PLACEHOLDERS.items():
        replacement = context.get(placeholder_key, "")
        output = output.replace(placeholder, replacement)
    return output


def serialize_json_ld(items: list[dict]) -> str:
    """Serialize JSON-LD as a single graph document."""
    graph = [{key: value for key, value in item.items() if key != "@context"} for item in items]
    return json.dumps({"@context": "https://schema.org", "@graph": graph}, indent=2)


def render_sitemap(site_url: str, pages: list[dict]) -> str:
    """Render the sitemap for all generated pages."""
    entries = []
    for page in pages:
        entries.append(
            f"""  <url>
    <loc>{escape_html(page['url'])}</loc>
    <lastmod>{escape_html(page['lastmod'])}</lastmod>
    <changefreq>{escape_html(page['changefreq'])}</changefreq>
    <priority>{escape_html(page['priority'])}</priority>
  </url>"""
        )

    return (
        '<?xml version="1.0" encoding="UTF-8"?>\n'
        '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n'
        + "\n".join(entries)
        + "\n</urlset>\n"
    )


def render_robots(site_url: str) -> str:
    """Render robots.txt."""
    return f"""User-agent: *
Allow: /

Sitemap: {site_url}sitemap.xml
"""


def write_page(path: str, html_output: str) -> None:
    """Write a rendered page to disk."""
    output_path = output_file_for(path)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(html_output, encoding="utf-8")


def maybe_write_cname(site_url: str) -> None:
    """Emit a CNAME file when the site uses a custom domain."""
    host = urlparse(site_url).netloc
    if host and not host.endswith("github.io"):
        CNAME_PATH.write_text(host + "\n", encoding="utf-8")


def main() -> int:
    """Generate the public multi-page site."""
    if not CATALOG_PATH.exists():
        print(f"Error: Catalog not found at {CATALOG_PATH}", file=sys.stderr)
        return 1

    if not AGENTS_CATALOG_PATH.exists():
        print(f"Error: Agent catalog not found at {AGENTS_CATALOG_PATH}", file=sys.stderr)
        return 1

    if not TEMPLATE_PATH.exists():
        print(f"Error: Template not found at {TEMPLATE_PATH}", file=sys.stderr)
        return 1

    catalog = json.loads(read_text(CATALOG_PATH))
    agent_catalog = json.loads(read_text(AGENTS_CATALOG_PATH))

    site_url = normalize_site_url(os.environ.get("DOTNET_SKILLS_SITE_URL", DEFAULT_SITE_URL))
    release_version = resolve_release_version()
    release_tag = resolve_release_tag(release_version)
    release_url = resolve_release_url(release_tag)
    social_image_url = build_absolute_url(site_url, SOCIAL_IMAGE_PATH)

    if OUTPUT_DIR.exists():
        shutil.rmtree(OUTPUT_DIR)
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    skills = load_skill_documents(catalog.get("skills", []), site_url)
    skills_by_name = {skill["name"]: skill for skill in skills}
    packages = load_package_documents(catalog.get("packages", []), skills_by_name, site_url)
    agents = load_agent_documents(agent_catalog.get("agents", []), site_url)
    category_infos = build_category_infos(skills, agents)
    credits = parse_credits_from_readme()

    print(f"Loaded {len(skills)} skills from catalog")
    print(f"Loaded {len(packages)} packages from catalog")
    print(f"Loaded {len(agents)} agents from catalog")

    template = read_text(TEMPLATE_PATH)

    pages: list[dict] = []

    def add_page(
        *,
        path: str,
        title: str,
        description: str,
        keywords: list[str],
        body_class: str,
        main_content: str,
        json_ld: list[dict],
        lastmod: str,
        page_data: dict | None = None,
        og_type: str = "website",
        changefreq: str = "weekly",
        priority: str = "0.8",
        extra_head: str = "",
    ) -> None:
        root_prefix = relative_root_prefix(path)
        canonical_url = build_absolute_url(site_url, path)
        page_html = render_page(
            template,
            {
                "page_title": escape_html(title),
                "page_description": escape_html(description),
                "page_keywords": escape_html(", ".join(dedupe_strings(keywords))),
                "canonical_url": escape_html(canonical_url),
                "og_type": escape_html(og_type),
                "social_image": escape_html(social_image_url),
                "page_json_ld": serialize_json_ld(json_ld),
                "page_extra_head": extra_head,
                "body_class": escape_html(body_class),
                "root_prefix": root_prefix,
                "root_href": "./" if not root_prefix else root_prefix,
                "site_url": site_url,
                "release_tag": escape_html(release_tag),
                "release_url": escape_html(release_url),
                "page_main_content": main_content,
                "page_data": json.dumps(page_data or {}),
                "copyright": render_copyright_year_range(),
            },
        )
        write_page(path, page_html)
        pages.append(
            {
                "path": path,
                "url": canonical_url,
                "lastmod": lastmod,
                "changefreq": changefreq,
                "priority": priority,
            }
        )

    root_body, root_page_data = render_home_page(skills, packages, agents, category_infos, release_tag, "")
    add_page(
        path="",
        title=".NET Skills for Modern Coding Platforms | dotnet-skills",
        description="A shared .NET skill catalog for Claude Code, GitHub Copilot, Gemini, Codex, and Junie with package, category, skill, agent, and about pages.",
        keywords=["dotnet", ".NET skills", ".NET skill packages", "Claude Code", "GitHub Copilot", "Gemini", "Codex", "Junie", "AI coding assistants", "skill catalog"],
        body_class="page-home",
        main_content=root_body,
        json_ld=build_root_json_ld(site_url, site_url, release_version),
        lastmod=get_git_last_modified(["scripts/generate_pages.py", "github-pages", "catalog/skills.json", "catalog/agents.json"]),
        page_data=root_page_data,
        og_type="website",
        priority="1.0",
    )

    packages_body, packages_page_data = render_packages_index_page(packages, "../")
    add_page(
        path="packages/",
        title=".NET Skill Packages | dotnet-skills",
        description="Browse one-command .NET skill packages, including curated stacks and category-wide installs such as AI, Code Quality, and Orleans.",
        keywords=["dotnet skill packages", "install package ai", "install package orleans", "code quality package", "dotnet-skills packages"],
        body_class="page-packages",
        main_content=packages_body,
        json_ld=build_collection_json_ld(
            site_url,
            build_absolute_url(site_url, "packages/"),
            ".NET Skill Packages",
            "One-command skill packages for the dotnet-skills catalog",
            [(package["title"], package["detail_url"]) for package in packages],
            [("Home", site_url), ("Packages", build_absolute_url(site_url, "packages/"))],
        ),
        lastmod=get_git_last_modified(["scripts/generate_pages.py", "catalog/skills.json"]),
        page_data=packages_page_data,
        og_type="website",
        priority="0.94",
    )

    skills_body, skills_page_data = render_skills_index_page(skills, category_infos, "../")
    add_page(
        path="skills/",
        title=".NET Skills Directory | dotnet-skills",
        description="Search the full .NET skill directory, filter by category, and open dedicated pages for each catalog skill.",
        keywords=["dotnet skill directory", "ASP.NET Core skill", "Orleans skill", "Aspire skill", "testing skill", "category pages"],
        body_class="page-skills",
        main_content=skills_body,
        json_ld=build_collection_json_ld(
            site_url,
            build_absolute_url(site_url, "skills/"),
            ".NET Skills Directory",
            "Search the full .NET skill directory",
            [(skill["title"], skill["detail_url"]) for skill in skills],
            [("Home", site_url), ("Skills", build_absolute_url(site_url, "skills/"))],
        ),
        lastmod=get_git_last_modified(["scripts/generate_pages.py", "github-pages", "catalog/skills.json"]),
        page_data=skills_page_data,
        og_type="website",
        priority="0.95",
    )

    categories_body = render_categories_index_page(category_infos, "../")
    add_page(
        path="categories/",
        title=".NET Skill Categories | dotnet-skills",
        description="Browse the catalog by category, from AI and cloud to testing, distributed systems, legacy maintenance, and code quality.",
        keywords=[".NET categories", "AI skills", "testing skills", "distributed .NET", "catalog categories"],
        body_class="page-categories",
        main_content=categories_body,
        json_ld=build_collection_json_ld(
            site_url,
            build_absolute_url(site_url, "categories/"),
            ".NET Skill Categories",
            "Category landing pages for the dotnet-skills catalog",
            [
                (category_name, build_absolute_url(site_url, f"categories/{category_info['slug']}/"))
                for category_name, category_info in sorted(category_infos.items())
            ],
            [("Home", site_url), ("Categories", build_absolute_url(site_url, "categories/"))],
        ),
        lastmod=get_git_last_modified(["scripts/generate_pages.py", "github-pages", "catalog/skills.json"]),
        page_data={"querySyncPath": "categories/"},
        og_type="website",
        priority="0.9",
    )

    agents_body = render_agents_index_page(agents, skills, "../")
    add_page(
        path="agents/",
        title=".NET Orchestration Agents | dotnet-skills",
        description="Browse orchestration agents that route work into the right .NET skills for AI, data, modernization, build, and architecture tasks.",
        keywords=[".NET agents", "orchestration agents", "AI routing", "data routing", "modernization agents"],
        body_class="page-agents",
        main_content=agents_body,
        json_ld=build_collection_json_ld(
            site_url,
            build_absolute_url(site_url, "agents/"),
            ".NET Orchestration Agents",
            "Orchestration agents that sit above the skill catalog",
            [(agent["title"], agent["detail_url"]) for agent in agents],
            [("Home", site_url), ("Agents", build_absolute_url(site_url, "agents/"))],
        ),
        lastmod=get_git_last_modified(["scripts/generate_pages.py", "github-pages", "catalog/agents.json"]),
        page_data={},
        og_type="website",
        priority="0.88",
    )

    about_body = render_about_page(skills, packages, agents, category_infos, credits, release_tag, "../")
    add_page(
        path="about/",
        title="About dotnet-skills | Credits and Catalog Structure",
        description="Learn what dotnet-skills is, how the catalog is published, and which open source projects are credited on the public site.",
        keywords=["about dotnet-skills", "catalog credits", "ManagedCode", "open source .NET catalog"],
        body_class="page-about",
        main_content=about_body,
        json_ld=build_about_json_ld(
            site_url,
            build_absolute_url(site_url, "about/"),
            [("Home", site_url), ("About", build_absolute_url(site_url, "about/"))],
        ),
        lastmod=get_git_last_modified(["README.md", "scripts/generate_pages.py", "github-pages"]),
        page_data={},
        og_type="article",
        priority="0.82",
    )

    for package in packages:
        root_prefix = "../../"
        package_body = render_package_detail_page(package, root_prefix)
        add_page(
            path=package["detail_path"],
            title=f"{package['title']} | dotnet-skills",
            description=trim_text(package["description"], 156),
            keywords=["dotnet package", package["title"], package["name"], *[skill["short_name"] for skill in package["skills"][:4]]],
            body_class="page-packages",
            main_content=package_body,
            json_ld=build_article_json_ld(
                site_url,
                package["detail_url"],
                package["title"],
                package["description"],
                package["lastmod"],
                [
                    ("Home", site_url),
                    ("Packages", build_absolute_url(site_url, "packages/")),
                    (package["title"], package["detail_url"]),
                ],
            ),
            lastmod=package["lastmod"],
            page_data={"skills": build_skill_payload(package["skills"], root_prefix)},
            og_type="article",
            priority="0.8",
        )

    for category_name, category_info in sorted(category_infos.items()):
        root_prefix = "../../"
        category_body, category_page_data = render_category_detail_page(category_name, category_info, root_prefix)
        category_url = build_absolute_url(site_url, f"categories/{category_info['slug']}/")
        add_page(
            path=f"categories/{category_info['slug']}/",
            title=f"{category_name} .NET Skills | dotnet-skills",
            description=f"Browse {len(category_info['skills'])} {category_name} skills in the dotnet-skills catalog, plus related orchestration agents and dedicated skill pages.",
            keywords=[".NET " + category_name, category_name + " skills", "dotnet-skills", *[skill["short_name"] for skill in category_info["skills"][:4]]],
            body_class="page-categories",
            main_content=category_body,
            json_ld=build_collection_json_ld(
                site_url,
                category_url,
                f"{category_name} .NET Skills",
                category_info["description"],
                [(skill["title"], skill["detail_url"]) for skill in category_info["skills"]],
                [
                    ("Home", site_url),
                    ("Categories", build_absolute_url(site_url, "categories/")),
                    (category_name, category_url),
                ],
            ),
            lastmod=get_git_last_modified(category_info["lastmod_paths"] + ["scripts/generate_pages.py"]),
            page_data=category_page_data,
            og_type="website",
            priority="0.84",
        )

    for skill in skills:
        related_skills = [
            candidate for candidate in category_infos[skill["category"]]["skills"] if candidate["name"] != skill["name"]
        ]
        related_agents = [
            agent for agent in agents if skill["name"] in set(agent.get("skills", []))
        ]
        root_prefix = "../../"
        skill_body = render_skill_detail_page(skill, related_skills, related_agents, root_prefix)
        add_page(
            path=skill["detail_path"],
            title=f"{skill['title']} skill | dotnet-skills",
            description=trim_text(skill["description"], 156),
            keywords=["dotnet skill", skill["title"], skill["category"], skill["short_name"], ".NET"],
            body_class="page-skills",
            main_content=skill_body,
            json_ld=build_article_json_ld(
                site_url,
                skill["detail_url"],
                f"{skill['title']} skill",
                skill["description"],
                skill["lastmod"],
                [
                    ("Home", site_url),
                    ("Skills", build_absolute_url(site_url, "skills/")),
                    (skill["title"], skill["detail_url"]),
                ],
            ),
            lastmod=skill["lastmod"],
            page_data={},
            og_type="article",
            priority="0.76",
        )

    for agent in agents:
        linked_skills = [skills_by_name[name] for name in agent.get("skills", []) if name in skills_by_name]
        root_prefix = "../../"
        agent_body = render_agent_detail_page(agent, linked_skills, root_prefix)
        add_page(
            path=agent["detail_path"],
            title=f"{agent['title']} agent | dotnet-skills",
            description=trim_text(agent["description"], 156),
            keywords=["dotnet agent", agent["title"], "orchestration agent", *[skill["short_name"] for skill in linked_skills[:4]]],
            body_class="page-agents",
            main_content=agent_body,
            json_ld=build_article_json_ld(
                site_url,
                agent["detail_url"],
                f"{agent['title']} agent",
                agent["description"],
                agent["lastmod"],
                [
                    ("Home", site_url),
                    ("Agents", build_absolute_url(site_url, "agents/")),
                    (agent["title"], agent["detail_url"]),
                ],
            ),
            lastmod=agent["lastmod"],
            page_data={},
            og_type="article",
            priority="0.72",
        )

    assets_dir = REPO_ROOT / "github-pages" / "assets"
    output_assets = OUTPUT_DIR / "assets"
    if output_assets.exists():
        shutil.rmtree(output_assets)
    shutil.copytree(assets_dir, output_assets)
    print(f"Copied assets to {output_assets}")

    SITEMAP_PATH.write_text(render_sitemap(site_url, pages), encoding="utf-8")
    print(f"Generated {SITEMAP_PATH}")

    ROBOTS_PATH.write_text(render_robots(site_url), encoding="utf-8")
    print(f"Generated {ROBOTS_PATH}")

    maybe_write_cname(site_url)
    if CNAME_PATH.exists():
        print(f"Generated {CNAME_PATH}")

    for page in pages:
        print(f"Generated {output_file_for(page['path'])}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
