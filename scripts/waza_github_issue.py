#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import tempfile
from collections import Counter
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_REPORT_JSON = ROOT / "artifacts" / "waza-skill-quality" / "report.json"
DEFAULT_REPORT_MD = ROOT / "artifacts" / "waza-skill-quality" / "report.md"
DEFAULT_TITLE = "Waza skill quality findings"
DEFAULT_LABEL = "waza-skill-quality"
BODY_MARKER = "<!-- waza-skill-quality-report -->"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Create or update a GitHub issue for Waza skill quality findings.")
    parser.add_argument("--report-json", type=Path, default=DEFAULT_REPORT_JSON, help="Waza report JSON path.")
    parser.add_argument("--report-md", type=Path, default=DEFAULT_REPORT_MD, help="Waza report Markdown path.")
    parser.add_argument("--title", default=DEFAULT_TITLE, help="Issue title to create or update.")
    parser.add_argument("--label", default=DEFAULT_LABEL, help="Issue label used for deduplication.")
    parser.add_argument("--repo", default=os.environ.get("GITHUB_REPOSITORY"), help="GitHub repository in owner/name form.")
    parser.add_argument("--dry-run", action="store_true", help="Print the issue body instead of calling gh.")
    return parser.parse_args()


def run_gh(args: list[str], *, input_text: str | None = None) -> str:
    completed = subprocess.run(
        ["gh", *args],
        input=input_text,
        text=True,
        capture_output=True,
        check=False,
    )
    if completed.returncode != 0:
        raise RuntimeError(
            f"gh command failed ({completed.returncode}): gh {' '.join(args)}\n"
            f"stdout:\n{completed.stdout}\n"
            f"stderr:\n{completed.stderr}"
        )
    return completed.stdout


def load_report(path: Path) -> dict[str, Any] | None:
    if not path.exists():
        print(f"Waza report JSON not found at {path}; skipping issue sync.")
        return None
    return json.loads(path.read_text(encoding="utf-8"))


def issue_summary(report: dict[str, Any]) -> tuple[Counter[str], list[dict[str, Any]]]:
    bad_skills = [skill for skill in report.get("skills", []) if skill.get("issues")]
    issue_counts: Counter[str] = Counter()
    for skill in bad_skills:
        for issue in skill.get("issues", []):
            issue_counts[str(issue.get("code", "unknown"))] += 1
    return issue_counts, bad_skills


def run_url() -> str | None:
    server = os.environ.get("GITHUB_SERVER_URL", "https://github.com")
    repo = os.environ.get("GITHUB_REPOSITORY")
    run_id = os.environ.get("GITHUB_RUN_ID")
    if not repo or not run_id:
        return None
    return f"{server}/{repo}/actions/runs/{run_id}"


def build_issue_body(report: dict[str, Any], markdown_report: str) -> str:
    issue_counts, bad_skills = issue_summary(report)
    run = run_url()
    run_line = f"- Workflow run: {run}" if run else "- Workflow run: unavailable outside GitHub Actions"
    issue_breakdown = ", ".join(f"`{code}`: {count}" for code, count in issue_counts.most_common()) or "none"

    top_rows = []
    for skill in bad_skills[:20]:
        issues = ", ".join(str(issue.get("code", "unknown")) for issue in skill.get("issues", []))
        top_rows.append(
            f"| [{skill.get('name')}]({skill.get('path')}) | {skill.get('sourceKind')} | "
            f"{skill.get('compliance')} | {skill.get('tokenCount')}/{skill.get('tokenLimit')} | {issues} |"
        )

    top_table = "\n".join(
        [
            "| Skill | Source | Compliance | Tokens | Issues |",
            "| --- | --- | --- | ---: | --- |",
            *top_rows,
        ]
    )

    return "\n".join(
        [
            BODY_MARKER,
            "# Waza skill quality findings",
            "",
            "Waza found catalog skill-quality warnings in CI.",
            "",
            "## Summary",
            "",
            f"- Skills checked: `{report.get('totalSkills')}`",
            f"- Skills with warnings: `{report.get('badSkills')}`",
            f"- Repo-owned skills with warnings: `{report.get('repoBadSkills')}`",
            f"- Imported upstream skills with warnings: `{report.get('importedBadSkills')}`",
            f"- Issue breakdown: {issue_breakdown}",
            run_line,
            "- Full report artifact: `waza-skill-quality`",
            "",
            "## First Findings",
            "",
            top_table if top_rows else "No per-skill findings.",
            "",
            "## Full Report",
            "",
            markdown_report.strip(),
            "",
        ]
    )


def find_open_issue(repo: str, title: str, label: str) -> int | None:
    output = run_gh(
        [
            "issue",
            "list",
            "--repo",
            repo,
            "--state",
            "open",
            "--label",
            label,
            "--json",
            "number,title",
            "--limit",
            "100",
        ]
    )
    issues = json.loads(output)
    for issue in issues:
        if issue.get("title") == title:
            return int(issue["number"])
    return None


def ensure_label(repo: str, label: str) -> None:
    run_gh(
        [
            "label",
            "create",
            label,
            "--repo",
            repo,
            "--color",
            "7B3FF2",
            "--description",
            "Waza skill quality findings",
            "--force",
        ]
    )


def write_temp_body(body: str) -> Path:
    temp = tempfile.NamedTemporaryFile("w", encoding="utf-8", delete=False)
    with temp:
        temp.write(body)
        temp.write("\n")
    return Path(temp.name)


def sync_issue(repo: str, title: str, label: str, body: str, has_findings: bool) -> None:
    ensure_label(repo, label)
    issue_number = find_open_issue(repo, title, label)

    if not has_findings:
        if issue_number is None:
            print("No Waza findings and no open Waza issue to close.")
            return
        run_gh(
            [
                "issue",
                "close",
                str(issue_number),
                "--repo",
                repo,
                "--comment",
                "Waza skill quality is clean in the latest CI run.",
            ]
        )
        print(f"Closed Waza issue #{issue_number}; latest report is clean.")
        return

    body_path = write_temp_body(body)
    try:
        if issue_number is None:
            output = run_gh(
                [
                    "issue",
                    "create",
                    "--repo",
                    repo,
                    "--title",
                    title,
                    "--label",
                    label,
                    "--body-file",
                    str(body_path),
                ]
            )
            print(output.strip())
        else:
            run_gh(
                [
                    "issue",
                    "edit",
                    str(issue_number),
                    "--repo",
                    repo,
                    "--title",
                    title,
                    "--body-file",
                    str(body_path),
                    "--add-label",
                    label,
                ]
            )
            print(f"Updated Waza issue #{issue_number}.")
    finally:
        body_path.unlink(missing_ok=True)


def main() -> int:
    args = parse_args()
    if not args.repo and not args.dry_run:
        print("error: --repo or GITHUB_REPOSITORY is required", file=sys.stderr)
        return 2

    report = load_report(args.report_json)
    if report is None:
        return 0

    markdown_report = args.report_md.read_text(encoding="utf-8") if args.report_md.exists() else ""
    body = build_issue_body(report, markdown_report)
    has_findings = int(report.get("badSkills") or 0) > 0

    if args.dry_run:
        print(body)
        return 0

    sync_issue(str(args.repo), args.title, args.label, body, has_findings)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
