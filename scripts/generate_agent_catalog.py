#!/usr/bin/env python3
"""Generate the agents catalog manifest from agent metadata."""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
AGENTS_DIR = ROOT / "agents"
MANIFEST_PATH = ROOT / "catalog" / "agents.json"


def unquote(value: str) -> str:
    value = value.strip()
    if len(value) >= 2 and value[0] == value[-1] and value[0] in {"'", '"'}:
        return value[1:-1]
    return value


def parse_frontmatter(path: Path) -> tuple[dict[str, str | list[str]], str]:
    text = path.read_text()
    if not text.startswith("---\n"):
        raise ValueError(f"{path} is missing YAML frontmatter")

    match = re.match(r"^---\n(.*?)\n---\n(.*)$", text, flags=re.DOTALL)
    if not match:
        raise ValueError(f"{path} has invalid frontmatter")

    raw_frontmatter, body = match.groups()
    data: dict[str, str | list[str]] = {}
    current_key: str | None = None
    current_list: list[str] = []

    for line in raw_frontmatter.splitlines():
        if not line.strip():
            continue

        # Check for list continuation
        if line.startswith("  - "):
            if current_key:
                current_list.append(line.strip()[2:].strip())
            continue

        # Save previous list if any
        if current_key and current_list:
            data[current_key] = current_list
            current_list = []
            current_key = None

        if ":" not in line:
            raise ValueError(f"{path} has malformed frontmatter line: {line}")

        key, value = line.split(":", 1)
        key = key.strip()
        value = value.strip()

        if not value:
            # Start of a list
            current_key = key
            current_list = []
        else:
            data[key] = unquote(value)

    # Save last list if any
    if current_key and current_list:
        data[current_key] = current_list

    return data, body


def parse_title(body: str, path: Path) -> str:
    for line in body.splitlines():
        if line.startswith("# "):
            return line[2:].strip()
    raise ValueError(f"{path} is missing an H1 title")


def collect_agents() -> list[dict[str, str | list[str]]]:
    agents: list[dict[str, str | list[str]]] = []
    for agent_dir in sorted(path for path in AGENTS_DIR.iterdir() if path.is_dir()):
        agent_path = agent_dir / "AGENT.md"
        if not agent_path.exists():
            continue

        metadata, body = parse_frontmatter(agent_path)
        title = parse_title(body, agent_path)

        required = ["name", "description"]
        missing = [key for key in required if key not in metadata or not str(metadata[key]).strip()]
        if missing:
            raise ValueError(f"{agent_path} is missing required frontmatter keys: {', '.join(missing)}")

        agents.append(
            {
                "name": str(metadata["name"]),
                "title": title,
                "description": str(metadata["description"]),
                "skills": metadata.get("skills", []),
                "tools": metadata.get("tools", ""),
                "model": metadata.get("model", "inherit"),
                "path": f"agents/{agent_dir.name}/",
            }
        )

    return agents


def write_manifest(output_path: Path, agents: list[dict[str, str | list[str]]]) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps({"agents": agents}, indent=2, sort_keys=False) + "\n")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output",
        type=Path,
        default=MANIFEST_PATH,
        help="Write the generated manifest to this path.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    agents = collect_agents()
    output_path = args.output.resolve()
    write_manifest(output_path, agents)
    print(f"Generated agent catalog for {len(agents)} agents at {output_path}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
