#!/usr/bin/env python3
"""Generate GitHub Pages site with skills data from the catalog."""

from datetime import date
import html
import json
import os
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
CATALOG_PATH = REPO_ROOT / "catalog" / "skills.json"
TEMPLATE_PATH = REPO_ROOT / "github-pages" / "index.html"
OUTPUT_DIR = REPO_ROOT / "artifacts" / "github-pages"
OUTPUT_PATH = OUTPUT_DIR / "index.html"
SITEMAP_PATH = OUTPUT_DIR / "sitemap.xml"
ROBOTS_PATH = OUTPUT_DIR / "robots.txt"

SKILLS_DATA_PLACEHOLDER = "SKILLS_DATA_PLACEHOLDER"
SKILLS_GRID_PLACEHOLDER = "<!-- SKILLS_GRID_PLACEHOLDER -->"
CATEGORY_TABS_PLACEHOLDER = "<!-- CATEGORY_TABS_PLACEHOLDER -->"
COPYRIGHT_YEAR_RANGE_PLACEHOLDER = "COPYRIGHT_YEAR_RANGE_PLACEHOLDER"
SITE_URL_PLACEHOLDER = "SITE_URL_PLACEHOLDER"
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


def render_sitemap(site_url: str) -> str:
    """Render a minimal sitemap for the GitHub Pages site."""
    today = date.today().isoformat()
    return f"""<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
  <url>
    <loc>{site_url}</loc>
    <lastmod>{today}</lastmod>
    <changefreq>weekly</changefreq>
    <priority>1.0</priority>
  </url>
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

    if not TEMPLATE_PATH.exists():
        print(f"Error: Template not found at {TEMPLATE_PATH}", file=sys.stderr)
        return 1

    with open(CATALOG_PATH, "r", encoding="utf-8") as f:
        catalog = json.load(f)

    skills = catalog.get("skills", [])
    print(f"Loaded {len(skills)} skills from catalog")

    with open(TEMPLATE_PATH, "r", encoding="utf-8") as f:
        template = f.read()

    site_url = normalize_site_url(os.environ.get("DOTNET_SKILLS_SITE_URL", DEFAULT_SITE_URL))

    # Generate HTML components
    skills_json = json.dumps(skills, indent=2)
    skills_grid_html = render_skills_grid(skills)
    category_tabs_html = render_category_tabs(skills)

    # Replace placeholders
    output_html = template
    output_html = output_html.replace(SKILLS_DATA_PLACEHOLDER, skills_json)
    output_html = output_html.replace(SKILLS_GRID_PLACEHOLDER, skills_grid_html)
    output_html = output_html.replace(CATEGORY_TABS_PLACEHOLDER, category_tabs_html)
    output_html = output_html.replace(COPYRIGHT_YEAR_RANGE_PLACEHOLDER, render_copyright_year_range())
    output_html = output_html.replace(SITE_URL_PLACEHOLDER, site_url)

    # Update counts
    output_html = output_html.replace(
        'id="skill-count">62',
        f'id="skill-count">{len(skills)}'
    )
    output_html = output_html.replace(
        'id="count-all">62',
        f'id="count-all">{len(skills)}'
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

    with open(SITEMAP_PATH, "w", encoding="utf-8") as f:
        f.write(render_sitemap(site_url))

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
