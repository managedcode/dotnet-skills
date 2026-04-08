#!/usr/bin/env python3
from __future__ import annotations

import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CATALOG_ROOT = ROOT / "catalog"

LINK_KEYS = ("repository", "docs", "nuget")

CURATED_BUNDLES = [
    {
        "name": "mcaf",
        "title": "MCAF bundle",
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
        "title": "Orleans bundle",
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
    },
]


def unquote(value: str) -> str:
    value = value.strip()
    if len(value) >= 2 and value[0] == value[-1] and value[0] in {"'", '"'}:
        return value[1:-1]
    return value


def slugify(value: str) -> str:
    return re.sub(r"[^a-z0-9]+", "-", value.lower()).strip("-")


def singularize_type_dir(type_dir_name: str) -> str:
    if type_dir_name.endswith("ies"):
        return f"{type_dir_name[:-3]}y"
    if type_dir_name.endswith("s"):
        return type_dir_name[:-1]
    return type_dir_name


def get_type_directories() -> list[str]:
    if not CATALOG_ROOT.is_dir():
        return []

    return sorted(
        [
            path.name
            for path in CATALOG_ROOT.iterdir()
            if path.is_dir() and not path.name.startswith(".")
        ],
        key=lambda item: (item.casefold(), item),
    )


def resolve_category_order(skills: list[dict[str, object]]) -> list[str]:
    categories = {
        str(skill["category"]).strip()
        for skill in skills
        if str(skill.get("category", "")).strip()
    }
    return sorted(categories, key=lambda item: (item.casefold(), item))


def build_catalog_definitions(skills: list[dict[str, object]] | None = None) -> dict[str, list[str]]:
    resolved_skills = skills if skills is not None else collect_skills()
    return {
        "categories": resolve_category_order(resolved_skills),
        "typeDirectories": get_type_directories(),
    }


def parse_frontmatter(path: Path) -> tuple[dict[str, str], str]:
    text = path.read_text(encoding="utf-8")
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


def parse_frontmatter_with_lists(path: Path) -> tuple[dict[str, str | list[str]], str]:
    text = path.read_text(encoding="utf-8")
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

        if line.startswith("  - "):
            if current_key is None:
                raise ValueError(f"{path} has a list item without a parent key: {line}")
            current_list.append(line.strip()[2:].strip())
            continue

        if current_key is not None:
            data[current_key] = current_list
            current_key = None
            current_list = []

        if ":" not in line:
            raise ValueError(f"{path} has malformed frontmatter line: {line}")

        key, value = line.split(":", 1)
        key = key.strip()
        value = value.strip()

        if not value:
            current_key = key
            current_list = []
        else:
            data[key] = unquote(value)

    if current_key is not None:
        data[current_key] = current_list

    return data, body


def parse_title(body: str, path: Path) -> str:
    for line in body.splitlines():
        if line.startswith("# "):
            return line[2:].strip()
    raise ValueError(f"{path} is missing an H1 title")


def load_package_manifest(package_dir: Path) -> tuple[Path, dict]:
    manifest_path = package_dir / "manifest.json"
    if not manifest_path.exists():
        raise ValueError(f"{package_dir} is missing manifest.json")

    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise ValueError(f"{manifest_path} contains invalid JSON: {exc}") from exc

    if not isinstance(manifest, dict):
        raise ValueError(f"{manifest_path} must contain a JSON object")

    return manifest_path, manifest


def load_optional_entity_manifest(entity_dir: Path) -> tuple[Path, dict] | tuple[None, dict]:
    manifest_path = entity_dir / "manifest.json"
    if not manifest_path.exists():
        return None, {}

    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise ValueError(f"{manifest_path} contains invalid JSON: {exc}") from exc

    if not isinstance(manifest, dict):
        raise ValueError(f"{manifest_path} must contain a JSON object")

    return manifest_path, manifest


def _read_skill_manifest_metadata(manifest_path: Path | None, entity_manifest: dict, expected_manifest_path: Path) -> dict[str, object]:
    if not manifest_path or not entity_manifest:
        raise ValueError(f"{expected_manifest_path} is required and must define `version` and `category`")

    allowed = {"version", "category", "packages", "package_prefix"}
    unknown = sorted(set(entity_manifest) - allowed)
    if unknown:
        raise ValueError(f"{manifest_path} has unsupported keys: {', '.join(unknown)}")

    result: dict[str, object] = {}

    version = entity_manifest.get("version")
    if not isinstance(version, str) or not version.strip():
        raise ValueError(f"{manifest_path} field `version` must be a non-empty string")
    result["version"] = version.strip()

    category = entity_manifest.get("category")
    if not isinstance(category, str) or not category.strip():
        raise ValueError(f"{manifest_path} field `category` must be a non-empty string")
    result["category"] = category.strip()

    if "packages" in entity_manifest:
        packages = entity_manifest["packages"]
        if not isinstance(packages, list) or any(not isinstance(item, str) or not item.strip() for item in packages):
            raise ValueError(f"{manifest_path} field `packages` must be a list of non-empty strings")
        result["packages"] = [item.strip() for item in packages]

    if "package_prefix" in entity_manifest:
        package_prefix = entity_manifest["package_prefix"]
        if not isinstance(package_prefix, str) or not package_prefix.strip():
            raise ValueError(f"{manifest_path} field `package_prefix` must be a non-empty string")
        result["package_prefix"] = package_prefix.strip()

    return result


def _read_agent_manifest_metadata(manifest_path: Path | None, entity_manifest: dict) -> dict[str, object]:
    if not manifest_path or not entity_manifest:
        return {}

    allowed = {"packages", "package_prefix"}
    unknown = sorted(set(entity_manifest) - allowed)
    if unknown:
        raise ValueError(f"{manifest_path} has unsupported keys: {', '.join(unknown)}")

    result: dict[str, object] = {}

    if "packages" in entity_manifest:
        packages = entity_manifest["packages"]
        if not isinstance(packages, list) or any(not isinstance(item, str) or not item.strip() for item in packages):
            raise ValueError(f"{manifest_path} field `packages` must be a list of non-empty strings")
        result["packages"] = [item.strip() for item in packages]

    if "package_prefix" in entity_manifest:
        package_prefix = entity_manifest["package_prefix"]
        if not isinstance(package_prefix, str) or not package_prefix.strip():
            raise ValueError(f"{manifest_path} field `package_prefix` must be a non-empty string")
        result["package_prefix"] = package_prefix.strip()

    return result


def ensure_no_legacy_package_skill_map(manifest_path: Path, package_manifest: dict) -> None:
    if "skills" in package_manifest:
        raise ValueError(
            f"{manifest_path} must not define a top-level `skills` map; move skill-specific metadata into "
            "the nearest `skills/<skill>/manifest.json` file"
        )


def _read_package_links(manifest_path: Path, package_manifest: dict) -> dict[str, str]:
    raw_links = package_manifest.get("links", {})
    if raw_links in (None, {}):
        return {}
    if not isinstance(raw_links, dict):
        raise ValueError(f"{manifest_path} field `links` must be an object")

    unknown = sorted(set(raw_links) - set(LINK_KEYS))
    if unknown:
        raise ValueError(f"{manifest_path} field `links` has unsupported keys: {', '.join(unknown)}")

    links: dict[str, str] = {}
    for key in LINK_KEYS:
        value = raw_links.get(key)
        if value in (None, ""):
            continue
        if not isinstance(value, str) or not value.strip():
            raise ValueError(f"{manifest_path} field `links.{key}` must be a non-empty string")
        links[key] = value.strip()

    return links


def collect_skills() -> list[dict[str, object]]:
    skills: list[dict[str, object]] = []

    for type_dir_name in get_type_directories():
        type_dir = CATALOG_ROOT / type_dir_name
        if not type_dir.is_dir():
            continue

        skill_type = singularize_type_dir(type_dir_name)

        for package_dir in sorted(p for p in type_dir.iterdir() if p.is_dir()):
            manifest_path, package_manifest = load_package_manifest(package_dir)
            ensure_no_legacy_package_skill_map(manifest_path, package_manifest)
            package_links = _read_package_links(manifest_path, package_manifest)
            skills_subdir = package_dir / "skills"
            if not skills_subdir.is_dir():
                continue

            package_name = package_dir.name

            for skill_dir in sorted(p for p in skills_subdir.iterdir() if p.is_dir()):
                skill_path = skill_dir / "SKILL.md"
                if not skill_path.exists():
                    continue
                skill_manifest_path, skill_manifest = load_optional_entity_manifest(skill_dir)

                metadata, body = parse_frontmatter(skill_path)
                title = parse_title(body, skill_path)

                required = ["name", "description", "compatibility"]
                missing = [key for key in required if key not in metadata or not metadata[key].strip()]
                if missing:
                    raise ValueError(f"{skill_path} is missing required frontmatter keys: {', '.join(missing)}")

                disallowed = [key for key in ("version", "category", "packages", "package_prefix") if key in metadata]
                if disallowed:
                    raise ValueError(
                        f"{skill_path} must not declare {', '.join(disallowed)} in frontmatter; move them to {skill_dir / 'manifest.json'}"
                    )

                skill_name = metadata["name"]
                skill_manifest_metadata = _read_skill_manifest_metadata(skill_manifest_path, skill_manifest, skill_dir / "manifest.json")

                skill_entry: dict[str, object] = {
                    "name": skill_name,
                    "title": title,
                    "version": skill_manifest_metadata["version"],
                    "category": skill_manifest_metadata["category"],
                    "type": skill_type,
                    "package": package_name,
                    "description": metadata["description"],
                    "compatibility": metadata["compatibility"],
                    "path": f"catalog/{type_dir_name}/{package_name}/skills/{skill_dir.name}/",
                }

                if package_links:
                    skill_entry["links"] = package_links
                for key in ("packages", "package_prefix"):
                    if key in skill_manifest_metadata:
                        skill_entry[key] = skill_manifest_metadata[key]
                skills.append(skill_entry)

    ensure_unique_entries(skills, "skill")
    return skills


def collect_agents() -> list[dict[str, object]]:
    agents: list[dict[str, object]] = []

    for type_dir_name in get_type_directories():
        type_dir = CATALOG_ROOT / type_dir_name
        if not type_dir.is_dir():
            continue

        for package_dir in sorted(p for p in type_dir.iterdir() if p.is_dir()):
            manifest_path, package_manifest = load_package_manifest(package_dir)
            ensure_no_legacy_package_skill_map(manifest_path, package_manifest)
            package_links = _read_package_links(manifest_path, package_manifest)
            agents_dir = package_dir / "agents"
            if not agents_dir.is_dir():
                continue

            for agent_dir in sorted(p for p in agents_dir.iterdir() if p.is_dir()):
                agent_path = agent_dir / "AGENT.md"
                if not agent_path.exists():
                    continue
                agent_manifest_path, agent_manifest = load_optional_entity_manifest(agent_dir)

                metadata, body = parse_frontmatter_with_lists(agent_path)
                title = parse_title(body, agent_path)

                required = ["name", "description"]
                missing = [key for key in required if key not in metadata or not str(metadata[key]).strip()]
                if missing:
                    raise ValueError(f"{agent_path} is missing required frontmatter keys: {', '.join(missing)}")

                agent_entry: dict[str, object] = {
                    "name": str(metadata["name"]),
                    "title": title,
                    "description": str(metadata["description"]),
                    "skills": metadata.get("skills", []),
                    "tools": metadata.get("tools", ""),
                    "model": metadata.get("model", "inherit"),
                    "package": package_dir.name,
                    "type": type_dir_name,
                    "path": f"catalog/{type_dir_name}/{package_dir.name}/agents/{agent_dir.name}/",
                }
                if package_links:
                    agent_entry["links"] = package_links
                agent_entry.update(_read_agent_manifest_metadata(agent_manifest_path, agent_manifest))
                agents.append(agent_entry)

    ensure_unique_entries(agents, "agent")
    return agents


def ensure_unique_entries(entries: list[dict[str, object]], kind: str) -> None:
    seen: dict[str, str] = {}
    duplicates: list[str] = []

    for entry in entries:
        name = str(entry["name"])
        path = str(entry["path"])
        previous = seen.get(name)
        if previous is None:
            seen[name] = path
            continue
        duplicates.append(f"{name}: {previous}, {path}")

    if duplicates:
        raise ValueError(f"Duplicate {kind} ids were found in catalog metadata: {'; '.join(duplicates)}")


def build_bundles(skills: list[dict[str, object]]) -> list[dict[str, object]]:
    skills_by_name = {str(skill["name"]): skill for skill in skills}
    bundles: list[dict[str, object]] = []

    for bundle in CURATED_BUNDLES:
        missing = [skill_name for skill_name in bundle["skills"] if skill_name not in skills_by_name]
        if missing:
            raise ValueError(
                f"Curated bundle {bundle['name']} references unknown skills: {', '.join(sorted(missing))}"
            )

        bundles.append(
            {
                "name": bundle["name"],
                "title": bundle["title"],
                "description": bundle["description"],
                "kind": bundle["kind"],
                "sourceCategory": "",
                "skills": bundle["skills"],
            }
        )

    for category in resolve_category_order(skills):
        category_skills = sorted(
            (skill for skill in skills if skill["category"] == category),
            key=lambda item: str(item["name"]),
        )
        if not category_skills:
            continue

        bundles.append(
            {
                "name": slugify(category),
                "title": f"{category} bundle",
                "description": f"Install all {len(category_skills)} skills from the {category} category in one command.",
                "kind": "category",
                "sourceCategory": category,
                "skills": [str(skill["name"]) for skill in category_skills],
            }
        )

    return bundles


def build_skill_manifest(skills: list[dict[str, object]], bundles: list[dict[str, object]]) -> dict[str, object]:
    return {"skills": skills, "bundles": bundles}


def build_agent_manifest(agents: list[dict[str, object]]) -> dict[str, object]:
    return {"agents": agents}
