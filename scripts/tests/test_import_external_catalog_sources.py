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


if __name__ == "__main__":
    unittest.main()
