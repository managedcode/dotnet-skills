from __future__ import annotations

import subprocess
import sys
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
README_PATH = ROOT / "README.md"
GENERATOR_PATH = ROOT / "scripts" / "generate_catalog.py"
CATALOG_URL = "https://skills.managed-code.com/skills/"


class GenerateCatalogTests(unittest.TestCase):
    def test_readme_points_to_site_without_embedded_catalog(self) -> None:
        readme = README_PATH.read_text(encoding="utf-8")

        self.assertIn(CATALOG_URL, readme)
        self.assertNotIn("<!-- BEGIN GENERATED CATALOG -->", readme)
        self.assertNotIn("<!-- END GENERATED CATALOG -->", readme)
        self.assertNotIn("| Skill | Version | Description |", readme)

    def test_default_generator_does_not_mutate_readme(self) -> None:
        before = README_PATH.read_bytes()

        completed = subprocess.run(
            [sys.executable, str(GENERATOR_PATH)],
            cwd=ROOT,
            check=False,
            capture_output=True,
            text=True,
        )

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn("Catalog metadata is valid", completed.stdout)
        self.assertEqual(before, README_PATH.read_bytes())


if __name__ == "__main__":
    unittest.main()
