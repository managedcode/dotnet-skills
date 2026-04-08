#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
import shutil
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CATALOG_ROOT = ROOT / "catalog"
EXTERNAL_SOURCES_ROOT = ROOT / "external-sources"
CONFIG_ROOT = EXTERNAL_SOURCES_ROOT / "imports"

ALLOWED_TYPES = {"Frameworks", "Libraries", "Tools", "Testing", "Platform"}
ALLOWED_SKILL_MANIFEST_KEYS = {"version", "category", "compatibility", "packages", "package_prefix"}
ALLOWED_PLUGIN_DEFAULT_KEYS = {"type", "category", "compatibility"}
ALLOWED_PLUGIN_OVERRIDE_KEYS = {
    "type",
    "package",
    "title",
    "category",
    "compatibility",
    "skillDefaults",
    "skillOverrides",
}

TOKEN_CASE_OVERRIDES = {
    "ai": "AI",
    "aspnet": "ASPNet",
    "dotnet": "DotNet",
    "maui": "MAUI",
    "msbuild": "MSBuild",
    "nuget": "NuGet",
}


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


def format_slug_token(value: str) -> str:
    token = value.strip()
    if not token:
        return token

    lowered = token.lower()
    if lowered in TOKEN_CASE_OVERRIDES:
        return TOKEN_CASE_OVERRIDES[lowered]

    return token.capitalize()


def titleize_slug(value: str) -> str:
    words = [part for part in re.split(r"[-_]+", value.strip()) if part]
    return " ".join(format_slug_token(word) for word in words) or value


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


def resolve_source_root(source_root_value: str) -> Path:
    return EXTERNAL_SOURCES_ROOT / source_root_value


def discover_upstream_plugins(source_root: Path) -> dict[str, tuple[Path, dict]]:
    plugins: dict[str, tuple[Path, dict]] = {}

    for plugin_manifest_path in sorted(source_root.glob("*/plugin.json")):
        plugin_dir = plugin_manifest_path.parent
        plugin_manifest = load_json(plugin_manifest_path)
        plugin_name = normalize_text(plugin_manifest.get("name", plugin_dir.name))

        if not plugin_name:
            raise ValueError(f"{plugin_manifest_path} must define a non-empty plugin name")
        if plugin_name != plugin_dir.name:
            raise ValueError(f"{plugin_manifest_path} name {plugin_name!r} must match directory name {plugin_dir.name!r}")
        if plugin_name in plugins:
            raise ValueError(f"Duplicate upstream plugin name detected: {plugin_name}")

        plugins[plugin_name] = (plugin_dir, plugin_manifest)

    if not plugins:
        raise ValueError(f"No upstream plugin.json files were found under {source_root}")

    return plugins


def validate_scalar_string(config_path: Path, field_name: str, value: object) -> str:
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{config_path} must define a non-empty {field_name}")
    return value


def validate_plugin_metadata_block(config_path: Path, plugin_name: str, block_name: str, block: object) -> None:
    if block is None:
        return
    if not isinstance(block, dict):
        raise ValueError(f"{config_path} plugin {plugin_name!r} field {block_name} must be an object")

    if block_name == "skillDefaults":
        unknown = sorted(set(block) - (ALLOWED_SKILL_MANIFEST_KEYS - {"version", "category"}))
        if unknown:
            raise ValueError(f"{config_path} plugin {plugin_name!r} field skillDefaults has unsupported keys: {', '.join(unknown)}")
        return

    for skill_name, skill_override in block.items():
        if not isinstance(skill_override, dict):
            raise ValueError(f"{config_path} plugin {plugin_name!r} skill override {skill_name!r} must be an object")
        unknown = sorted(set(skill_override) - ALLOWED_SKILL_MANIFEST_KEYS - {"compatibility"})
        if unknown:
            raise ValueError(
                f"{config_path} plugin {plugin_name!r} skill override {skill_name!r} has unsupported keys: {', '.join(unknown)}"
            )


def validate_plugin_defaults(config_path: Path, defaults: object) -> None:
    if defaults is None:
        return
    if not isinstance(defaults, dict):
        raise ValueError(f"{config_path} field pluginDefaults must be an object")

    unknown = sorted(set(defaults) - ALLOWED_PLUGIN_DEFAULT_KEYS)
    if unknown:
        raise ValueError(f"{config_path} field pluginDefaults has unsupported keys: {', '.join(unknown)}")

    package_type = defaults.get("type")
    if package_type is not None and package_type not in ALLOWED_TYPES:
        raise ValueError(f"{config_path} field pluginDefaults.type has unsupported type {package_type!r}")

    for field_name in ("category", "compatibility"):
        value = defaults.get(field_name)
        if value is not None and (not isinstance(value, str) or not value.strip()):
            raise ValueError(f"{config_path} field pluginDefaults.{field_name} must be a non-empty string")


def validate_plugin_override(config_path: Path, plugin_name: str, override: object) -> None:
    if not isinstance(override, dict):
        raise ValueError(f"{config_path} plugin override {plugin_name!r} must be an object")

    unknown = sorted(set(override) - ALLOWED_PLUGIN_OVERRIDE_KEYS)
    if unknown:
        raise ValueError(f"{config_path} plugin override {plugin_name!r} has unsupported keys: {', '.join(unknown)}")

    package_type = override.get("type")
    if package_type is not None and package_type not in ALLOWED_TYPES:
        raise ValueError(f"{config_path} plugin override {plugin_name!r} has unsupported type {package_type!r}")

    for field_name in ("package", "title", "category", "compatibility"):
        value = override.get(field_name)
        if value is not None and (not isinstance(value, str) or not value.strip()):
            raise ValueError(f"{config_path} plugin override {plugin_name!r} field {field_name} must be a non-empty string")

    validate_plugin_metadata_block(config_path, plugin_name, "skillDefaults", override.get("skillDefaults"))
    validate_plugin_metadata_block(config_path, plugin_name, "skillOverrides", override.get("skillOverrides"))


def format_package_suffix(value: str) -> str:
    parts = [part for part in re.split(r"[-_]+", value.strip()) if part]
    return "-".join(format_slug_token(part) for part in parts) or titleize_slug(value).replace(" ", "-")


def derive_package_name(managed_prefix: str, plugin_name: str) -> str:
    normalized_plugin_name = plugin_name.strip().lower()
    prefix_tail = re.split(r"[-_]+", managed_prefix.strip())[-1].lower()

    if normalized_plugin_name == prefix_tail:
        return managed_prefix

    if normalized_plugin_name.startswith(f"{prefix_tail}-"):
        suffix = plugin_name[len(prefix_tail) + 1 :]
    else:
        suffix = plugin_name

    return f"{managed_prefix}-{format_package_suffix(suffix)}"


def resolve_plugin_policy(config_path: Path, config: dict, plugin_name: str) -> dict[str, object]:
    managed_prefix = str(config["managedPackagePrefix"])
    title_prefix = str(config["titlePrefix"])
    plugin_defaults = config.get("pluginDefaults", {})
    plugin_override = config.get("pluginOverrides", {}).get(plugin_name, {})

    validate_plugin_override(config_path, plugin_name, plugin_override)

    policy: dict[str, object] = {
        "package": derive_package_name(managed_prefix, plugin_name),
        "title": f"{title_prefix}: {plugin_name}",
    }

    if isinstance(plugin_defaults, dict):
        for key in ("type", "category", "compatibility"):
            value = plugin_defaults.get(key)
            if value is not None:
                policy[key] = value

    if isinstance(plugin_override, dict):
        for key in ("type", "package", "title", "category", "compatibility", "skillDefaults", "skillOverrides"):
            value = plugin_override.get(key)
            if value is not None:
                policy[key] = value

    package_type = policy.get("type")
    if package_type not in ALLOWED_TYPES:
        raise ValueError(
            f"{config_path} plugin {plugin_name!r} must resolve to a supported type. "
            "Set pluginDefaults.type or pluginOverrides.<plugin>.type."
        )

    for field_name in ("package", "title", "category", "compatibility"):
        value = policy.get(field_name)
        if not isinstance(value, str) or not value.strip():
            raise ValueError(f"{config_path} plugin {plugin_name!r} must resolve a non-empty {field_name}")

    validate_plugin_metadata_block(config_path, plugin_name, "skillDefaults", policy.get("skillDefaults"))
    validate_plugin_metadata_block(config_path, plugin_name, "skillOverrides", policy.get("skillOverrides"))
    return policy


def validate_config(config_path: Path, config: dict) -> None:
    validate_scalar_string(config_path, "id", config.get("id"))
    validate_scalar_string(config_path, "repository", config.get("repository"))
    source_root_value = validate_scalar_string(config_path, "sourceRoot", config.get("sourceRoot"))
    validate_scalar_string(config_path, "docsBase", config.get("docsBase"))
    validate_scalar_string(config_path, "managedPackagePrefix", config.get("managedPackagePrefix"))
    validate_scalar_string(config_path, "titlePrefix", config.get("titlePrefix"))

    plugin_root = resolve_source_root(source_root_value)
    if not plugin_root.is_dir():
        raise ValueError(f"{config_path} points to a missing vendored source root: {plugin_root}")

    validate_plugin_defaults(config_path, config.get("pluginDefaults", {}))

    overrides = config.get("pluginOverrides", {})
    if overrides is None:
        overrides = {}
    if not isinstance(overrides, dict):
        raise ValueError(f"{config_path} field pluginOverrides must be an object")

    plugins = discover_upstream_plugins(plugin_root)
    unknown_override_names = sorted(set(overrides) - set(plugins))
    if unknown_override_names:
        raise ValueError(
            f"{config_path} declares overrides for unknown upstream plugins: {', '.join(unknown_override_names)}"
        )

    for plugin_name in sorted(overrides):
        validate_plugin_override(config_path, plugin_name, overrides[plugin_name])

    for plugin_name in sorted(plugins):
        resolve_plugin_policy(config_path, config, plugin_name)


def resolve_skill_manifest(plugin_policy: dict, skill_name: str, plugin_version: str) -> dict[str, object]:
    manifest: dict[str, object] = {
        "version": plugin_version,
        "category": plugin_policy["category"],
        "compatibility": normalize_text(plugin_policy["compatibility"]),
    }

    skill_defaults = plugin_policy.get("skillDefaults", {})
    if isinstance(skill_defaults, dict):
        for key in ("packages", "package_prefix"):
            if key in skill_defaults:
                manifest[key] = skill_defaults[key]

    skill_overrides = plugin_policy.get("skillOverrides", {})
    if isinstance(skill_overrides, dict) and isinstance(skill_overrides.get(skill_name), dict):
        override = skill_overrides[skill_name]
        for key in ("version", "category", "compatibility", "packages", "package_prefix"):
            if key in override:
                manifest[key] = override[key]
    return manifest


def validate_skill_manifest(manifest_path: Path, manifest: dict[str, object]) -> None:
    version = manifest.get("version")
    category = manifest.get("category")
    compatibility = manifest.get("compatibility")
    if not isinstance(version, str) or not version.strip():
        raise ValueError(f"{manifest_path} field version must be a non-empty string")
    if not isinstance(category, str) or not category.strip():
        raise ValueError(f"{manifest_path} field category must be a non-empty string")
    if not isinstance(compatibility, str) or not compatibility.strip():
        raise ValueError(f"{manifest_path} field compatibility must be a non-empty string")

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
        package_dir = path.parents[2]
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


def expand_plugin_entries(plugin_dir: Path, entries: object, *, pattern: str, entry_label: str) -> list[Path]:
    if entries is None:
        return []
    if not isinstance(entries, list) or any(not isinstance(entry, str) or not entry.strip() for entry in entries):
        raise ValueError(f"{plugin_dir / 'plugin.json'} field {entry_label} must be a list of non-empty strings")

    discovered: list[Path] = []
    for entry in entries:
        entry_path = plugin_dir / entry
        if entry_path.is_dir():
            discovered.extend(sorted(path for path in entry_path.rglob(pattern) if path.is_file()))
            continue
        if entry_path.is_file():
            discovered.append(entry_path)
            continue
        raise ValueError(f"{plugin_dir / 'plugin.json'} points to a missing {entry_label} path: {entry_path}")

    unique_paths: list[Path] = []
    seen: set[Path] = set()
    for path in discovered:
        if path in seen:
            continue
        seen.add(path)
        unique_paths.append(path)
    return unique_paths


def import_source(config_path: Path, config: dict) -> dict[str, int]:
    validate_config(config_path, config)

    plugin_root = resolve_source_root(str(config["sourceRoot"]))
    docs_base = str(config["docsBase"]).rstrip("/")
    repository = str(config["repository"]).rstrip("/")
    managed_prefix = str(config["managedPackagePrefix"])
    plugins = discover_upstream_plugins(plugin_root)
    resolved_policies = {plugin_name: resolve_plugin_policy(config_path, config, plugin_name) for plugin_name in plugins}

    target_package_dirs = {
        CATALOG_ROOT / str(policy["type"]) / str(policy["package"])
        for policy in resolved_policies.values()
    }

    existing_skills = collect_existing_entries("skill", target_package_dirs)
    existing_agents = collect_existing_entries("agent", target_package_dirs)

    if config.get("replaceSkillConflicts", False):
        managed_names = {
            path.parent.name
            for path in CATALOG_ROOT.glob("*/*")
            if path.is_dir() and path.name.startswith(managed_prefix)
        }
        for package_dir in CATALOG_ROOT.glob("*/*"):
            if package_dir.is_dir() and package_dir.name in managed_names:
                remove_path(package_dir)

    imported_skill_count = 0
    imported_agent_count = 0
    removed_conflicts = 0

    for plugin_name, (plugin_dir, plugin_manifest) in sorted(plugins.items()):
        plugin_policy = resolved_policies[plugin_name]
        package_dir = CATALOG_ROOT / str(plugin_policy["type"]) / str(plugin_policy["package"])
        temp_package_dir = package_dir.parent / f".{package_dir.name}.tmp-import"
        pending_skill_conflicts: set[Path] = set()
        pending_agent_conflicts: set[Path] = set()

        remove_path(temp_package_dir)
        try:
            (temp_package_dir / "skills").mkdir(parents=True, exist_ok=True)

            package_manifest = {
                "name": plugin_name,
                "title": plugin_policy["title"],
                "description": normalize_text(plugin_manifest.get("description", "")),
                "links": {
                    "repository": repository,
                    "docs": f"{docs_base}/{plugin_name}",
                },
            }
            (temp_package_dir / "manifest.json").write_text(json.dumps(package_manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

            skill_paths = expand_plugin_entries(plugin_dir, plugin_manifest.get("skills"), pattern="SKILL.md", entry_label="skills")
            for upstream_skill_md in skill_paths:
                upstream_skill_dir = upstream_skill_md.parent
                metadata, _ = parse_markdown_frontmatter(upstream_skill_md)
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

                manifest = resolve_skill_manifest(plugin_policy, skill_name, normalize_text(plugin_manifest["version"]))
                validate_skill_manifest(local_skill_dir / "manifest.json", manifest)
                (local_skill_dir / "manifest.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

                shutil.copy2(upstream_skill_md, local_skill_dir / "SKILL.md")

                imported_skill_count += 1

            agent_paths = expand_plugin_entries(plugin_dir, plugin_manifest.get("agents", []), pattern="*.agent.md", entry_label="agents")
            for upstream_agent_md in agent_paths:
                metadata, _ = parse_markdown_frontmatter(upstream_agent_md)
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
                shutil.copy2(upstream_agent_md, local_agent_dir / "AGENT.md")
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
        raise SystemExit("No external catalog source configs were found under external-sources/imports/")

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
