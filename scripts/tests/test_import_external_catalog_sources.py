from __future__ import annotations

import importlib.util
import json
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
