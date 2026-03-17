#!/usr/bin/env python3
"""Generate GitHub Pages site with skills and agents data from the catalog."""

from datetime import date
import html
import json
import os
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
CATALOG_PATH = REPO_ROOT / "catalog" / "skills.json"
AGENTS_CATALOG_PATH = REPO_ROOT / "catalog" / "agents.json"
TEMPLATE_PATH = REPO_ROOT / "github-pages" / "index.html"
OUTPUT_DIR = REPO_ROOT / "artifacts" / "github-pages"
OUTPUT_PATH = OUTPUT_DIR / "index.html"
SITEMAP_PATH = OUTPUT_DIR / "sitemap.xml"
ROBOTS_PATH = OUTPUT_DIR / "robots.txt"

SKILLS_DATA_PLACEHOLDER = "SKILLS_DATA_PLACEHOLDER"
SKILLS_GRID_PLACEHOLDER = "<!-- SKILLS_GRID_PLACEHOLDER -->"
AGENTS_GRID_PLACEHOLDER = "<!-- AGENTS_GRID_PLACEHOLDER -->"
CATEGORY_TABS_PLACEHOLDER = "<!-- CATEGORY_TABS_PLACEHOLDER -->"
COPYRIGHT_YEAR_RANGE_PLACEHOLDER = "COPYRIGHT_YEAR_RANGE_PLACEHOLDER"
SITE_URL_PLACEHOLDER = "SITE_URL_PLACEHOLDER"
CATALOG_VERSION_PLACEHOLDER = "CATALOG_VERSION_PLACEHOLDER"
COPYRIGHT_START_YEAR = 2024
DEFAULT_SITE_URL = "https://managedcode.github.io/dotnet-skills/"


def escape_html(text: str) -> str:
    """Escape HTML special characters."""
    return html.escape(text, quote=True)


def render_skill_card(skill: dict) -> str:
    """Render a single skill card HTML."""
    name = escape_html(skill["name"])
    title = escape_html(skill.get("title", skill["name"]))
    version = escape_html(skill["version"])
    description = escape_html(skill["description"])
    category = escape_html(skill["category"])
    short_name = name.replace("dotnet-", "")

    return f'''<div class="skill-card" data-category="{category}" data-skill="{name}" onclick="openSkillModal('{name}')">
          <div class="skill-header">
            <span class="skill-name">{title}</span>
            <span class="skill-version">v{version}</span>
          </div>
          <p class="skill-description">{description}</p>
          <div class="skill-footer">
            <span class="skill-category">{category}</span>
            <span class="skill-install-cmd">dotnet skills install {short_name}</span>
          </div>
        </div>'''


def render_skills_grid(skills: list) -> str:
    """Render all skill cards as HTML."""
    cards = [render_skill_card(skill) for skill in skills]
    return "\n        ".join(cards)


def render_agent_card(agent: dict) -> str:
    """Render a single orchestration agent card HTML."""
    name = escape_html(agent["name"])
    title = escape_html(agent.get("title", agent["name"]))
    description = escape_html(agent["description"])
    short_name = name.replace("dotnet-", "")
    install_command = f"dotnet skills agent install {short_name}"
    linked_skills = [escape_html(skill.replace("dotnet-", "")) for skill in agent.get("skills", [])[:4]]
    source_path = escape_html(agent.get("path", "agents/"))

    if linked_skills:
        skills_html = "".join(f'<span class="agent-skill-chip">{skill}</span>' for skill in linked_skills)
    else:
        skills_html = '<span class="agent-skill-chip">no linked skills</span>'

    extra_skills_count = max(0, len(agent.get("skills", [])) - len(linked_skills))
    extra_skills_label = f"+{extra_skills_count} more" if extra_skills_count > 0 else ""
    extra_skills_badge = (
        f'<span class="agent-meta-pill">{escape_html(extra_skills_label)}</span>'
        if extra_skills_label
        else ""
    )

    return f'''<div class="agent-card">
          <div class="agent-card-top">
            <div>
              <h3>{title}</h3>
              <p>{description}</p>
            </div>
            <span class="agent-meta-pill">{len(agent.get("skills", []))} linked skills</span>
          </div>
          <div class="agent-skill-list">
            {skills_html}
          </div>
          <div class="agent-card-footer">
            <code class="agent-command">{escape_html(install_command)}</code>
            <div class="agent-card-links">
              {extra_skills_badge}
              <a href="https://github.com/managedcode/dotnet-skills/tree/main/{source_path}" target="_blank" rel="noopener noreferrer">Source</a>
            </div>
          </div>
        </div>'''


def render_agents_grid(agents: list) -> str:
    """Render all orchestration agent cards as HTML."""
    if not agents:
        return """<div class="agent-card">
          <div class="agent-card-top">
            <div>
              <h3>Agent catalog unavailable</h3>
              <p>No orchestration agents were found in the current catalog build.</p>
            </div>
          </div>
        </div>"""

    cards = [render_agent_card(agent) for agent in agents]
    return "\n        ".join(cards)


def render_category_tabs(skills: list) -> str:
    """Render category filter tabs as HTML."""
    categories = {}
    for skill in skills:
        cat = skill["category"]
        categories[cat] = categories.get(cat, 0) + 1

    sorted_categories = sorted(categories.keys())
    tabs = []
    for cat in sorted_categories:
        count = categories[cat]
        escaped_cat = escape_html(cat)
        tabs.append(f'<button class="filter-tab" data-category="{escaped_cat}">{escaped_cat} <span class="count">{count}</span></button>')

    return "\n        ".join(tabs)


def render_copyright_year_range() -> str:
    """Render a stable copyright year or year range for the site footer."""
    current_year = date.today().year
    if current_year <= COPYRIGHT_START_YEAR:
        return str(COPYRIGHT_START_YEAR)

    return f"{COPYRIGHT_START_YEAR}-{current_year}"


def normalize_site_url(raw_url: str) -> str:
    """Normalize the public site URL for canonical and sitemap output."""
    return raw_url.rstrip("/") + "/"


def get_git_last_modified(path: str) -> str:
    """Get the last commit date for a file or directory from git."""
    try:
        result = subprocess.run(
            ["git", "log", "-1", "--date=short", "--format=%cd", "--", path],
            capture_output=True,
            text=True,
            cwd=REPO_ROOT,
            timeout=5
        )
        if result.returncode == 0 and result.stdout.strip():
            return result.stdout.strip()
    except (subprocess.TimeoutExpired, FileNotFoundError):
        pass
    return date.today().isoformat()


def get_git_dates_for_skills(skills: list) -> dict:
    """Get git last modified dates for all skills."""
    dates = {}
    for skill in skills:
        skill_path = skill.get("path", f"skills/{skill['name']}")
        dates[skill["name"]] = get_git_last_modified(skill_path)
    return dates


def get_git_dates_for_agents(agents: list) -> dict:
    """Get git last modified dates for all agents."""
    dates = {}
    for agent in agents:
        agent_path = agent.get("path", f"agents/{agent['name']}")
        dates[agent["name"]] = get_git_last_modified(agent_path)
    return dates


def get_main_page_date() -> str:
    """Get the last commit date for the main page template."""
    return get_git_last_modified("github-pages/index.html")


def render_sitemap(
    site_url: str,
    skills: list,
    agents: list,
    skill_dates: dict,
    agent_dates: dict,
    main_page_date: str
) -> str:
    """Render an enhanced sitemap for the GitHub Pages site with skill and agent anchors.

    Uses actual git commit dates for lastmod instead of today's date.
    """
    # Build skill URLs with fragment identifiers and git dates
    skill_entries = []
    for skill in skills:
        name = html.escape(skill["name"])
        lastmod = skill_dates.get(skill["name"], main_page_date)
        skill_entries.append(f"""  <url>
    <loc>{site_url}#skill-{name}</loc>
    <lastmod>{lastmod}</lastmod>
    <changefreq>weekly</changefreq>
    <priority>0.7</priority>
  </url>""")

    # Build agent URLs with fragment identifiers and git dates
    agent_entries = []
    for agent in agents:
        name = html.escape(agent["name"])
        lastmod = agent_dates.get(agent["name"], main_page_date)
        agent_entries.append(f"""  <url>
    <loc>{site_url}#agent-{name}</loc>
    <lastmod>{lastmod}</lastmod>
    <changefreq>weekly</changefreq>
    <priority>0.6</priority>
  </url>""")

    # Build category URLs - use newest skill date in each category
    category_dates = {}
    for skill in skills:
        cat = skill["category"]
        skill_date = skill_dates.get(skill["name"], main_page_date)
        if cat not in category_dates or skill_date > category_dates[cat]:
            category_dates[cat] = skill_date

    categories = sorted(set(s["category"] for s in skills))
    category_entries = []
    for cat in categories:
        escaped_cat = html.escape(cat).replace(" ", "-").lower()
        lastmod = category_dates.get(cat, main_page_date)
        category_entries.append(f"""  <url>
    <loc>{site_url}#category-{escaped_cat}</loc>
    <lastmod>{lastmod}</lastmod>
    <changefreq>weekly</changefreq>
    <priority>0.8</priority>
  </url>""")

    all_entries = "\n".join(skill_entries + agent_entries + category_entries)

    return f"""<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
  <url>
    <loc>{site_url}</loc>
    <lastmod>{main_page_date}</lastmod>
    <changefreq>weekly</changefreq>
    <priority>1.0</priority>
  </url>
  <url>
    <loc>{site_url}#about</loc>
    <lastmod>{main_page_date}</lastmod>
    <changefreq>monthly</changefreq>
    <priority>0.9</priority>
  </url>
  <url>
    <loc>{site_url}#agents</loc>
    <lastmod>{main_page_date}</lastmod>
    <changefreq>weekly</changefreq>
    <priority>0.9</priority>
  </url>
  <url>
    <loc>{site_url}#catalog</loc>
    <lastmod>{main_page_date}</lastmod>
    <changefreq>weekly</changefreq>
    <priority>0.9</priority>
  </url>
{all_entries}
</urlset>
"""


def render_robots(site_url: str) -> str:
    """Render robots.txt that points crawlers at the sitemap."""
    return f"""User-agent: *
Allow: /

Sitemap: {site_url}sitemap.xml
"""


def main() -> int:
    """Generate the GitHub Pages site with embedded skills data."""

    if not CATALOG_PATH.exists():
        print(f"Error: Catalog not found at {CATALOG_PATH}", file=sys.stderr)
        print("Run 'python3 scripts/generate_catalog.py' first.", file=sys.stderr)
        return 1

    if not AGENTS_CATALOG_PATH.exists():
        print(f"Error: Agent catalog not found at {AGENTS_CATALOG_PATH}", file=sys.stderr)
        print("Run 'python3 scripts/generate_agent_catalog.py' first.", file=sys.stderr)
        return 1

    if not TEMPLATE_PATH.exists():
        print(f"Error: Template not found at {TEMPLATE_PATH}", file=sys.stderr)
        return 1

    with open(CATALOG_PATH, "r", encoding="utf-8") as f:
        catalog = json.load(f)

    skills = catalog.get("skills", [])
    print(f"Loaded {len(skills)} skills from catalog")

    with open(AGENTS_CATALOG_PATH, "r", encoding="utf-8") as f:
        agent_catalog = json.load(f)

    agents = agent_catalog.get("agents", [])
    print(f"Loaded {len(agents)} agents from catalog")

    with open(TEMPLATE_PATH, "r", encoding="utf-8") as f:
        template = f.read()

    site_url = normalize_site_url(os.environ.get("DOTNET_SKILLS_SITE_URL", DEFAULT_SITE_URL))

    # Generate HTML components
    skills_json = json.dumps(skills, indent=2)
    skills_grid_html = render_skills_grid(skills)
    agents_grid_html = render_agents_grid(agents)
    category_tabs_html = render_category_tabs(skills)

    # Replace placeholders
    output_html = template
    output_html = output_html.replace(SKILLS_DATA_PLACEHOLDER, skills_json)
    output_html = output_html.replace(SKILLS_GRID_PLACEHOLDER, skills_grid_html)
    output_html = output_html.replace(AGENTS_GRID_PLACEHOLDER, agents_grid_html)
    output_html = output_html.replace(CATEGORY_TABS_PLACEHOLDER, category_tabs_html)
    output_html = output_html.replace(COPYRIGHT_YEAR_RANGE_PLACEHOLDER, render_copyright_year_range())
    output_html = output_html.replace(SITE_URL_PLACEHOLDER, site_url)
    catalog_version = catalog.get("version", "1.0.0")
    output_html = output_html.replace(CATALOG_VERSION_PLACEHOLDER, catalog_version)

    # Update counts
    output_html = output_html.replace(
        'id="skill-count">62',
        f'id="skill-count">{len(skills)}'
    )
    output_html = output_html.replace(
        'id="count-all">62',
        f'id="count-all">{len(skills)}'
    )
    output_html = output_html.replace(
        'id="agent-count">6',
        f'id="agent-count">{len(agents)}'
    )
    categories_count = len(set(s["category"] for s in skills))
    output_html = output_html.replace(
        'id="category-count">14',
        f'id="category-count">{categories_count}'
    )

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    with open(OUTPUT_PATH, "w", encoding="utf-8") as f:
        f.write(output_html)

    print(f"Generated {OUTPUT_PATH}")

    # Get git dates for sitemap
    print("Fetching git dates for skills and agents...")
    skill_dates = get_git_dates_for_skills(skills)
    agent_dates = get_git_dates_for_agents(agents)
    main_page_date = get_main_page_date()

    with open(SITEMAP_PATH, "w", encoding="utf-8") as f:
        f.write(render_sitemap(site_url, skills, agents, skill_dates, agent_dates, main_page_date))

    print(f"Generated {SITEMAP_PATH}")

    with open(ROBOTS_PATH, "w", encoding="utf-8") as f:
        f.write(render_robots(site_url))

    print(f"Generated {ROBOTS_PATH}")

    assets_dir = REPO_ROOT / "github-pages" / "assets"
    if assets_dir.exists():
        import shutil
        output_assets = OUTPUT_DIR / "assets"
        if output_assets.exists():
            shutil.rmtree(output_assets)
        shutil.copytree(assets_dir, output_assets)
        print(f"Copied assets to {output_assets}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
