#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

from catalog_index import build_bundles, build_skill_manifest, collect_skills, resolve_category_order


ROOT = Path(__file__).resolve().parents[1]
README_PATH = ROOT / "README.md"

BEGIN_MARKER = "<!-- BEGIN GENERATED CATALOG -->"
END_MARKER = "<!-- END GENERATED CATALOG -->"
README_SKILLS_BADGE_LINE_PATTERN = re.compile(
    r"(?m)^\[!\[Skills\]\(https://img\.shields\.io/badge/skills-[^\n]+-blue\)\]\(#catalog\)\n?"
)
README_SKILLS_BADGE_LINE_TEMPLATE = "[![Skills](https://img.shields.io/badge/skills-growing-blue)](#catalog)\n"
README_SKILLS_INTRO_LINE_PATTERN = re.compile(
    r"(?m)^This catalog fixes that\..*\n?"
)
README_SKILLS_INTRO_LINE_TEMPLATE = (
    "This catalog fixes that. A growing catalog covering the entire .NET ecosystem"
    "—from ASP.NET Core to Orleans, from MAUI to Semantic Kernel. Install them once, and your AI agent"
    " actually knows modern .NET.\n"
)

def render_catalog(skills: list[dict[str, str]]) -> str:
    category_order = resolve_category_order(skills)
    grouped: dict[str, list[dict[str, str]]] = {category: [] for category in category_order}
    for skill in skills:
        grouped[skill["category"]].append(skill)

    for category in grouped:
        grouped[category].sort(key=lambda item: item["name"])

    lines: list[str] = [BEGIN_MARKER, "", f"This catalog currently contains **{len(skills)}** skills.", ""]

    for category in category_order:
        items = grouped[category]
        if not items:
            continue

        lines.extend(
            [
                f"### {category}",
                "",
                "| Skill | Version | Description |",
                "|-------|---------|-------------|",
            ]
        )

        for item in items:
            skill_name = item["name"]
            version = item["version"]
            # Escape pipes in description for markdown table
            description = item["description"].replace("|", "\\|")
            path = item["path"]

            lines.append(f"| [`{skill_name}`]({path}) | `{version}` | {description} |")

        lines.append("")

    lines.append(END_MARKER)
    return "\n".join(lines)


def normalize_repeated_generated_line(
    readme: str,
    pattern: re.Pattern[str],
    replacement_line: str,
    label: str,
) -> str:
    matches = list(pattern.finditer(readme))
    if not matches:
        raise ValueError(f"README.md is missing the generated {label} pattern")

    first_start = matches[0].start()
    without_duplicates = pattern.sub("", readme)
    return without_duplicates[:first_start] + replacement_line + without_duplicates[first_start:]


def apply_readme_count_metadata(readme: str, skill_count: int) -> str:
    badge_line = README_SKILLS_BADGE_LINE_TEMPLATE
    badge_normalized = normalize_repeated_generated_line(
        readme,
        README_SKILLS_BADGE_LINE_PATTERN,
        badge_line,
        "skills badge",
    )

    intro_line = README_SKILLS_INTRO_LINE_TEMPLATE
    return normalize_repeated_generated_line(
        badge_normalized,
        README_SKILLS_INTRO_LINE_PATTERN,
        intro_line,
        "intro skill count",
    )


def render_readme(readme: str, rendered_catalog: str, skill_count: int) -> str:
    updated = apply_readme_count_metadata(readme, skill_count)
    pattern = re.compile(
        rf"{re.escape(BEGIN_MARKER)}.*?{re.escape(END_MARKER)}",
        flags=re.DOTALL,
    )
    if not pattern.search(updated):
        raise ValueError("README.md is missing generated catalog markers")

    return pattern.sub(rendered_catalog, updated)


def update_readme(rendered_catalog: str, skill_count: int) -> bool:
    readme = README_PATH.read_text()
    updated = render_readme(readme, rendered_catalog, skill_count)
    changed = updated != readme
    README_PATH.write_text(updated)
    return changed


def check_readme(rendered_catalog: str, skill_count: int) -> bool:
    readme = README_PATH.read_text()
    return render_readme(readme, rendered_catalog, skill_count) == readme


def write_manifest_to_path(path: Path, skills: list[dict[str, object]], bundles: list[dict[str, object]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(build_skill_manifest(skills, bundles), indent=2, sort_keys=False) + "\n", encoding="utf-8")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Scan the catalog tree, validate metadata, and update the generated README catalog.")
    parser.add_argument("--check", action="store_true", help="Fail if generated files are out of date.")
    parser.add_argument(
        "--validate-only",
        action="store_true",
        help="Validate catalog metadata and rendering without writing generated files.",
    )
    parser.add_argument(
        "--manifest-output",
        type=Path,
        help="Export a transient machine-readable manifest to a custom path without mutating README.md.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if sum(1 for value in [args.check, args.validate_only, args.manifest_output is not None] if value) > 1:
        print("--check, --validate-only, and --manifest-output are mutually exclusive.", file=sys.stderr)
        return 2

    skills = collect_skills(include_token_counts=True)
    bundles = build_bundles(skills)
    rendered_catalog = render_catalog(skills)

    if args.manifest_output is not None:
        write_manifest_to_path(args.manifest_output, skills, bundles)
        print(f"Wrote manifest to {args.manifest_output}")
        return 0

    if args.validate_only:
        print(f"Catalog metadata is valid for {len(skills)} skills and {len(bundles)} bundles.")
        return 0

    if args.check:
        readme_ok = check_readme(rendered_catalog, len(skills))
        if not readme_ok:
            print("README.md catalog section is out of date.", file=sys.stderr)
            return 1
        print("Catalog is up to date.")
        return 0

    update_readme(rendered_catalog, len(skills))
    print(f"Generated README catalog for {len(skills)} skills and {len(bundles)} bundles.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
