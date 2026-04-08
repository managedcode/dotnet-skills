#!/usr/bin/env python3
"""Validate or export the agents catalog by scanning catalog package folders."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from catalog_index import build_agent_manifest, collect_agents

ROOT = Path(__file__).resolve().parents[1]
MANIFEST_PATH = ROOT / "artifacts" / "agent-catalog.json"


def write_manifest(output_path: Path, agents: list[dict[str, object]]) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(build_agent_manifest(agents), indent=2, sort_keys=False) + "\n", encoding="utf-8")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output",
        type=Path,
        default=MANIFEST_PATH,
        help="Write a transient exported manifest to this path.",
    )
    parser.add_argument(
        "--validate-only",
        action="store_true",
        help="Validate agent metadata without writing an output file.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    agents = collect_agents()
    if args.validate_only:
        print(f"Agent metadata is valid for {len(agents)} agents.")
        return 0

    output_path = args.output.resolve()
    write_manifest(output_path, agents)
    print(f"Generated agent catalog for {len(agents)} agents at {output_path}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
