#!/usr/bin/env python3
from __future__ import annotations

import argparse
import concurrent.futures
import json
import os
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
CATALOG_ROOT = ROOT / "catalog"
DEFAULT_REPORT = ROOT / "artifacts" / "waza-skill-quality" / "report.md"
DEFAULT_JSON = ROOT / "artifacts" / "waza-skill-quality" / "report.json"

IGNORED_SPEC_CHECKS = {
    "spec-compatibility": "Repository-authored skills intentionally use the string compatibility field required by AGENTS.md.",
    "spec-license": "Skill-level license is not required in this repository catalog.",
    "spec-version": "Skill version lives in sibling manifest.json in this repository.",
}

NON_ACTIONABLE_LINK_HOSTS = (
    "https://www.nuget.org/packages/",
    "https://www.nuget.org/profiles/",
    "https://www.nuget.org/users/",
    "https://marketplace.visualstudio.com/items",
    "https://platform.openai.com/",
    "https://openai.com/",
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run Waza quality checks across catalog skills and write a CI report.")
    parser.add_argument("--waza", default=os.environ.get("WAZA_BIN", "waza"), help="Path to the Waza executable.")
    parser.add_argument("--catalog-root", type=Path, default=CATALOG_ROOT, help="Catalog root to scan.")
    parser.add_argument("--report", type=Path, default=DEFAULT_REPORT, help="Markdown report output path.")
    parser.add_argument("--json-output", type=Path, default=DEFAULT_JSON, help="JSON report output path.")
    parser.add_argument(
        "--fail-on",
        choices=("none", "warnings"),
        default="none",
        help="Whether findings should fail the process. Default emits report and warnings only.",
    )
    parser.add_argument(
        "--github-annotations",
        action="store_true",
        default=os.environ.get("GITHUB_ACTIONS") == "true",
        help="Emit GitHub Actions warning annotations.",
    )
    parser.add_argument("--annotation-limit", type=int, default=50, help="Maximum GitHub annotations to emit.")
    parser.add_argument("--workers", type=int, default=4, help="Number of parallel Waza check workers.")
    return parser.parse_args()


def run_json(command: list[str], *, cwd: Path) -> Any:
    completed = subprocess.run(command, cwd=cwd, check=False, text=True, capture_output=True)
    if completed.returncode != 0:
        raise RuntimeError(
            f"Command failed ({completed.returncode}): {' '.join(command)}\n"
            f"stdout:\n{completed.stdout}\n"
            f"stderr:\n{completed.stderr}"
        )
    try:
        return json.loads(completed.stdout)
    except json.JSONDecodeError as exc:
        raise RuntimeError(f"Command did not return JSON: {' '.join(command)}\n{completed.stdout}") from exc


def discover_skill_dirs(catalog_root: Path) -> list[Path]:
    return sorted(path.parent for path in catalog_root.glob("*/*/skills/*/SKILL.md"))


def rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return path.as_posix()


def run_check(waza: str, skill_dir: Path) -> dict[str, Any]:
    report = run_json([waza, "check", rel(skill_dir), "--format", "json"], cwd=ROOT)
    skills = report.get("skills", [])
    if len(skills) != 1:
        raise RuntimeError(f"Expected one Waza check result for {skill_dir}, got {len(skills)}")
    return skills[0]


def is_imported_skill_path(skill_path: str) -> bool:
    parts = Path(skill_path).parts
    return len(parts) > 2 and parts[2].startswith("Official-DotNet")


def load_token_profiles(waza: str, catalog_root: Path) -> dict[str, dict[str, Any]]:
    profile = run_json([waza, "tokens", "profile", f"./{rel(catalog_root)}", "--format", "json"], cwd=ROOT)
    items = profile if isinstance(profile, list) else profile.get("files", []) or profile.get("analyses", [])
    profiles: dict[str, dict[str, Any]] = {}
    for item in items:
        path = item.get("path")
        if isinstance(path, str) and path.endswith("SKILL.md"):
            profiles[path] = item
    return profiles


def load_coverage(waza: str, catalog_root: Path) -> dict[str, Any]:
    return run_json([waza, "coverage", "--format", "json", "--path", rel(catalog_root), "."], cwd=ROOT)


def is_actionable_dead_url(dead_url: dict[str, Any]) -> bool:
    source = str(dead_url.get("source", ""))
    target = str(dead_url.get("target", ""))
    reason = str(dead_url.get("reason", ""))

    if "HTTP 429" in reason:
        return False
    if source.startswith("references/official-docs/"):
        return False
    if target.startswith(NON_ACTIONABLE_LINK_HOSTS):
        return False
    return True


def collect_issues(check: dict[str, Any], profile: dict[str, Any] | None) -> list[dict[str, str]]:
    issues: list[dict[str, str]] = []

    compliance = check.get("compliance", {})
    compliance_level = str(compliance.get("level", ""))
    if compliance_level in {"Low", "Medium"}:
        issues.append(
            {
                "code": "compliance",
                "severity": "warning",
                "message": f"Waza compliance is {compliance_level}; add clearer routing triggers and anti-triggers.",
            }
        )

    for item in check.get("specCompliance", []):
        if item.get("passed") is True:
            continue
        name = str(item.get("name", "spec"))
        if name in IGNORED_SPEC_CHECKS:
            continue
        summary = str(item.get("summary", "Spec compliance warning"))
        issues.append({"code": name, "severity": "warning", "message": summary})

    token_budget = check.get("tokenBudget", {})
    token_status = str(token_budget.get("status", ""))
    if token_status in {"warning", "exceeded"}:
        count = token_budget.get("count")
        limit = token_budget.get("limit")
        issues.append(
            {
                "code": "tokens",
                "severity": "warning",
                "message": f"Token budget is {token_status}: {count}/{limit} tokens.",
            }
        )

    links = check.get("links") or {}
    if links and links.get("passed") is False:
        dead = [dead_url for dead_url in links.get("deadURLs") or [] if is_actionable_dead_url(dead_url)]
        orphaned = links.get("orphanedFiles") or []
        if dead:
            issues.append({"code": "dead-links", "severity": "warning", "message": f"{len(dead)} dead external link(s)."})
        if orphaned:
            issues.append({"code": "orphaned-references", "severity": "warning", "message": f"{len(orphaned)} orphaned reference file(s)."})

    if profile:
        for warning in profile.get("warnings", []) or []:
            warning_text = str(warning)
            if "token count" in warning_text:
                continue
            issues.append({"code": "token-profile", "severity": "warning", "message": warning_text})

    return issues


def build_report(waza: str, catalog_root: Path, workers: int) -> dict[str, Any]:
    skill_dirs = discover_skill_dirs(catalog_root)
    profiles = load_token_profiles(waza, catalog_root)
    coverage = load_coverage(waza, catalog_root)

    skills: list[dict[str, Any]] = []
    worker_count = max(1, workers)
    with concurrent.futures.ThreadPoolExecutor(max_workers=worker_count) as executor:
        checks = list(executor.map(lambda path: (path, run_check(waza, path)), skill_dirs))

    for skill_dir, check in checks:
        skill_path = rel(skill_dir / "SKILL.md")
        profile = profiles.get(skill_path)
        issues = collect_issues(check, profile)
        source_kind = "imported" if is_imported_skill_path(skill_path) else "repo"
        skills.append(
            {
                "name": check.get("name") or skill_dir.name,
                "path": skill_path,
                "skillDir": rel(skill_dir),
                "sourceKind": source_kind,
                "tokenCount": check.get("tokenBudget", {}).get("count"),
                "tokenLimit": check.get("tokenBudget", {}).get("limit"),
                "compliance": check.get("compliance", {}).get("level"),
                "evalFound": check.get("eval", {}).get("found", False),
                "issues": issues,
            }
        )

    bad_skills = [skill for skill in skills if skill["issues"]]
    repo_bad_skills = [skill for skill in bad_skills if skill.get("sourceKind") == "repo"]
    imported_bad_skills = [skill for skill in bad_skills if skill.get("sourceKind") == "imported"]
    return {
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "waza": waza,
        "totalSkills": len(skills),
        "badSkills": len(bad_skills),
        "repoBadSkills": len(repo_bad_skills),
        "importedBadSkills": len(imported_bad_skills),
        "coverage": {
            "total": coverage.get("total_skills"),
            "covered": coverage.get("covered"),
            "partial": coverage.get("partial"),
            "uncovered": coverage.get("uncovered"),
            "coveragePct": coverage.get("coverage_pct"),
        },
        "skills": skills,
    }


def render_markdown(report: dict[str, Any]) -> str:
    bad_skills = [skill for skill in report["skills"] if skill["issues"]]
    repo_bad_skills = [skill for skill in bad_skills if skill.get("sourceKind") == "repo"]
    imported_bad_skills = [skill for skill in bad_skills if skill.get("sourceKind") == "imported"]
    lines = [
        "# Waza Skill Quality Report",
        "",
        f"- Generated: `{report['generatedAt']}`",
        f"- Skills checked: `{report['totalSkills']}`",
        f"- Skills with warnings: `{report['badSkills']}`",
        f"- Repo-owned skills with warnings: `{report.get('repoBadSkills', len(repo_bad_skills))}`",
        f"- Imported upstream skills with warnings: `{report.get('importedBadSkills', len(imported_bad_skills))}`",
        f"- Eval coverage: `{report['coverage']['covered']}/{report['coverage']['total']}` full, `{report['coverage']['partial']}` partial, `{report['coverage']['uncovered']}` missing",
        "",
    ]

    if not bad_skills:
        lines.extend(["No Waza skill-quality warnings were found.", ""])
        return "\n".join(lines)

    def append_warning_table(title: str, skills: list[dict[str, Any]]) -> None:
        if not skills:
            return
        lines.extend(
            [
                f"## {title}",
                "",
                "| Skill | Tokens | Compliance | Issues |",
                "| --- | ---: | --- | --- |",
            ]
        )
        for skill in skills:
            issues = "<br>".join(f"`{issue['code']}`: {issue['message']}" for issue in skill["issues"])
            lines.append(
                f"| [{skill['name']}]({skill['path']}) | {skill.get('tokenCount')}/{skill.get('tokenLimit')} | {skill.get('compliance')} | {issues} |"
            )
        lines.append("")

    append_warning_table("Repo-Owned Warnings", repo_bad_skills)
    append_warning_table("Imported Upstream Warnings", imported_bad_skills)

    lines.extend(
        [
            "## Notes",
            "",
            "- Waza `spec-compatibility`, `spec-license`, and `spec-version` warnings are ignored because this repository stores compatibility and version metadata according to root `AGENTS.md`.",
            "- Waza link findings caused by external rate limits, official-doc snapshot internals, or known HEAD/anti-bot false positives are filtered out of CI warnings.",
            "- Imported `Official-DotNet-*` skills are reported separately because their markdown is synchronized from upstream and should be fixed through the import/upstream path, not edited directly.",
            "- Missing eval coverage is reported as an aggregate backlog, not as a per-skill warning, until eval suites are intentionally added.",
            "",
        ]
    )
    return "\n".join(lines)


def write_outputs(report: dict[str, Any], markdown_path: Path, json_path: Path) -> None:
    markdown_path.parent.mkdir(parents=True, exist_ok=True)
    json_path.parent.mkdir(parents=True, exist_ok=True)
    markdown = render_markdown(report)
    markdown_path.write_text(markdown + "\n", encoding="utf-8")
    json_path.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary_path:
        with Path(summary_path).open("a", encoding="utf-8") as summary:
            summary.write(markdown)
            summary.write("\n")


def emit_annotations(report: dict[str, Any], limit: int) -> None:
    emitted = 0
    for skill in report["skills"]:
        if skill.get("sourceKind") == "imported":
            continue
        if emitted >= limit:
            break
        for issue in skill["issues"]:
            if emitted >= limit:
                break
            message = f"{skill['name']}: {issue['message']}"
            print(f"::warning file={skill['path']}::{message}")
            emitted += 1


def main() -> int:
    args = parse_args()
    report = build_report(args.waza, args.catalog_root, args.workers)
    write_outputs(report, args.report, args.json_output)

    if args.github_annotations:
        emit_annotations(report, args.annotation_limit)

    print(
        f"Waza checked {report['totalSkills']} skill(s); warnings: {report['badSkills']} "
        f"(repo-owned: {report['repoBadSkills']}, imported: {report['importedBadSkills']})."
    )
    print(f"Markdown report: {rel(args.report)}")
    print(f"JSON report: {rel(args.json_output)}")

    if args.fail_on == "warnings" and report["badSkills"] > 0:
        return 1
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except RuntimeError as exc:
        print(f"error: {exc}", file=sys.stderr)
        raise SystemExit(2)
