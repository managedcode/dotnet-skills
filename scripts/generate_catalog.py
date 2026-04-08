#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
README_PATH = ROOT / "README.md"
MANIFEST_PATH = ROOT / "catalog" / "skills.json"

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

CATEGORY_ORDER = [
    "Core",
    "Web",
    "Cloud",
    "Distributed",
    "Desktop",
    "Cross-Platform UI",
    "Data",
    "AI",
    "Legacy",
    "Testing",
    "Code Quality",
    "Architecture",
    "Metrics",
]

TYPE_DIRS = ["Frameworks", "Libraries", "Tools", "Testing", "Platform"]

TYPE_SINGULAR: dict[str, str] = {
    "Frameworks": "Framework",
    "Libraries": "Library",
    "Tools": "Tool",
    "Testing": "Testing",
    "Platform": "Platform",
}

CURATED_PACKAGES = [
    {
        "name": "mcaf",
        "title": "MCAF package",
        "description": "Install the locally mirrored MCAF governance skills in one command, including adoption, delivery workflow, developer experience, documentation, feature specs, review planning, NFRs, source-control policy, UI/UX, and ML/AI delivery guidance.",
        "kind": "curated",
        "skills": [
            "dotnet-mcaf",
            "dotnet-mcaf-agile-delivery",
            "dotnet-mcaf-devex",
            "dotnet-mcaf-documentation",
            "dotnet-mcaf-feature-spec",
            "dotnet-mcaf-human-review-planning",
            "dotnet-mcaf-ml-ai-delivery",
            "dotnet-mcaf-nfr",
            "dotnet-mcaf-source-control",
            "dotnet-mcaf-ui-ux",
        ],
    },
    {
        "name": "orleans",
        "title": "Orleans package",
        "description": "Install the main Orleans stack in one command, including Orleans core guidance, adjacent ManagedCode integrations, worker-hosting patterns, Aspire orchestration, and SignalR delivery support.",
        "kind": "curated",
        "skills": [
            "dotnet-orleans",
            "dotnet-managedcode-orleans-graph",
            "dotnet-managedcode-orleans-signalr",
            "dotnet-worker-services",
            "dotnet-aspire",
            "dotnet-signalr",
        ],
    }
]


def unquote(value: str) -> str:
    value = value.strip()
    if len(value) >= 2 and value[0] == value[-1] and value[0] in {"'", '"'}:
        return value[1:-1]
    return value


def slugify(value: str) -> str:
    return re.sub(r"[^a-z0-9]+", "-", value.lower()).strip("-")


def parse_frontmatter(path: Path) -> tuple[dict[str, str], str]:
    text = path.read_text()
    if not text.startswith("---\n"):
        raise ValueError(f"{path} is missing YAML frontmatter")

    match = re.match(r"^---\n(.*?)\n---\n(.*)$", text, flags=re.DOTALL)
    if not match:
        raise ValueError(f"{path} has invalid frontmatter")

    raw_frontmatter, body = match.groups()
    data: dict[str, str] = {}
    for line in raw_frontmatter.splitlines():
        if not line.strip():
            continue
        if ":" not in line:
            raise ValueError(f"{path} has malformed frontmatter line: {line}")
        key, value = line.split(":", 1)
        data[key.strip()] = unquote(value)
    return data, body


def parse_title(body: str, path: Path) -> str:
    for line in body.splitlines():
        if line.startswith("# "):
            return line[2:].strip()
    raise ValueError(f"{path} is missing an H1 title")


def collect_skills() -> list[dict[str, str]]:
    skills: list[dict[str, str]] = []

    catalog_root = ROOT / "catalog"
    for type_dir_name in TYPE_DIRS:
        type_dir = catalog_root / type_dir_name
        if not type_dir.is_dir():
            continue

        skill_type = TYPE_SINGULAR[type_dir_name]

        for package_dir in sorted(p for p in type_dir.iterdir() if p.is_dir()):
            skills_subdir = package_dir / "skills"
            if not skills_subdir.is_dir():
                continue

            package_name = package_dir.name

            for skill_dir in sorted(p for p in skills_subdir.iterdir() if p.is_dir()):
                skill_path = skill_dir / "SKILL.md"
                if not skill_path.exists():
                    continue

                metadata, body = parse_frontmatter(skill_path)
                title = parse_title(body, skill_path)

                required = ["name", "version", "category", "description", "compatibility"]
                missing = [key for key in required if key not in metadata or not metadata[key].strip()]
                if missing:
                    raise ValueError(f"{skill_path} is missing required frontmatter keys: {', '.join(missing)}")

                category = metadata["category"]
                if category not in CATEGORY_ORDER:
                    raise ValueError(f"{skill_path} has unsupported category: {category}")

                skill_name = metadata["name"]

                skills.append(
                    {
                        "name": skill_name,
                        "title": title,
                        "version": metadata["version"],
                        "category": category,
                        "type": skill_type,
                        "package": package_name,
                        "description": metadata["description"],
                        "compatibility": metadata["compatibility"],
                        "path": f"catalog/{type_dir_name}/{package_name}/skills/{skill_dir.name}/",
                    }
                )

    return skills


def build_packages(skills: list[dict[str, str]]) -> list[dict[str, object]]:
    skills_by_name = {skill["name"]: skill for skill in skills}
    packages: list[dict[str, object]] = []

    for package in CURATED_PACKAGES:
        missing = [skill_name for skill_name in package["skills"] if skill_name not in skills_by_name]
        if missing:
            raise ValueError(
                f"Curated package {package['name']} references unknown skills: {', '.join(sorted(missing))}"
            )

        packages.append(
            {
                "name": package["name"],
                "title": package["title"],
                "description": package["description"],
                "kind": package["kind"],
                "sourceCategory": "",
                "skills": package["skills"],
            }
        )

    for category in CATEGORY_ORDER:
        category_skills = sorted(
            (skill for skill in skills if skill["category"] == category),
            key=lambda item: item["name"],
        )
        if not category_skills:
            continue

        packages.append(
            {
                "name": slugify(category),
                "title": f"{category} package",
                "description": f"Install all {len(category_skills)} skills from the {category} category in one command.",
                "kind": "category",
                "sourceCategory": category,
                "skills": [skill["name"] for skill in category_skills],
            }
        )

    return packages


def render_catalog(skills: list[dict[str, str]]) -> str:
    grouped: dict[str, list[dict[str, str]]] = {category: [] for category in CATEGORY_ORDER}
    for skill in skills:
        grouped[skill["category"]].append(skill)

    for category in grouped:
        grouped[category].sort(key=lambda item: item["name"])

    lines: list[str] = [BEGIN_MARKER, "", f"This catalog currently contains **{len(skills)}** skills.", ""]

    for category in CATEGORY_ORDER:
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


def write_manifest(skills: list[dict[str, str]], packages: list[dict[str, object]]) -> None:
    write_manifest_to_path(MANIFEST_PATH, skills, packages)


def write_manifest_to_path(path: Path, skills: list[dict[str, str]], packages: list[dict[str, object]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps({"skills": skills, "packages": packages}, indent=2, sort_keys=False) + "\n")


def check_manifest(skills: list[dict[str, str]], packages: list[dict[str, object]]) -> bool:
    if not MANIFEST_PATH.exists():
        return False
    current = json.loads(MANIFEST_PATH.read_text())
    return current == {"skills": skills, "packages": packages}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate the README catalog and skills manifest from skill metadata.")
    parser.add_argument("--check", action="store_true", help="Fail if generated files are out of date.")
    parser.add_argument(
        "--validate-only",
        action="store_true",
        help="Validate skill metadata and catalog rendering without writing or checking generated files.",
    )
    parser.add_argument(
        "--manifest-output",
        type=Path,
        help="Write only the machine-readable manifest to a custom path without mutating README.md.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if sum(1 for value in [args.check, args.validate_only, args.manifest_output is not None] if value) > 1:
        print("--check, --validate-only, and --manifest-output are mutually exclusive.", file=sys.stderr)
        return 2

    skills = collect_skills()
    packages = build_packages(skills)
    rendered_catalog = render_catalog(skills)

    if args.manifest_output is not None:
        write_manifest_to_path(args.manifest_output, skills, packages)
        print(f"Wrote manifest to {args.manifest_output}")
        return 0

    if args.validate_only:
        print(f"Catalog metadata is valid for {len(skills)} skills and {len(packages)} packages.")
        return 0

    if args.check:
        readme_ok = check_readme(rendered_catalog, len(skills))
        manifest_ok = check_manifest(skills, packages)
        if not readme_ok or not manifest_ok:
            if not readme_ok:
                print("README.md catalog section is out of date.", file=sys.stderr)
            if not manifest_ok:
                print("catalog/skills.json is out of date.", file=sys.stderr)
            return 1
        print("Catalog is up to date.")
        return 0

    update_readme(rendered_catalog, len(skills))
    write_manifest(skills, packages)
    print(f"Generated catalog for {len(skills)} skills and {len(packages)} packages.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
