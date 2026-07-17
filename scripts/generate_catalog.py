#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from catalog_index import build_bundles, build_skill_manifest, collect_skills


def write_manifest_to_path(path: Path, skills: list[dict[str, object]], bundles: list[dict[str, object]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(build_skill_manifest(skills, bundles), indent=2, sort_keys=False) + "\n", encoding="utf-8")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Scan the catalog tree, validate metadata, and optionally export a manifest.")
    parser.add_argument("--check", action="store_true", help="Validate catalog metadata without writing files.")
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
    if sum(1 for value in [args.check, args.validate_only, args.manifest_output is not None] if value) > 1:
        print("--check, --validate-only, and --manifest-output are mutually exclusive.", file=sys.stderr)
        return 2

    skills = collect_skills(include_token_counts=True)
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
