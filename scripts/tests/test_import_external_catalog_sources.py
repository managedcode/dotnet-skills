from __future__ import annotations

import importlib.util
import json
import shutil
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch


REPO_ROOT = Path(__file__).resolve().parents[2]
MODULE_PATH = REPO_ROOT / "scripts" / "import_external_catalog_sources.py"
SPEC = importlib.util.spec_from_file_location("import_external_catalog_sources", MODULE_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Failed to load import_external_catalog_sources module from {MODULE_PATH}")
IMPORTER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(IMPORTER)


class ImportExternalCatalogSourcesTests(unittest.TestCase):
    def source_config(self) -> dict:
        return {
            "id": "dotnet-skills",
            "repository": "https://github.com/dotnet/skills",
            "sourceRoot": "upstreams/dotnet-skills",
            "docsBase": "https://github.com/dotnet/skills/tree/main/plugins",
            "docsRoot": "https://github.com/dotnet/skills/tree/main",
            "standaloneVersionFile": "plugins/dotnet/plugin.json",
            "titlePrefix": "Official .NET skills",
            "managedPackagePrefix": "Official-DotNet",
            "replaceSkillConflicts": True,
            "pluginDefaults": {
                "type": "Platform", "category": "Core", "compatibility": "Requires .NET.",
            },
            "pluginOverrides": {
                "dotnet-blazor": {"type": "Frameworks", "category": "Web"},
            },
        }

    def write_json(self, path: Path, payload: dict) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

    def write_skill(self, plugin_dir: Path, skill_name: str, title: str) -> None:
        skill_dir = plugin_dir / "skills" / skill_name
        skill_dir.mkdir(parents=True, exist_ok=True)
        skill_dir.joinpath("SKILL.md").write_text(
            "\n".join(
                [
                    "---",
                    f"name: {skill_name}",
                    f'description: "{title}."',
                    "---",
                    "",
                    f"# {title}",
                    "",
                ]
            ),
            encoding="utf-8",
        )

    def test_parse_frontmatter_allows_comments_after_folded_block(self) -> None:
        with tempfile.TemporaryDirectory() as temp_root_value:
            skill_path = Path(temp_root_value) / "SKILL.md"
            skill_path.write_text(
                "\n".join(
                    [
                        "---",
                        "name: find-untested-sources",
                        "description: >",
                        "  Parse source files and tests.",
                        "  Emit JSON output.",
                        "# Kept out of default model menus but still invocable by name.",
                        "disable-model-invocation: true",
                        "---",
                        "",
                        "# Find Untested Sources",
                        "",
                    ]
                ),
                encoding="utf-8",
            )

            metadata, body = IMPORTER.parse_markdown_frontmatter(skill_path)

        self.assertEqual(metadata["name"], "find-untested-sources")
        self.assertEqual(metadata["description"], "Parse source files and tests. Emit JSON output.")
        self.assertEqual(metadata["disable-model-invocation"], "true")
        self.assertIn("# Find Untested Sources", body)

    def test_parse_frontmatter_keeps_nested_metadata_opaque(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "SKILL.md"
            source = """---
name: real-skill
metadata:
  name: must-not-override-name
  category: must-not-become-catalog-metadata
  nested:
    flags:
      - portable
description: A portable skill.
---
# Real skill
"""
            path.write_text(source)
            metadata, _ = IMPORTER.parse_markdown_frontmatter(path)
            self.assertEqual({"name": "real-skill", "description": "A portable skill."}, metadata)
            self.assertEqual(source, path.read_text())

    def test_combined_source_automatically_discovers_refreshes_and_removes_entries(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            external = root / "external-sources"
            source = external / "upstreams" / "dotnet-skills"
            config_path = external / "imports" / "dotnet-skills.json"
            config = self.source_config()
            for plugin in ("dotnet", "dotnet-blazor"):
                directory = source / "plugins" / plugin
                self.write_json(directory / "plugin.json", {
                    "name": plugin, "version": "1.2.3", "skills": ["./skills/"],
                })
                self.write_skill(directory, f"{plugin}-first", "First skill")
            for host, skill in ((".agents", "create-skill"), (".github", "agentic-workflows")):
                self.write_skill(source / host, skill, "Standalone skill")
            self.write_skill(source / "eng" / "fixtures", "sample-skill", "Fixture only")
            reference = source / "plugins" / "dotnet-blazor" / "skills" / "dotnet-blazor-first" / "references" / "usage.md"
            reference.parent.mkdir()
            reference.write_bytes(b"# Original upstream reference\r\n")

            with (
                patch.object(IMPORTER, "ROOT", root),
                patch.object(IMPORTER, "CATALOG_ROOT", root / "catalog"),
                patch.object(IMPORTER, "EXTERNAL_SOURCES_ROOT", external),
            ):
                first = IMPORTER.import_source(config_path, config)
                self.assertEqual(4, first["skills"])
                blazor = root / "catalog" / "Frameworks" / "Official-DotNet-Blazor"
                standalone = root / "catalog" / "Platform" / "Official-DotNet-Create-Skill"
                self.assertEqual(reference.read_bytes(), (blazor / "skills" / "dotnet-blazor-first" / "references" / "usage.md").read_bytes())
                self.assertEqual("1.2.3", IMPORTER.load_json(standalone / "skills" / "create-skill" / "manifest.json")["version"])
                self.assertEqual(config["docsRoot"] + "/.agents/skills/create-skill", IMPORTER.load_json(standalone / "manifest.json")["links"]["docs"])
                self.assertEqual(config["docsRoot"] + "/plugins/dotnet-blazor", IMPORTER.load_json(blazor / "manifest.json")["links"]["docs"])

                # Neither a new plugin nor a new task needs a local registry entry.
                new_plugin = source / "plugins" / "dotnet-future"
                self.write_json(new_plugin / "plugin.json", {
                    "name": "dotnet-future", "version": "2.0.0", "skills": "./skills/",
                })
                self.write_skill(new_plugin, "future-skill", "New plugin skill")
                self.write_skill(source / "plugins" / "dotnet-blazor", "new-blazor-task", "New Blazor task")
                second = IMPORTER.import_source(config_path, config)
                self.assertEqual(6, second["skills"])
                self.assertTrue((root / "catalog" / "Platform" / "Official-DotNet-Future" / "skills" / "future-skill" / "SKILL.md").is_file())
                self.assertEqual("Web", IMPORTER.load_json(blazor / "skills" / "new-blazor-task" / "manifest.json")["category"])

                # Updates must copy even when an upstream plugin version stays the same.
                self.write_skill(source / "plugins" / "dotnet-blazor", "dotnet-blazor-first", "Updated guidance")
                reference.write_bytes(b"# Updated upstream reference\n")
                shutil.rmtree(new_plugin)
                shutil.rmtree(source / ".agents" / "skills" / "create-skill")
                third = IMPORTER.import_source(config_path, config)
                self.assertEqual(4, third["skills"])
                self.assertFalse((root / "catalog" / "Platform" / "Official-DotNet-Future").exists())
                self.assertFalse(standalone.exists())
                self.assertEqual(reference.read_bytes(), (blazor / "skills" / "dotnet-blazor-first" / "references" / "usage.md").read_bytes())
                upstream_skill = reference.parents[1] / "SKILL.md"
                self.assertEqual(upstream_skill.read_bytes(), (blazor / "skills" / "dotnet-blazor-first" / "SKILL.md").read_bytes())
                self.assertFalse(list((root / "catalog").rglob("sample-skill")))

    def test_standalone_version_rejects_missing_or_placeholder_metadata(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            with self.assertRaisesRegex(ValueError, "does not exist"):
                IMPORTER.discover_repository_version(root, "plugin.json")
            for invalid in (None, "", "0.0.0"):
                self.write_json(root / "plugin.json", {"version": invalid})
                with self.subTest(version=invalid), self.assertRaisesRegex(ValueError, "non-placeholder"):
                    IMPORTER.discover_repository_version(root, "plugin.json")

    def test_official_catalog_covers_every_real_upstream_skill_verbatim(self) -> None:
        source = REPO_ROOT / "external-sources" / "upstreams" / "dotnet-skills"
        upstream_paths = [
            *source.glob("plugins/*/skills/**/SKILL.md"),
            *source.glob(".agents/skills/*/SKILL.md"),
            *source.glob(".github/skills/*/SKILL.md"),
        ]
        expected = {IMPORTER.parse_markdown_frontmatter(path)[0]["name"]: path for path in upstream_paths}
        imported_paths = list((REPO_ROOT / "catalog").glob("*/Official-DotNet*/skills/*/SKILL.md"))
        actual = {IMPORTER.parse_markdown_frontmatter(path)[0]["name"]: path for path in imported_paths}
        self.assertTrue(expected)
        self.assertEqual(len(upstream_paths), len(expected), "Duplicate upstream skill names")
        self.assertEqual(len(imported_paths), len(actual), "Duplicate imported skill names")
        self.assertEqual(set(expected), set(actual))
        for name, upstream in expected.items():
            with self.subTest(skill=name):
                self.assertEqual(upstream.read_bytes(), actual[name].read_bytes())
                for support_file in upstream.parent.rglob("*"):
                    if support_file.is_file() and support_file.name != "manifest.json":
                        imported_file = actual[name].parent / support_file.relative_to(upstream.parent)
                        self.assertTrue(imported_file.is_file(), str(imported_file.relative_to(REPO_ROOT)))
                        self.assertEqual(support_file.read_bytes(), imported_file.read_bytes())

    def test_import_source_skips_excluded_skill_overrides(self) -> None:
        with tempfile.TemporaryDirectory() as temp_root_value:
            temp_root = Path(temp_root_value)
            catalog_root = temp_root / "catalog"
            external_root = temp_root / "external-sources"
            config_root = external_root / "imports"
            plugin_dir = external_root / "upstreams" / "dotnet-skills" / "dotnet-ai"

            self.write_json(
                plugin_dir / "plugin.json",
                {
                    "name": "dotnet-ai",
                    "version": "0.1.0",
                    "description": "AI and MCP skills.",
                    "skills": ["./skills/"],
                },
            )
            self.write_skill(plugin_dir, "mcp", "MCP C# SDK for .NET")
            self.write_skill(plugin_dir, "mcp-csharp-create", "C# MCP Server Creation")

            config_path = config_root / "dotnet-skills.json"
            config = {
                "id": "dotnet-skills",
                "repository": "https://github.com/dotnet/skills",
                "sourceRoot": "upstreams/dotnet-skills",
                "docsBase": "https://github.com/dotnet/skills/tree/main/plugins",
                "titlePrefix": "Official .NET skills",
                "managedPackagePrefix": "Official-DotNet",
                "pluginDefaults": {
                    "type": "Platform",
                    "category": "AI",
                    "compatibility": "Requires a .NET repository working with AI, ML, or MCP workloads.",
                },
                "pluginOverrides": {
                    "dotnet-ai": {
                        "skillOverrides": {
                            "mcp-csharp-create": {
                                "exclude": True,
                            }
                        }
                    }
                },
            }

            with (
                patch.object(IMPORTER, "ROOT", temp_root),
                patch.object(IMPORTER, "CATALOG_ROOT", catalog_root),
                patch.object(IMPORTER, "EXTERNAL_SOURCES_ROOT", external_root),
                patch.object(IMPORTER, "CONFIG_ROOT", config_root),
            ):
                summary = IMPORTER.import_source(config_path, config)

            self.assertEqual(summary["skills"], 1)

            package_root = catalog_root / "Platform" / "Official-DotNet-AI" / "skills"
            self.assertTrue((package_root / "mcp" / "SKILL.md").is_file())
            self.assertFalse((package_root / "mcp-csharp-create").exists())

    def test_import_source_supports_standard_claude_plugin_layout(self) -> None:
        with tempfile.TemporaryDirectory() as temp_root_value:
            temp_root = Path(temp_root_value)
            catalog_root = temp_root / "catalog"
            external_root = temp_root / "external-sources"
            config_root = external_root / "imports"
            plugin_dir = external_root / "upstreams" / "webgpu-claude-skill"

            self.write_json(
                plugin_dir / ".claude-plugin" / "plugin.json",
                {
                    "name": "webgpu-threejs-tsl",
                    "version": "1.0.0",
                    "description": "WebGPU-enabled Three.js development with TSL.",
                    "skills": "./skills/",
                },
            )
            self.write_skill(plugin_dir, "webgpu-threejs-tsl", "WebGPU Three.js with TSL")

            config_path = config_root / "webgpu-claude-skill.json"
            config = {
                "id": "webgpu-claude-skill",
                "repository": "https://github.com/dgreenheck/webgpu-claude-skill",
                "sourceRoot": "upstreams/webgpu-claude-skill",
                "docsBase": "https://github.com/dgreenheck/webgpu-claude-skill/tree/main/skills",
                "titlePrefix": "Three.js skills",
                "managedPackagePrefix": "ThreeJS",
                "pluginDefaults": {
                    "type": "Frameworks",
                    "category": "Web",
                    "compatibility": "Requires a JavaScript or TypeScript project using Three.js WebGPU.",
                },
                "pluginOverrides": {
                    "webgpu-threejs-tsl": {
                        "package": "ThreeJS-WebGPU-TSL",
                        "title": "WebGPU Three.js with TSL",
                        "skillDefaults": {
                            "packages": ["three"],
                        },
                    }
                },
            }

            with (
                patch.object(IMPORTER, "ROOT", temp_root),
                patch.object(IMPORTER, "CATALOG_ROOT", catalog_root),
                patch.object(IMPORTER, "EXTERNAL_SOURCES_ROOT", external_root),
                patch.object(IMPORTER, "CONFIG_ROOT", config_root),
            ):
                summary = IMPORTER.import_source(config_path, config)

            self.assertEqual(summary["skills"], 1)
            imported_skill = catalog_root / "Frameworks" / "ThreeJS-WebGPU-TSL" / "skills" / "webgpu-threejs-tsl"
            self.assertTrue((imported_skill / "SKILL.md").is_file())
            self.assertEqual(
                json.loads((imported_skill / "manifest.json").read_text(encoding="utf-8"))["packages"],
                ["three"],
            )

    def test_discovery_prefers_flat_manifest_over_duplicate_claude_manifest(self) -> None:
        with tempfile.TemporaryDirectory() as temp_root_value:
            source_root = Path(temp_root_value)
            plugin_dir = source_root / "dotnet-msbuild"
            flat_manifest = {
                "name": "dotnet-msbuild",
                "version": "1.0.0",
                "description": "Flat manifest.",
                "skills": ["./skills/"],
            }
            nested_manifest = {
                **flat_manifest,
                "description": "Nested compatibility manifest.",
            }
            self.write_json(plugin_dir / "plugin.json", flat_manifest)
            self.write_json(plugin_dir / ".claude-plugin" / "plugin.json", nested_manifest)

            plugins = IMPORTER.discover_upstream_plugins(source_root)

            self.assertEqual(list(plugins), ["dotnet-msbuild"])
            self.assertEqual(plugins["dotnet-msbuild"][1]["description"], "Flat manifest.")

    def test_import_source_supports_canonical_agents_skills_layout(self) -> None:
        with tempfile.TemporaryDirectory() as temp_root_value:
            temp_root = Path(temp_root_value)
            catalog_root = temp_root / "catalog"
            external_root = temp_root / "external-sources"
            config_root = external_root / "imports"
            source_root = external_root / "upstreams" / "astro"
            skill_dir = source_root / ".agents" / "skills" / "astro-developer"

            skill_dir.mkdir(parents=True, exist_ok=True)
            skill_dir.joinpath("SKILL.md").write_text(
                "\n".join(
                    [
                        "---",
                        "name: astro-developer",
                        'description: "Develop features and fixes in the Astro monorepo."',
                        "---",
                        "",
                        "# Astro Developer",
                        "",
                    ]
                ),
                encoding="utf-8",
            )
            skill_dir.joinpath("architecture.md").write_text("# Architecture\n", encoding="utf-8")
            self.write_json(
                source_root / "packages" / "astro" / "package.json",
                {
                    "name": "astro",
                    "version": "7.1.3",
                },
            )

            config_path = config_root / "astro.json"
            config = {
                "id": "astro",
                "repository": "https://github.com/withastro/astro",
                "sourceRoot": "upstreams/astro",
                "docsBase": "https://github.com/withastro/astro/tree/main/.agents/skills",
                "titlePrefix": "Official Astro skills",
                "managedPackagePrefix": "Official-Astro",
                "pluginDefaults": {
                    "type": "Frameworks",
                    "category": "Web",
                    "compatibility": "Requires the withastro/astro monorepo.",
                },
                "pluginOverrides": {
                    "astro-developer": {
                        "package": "Official-Astro",
                        "title": "Official Astro: Astro Developer",
                    }
                },
            }

            with (
                patch.object(IMPORTER, "ROOT", temp_root),
                patch.object(IMPORTER, "CATALOG_ROOT", catalog_root),
                patch.object(IMPORTER, "EXTERNAL_SOURCES_ROOT", external_root),
                patch.object(IMPORTER, "CONFIG_ROOT", config_root),
            ):
                summary = IMPORTER.import_source(config_path, config)

            self.assertEqual(summary["skills"], 1)
            imported_skill = catalog_root / "Frameworks" / "Official-Astro" / "skills" / "astro-developer"
            self.assertEqual(
                (imported_skill / "SKILL.md").read_text(encoding="utf-8"),
                (skill_dir / "SKILL.md").read_text(encoding="utf-8"),
            )
            self.assertTrue((imported_skill / "architecture.md").is_file())
            self.assertEqual(
                json.loads((imported_skill / "manifest.json").read_text(encoding="utf-8"))["version"],
                "7.1.3",
            )


if __name__ == "__main__":
    unittest.main()
