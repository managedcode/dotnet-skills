#!/usr/bin/env python3
"""Generate GitHub Pages site with skills data from the catalog."""

import json
import os
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
CATALOG_PATH = REPO_ROOT / "catalog" / "skills.json"
TEMPLATE_PATH = REPO_ROOT / "github-pages" / "index.html"
OUTPUT_DIR = REPO_ROOT / "artifacts" / "github-pages"
OUTPUT_PATH = OUTPUT_DIR / "index.html"

PLACEHOLDER = "SKILLS_DATA_PLACEHOLDER"


def main() -> int:
    """Generate the GitHub Pages site with embedded skills data."""

    # Check required files exist
    if not CATALOG_PATH.exists():
        print(f"Error: Catalog not found at {CATALOG_PATH}", file=sys.stderr)
        print("Run 'python3 scripts/generate_catalog.py' first.", file=sys.stderr)
        return 1

    if not TEMPLATE_PATH.exists():
        print(f"Error: Template not found at {TEMPLATE_PATH}", file=sys.stderr)
        return 1

    # Load catalog
    with open(CATALOG_PATH, "r", encoding="utf-8") as f:
        catalog = json.load(f)

    skills = catalog.get("skills", [])
    print(f"Loaded {len(skills)} skills from catalog")

    # Load template
    with open(TEMPLATE_PATH, "r", encoding="utf-8") as f:
        template = f.read()

    if PLACEHOLDER not in template:
        print(f"Error: Placeholder '{PLACEHOLDER}' not found in template", file=sys.stderr)
        return 1

    # Create skills JSON for embedding
    skills_json = json.dumps(skills, indent=2)

    # Replace placeholder
    output_html = template.replace(PLACEHOLDER, skills_json)

    # Write output
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    with open(OUTPUT_PATH, "w", encoding="utf-8") as f:
        f.write(output_html)

    print(f"Generated {OUTPUT_PATH}")

    # Copy any additional assets if they exist
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
