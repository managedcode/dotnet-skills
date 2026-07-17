#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable


ROOT = Path(__file__).resolve().parents[1]
CATALOG_ROOT = ROOT / "catalog"
SUITE_ROOT = ROOT / "tests" / "skillopt"
DEFAULT_REPORT = ROOT / "artifacts" / "skillopt" / "report.json"
EXTERNAL_IMPORT_CONFIG_ROOT = ROOT / "external-sources" / "imports"

TASK_FORMAT = "skillopt_sleep.tasks.v1"
KNOWN_SPLITS = {"train", "val", "test"}
KNOWN_REFERENCE_KINDS = {"exact", "rubric", "rule"}
KNOWN_RULE_OPERATORS = {
    "contains",
    "max_chars",
    "min_chars",
    "regex",
    "section_present",
    "tool_called",
}


@dataclass(frozen=True)
class CatalogSkill:
    skill_id: str
    skill_path: Path
    source_kind: str
    suite_path: Path

    @property
    def covered(self) -> bool:
        return self.suite_path.is_file()


class SkillOptCatalogError(RuntimeError):
    pass


def rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return path.as_posix()


def imported_package_prefixes(import_config_root: Path = EXTERNAL_IMPORT_CONFIG_ROOT) -> tuple[str, ...]:
    prefixes: set[str] = set()
    for config_path in import_config_root.glob("*.json"):
        config = json.loads(config_path.read_text(encoding="utf-8"))
        prefix = config.get("managedPackagePrefix")
        if isinstance(prefix, str) and prefix.strip():
            prefixes.add(prefix.strip())
    return tuple(sorted(prefixes))


def is_imported_skill_path(skill_path: Path, prefixes: Iterable[str]) -> bool:
    try:
        parts = skill_path.resolve().relative_to(ROOT).parts
    except ValueError:
        parts = skill_path.parts
    return len(parts) > 2 and any(parts[2].startswith(prefix) for prefix in prefixes)


def discover_skills(
    catalog_root: Path = CATALOG_ROOT,
    suite_root: Path = SUITE_ROOT,
    import_config_root: Path = EXTERNAL_IMPORT_CONFIG_ROOT,
) -> list[CatalogSkill]:
    prefixes = imported_package_prefixes(import_config_root)
    discovered: list[CatalogSkill] = []
    ids: dict[str, Path] = {}

    for skill_path in sorted(catalog_root.glob("*/*/skills/*/SKILL.md")):
        skill_id = skill_path.parent.name
        prior = ids.get(skill_id)
        if prior is not None:
            raise SkillOptCatalogError(
                f"Duplicate skill id {skill_id!r}: {rel(prior)} and {rel(skill_path)}"
            )
        ids[skill_id] = skill_path
        source_kind = "imported" if is_imported_skill_path(skill_path, prefixes) else "repo"
        discovered.append(
            CatalogSkill(
                skill_id=skill_id,
                skill_path=skill_path,
                source_kind=source_kind,
                suite_path=suite_root / f"{skill_id}.tasks.json",
            )
        )
    return discovered


def load_suite(path: Path) -> dict[str, Any]:
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise SkillOptCatalogError(f"{rel(path)}: invalid JSON: {exc}") from exc
    if not isinstance(payload, dict):
        raise SkillOptCatalogError(f"{rel(path)}: root must be a JSON object")
    return payload


def _validate_rule_judge(task_label: str, task: dict[str, Any], errors: list[str]) -> None:
    judge = task.get("judge")
    if not isinstance(judge, dict) or judge.get("kind") != "rule":
        errors.append(f"{task_label}: rule reference requires judge.kind=rule")
        return
    checks = judge.get("checks")
    if not isinstance(checks, list) or not checks:
        errors.append(f"{task_label}: rule judge requires at least one check")
        return
    for check_index, check in enumerate(checks):
        check_label = f"{task_label}.judge.checks[{check_index}]"
        if not isinstance(check, dict):
            errors.append(f"{check_label}: must be an object")
            continue
        operator = check.get("op")
        if operator not in KNOWN_RULE_OPERATORS:
            errors.append(f"{check_label}: unsupported operator {operator!r}")
            continue
        if "arg" not in check:
            errors.append(f"{check_label}: arg is required")
            continue
        argument = check.get("arg")
        if operator in {"contains", "regex", "section_present", "tool_called"}:
            if not isinstance(argument, str) or not argument:
                errors.append(f"{check_label}: {operator} requires a non-empty string arg")
                continue
        if operator in {"max_chars", "min_chars"}:
            if not isinstance(argument, int) or isinstance(argument, bool) or argument < 0:
                errors.append(f"{check_label}: {operator} requires a non-negative integer arg")
                continue
        if operator == "regex":
            try:
                re.compile(argument)
            except re.error as exc:
                errors.append(f"{check_label}: invalid regex: {exc}")


def validate_suite(skill: CatalogSkill, payload: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    prefix = rel(skill.suite_path)

    if payload.get("format") != TASK_FORMAT:
        errors.append(f"{prefix}: format must be {TASK_FORMAT!r}")
    if payload.get("skill") != skill.skill_id:
        errors.append(f"{prefix}: skill must be {skill.skill_id!r}")
    if payload.get("reviewed") is not True:
        errors.append(f"{prefix}: reviewed must be true before a real backend can use the suite")

    target = payload.get("target_skill_path")
    if not isinstance(target, str) or not target.strip():
        errors.append(f"{prefix}: target_skill_path is required")
    else:
        target_path = ROOT / target
        if target_path.resolve() != skill.skill_path.resolve():
            errors.append(
                f"{prefix}: target_skill_path must point to {rel(skill.skill_path)}, got {target!r}"
            )

    minimum_score = payload.get("minimum_test_score")
    if not isinstance(minimum_score, (int, float)) or isinstance(minimum_score, bool):
        errors.append(f"{prefix}: minimum_test_score must be a number from 0 to 1")
    elif not 0 <= float(minimum_score) <= 1:
        errors.append(f"{prefix}: minimum_test_score must be between 0 and 1")

    tasks = payload.get("tasks")
    if not isinstance(tasks, list):
        errors.append(f"{prefix}: tasks must be an array")
        return errors
    if len(tasks) < 6:
        errors.append(f"{prefix}: tasks must contain at least 6 entries")

    seen_ids: set[str] = set()
    split_counts = {split: 0 for split in KNOWN_SPLITS}
    for index, task in enumerate(tasks):
        task_label = f"{prefix}: tasks[{index}]"
        if not isinstance(task, dict):
            errors.append(f"{task_label}: must be an object")
            continue

        task_id = task.get("id")
        if not isinstance(task_id, str) or not task_id.strip():
            errors.append(f"{task_label}: id is required")
        elif task_id in seen_ids:
            errors.append(f"{task_label}: duplicate id {task_id!r}")
        else:
            seen_ids.add(task_id)

        for field in ("project", "intent"):
            value = task.get(field)
            if not isinstance(value, str) or not value.strip():
                errors.append(f"{task_label}: {field} is required")

        split = task.get("split")
        if split not in KNOWN_SPLITS:
            errors.append(f"{task_label}: split must be one of {sorted(KNOWN_SPLITS)}")
        else:
            split_counts[split] += 1

        if task.get("origin", "real") != "real":
            errors.append(f"{task_label}: committed catalog eval tasks must use origin=real")

        reference_kind = task.get("reference_kind")
        if reference_kind not in KNOWN_REFERENCE_KINDS:
            errors.append(
                f"{task_label}: reference_kind must be one of {sorted(KNOWN_REFERENCE_KINDS)}"
            )
        elif reference_kind == "rule":
            _validate_rule_judge(task_label, task, errors)
        else:
            reference = task.get("reference")
            if not isinstance(reference, str) or not reference.strip():
                errors.append(f"{task_label}: {reference_kind} reference requires reference text")

    for split, count in sorted(split_counts.items()):
        if count < 2:
            errors.append(f"{prefix}: split {split!r} must contain at least 2 tasks, found {count}")
    return errors


def validate_suites(skills: list[CatalogSkill]) -> list[str]:
    errors: list[str] = []
    known_ids = {skill.skill_id for skill in skills}
    suite_root = skills[0].suite_path.parent if skills else SUITE_ROOT
    for orphan in sorted(suite_root.glob("*.tasks.json")):
        if orphan.name.removesuffix(".tasks.json") not in known_ids:
            errors.append(f"{rel(orphan)}: no matching catalog skill")
    for skill in skills:
        if skill.covered:
            try:
                errors.extend(validate_suite(skill, load_suite(skill.suite_path)))
            except SkillOptCatalogError as exc:
                errors.append(str(exc))
    return errors


def coverage_payload(skills: list[CatalogSkill]) -> dict[str, Any]:
    def summarize(kind: str) -> dict[str, int]:
        selected = [skill for skill in skills if skill.source_kind == kind]
        covered = [skill for skill in selected if skill.covered]
        return {"total": len(selected), "covered": len(covered), "missing": len(selected) - len(covered)}

    return {
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "total": len(skills),
        "covered": sum(skill.covered for skill in skills),
        "repo": summarize("repo"),
        "imported": summarize("imported"),
        "skills": [
            {
                "skill": skill.skill_id,
                "skillPath": rel(skill.skill_path),
                "sourceKind": skill.source_kind,
                "taskSet": rel(skill.suite_path) if skill.covered else None,
                "covered": skill.covered,
            }
            for skill in skills
        ],
    }


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def select_skills(
    skills: list[CatalogSkill],
    *,
    skill_ids: list[str],
    all_skills: bool,
    covered_only: bool,
    include_imported: bool,
) -> list[CatalogSkill]:
    runnable = [skill for skill in skills if include_imported or skill.source_kind == "repo"]
    by_id = {skill.skill_id: skill for skill in runnable}

    if skill_ids:
        missing_ids = sorted(set(skill_ids) - by_id.keys())
        if missing_ids:
            raise SkillOptCatalogError(f"Unknown or excluded skill id(s): {', '.join(missing_ids)}")
        selected = [by_id[skill_id] for skill_id in dict.fromkeys(skill_ids)]
    elif all_skills:
        missing_suites = [skill.skill_id for skill in runnable if not skill.covered]
        if missing_suites:
            raise SkillOptCatalogError(
                "--all requires complete dynamic-eval coverage; "
                f"{len(missing_suites)} skill(s) are missing task sets. "
                "Use coverage to inspect the backlog or --covered to run the current suites."
            )
        selected = runnable
    elif covered_only:
        selected = [skill for skill in runnable if skill.covered]
    else:
        raise SkillOptCatalogError("Choose --skill, --all, or --covered")

    uncovered = [skill.skill_id for skill in selected if not skill.covered]
    if uncovered:
        raise SkillOptCatalogError(f"Missing SkillOpt task set(s): {', '.join(uncovered)}")
    if not selected:
        raise SkillOptCatalogError("No covered skills matched the requested selection")
    return selected


def _load_skillopt_api() -> tuple[Any, Any, Any, Any, Any]:
    try:
        from skillopt_sleep.backend import get_backend
        from skillopt_sleep.consolidate import consolidate
        from skillopt_sleep.replay import aggregate_scores, replay_batch
        from skillopt_sleep.types import TaskRecord
    except ImportError as exc:
        raise SkillOptCatalogError(
            "SkillOpt is not installed in this Python environment. "
            "Install a compatible release with: python3 -m pip install 'skillopt>=0.2,<0.3'"
        ) from exc
    return get_backend, consolidate, aggregate_scores, replay_batch, TaskRecord


def run_dynamic_eval(
    skill: CatalogSkill,
    *,
    backend_name: str,
    model: str,
    edit_budget: int,
    progress: bool,
) -> dict[str, Any]:
    get_backend, consolidate, aggregate_scores, replay_batch, TaskRecord = _load_skillopt_api()
    payload = load_suite(skill.suite_path)
    tasks = [TaskRecord.from_dict(task) for task in payload["tasks"]]
    test_tasks = [task for task in tasks if task.split == "test"]
    skill_text = skill.skill_path.read_text(encoding="utf-8")

    backend = get_backend(backend_name, model=model, project_dir=str(ROOT))
    backend.preferences = (
        "Preserve the skill's routing scope, YAML frontmatter, and repository policy. "
        "Propose only short reusable operating rules supported by failed training trajectories."
    )
    if progress:
        print(f"[skillopt] {skill.skill_id}: baseline test", file=sys.stderr)
    baseline_pairs = replay_batch(backend, test_tasks, skill_text, "")
    baseline_hard, baseline_soft = aggregate_scores(baseline_pairs)

    if progress:
        print(f"[skillopt] {skill.skill_id}: optimize and validation-gate", file=sys.stderr)
    result = consolidate(
        backend,
        tasks,
        skill_text,
        "",
        edit_budget=edit_budget,
        gate_metric="mixed",
        gate_mixed_weight=0.5,
        gate_mode="on",
        rollouts_k=1,
        evolve_skill=True,
        evolve_memory=False,
    )

    candidate_hard, candidate_soft = baseline_hard, baseline_soft
    if result.accepted:
        if progress:
            print(f"[skillopt] {skill.skill_id}: candidate test", file=sys.stderr)
        candidate_pairs = replay_batch(backend, test_tasks, result.new_skill, "")
        candidate_hard, candidate_soft = aggregate_scores(candidate_pairs)

    minimum = float(payload["minimum_test_score"])
    backend_call_failed = bool(getattr(result, "call_error", ""))
    if backend_name == "mock":
        status = "preflight"
    elif backend_call_failed:
        status = "backend-error"
    else:
        status = "pass" if baseline_hard >= minimum else "needs-optimization"
    return {
        "skill": skill.skill_id,
        "skillPath": rel(skill.skill_path),
        "sourceKind": skill.source_kind,
        "taskSet": rel(skill.suite_path),
        "status": status,
        "minimumTestScore": minimum,
        "baseline": {
            "validation": result.baseline_score,
            "testHard": baseline_hard,
            "testSoft": baseline_soft,
        },
        "candidate": {
            "acceptedByValidationGate": result.accepted,
            "gateAction": result.gate_action,
            "validation": result.candidate_score,
            "testHard": candidate_hard,
            "testSoft": candidate_soft,
            "testRegression": candidate_hard < baseline_hard,
        },
        "backendCallFailed": backend_call_failed,
        "proposedEdits": [
            {
                "target": edit.target,
                "operation": edit.op,
                "content": edit.content,
                "anchor": edit.anchor,
                "rationale": edit.rationale,
            }
            for edit in result.applied_edits
        ],
        "rejectedEdits": [
            {
                "target": edit.target,
                "operation": edit.op,
                "content": edit.content,
                "anchor": edit.anchor,
                "rationale": edit.rationale,
            }
            for edit in result.rejected_edits
        ],
        "tokensUsed": backend.tokens_used(),
    }


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Validate and run SkillOpt dynamic evaluations for catalog skills."
    )
    parser.add_argument("--catalog-root", type=Path, default=CATALOG_ROOT)
    parser.add_argument("--suite-root", type=Path, default=SUITE_ROOT)
    subparsers = parser.add_subparsers(dest="command", required=True)

    coverage = subparsers.add_parser("coverage", help="Report dynamic-eval coverage without model calls.")
    coverage.add_argument("--json-output", type=Path)
    coverage.add_argument("--verbose", action="store_true")
    coverage.add_argument("--fail-on-missing", action="store_true")
    coverage.add_argument("--include-imported", action="store_true")

    validate = subparsers.add_parser("validate", help="Validate committed SkillOpt task sets.")
    validate.add_argument("--fail-on-missing", action="store_true")
    validate.add_argument("--include-imported", action="store_true")

    run = subparsers.add_parser("run", help="Run local SkillOpt evaluations without modifying skills.")
    selection = run.add_mutually_exclusive_group(required=True)
    selection.add_argument("--skill", action="append", default=[], help="Skill id to evaluate; repeatable.")
    selection.add_argument("--all", action="store_true", help="Require and evaluate every selected catalog skill.")
    selection.add_argument("--covered", action="store_true", help="Evaluate every skill that has a task set.")
    run.add_argument("--include-imported", action="store_true")
    run.add_argument(
        "--backend",
        required=True,
        choices=("mock", "claude", "codex", "copilot", "azure_openai"),
    )
    run.add_argument("--model", default="")
    run.add_argument("--edit-budget", type=int, default=4)
    run.add_argument("--progress", action="store_true")
    run.add_argument("--report", type=Path, default=DEFAULT_REPORT)
    run.add_argument(
        "--report-only",
        action="store_true",
        help="Return success even when a baseline misses its threshold.",
    )
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    skills = discover_skills(args.catalog_root, args.suite_root)
    errors = validate_suites(skills)

    if args.command == "coverage":
        payload = coverage_payload(skills)
        repo = payload["repo"]
        imported = payload["imported"]
        print(
            f"SkillOpt coverage: repo-owned {repo['covered']}/{repo['total']}; "
            f"imported {imported['covered']}/{imported['total']}."
        )
        if args.verbose:
            missing = [item for item in payload["skills"] if not item["covered"]]
            for item in missing:
                print(f"missing [{item['sourceKind']}]: {item['skill']} ({item['skillPath']})")
        if args.json_output:
            write_json(args.json_output, payload)
            print(f"JSON report: {rel(args.json_output)}")
        if errors:
            for error in errors:
                print(f"error: {error}", file=sys.stderr)
            return 1
        required = [
            skill for skill in skills if args.include_imported or skill.source_kind == "repo"
        ]
        return 1 if args.fail_on_missing and any(not skill.covered for skill in required) else 0

    if errors:
        for error in errors:
            print(f"error: {error}", file=sys.stderr)
        return 1

    if args.command == "validate":
        covered = sum(skill.covered for skill in skills)
        print(f"Validated {covered} SkillOpt task set(s) across {len(skills)} catalog skill(s).")
        required = [
            skill for skill in skills if args.include_imported or skill.source_kind == "repo"
        ]
        return 1 if args.fail_on_missing and any(not skill.covered for skill in required) else 0

    selected = select_skills(
        skills,
        skill_ids=args.skill,
        all_skills=args.all,
        covered_only=args.covered,
        include_imported=args.include_imported,
    )
    results: list[dict[str, Any]] = []
    failures: list[dict[str, str]] = []
    for skill in selected:
        try:
            result = run_dynamic_eval(
                skill,
                backend_name=args.backend,
                model=args.model,
                edit_budget=args.edit_budget,
                progress=args.progress,
            )
            results.append(result)
            print(
                f"{skill.skill_id}: {result['status']} "
                f"(test {result['baseline']['testHard']:.3f}, "
                f"minimum {result['minimumTestScore']:.3f})"
            )
        except Exception as exc:  # keep a catalog-wide run going and report every failed skill
            failures.append({"skill": skill.skill_id, "error": str(exc)})
            print(f"{skill.skill_id}: error: {exc}", file=sys.stderr)

    report = {
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "backend": args.backend,
        "model": args.model or None,
        "dryRun": True,
        "selected": len(selected),
        "evaluated": len(results),
        "failed": len(failures),
        "backendErrors": sum(item["status"] == "backend-error" for item in results),
        "needsOptimization": sum(item["status"] == "needs-optimization" for item in results),
        "results": results,
        "failures": failures,
    }
    write_json(args.report, report)
    print(f"JSON report: {rel(args.report)}")

    if failures:
        return 1
    if any(item["status"] == "backend-error" for item in results):
        return 1
    if not args.report_only and any(item["status"] == "needs-optimization" for item in results):
        return 1
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except SkillOptCatalogError as exc:
        print(f"error: {exc}", file=sys.stderr)
        raise SystemExit(2)
