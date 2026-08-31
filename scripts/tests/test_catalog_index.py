from __future__ import annotations

import importlib.util
import tempfile
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
MODULE_PATH = REPO_ROOT / "scripts" / "catalog_index.py"
SPEC = importlib.util.spec_from_file_location("catalog_index", MODULE_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Failed to load catalog_index from {MODULE_PATH}")
CATALOG_INDEX = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CATALOG_INDEX)


class CatalogIndexTests(unittest.TestCase):
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

            metadata, body = CATALOG_INDEX.parse_frontmatter(skill_path)

        self.assertEqual(metadata["name"], "find-untested-sources")
        self.assertEqual(metadata["description"], "Parse source files and tests. Emit JSON output.")
        self.assertEqual(metadata["disable-model-invocation"], "true")
        self.assertIn("# Find Untested Sources", body)

    def test_validate_skill_entrypoint_links_accepts_existing_relative_links(self) -> None:
        with tempfile.TemporaryDirectory() as temp_root_value:
            skill_dir = Path(temp_root_value)
            references_dir = skill_dir / "references"
            references_dir.mkdir()
            (references_dir / "commands.md").write_text("# Commands\n", encoding="utf-8")
            skill_path = skill_dir / "SKILL.md"

            CATALOG_INDEX.validate_skill_entrypoint_links(
                skill_path,
                "Read [commands](references/commands.md), [the docs](https://example.com), and [this section](#section).",
            )

    def test_validate_skill_entrypoint_links_rejects_missing_relative_links(self) -> None:
        with tempfile.TemporaryDirectory() as temp_root_value:
            skill_path = Path(temp_root_value) / "SKILL.md"

            with self.assertRaisesRegex(ValueError, r"missing relative path `references/missing\.md`"):
                CATALOG_INDEX.validate_skill_entrypoint_links(
                    skill_path,
                    "Read [missing commands](references/missing.md).",
                )


if __name__ == "__main__":
    unittest.main()
