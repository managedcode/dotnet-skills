from __future__ import annotations

import subprocess
import sys
import unittest
from unittest.mock import patch
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
README_PATH = ROOT / "README.md"
GENERATOR_PATH = ROOT / "scripts" / "generate_catalog.py"
CATALOG_URL = "https://skills.managed-code.com/skills/"
sys.path.insert(0, str(ROOT / "scripts"))
import generate_catalog
from catalog_index import catalog_statistics


class GenerateCatalogTests(unittest.TestCase):
    def test_statistics_deduplicate_nuget_ids_and_exclude_npm_and_prefixes(self) -> None:
        skills = [
            {"packages": ["MSTest", "MSTest.TestFramework"], "package_prefix": "Microsoft."},
            {"packages": ["mstest", "TUnit"]},
            {"packages": ["TUnit", "@playwright/test"], "package_ecosystem": "npm"},
            {"package_prefix": "Avalonia."},
        ]
        self.assertEqual({"skills": 4, "nuget_packages": 3}, catalog_statistics(skills))

    def test_readme_refresh_is_bounded_and_idempotent(self) -> None:
        original = README_PATH.read_text(encoding="utf-8")
        skills = [{"packages": ["TUnit"]}, {"packages": ["tunit"]}]
        updated = generate_catalog.render_readme_statistics(original, skills)
        self.assertIn("skills-2-blue", updated)
        self.assertIn("**2 skills make dotnet-skills", updated)
        self.assertIn("**1 NuGet packages**", updated)
        self.assertEqual(original.split("<!-- END CATALOG SUMMARY -->", 1)[1], updated.split("<!-- END CATALOG SUMMARY -->", 1)[1])
        self.assertEqual(updated, generate_catalog.render_readme_statistics(updated, skills))

    def test_readme_refresh_rejects_missing_or_duplicate_summary_markers(self) -> None:
        original = README_PATH.read_text(encoding="utf-8")
        for invalid in [original.replace("<!-- BEGIN CATALOG SUMMARY -->", ""), original + original]:
            with self.subTest(source=invalid[:20]):
                with self.assertRaises(ValueError):
                    generate_catalog.render_readme_statistics(invalid, [])

    def test_readme_update_uses_scanned_skills_without_tokenizer(self) -> None:
        args = generate_catalog.argparse.Namespace(check=False, validate_only=False, update_readme=True, manifest_output=None)
        skills = [{"packages": ["TUnit"]}]
        original = README_PATH.read_text(encoding="utf-8")
        with patch.object(generate_catalog, "parse_args", return_value=args), patch.object(generate_catalog, "collect_skills", return_value=skills) as scan, patch.object(generate_catalog, "ROOT") as root:
            readme_path = root.__truediv__.return_value
            readme_path.read_text.return_value = original
            self.assertEqual(0, generate_catalog.main())
            readme_path.write_text.assert_called_once_with(generate_catalog.render_readme_statistics(original, skills), encoding="utf-8")
        scan.assert_called_once_with(include_token_counts=False)

    def test_release_refreshes_readme_before_build_and_publishes_the_updated_commit(self) -> None:
        workflow = (ROOT / ".github/workflows/publish-catalog.yml").read_text(encoding="utf-8")
        self.assertLess(workflow.index("python3 scripts/generate_catalog.py --update-readme"), workflow.index("- name: Build dotnet tool"))
        self.assertIn("source_sha: ${{ steps.readme.outputs.source_sha }}", workflow)
        self.assertIn('--target-commit "${{ steps.readme.outputs.source_sha }}"', workflow)
        self.assertIn('--target "${{ steps.readme.outputs.source_sha }}"', workflow)
        self.assertIn("ref: ${{ needs.release.outputs.source_sha }}", workflow)

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
