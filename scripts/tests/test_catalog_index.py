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


if __name__ == "__main__":
    unittest.main()
