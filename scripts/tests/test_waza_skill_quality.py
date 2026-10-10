from __future__ import annotations

import importlib.util
import unittest
from pathlib import Path
from unittest.mock import patch


REPO_ROOT = Path(__file__).resolve().parents[2]
MODULE_PATH = REPO_ROOT / "scripts" / "waza_skill_quality.py"
SPEC = importlib.util.spec_from_file_location("waza_skill_quality", MODULE_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Failed to load waza_skill_quality module from {MODULE_PATH}")
WAZA_QUALITY = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(WAZA_QUALITY)


class WazaSkillQualityTests(unittest.TestCase):
    def test_imported_frontmatter_parser_error_is_reported_without_rewriting(self) -> None:
        failure = RuntimeError("Command failed (2)\nstderr:\nparsing SKILL.md: parsing frontmatter: invalid YAML")
        with patch.object(WAZA_QUALITY, "run_json", side_effect=failure):
            check = WAZA_QUALITY.run_check("waza", REPO_ROOT / "catalog/Tools/Roslynk/skills/roslynk")
        issues = WAZA_QUALITY.collect_issues(check, None)
        self.assertEqual("spec-frontmatter", issues[0]["code"])
        self.assertIn("invalid YAML", issues[0]["message"])

    def test_owned_parser_errors_and_imported_execution_errors_still_fail(self) -> None:
        for path, message in (
            ("catalog/Frameworks/Orleans/skills/orleans", "parsing frontmatter"),
            ("catalog/Tools/Roslynk/skills/roslynk", "Waza crashed"),
        ):
            with self.subTest(path=path), patch.object(WAZA_QUALITY, "run_json", side_effect=RuntimeError(message)):
                with self.assertRaises(RuntimeError):
                    WAZA_QUALITY.run_check("waza", REPO_ROOT / path)

    def test_transient_link_failure_is_rechecked_before_reporting(self) -> None:
        failed = {"links": {"passed": False, "deadURLs": [{"target": "https://github.com/example/repo", "reason": "HTTP 503"}]}}
        recovered = {"links": {"passed": True}}
        with patch.object(WAZA_QUALITY, "run_json", side_effect=[{"skills": [failed]}, {"skills": [recovered]}]) as run, patch.object(WAZA_QUALITY.time, "sleep"):
            self.assertEqual(recovered, WAZA_QUALITY.run_check("waza", Path("skill")))
            self.assertEqual(2, run.call_count)

    def test_persistent_broken_link_remains_actionable_with_exact_target(self) -> None:
        failed = {"links": {"passed": False, "deadURLs": [{"target": "https://github.com/example/missing", "reason": "HTTP 404"}]}}
        with patch.object(WAZA_QUALITY, "run_json", return_value={"skills": [failed]}) as run, patch.object(WAZA_QUALITY.time, "sleep"):
            check = WAZA_QUALITY.run_check("waza", Path("skill"))
        self.assertEqual(3, run.call_count)
        issues = WAZA_QUALITY.collect_issues(check, None)
        self.assertEqual("dead-links", issues[0]["code"])
        self.assertIn("https://github.com/example/missing", issues[0]["message"])
        self.assertIn("HTTP 404", issues[0]["message"])

    def test_clean_links_and_other_findings_do_not_trigger_retries(self) -> None:
        check = {"links": {"passed": True}, "tokenBudget": {"status": "warning", "count": 3600, "limit": 5000}}
        with patch.object(WAZA_QUALITY, "run_json", return_value={"skills": [check]}) as run:
            self.assertEqual(check, WAZA_QUALITY.run_check("waza", Path("skill")))
        self.assertEqual(1, run.call_count)
        self.assertEqual("tokens", WAZA_QUALITY.collect_issues(check, None)[0]["code"])

    def test_vendir_managed_package_prefixes_are_imported(self) -> None:
        self.assertTrue(
            WAZA_QUALITY.is_imported_skill_path(
                "catalog/Frameworks/ThreeJS-WebGPU-TSL/skills/webgpu-threejs-tsl/SKILL.md"
            )
        )
        self.assertTrue(
            WAZA_QUALITY.is_imported_skill_path(
                "catalog/Platform/Official-DotNet/skills/setup-local-sdk/SKILL.md"
            )
        )

    def test_repo_owned_package_is_not_imported(self) -> None:
        self.assertFalse(
            WAZA_QUALITY.is_imported_skill_path(
                "catalog/Frameworks/ASPNet-Core/skills/aspnet-core/SKILL.md"
            )
        )


if __name__ == "__main__":
    unittest.main()
