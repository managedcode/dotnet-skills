#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
import shutil
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CATALOG_ROOT = ROOT / "catalog"
CONFIG_ROOT = ROOT / "catalog-sources"

ALLOWED_TYPES = {"Frameworks", "Libraries", "Tools", "Testing", "Platform"}
ALLOWED_SKILL_MANIFEST_KEYS = {"version", "category", "packages", "package_prefix"}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Import vendored external skill sources into catalog packages.")
    parser.add_argument("--validate-config", action="store_true", help="Validate source config and upstream layout without writing files.")
    return parser.parse_args()


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def parse_markdown_frontmatter(path: Path) -> tuple[dict, str]:
    text = path.read_text(encoding="utf-8")
    match = re.match(r"^---\n(.*?)\n---\n(.*)$", text, flags=re.DOTALL)
    if not match:
        raise ValueError(f"{path} is missing YAML frontmatter")

    raw_frontmatter, body = match.groups()
    return parse_simple_yaml_mapping(path, raw_frontmatter), body


def parse_simple_yaml_mapping(path: Path, raw_frontmatter: str) -> dict:
    data: dict[str, object] = {}
    lines = raw_frontmatter.splitlines()
    index = 0

    while index < len(lines):
        line = lines[index]
        stripped = line.strip()
        if not stripped:
            index += 1
            continue

        if ":" not in line:
            raise ValueError(f"{path} has malformed frontmatter line: {line}")

        key, raw_value = line.split(":", 1)
        key = key.strip()
        value = raw_value.strip()

        if re.fullmatch(r"[>|][+-]?", value):
            index += 1
            block_lines: list[str] = []
            while index < len(lines):
                candidate = lines[index]
                if candidate.startswith("  ") or candidate.startswith("\t"):
                    block_lines.append(candidate.lstrip())
                    index += 1
                    continue
                if not candidate.strip():
                    block_lines.append("")
                    index += 1
                    continue
                break
            data[key] = " ".join(part for part in (segment.strip() for segment in block_lines) if part)
            continue

        if not value:
            index += 1
            items: list[str] = []
            while index < len(lines):
                candidate = lines[index]
                if candidate.startswith("  - "):
                    items.append(candidate[4:].strip())
                    index += 1
                    continue
                if not candidate.strip():
                    index += 1
                    continue
                break
            data[key] = items
            continue

        index += 1
        continuation_lines: list[str] = []
        while index < len(lines):
            candidate = lines[index]
            if candidate.startswith("  ") or candidate.startswith("\t"):
                continuation_lines.append(candidate.strip())
                index += 1
                continue
            if not candidate.strip():
                index += 1
                continue
            break

        scalar_value = unquote(value)
        if continuation_lines:
            scalar_value = normalize_text(" ".join([scalar_value, *continuation_lines]))
        data[key] = scalar_value

    return data


def unquote(value: str) -> str:
    value = value.strip()
    if len(value) >= 2 and value[0] == value[-1] and value[0] in {"'", '"'}:
        return value[1:-1]
    return value


def normalize_text(value: object) -> str:
    return " ".join(str(value).split())


def titleize_slug(value: str) -> str:
    words = [part for part in re.split(r"[-_]+", value.strip()) if part]
    return " ".join(word.upper() if word.isupper() else word.capitalize() for word in words) or value


def ensure_top_level_heading(body: str, fallback_title: str) -> str:
    stripped_body = body.lstrip("\n")
    for line in stripped_body.splitlines():
        if not line.strip():
            continue
        if line.startswith("# "):
            return stripped_body
        if line.startswith("#"):
            heading_text = re.sub(r"^#+\s*", "", line).strip()
            title = heading_text or fallback_title
            return f"# {title}\n\n{stripped_body}"
        break

    return f"# {fallback_title}\n\n{stripped_body}" if stripped_body else f"# {fallback_title}\n"


def render_skill_markdown(name: str, description: str, compatibility: str, body: str, source_path: str) -> str:
    normalized_body = ensure_top_level_heading(body, titleize_slug(name))
    frontmatter = [
        "---",
        f"name: {name}",
        f"description: {json.dumps(description, ensure_ascii=False)}",
        f"compatibility: {json.dumps(compatibility, ensure_ascii=False)}",
        "---",
        "",
        f"<!-- Imported from {source_path} via vendir. Edit upstream or catalog-sources config, then rerun scripts/import_external_catalog_sources.py. -->",
        "",
    ]
    return "\n".join(frontmatter) + normalized_body


def render_agent_markdown(name: str, description: str, skills: list[str], body: str, source_path: str) -> str:
    frontmatter = [
        "---",
        f"name: {name}",
        f"description: {json.dumps(description, ensure_ascii=False)}",
    ]
    if skills:
        frontmatter.append("skills:")
        frontmatter.extend(f"  - {skill}" for skill in skills)
    frontmatter.extend(
        [
            "---",
            "",
            f"<!-- Imported from {source_path} via vendir. Edit upstream or catalog-sources config, then rerun scripts/import_external_catalog_sources.py. -->",
            "",
        ]
    )
    return "\n".join(frontmatter) + body


def remove_path(path: Path) -> None:
    if path.is_dir():
        shutil.rmtree(path)
    elif path.exists():
        path.unlink()


def copy_directory_contents(source_dir: Path, destination_dir: Path, *, skip_names: set[str]) -> None:
    destination_dir.mkdir(parents=True, exist_ok=True)
    for child in source_dir.iterdir():
        if child.name in skip_names:
            continue

        target = destination_dir / child.name
        if child.is_dir():
            shutil.copytree(child, target, dirs_exist_ok=True)
        else:
            shutil.copy2(child, target)


def validate_config(config_path: Path, config: dict) -> None:
    source_id = config.get("id")
    if not isinstance(source_id, str) or not source_id.strip():
        raise ValueError(f"{config_path} must define a non-empty id")

    repository = config.get("repository")
    if not isinstance(repository, str) or not repository.strip():
        raise ValueError(f"{config_path} must define a non-empty repository")

    source_root = config.get("sourceRoot")
    if not isinstance(source_root, str) or not source_root.strip():
        raise ValueError(f"{config_path} must define a non-empty sourceRoot")

    plugin_root = ROOT / source_root
    if not plugin_root.is_dir():
        raise ValueError(f"{config_path} points to a missing vendored source root: {plugin_root}")

    docs_base = config.get("docsBase")
    if not isinstance(docs_base, str) or not docs_base.strip():
        raise ValueError(f"{config_path} must define a non-empty docsBase")

    managed_package_prefix = config.get("managedPackagePrefix")
    if not isinstance(managed_package_prefix, str) or not managed_package_prefix.strip():
        raise ValueError(f"{config_path} must define a non-empty managedPackagePrefix")

    plugins = config.get("plugins")
    if not isinstance(plugins, dict) or not plugins:
        raise ValueError(f"{config_path} must define a non-empty plugins object")

    for plugin_name, plugin_config in plugins.items():
        if not isinstance(plugin_config, dict):
            raise ValueError(f"{config_path} plugin {plugin_name!r} must be an object")

        plugin_dir = plugin_root / plugin_name
        if not plugin_dir.is_dir():
            raise ValueError(f"{config_path} plugin {plugin_name!r} points to a missing vendored plugin directory: {plugin_dir}")
        if not (plugin_dir / "plugin.json").exists():
            raise ValueError(f"{plugin_dir} is missing plugin.json")

        package_type = plugin_config.get("type")
        if package_type not in ALLOWED_TYPES:
            raise ValueError(f"{config_path} plugin {plugin_name!r} has unsupported type {package_type!r}")

        package_name = plugin_config.get("package")
        if not isinstance(package_name, str) or not package_name.strip():
            raise ValueError(f"{config_path} plugin {plugin_name!r} must define a non-empty package")

        title = plugin_config.get("title")
        if not isinstance(title, str) or not title.strip():
            raise ValueError(f"{config_path} plugin {plugin_name!r} must define a non-empty title")

        category = plugin_config.get("category")
        if not isinstance(category, str) or not category.strip():
            raise ValueError(f"{config_path} plugin {plugin_name!r} must define a non-empty category")

        compatibility = plugin_config.get("compatibility")
        if not isinstance(compatibility, str) or not compatibility.strip():
            raise ValueError(f"{config_path} plugin {plugin_name!r} must define a non-empty compatibility")

        for block_name in ("skillDefaults", "skillOverrides"):
            block = plugin_config.get(block_name, {} if block_name == "skillOverrides" else {})
            if block is None:
                continue
            if not isinstance(block, dict):
                raise ValueError(f"{config_path} plugin {plugin_name!r} field {block_name} must be an object")
            if block_name == "skillDefaults":
                unknown = sorted(set(block) - (ALLOWED_SKILL_MANIFEST_KEYS - {"version", "category"}))
                if unknown:
                    raise ValueError(f"{config_path} plugin {plugin_name!r} field skillDefaults has unsupported keys: {', '.join(unknown)}")
            else:
                for skill_name, skill_override in block.items():
                    if not isinstance(skill_override, dict):
                        raise ValueError(f"{config_path} plugin {plugin_name!r} skill override {skill_name!r} must be an object")
                    unknown = sorted(set(skill_override) - ALLOWED_SKILL_MANIFEST_KEYS - {"compatibility"})
                    if unknown:
                        raise ValueError(
                            f"{config_path} plugin {plugin_name!r} skill override {skill_name!r} has unsupported keys: {', '.join(unknown)}"
                        )


def resolve_skill_manifest(plugin_config: dict, skill_name: str, plugin_version: str) -> tuple[dict[str, object], str]:
    manifest: dict[str, object] = {
        "version": plugin_version,
        "category": plugin_config["category"],
    }

    skill_defaults = plugin_config.get("skillDefaults", {})
    if isinstance(skill_defaults, dict):
        for key in ("packages", "package_prefix"):
            if key in skill_defaults:
                manifest[key] = skill_defaults[key]

    skill_overrides = plugin_config.get("skillOverrides", {})
    if isinstance(skill_overrides, dict) and isinstance(skill_overrides.get(skill_name), dict):
        override = skill_overrides[skill_name]
        for key in ("version", "category", "packages", "package_prefix"):
            if key in override:
                manifest[key] = override[key]
        compatibility = normalize_text(override.get("compatibility", plugin_config["compatibility"]))
    else:
        compatibility = normalize_text(plugin_config["compatibility"])

    return manifest, compatibility


def validate_skill_manifest(manifest_path: Path, manifest: dict[str, object]) -> None:
    version = manifest.get("version")
    category = manifest.get("category")
    if not isinstance(version, str) or not version.strip():
        raise ValueError(f"{manifest_path} field version must be a non-empty string")
    if not isinstance(category, str) or not category.strip():
        raise ValueError(f"{manifest_path} field category must be a non-empty string")

    packages = manifest.get("packages")
    if packages is not None:
        if not isinstance(packages, list) or any(not isinstance(item, str) or not item.strip() for item in packages):
            raise ValueError(f"{manifest_path} field packages must be a list of non-empty strings")

    package_prefix = manifest.get("package_prefix")
    if package_prefix is not None and (not isinstance(package_prefix, str) or not package_prefix.strip()):
        raise ValueError(f"{manifest_path} field package_prefix must be a non-empty string")


def collect_existing_entries(kind: str, excluded_package_dirs: set[Path]) -> dict[str, list[Path]]:
    pattern = "catalog/*/*/skills/*/SKILL.md" if kind == "skill" else "catalog/*/*/agents/*/AGENT.md"
    entries: dict[str, list[Path]] = {}

    for path in ROOT.glob(pattern):
        package_dir = path.parents[2] if kind == "skill" else path.parents[2]
        if package_dir in excluded_package_dirs:
            continue

        metadata, _ = parse_markdown_frontmatter(path)
        name = normalize_text(metadata.get("name"))
        if not name:
            continue
        entries.setdefault(name, []).append(path)

    return entries


def cleanup_empty_package_dirs() -> None:
    for package_dir in sorted(CATALOG_ROOT.glob("*/*")):
        if not package_dir.is_dir():
            continue
        skills_dir = package_dir / "skills"
        agents_dir = package_dir / "agents"
        has_skills = skills_dir.is_dir() and any(child.is_dir() for child in skills_dir.iterdir())
        has_agents = agents_dir.is_dir() and any(child.is_dir() for child in agents_dir.iterdir())
        if not has_skills and not has_agents:
            remove_path(package_dir)


def import_source(config_path: Path, config: dict) -> dict[str, int]:
    validate_config(config_path, config)

    plugin_root = ROOT / str(config["sourceRoot"])
    docs_base = str(config["docsBase"]).rstrip("/")
    repository = str(config["repository"]).rstrip("/")
    managed_prefix = str(config["managedPackagePrefix"])
    plugins: dict[str, dict] = config["plugins"]

    target_package_dirs = {
        CATALOG_ROOT / plugin_config["type"] / plugin_config["package"]
        for plugin_config in plugins.values()
    }

    existing_skills = collect_existing_entries("skill", target_package_dirs)
    existing_agents = collect_existing_entries("agent", target_package_dirs)

    if config.get("replaceSkillConflicts", False):
        managed_names = {
            path.parent.name
            for path in CATALOG_ROOT.glob(f"*/*")
            if path.is_dir() and path.name.startswith(managed_prefix)
        }
        for package_dir in CATALOG_ROOT.glob("*/*"):
            if package_dir.is_dir() and package_dir.name in managed_names:
                remove_path(package_dir)

    imported_skill_count = 0
    imported_agent_count = 0
    removed_conflicts = 0

    for plugin_name, plugin_config in plugins.items():
        plugin_dir = plugin_root / plugin_name
        plugin_manifest = load_json(plugin_dir / "plugin.json")
        package_dir = CATALOG_ROOT / plugin_config["type"] / plugin_config["package"]
        temp_package_dir = package_dir.parent / f".{package_dir.name}.tmp-import"
        pending_skill_conflicts: set[Path] = set()
        pending_agent_conflicts: set[Path] = set()

        remove_path(temp_package_dir)
        try:
            (temp_package_dir / "skills").mkdir(parents=True, exist_ok=True)

            package_manifest = {
                "name": plugin_name,
                "title": plugin_config["title"],
                "description": f"{normalize_text(plugin_manifest.get('description', ''))} Imported from dotnet/skills via vendir.".strip(),
                "links": {
                    "repository": repository,
                    "docs": f"{docs_base}/{plugin_name}",
                },
            }
            (temp_package_dir / "manifest.json").write_text(json.dumps(package_manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

            skill_names: list[str] = []
            skills_root = plugin_dir / "skills"
            for upstream_skill_md in sorted(skills_root.glob("*/SKILL.md")):
                upstream_skill_dir = upstream_skill_md.parent
                metadata, body = parse_markdown_frontmatter(upstream_skill_md)
                skill_name = normalize_text(metadata.get("name"))
                description = normalize_text(metadata.get("description"))
                if not skill_name or not description:
                    raise ValueError(f"{upstream_skill_md} must define non-empty name and description")

                conflicts = existing_skills.get(skill_name, [])
                if conflicts:
                    if not config.get("replaceSkillConflicts", False):
                        conflict_text = ", ".join(str(path) for path in conflicts)
                        raise ValueError(f"Imported skill {skill_name!r} conflicts with existing skills: {conflict_text}")
                    pending_skill_conflicts.update(conflicts)
                    existing_skills.pop(skill_name, None)

                local_skill_dir = temp_package_dir / "skills" / skill_name
                local_skill_dir.mkdir(parents=True, exist_ok=True)
                copy_directory_contents(upstream_skill_dir, local_skill_dir, skip_names={"SKILL.md"})

                manifest, compatibility = resolve_skill_manifest(plugin_config, skill_name, normalize_text(plugin_manifest["version"]))
                validate_skill_manifest(local_skill_dir / "manifest.json", manifest)
                (local_skill_dir / "manifest.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

                source_path = upstream_skill_md.relative_to(ROOT / "upstreams").as_posix()
                rendered_skill = render_skill_markdown(skill_name, description, compatibility, body, f"upstreams/{source_path}")
                (local_skill_dir / "SKILL.md").write_text(rendered_skill, encoding="utf-8")

                skill_names.append(skill_name)
                imported_skill_count += 1

            agents_root = plugin_dir / "agents"
            if agents_root.is_dir():
                for upstream_agent_md in sorted(agents_root.glob("*.agent.md")):
                    metadata, body = parse_markdown_frontmatter(upstream_agent_md)
                    agent_name = normalize_text(metadata.get("name"))
                    description = normalize_text(metadata.get("description"))
                    if not agent_name or not description:
                        raise ValueError(f"{upstream_agent_md} must define non-empty name and description")

                    conflicts = existing_agents.get(agent_name, [])
                    if conflicts:
                        if not config.get("replaceAgentConflicts", False):
                            conflict_text = ", ".join(str(path) for path in conflicts)
                            raise ValueError(f"Imported agent {agent_name!r} conflicts with existing agents: {conflict_text}")
                        pending_agent_conflicts.update(conflicts)
                        existing_agents.pop(agent_name, None)

                    local_agent_dir = temp_package_dir / "agents" / agent_name
                    local_agent_dir.mkdir(parents=True, exist_ok=True)
                    source_path = upstream_agent_md.relative_to(ROOT / "upstreams").as_posix()
                    rendered_agent = render_agent_markdown(agent_name, description, skill_names, body, f"upstreams/{source_path}")
                    (local_agent_dir / "AGENT.md").write_text(rendered_agent, encoding="utf-8")
                    imported_agent_count += 1

            for conflict_path in sorted(pending_skill_conflicts | pending_agent_conflicts):
                remove_path(conflict_path.parent)
                removed_conflicts += 1

            remove_path(package_dir)
            temp_package_dir.rename(package_dir)
        finally:
            remove_path(temp_package_dir)

    cleanup_empty_package_dirs()
    return {
        "skills": imported_skill_count,
        "agents": imported_agent_count,
        "removed_conflicts": removed_conflicts,
    }


def main() -> int:
    args = parse_args()

    config_paths = sorted(CONFIG_ROOT.glob("*.json"))
    if not config_paths:
        raise SystemExit("No catalog source configs were found under catalog-sources/")

    if args.validate_config:
        for path in config_paths:
            validate_config(path, load_json(path))
        print(f"Validated {len(config_paths)} external catalog source config(s).")
        return 0

    total_skills = 0
    total_agents = 0
    total_removed = 0
    for path in config_paths:
        summary = import_source(path, load_json(path))
        total_skills += summary["skills"]
        total_agents += summary["agents"]
        total_removed += summary["removed_conflicts"]

    print(f"Imported {total_skills} external skills and {total_agents} external agents.")
    print(f"Removed {total_removed} conflicting local entries.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
