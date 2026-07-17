from __future__ import annotations

import copy
import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest import mock


REPO_ROOT = Path(__file__).resolve().parents[2]
MODULE_PATH = REPO_ROOT / "scripts" / "skillopt_catalog.py"
SPEC = importlib.util.spec_from_file_location("skillopt_catalog", MODULE_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Failed to load skillopt_catalog module from {MODULE_PATH}")
SKILLOPT_CATALOG = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = SKILLOPT_CATALOG
SPEC.loader.exec_module(SKILLOPT_CATALOG)


class SkillOptCatalogTests(unittest.TestCase):
    def quality_skill(self):
        return next(
            skill
            for skill in SKILLOPT_CATALOG.discover_skills()
            if skill.skill_id == "quality-ci"
        )

    def test_discovers_repo_owned_and_imported_skills(self) -> None:
        skills = SKILLOPT_CATALOG.discover_skills()

        self.assertTrue(any(skill.source_kind == "repo" for skill in skills))
        self.assertTrue(any(skill.source_kind == "imported" for skill in skills))
        self.assertEqual(len(skills), len({skill.skill_id for skill in skills}))

    def test_validates_pilot_task_set(self) -> None:
        skill = self.quality_skill()

        errors = SKILLOPT_CATALOG.validate_suite(
            skill,
            SKILLOPT_CATALOG.load_suite(skill.suite_path),
        )

        self.assertEqual([], errors)

    def test_rejects_unreviewed_and_incomplete_task_set(self) -> None:
        skill = self.quality_skill()
        payload = copy.deepcopy(SKILLOPT_CATALOG.load_suite(skill.suite_path))
        payload["reviewed"] = False
        payload["tasks"] = payload["tasks"][:2]

        errors = SKILLOPT_CATALOG.validate_suite(skill, payload)

        self.assertTrue(any("reviewed must be true" in error for error in errors))
        self.assertTrue(any("at least 6" in error for error in errors))
        self.assertTrue(any("split 'test'" in error for error in errors))
        self.assertTrue(any("split 'val'" in error for error in errors))

    def test_all_requires_complete_coverage_but_covered_runs_existing_suites(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            covered_path = root / "covered.tasks.json"
            covered_path.write_text("{}", encoding="utf-8")
            skills = [
                SKILLOPT_CATALOG.CatalogSkill("covered", root / "covered.md", "repo", covered_path),
                SKILLOPT_CATALOG.CatalogSkill("missing", root / "missing.md", "repo", root / "missing.tasks.json"),
            ]

            with self.assertRaisesRegex(SKILLOPT_CATALOG.SkillOptCatalogError, "complete dynamic-eval coverage"):
                SKILLOPT_CATALOG.select_skills(
                    skills,
                    skill_ids=[],
                    all_skills=True,
                    covered_only=False,
                    include_imported=False,
                )

            selected = SKILLOPT_CATALOG.select_skills(
                skills,
                skill_ids=[],
                all_skills=False,
                covered_only=True,
                include_imported=False,
            )
            self.assertEqual(["covered"], [skill.skill_id for skill in selected])

    def test_dynamic_eval_reports_current_and_candidate_scores_without_writing(self) -> None:
        skill = self.quality_skill()

        class FakeTaskRecord:
            @staticmethod
            def from_dict(payload):
                return SimpleNamespace(split=payload["split"])

        class FakeBackend:
            preferences = ""

            @staticmethod
            def tokens_used():
                return 123

        class FakeReplay:
            hard = 1.0
            soft = 1.0

        def fake_replay_batch(_backend, tasks, _skill, _memory):
            return [(task, FakeReplay()) for task in tasks]

        def fake_aggregate_scores(pairs):
            if not pairs:
                return 0.0, 0.0
            return 1.0, 1.0

        edit = SimpleNamespace(
            target="skill",
            op="add",
            content="Keep one owner per quality gate.",
            anchor="",
            rationale="Avoid conflicting tools.",
        )
        consolidation = SimpleNamespace(
            accepted=True,
            gate_action="accept_new_best",
            baseline_score=0.5,
            candidate_score=1.0,
            new_skill="candidate",
            applied_edits=[edit],
            rejected_edits=[],
            call_error="",
        )

        fake_api = (
            lambda *_args, **_kwargs: FakeBackend(),
            lambda *_args, **_kwargs: consolidation,
            fake_aggregate_scores,
            fake_replay_batch,
            FakeTaskRecord,
        )
        with mock.patch.object(SKILLOPT_CATALOG, "_load_skillopt_api", return_value=fake_api):
            result = SKILLOPT_CATALOG.run_dynamic_eval(
                skill,
                backend_name="codex",
                model="",
                edit_budget=2,
                progress=False,
            )

        self.assertEqual("pass", result["status"])
        self.assertEqual(1.0, result["baseline"]["testHard"])
        self.assertEqual(1.0, result["candidate"]["testHard"])
        self.assertFalse(result["candidate"]["testRegression"])
        self.assertEqual(1, len(result["proposedEdits"]))


if __name__ == "__main__":
    unittest.main()
