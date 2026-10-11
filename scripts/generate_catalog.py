#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

from catalog_index import ROOT, build_bundles, build_skill_manifest, catalog_statistics, collect_skills


def render_readme_statistics(readme: str, skills: list[dict]) -> str:
    """Refresh the canonical Skills badge and bounded catalog summary only."""
    stats = catalog_statistics(skills)
    badge = f"[![Skills](https://img.shields.io/badge/skills-{stats['skills']}-blue)](https://skills.managed-code.com/skills/)"
    updated, badge_count = re.subn(
        r"(?m)^\[!\[Skills\]\(https://img\.shields\.io/badge/skills-[^\n]+$",
        lambda _: badge,
        readme,
    )
    summary = (
        "<!-- BEGIN CATALOG SUMMARY -->\n"
        f"This catalog fixes that. **{stats['skills']} skills make dotnet-skills one of the largest .NET-focused skill collections for AI coding assistants**, "
        f"with explicit mappings for **{stats['nuget_packages']} NuGet packages**. "
        "It covers the ecosystem—from ASP.NET Core to Orleans, from MAUI to Semantic Kernel. "
        "Install them once, and your AI agent actually knows modern .NET.\n"
        "<!-- END CATALOG SUMMARY -->"
    )
    updated, summary_count = re.subn(
        r"<!-- BEGIN CATALOG SUMMARY -->.*?<!-- END CATALOG SUMMARY -->",
        lambda _: summary,
        updated,
        flags=re.DOTALL,
    )
    if badge_count != 1 or summary_count != 1:
        raise ValueError("README must contain exactly one Skills badge and one CATALOG SUMMARY block")
    return updated


def write_manifest_to_path(path: Path, skills: list[dict[str, object]], bundles: list[dict[str, object]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(build_skill_manifest(skills, bundles), indent=2, sort_keys=False) + "\n", encoding="utf-8")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Scan the catalog tree, validate metadata, and optionally export a manifest.")
    parser.add_argument("--check", action="store_true", help="Validate catalog metadata without writing files.")
    parser.add_argument("--update-readme", action="store_true", help="Refresh README skill and distinct NuGet package counts from the scanned catalog.")
    parser.add_argument(
        "--validate-only",
        action="store_true",
        help="Validate catalog metadata without writing files.",
    )
    parser.add_argument(
        "--manifest-output",
        type=Path,
        help="Export a transient machine-readable manifest to a custom path.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if sum(1 for value in [args.check, args.validate_only, args.update_readme, args.manifest_output is not None] if value) > 1:
        print("--check, --validate-only, --update-readme, and --manifest-output are mutually exclusive.", file=sys.stderr)
        return 2

    skills = collect_skills(include_token_counts=not args.update_readme)
    if args.update_readme:
        readme_path = ROOT / "README.md"
        readme = readme_path.read_text(encoding="utf-8")
        updated = render_readme_statistics(readme, skills)
        if updated != readme:
            readme_path.write_text(updated, encoding="utf-8")
        stats = catalog_statistics(skills)
        print(f"README statistics are current: {stats['skills']} skills, {stats['nuget_packages']} distinct NuGet packages.")
        return 0

    bundles = build_bundles(skills)

    if args.manifest_output is not None:
        write_manifest_to_path(args.manifest_output, skills, bundles)
        print(f"Wrote manifest to {args.manifest_output}")
        return 0

    if args.validate_only or args.check:
        print(f"Catalog metadata is valid for {len(skills)} skills and {len(bundles)} bundles.")
        return 0

    print(f"Catalog metadata is valid for {len(skills)} skills and {len(bundles)} bundles.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
