#!/usr/bin/env python3
from __future__ import annotations

import argparse
import base64
import glob
import hashlib
import html
import json
import os
import re
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import Any
from urllib.parse import urlparse


USER_AGENT = "dotnet-skills-upstream-watch"
ISSUE_TITLE_PREFIX = "Upstream update: "
MAX_ISSUE_TITLE_LENGTH = 256
MARKER_RE = re.compile(r"<!-- upstream-watch:id=(?P<watch_id>[^>]+) -->")
VALUE_MARKER_RE = re.compile(r"<!-- upstream-watch:value=(?P<value>[^>]+) -->")
ISSUE_KEY_MARKER_RE = re.compile(r"<!-- upstream-watch:issue-key=(?P<issue_key>[^>]+) -->")
PAYLOAD_MARKER_RE = re.compile(r"<!-- upstream-watch:payload-b64=(?P<payload>[^>]+) -->")


def load_json(path: Path, default: Any) -> Any:
    if not path.exists():
        return default
    return json.loads(path.read_text())


def dump_json(path: Path, data: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2, sort_keys=True) + "\n")


def resolve_config_paths(config_reference: str) -> list[Path]:
    if any(char in config_reference for char in "*?[]"):
        paths = [Path(path_str) for path_str in sorted(glob.glob(config_reference))]
        if not paths:
            raise ValueError(f"No config files matched pattern: {config_reference}")
        return paths

    base_path = Path(config_reference)
    if not base_path.exists():
        raise ValueError(f"Config file not found: {config_reference}")

    paths = [base_path]
    sibling_pattern = f"{base_path.stem}*{base_path.suffix}"
    excluded_names = {
        base_path.name,
        f"{base_path.stem}-state{base_path.suffix}",
    }
    for sibling in sorted(base_path.parent.glob(sibling_pattern)):
        if sibling == base_path:
            continue
        if sibling.name in excluded_names:
            continue
        paths.append(sibling)
    return paths


def merge_raw_configs(config_paths: list[Path]) -> dict[str, Any]:
    merged: dict[str, Any] = {
        "github_releases": [],
        "documentation": [],
    }
    labels_source: Path | None = None
    watch_issue_label_source: Path | None = None

    for path in config_paths:
        raw_config = load_json(path, default={})
        if not isinstance(raw_config, dict):
            raise ValueError(f"Config file {path} must contain a JSON object")

        if "watch_issue_label" in raw_config:
            value = raw_config["watch_issue_label"]
            if watch_issue_label_source is None:
                merged["watch_issue_label"] = value
                watch_issue_label_source = path
            elif merged.get("watch_issue_label") != value:
                raise ValueError(
                    f"Conflicting watch_issue_label between {watch_issue_label_source} and {path}"
                )

        if "labels" in raw_config:
            value = raw_config["labels"]
            if labels_source is None:
                merged["labels"] = value
                labels_source = path
            elif merged.get("labels") != value:
                raise ValueError(f"Conflicting labels between {labels_source} and {path}")

        for key in ("github_releases", "documentation"):
            value = raw_config.get(key, [])
            if value in (None, []):
                continue
            if not isinstance(value, list):
                raise ValueError(f"{key} in {path} must be a list")
            merged[key].extend(value)

    merged.setdefault("labels", [])
    return merged


def slugify(value: str) -> str:
    return re.sub(r"-+", "-", re.sub(r"[^a-z0-9]+", "-", value.lower())).strip("-")


def parse_github_repo_reference(reference: str) -> tuple[str, str]:
    cleaned = reference.strip().removesuffix(".git").rstrip("/")
    if cleaned.startswith("https://") or cleaned.startswith("http://"):
        parsed = urlparse(cleaned)
        if parsed.netloc.lower() != "github.com":
            raise ValueError(f"Unsupported GitHub repository URL: {reference}")
        parts = [part for part in parsed.path.split("/") if part]
    else:
        parts = [part for part in cleaned.split("/") if part]

    if len(parts) < 2:
        raise ValueError(f"GitHub repository reference must be owner/repo or a GitHub repo URL: {reference}")

    owner, repo = parts[:2]
    return owner, repo


def is_http_url(reference: str) -> bool:
    return reference.startswith("https://") or reference.startswith("http://")


def classify_source(reference: str) -> str:
    if not is_http_url(reference):
        return "github_release"

    parsed = urlparse(reference)
    if parsed.netloc.lower() == "github.com":
        parts = [part for part in parsed.path.split("/") if part]
        if len(parts) >= 2:
            return "github_release"

    return "http_document"


def default_http_watch_id(url: str) -> str:
    parsed = urlparse(url)
    host = slugify(parsed.netloc)
    path = slugify(parsed.path.strip("/")) or "root"
    return f"{host}-{path}-docs"


def default_http_watch_name(url: str) -> str:
    parsed = urlparse(url)
    path = parsed.path.strip("/") or "/"
    return f"{parsed.netloc}{path} documentation"


def normalize_github_release_watch(watch: dict[str, Any]) -> dict[str, Any]:
    repo_reference = watch.get("source") or watch.get("repo")
    if not isinstance(repo_reference, str) or not repo_reference.strip():
        raise ValueError("github_release watch requires a non-empty source field")

    owner, repo = parse_github_repo_reference(repo_reference)
    normalized: dict[str, Any] = {
        "id": watch.get("id") or f"{slugify(owner)}-{slugify(repo)}-release",
        "kind": "github_release",
        "name": watch.get("name") or f"{owner}/{repo} release",
        "owner": owner,
        "repo": repo,
        "notes": watch.get("notes") or f"Review the linked skills when {owner}/{repo} ships a new release.",
        "skills": watch.get("skills"),
    }

    for key in ("match_tag_regex", "exclude_tag_regex", "include_prereleases"):
        if key in watch:
            normalized[key] = watch[key]

    return normalized


def normalize_http_document_watch(watch: dict[str, Any]) -> dict[str, Any]:
    url = watch.get("source") or watch.get("url")
    if not isinstance(url, str) or not url.strip():
        raise ValueError("http_document watch requires a non-empty source field")

    return {
        "id": watch.get("id") or default_http_watch_id(url),
        "kind": "http_document",
        "name": watch.get("name") or default_http_watch_name(url),
        "url": url,
        "notes": watch.get("notes") or f"Review the linked skills when {url} changes.",
        "skills": watch.get("skills"),
    }


def validate_labels(labels: list[dict[str, Any]]) -> None:
    names: set[str] = set()
    for label in labels:
        name = label.get("name")
        if not name:
            raise ValueError("Label without name in upstream-watch config")
        if name in names:
            raise ValueError(f"Duplicate label name {name!r} in upstream-watch config")
        names.add(name)


def validate_skills(watch: dict[str, Any]) -> None:
    skills = watch.get("skills")
    if not isinstance(skills, list) or not skills or not all(isinstance(skill, str) and skill for skill in skills):
        raise ValueError(f"Watch {watch.get('id', '<unknown>')} must define a non-empty skills list")


def default_issue_key(skills: list[str]) -> str:
    return "+".join(sorted({slugify(skill) for skill in skills}))


def default_issue_name(skills: list[str]) -> str:
    ordered = sorted(dict.fromkeys(skills))
    if not ordered:
        return "upstream-watch"
    if len(ordered) == 1:
        return ordered[0]
    return " + ".join(ordered)


def condensed_issue_name(skills: list[str]) -> str | None:
    ordered = sorted(dict.fromkeys(skills))
    if not ordered:
        return None
    if len(ordered) <= 3:
        return " + ".join(ordered)
    return f"{ordered[0]} + {ordered[1]} + {ordered[2]} + {len(ordered) - 3} more"


def validate_issue_group_fields(normalized: dict[str, Any], raw_watch: dict[str, Any]) -> None:
    issue_key = raw_watch.get("issue_key") or default_issue_key(normalized["skills"])
    issue_name = raw_watch.get("issue_name") or default_issue_name(normalized["skills"])

    if not isinstance(issue_key, str) or not issue_key.strip():
        raise ValueError(f"Watch {normalized.get('id', '<unknown>')} must define a non-empty issue_key")
    if not isinstance(issue_name, str) or not issue_name.strip():
        raise ValueError(f"Watch {normalized.get('id', '<unknown>')} must define a non-empty issue_name")

    normalized["issue_key"] = issue_key.strip()
    normalized["issue_name"] = issue_name.strip()


def normalize_human_watch(watch: dict[str, Any], kind: str) -> dict[str, Any]:
    if not isinstance(watch, dict):
        raise ValueError(f"{kind} entries must be JSON objects")
    normalized = normalize_github_release_watch(watch) if kind == "github_release" else normalize_http_document_watch(watch)
    validate_skills(normalized)
    validate_issue_group_fields(normalized, watch)
    return normalized


def normalize_config(raw_config: dict[str, Any]) -> dict[str, Any]:
    labels = raw_config.get("labels", [])
    if not isinstance(labels, list):
        raise ValueError("labels must be a list")
    validate_labels(labels)

    github_releases = raw_config.get("github_releases", [])
    documentation = raw_config.get("documentation", [])
    if not isinstance(github_releases, list) or not isinstance(documentation, list):
        raise ValueError("github_releases and documentation must both be lists")

    watches: list[dict[str, Any]] = []
    watch_ids: set[str] = set()

    for watch in github_releases:
        normalized = normalize_human_watch(watch, "github_release")
        if normalized["id"] in watch_ids:
            raise ValueError(f"Duplicate watch id {normalized['id']!r} in upstream-watch config")
        watch_ids.add(normalized["id"])
        watches.append(normalized)

    for watch in documentation:
        normalized = normalize_human_watch(watch, "http_document")
        if normalized["id"] in watch_ids:
            raise ValueError(f"Duplicate watch id {normalized['id']!r} in upstream-watch config")
        watch_ids.add(normalized["id"])
        watches.append(normalized)

    return {
        "watch_issue_label": raw_config.get("watch_issue_label", "upstream-update"),
        "labels": labels,
        "watches": watches,
    }


def run_curl(
    url: str,
    *,
    headers: dict[str, str] | None = None,
    method: str = "GET",
    data: dict[str, Any] | None = None,
) -> tuple[dict[str, str], bytes]:
    headers = headers or {}

    with tempfile.TemporaryDirectory() as tmp:
        headers_path = Path(tmp) / "headers.txt"
        body_path = Path(tmp) / "body.bin"

        cmd = [
            "curl",
            "-fsSL",
            "-A",
            USER_AGENT,
            "-X",
            method,
            "-D",
            str(headers_path),
            "-o",
            str(body_path),
        ]

        for key, value in headers.items():
            cmd.extend(["-H", f"{key}: {value}"])

        if data is not None:
            cmd.extend(["-H", "Content-Type: application/json", "--data", json.dumps(data)])

        cmd.append(url)

        result = subprocess.run(cmd, capture_output=True, text=True)
        if result.returncode != 0:
            raise RuntimeError(result.stderr.strip() or f"curl failed for {url}")

        return parse_headers(headers_path.read_text()), body_path.read_bytes()


def parse_headers(raw_headers: str) -> dict[str, str]:
    blocks = re.split(r"\r?\n\r?\n", raw_headers.strip())
    for block in reversed(blocks):
        lines = [line for line in block.splitlines() if line.strip()]
        if not lines:
            continue
        parsed: dict[str, str] = {}
        for line in lines[1:]:
            if ":" not in line:
                continue
            key, value = line.split(":", 1)
            parsed[key.strip().lower()] = value.strip()
        if parsed:
            return parsed
    return {}


def decode_json(body: bytes) -> Any:
    return json.loads(body.decode("utf-8"))


def gh_api(
    path: str,
    *,
    token: str | None,
    method: str = "GET",
    data: dict[str, Any] | None = None,
) -> Any:
    env = os.environ.copy()
    if token:
        env["GH_TOKEN"] = token

    cmd = [
        "gh",
        "api",
        path.lstrip("/"),
        "--method",
        method,
        "-H",
        "Accept: application/vnd.github+json",
        "-H",
        "X-GitHub-Api-Version: 2022-11-28",
    ]

    payload = None
    if data is not None:
        cmd.extend(["--input", "-"])
        payload = json.dumps(data)

    result = subprocess.run(cmd, capture_output=True, text=True, input=payload, env=env)
    if result.returncode != 0:
        raise RuntimeError(result.stderr.strip() or f"gh api failed for {path}")

    stdout = result.stdout.strip()
    if not stdout:
        return {}
    return json.loads(stdout)


def human_release(release: dict[str, Any]) -> str:
    tag = release.get("tag_name") or release.get("name") or str(release.get("id"))
    published_at = release.get("published_at")
    if published_at:
        return f"{tag} ({published_at})"
    return tag


def fetch_github_release(watch: dict[str, Any], token: str | None) -> dict[str, Any]:
    releases = gh_api(
        f"/repos/{watch['owner']}/{watch['repo']}/releases?per_page=10",
        token=token,
    )
    if not isinstance(releases, list):
        raise RuntimeError(f"Unexpected release payload for {watch['id']}")

    include_prereleases = bool(watch.get("include_prereleases", False))
    match_tag_regex = watch.get("match_tag_regex")
    exclude_tag_regex = watch.get("exclude_tag_regex")
    selected = None
    for release in releases:
        if release.get("draft"):
            continue
        if release.get("prerelease") and not include_prereleases:
            continue
        tag_name = release.get("tag_name") or ""
        if match_tag_regex and not re.search(match_tag_regex, tag_name):
            continue
        if exclude_tag_regex and re.search(exclude_tag_regex, tag_name):
            continue
        selected = release
        break

    if selected is None:
        raise RuntimeError(f"No matching release found for {watch['owner']}/{watch['repo']}")

    return {
        "kind": "github_release",
        "value": selected.get("tag_name") or selected.get("name") or str(selected.get("id")),
        "human": human_release(selected),
        "source_url": selected.get("html_url") or watch.get("source_url"),
        "published_at": selected.get("published_at"),
    }


def extract_title(body: bytes) -> str | None:
    match = re.search(rb"<title>(.*?)</title>", body, flags=re.IGNORECASE | re.DOTALL)
    if not match:
        return None
    title = re.sub(r"\s+", " ", html.unescape(match.group(1).decode("utf-8", errors="ignore"))).strip()
    return title or None


def fetch_http_document(watch: dict[str, Any]) -> dict[str, Any]:
    headers, body = run_curl(watch["url"])
    sha256 = hashlib.sha256(body).hexdigest()
    etag = headers.get("etag")
    last_modified = headers.get("last-modified")
    identifier = etag or last_modified or sha256
    title = extract_title(body)

    detail = []
    if title:
        detail.append(title)
    if etag:
        detail.append(f"ETag {etag}")
    elif last_modified:
        detail.append(f"Last-Modified {last_modified}")
    else:
        detail.append(f"SHA {sha256[:12]}")

    return {
        "kind": "http_document",
        "value": identifier,
        "human": " | ".join(detail),
        "source_url": watch["url"],
        "etag": etag,
        "last_modified": last_modified,
        "title": title,
    }


def fetch_snapshot(watch: dict[str, Any], token: str | None) -> dict[str, Any]:
    kind = watch["kind"]
    if kind == "github_release":
        return fetch_github_release(watch, token)
    if kind == "http_document":
        return fetch_http_document(watch)
    raise RuntimeError(f"Unsupported watch kind: {kind}")


def watch_source_url(watch: dict[str, Any]) -> str:
    if watch["kind"] == "github_release":
        return f"https://github.com/{watch['owner']}/{watch['repo']}/releases"
    return watch["url"]


def minimal_snapshot(snapshot: dict[str, Any]) -> dict[str, Any]:
    return {
        key: snapshot[key]
        for key in ("kind", "value", "human", "source_url", "published_at", "title", "etag", "last_modified")
        if snapshot.get(key) is not None
    }


def encode_issue_payload(issue_key: str, skills: list[str], pending_watches: dict[str, dict[str, Any]]) -> str:
    payload = {
        "issue_key": issue_key,
        "skills": sorted(dict.fromkeys(skills)),
        "watches": pending_watches,
    }
    raw = json.dumps(payload, separators=(",", ":"), sort_keys=True).encode("utf-8")
    return base64.urlsafe_b64encode(raw).decode("ascii")


def decode_issue_payload(body: str) -> dict[str, Any] | None:
    match = PAYLOAD_MARKER_RE.search(body)
    if not match:
        return None

    try:
        decoded = base64.urlsafe_b64decode(match.group("payload").encode("ascii"))
        payload = json.loads(decoded.decode("utf-8"))
    except Exception:  # noqa: BLE001
        return None

    if not isinstance(payload, dict):
        return None
    return payload


def collect_group_skills(
    pending_watches: dict[str, dict[str, Any]],
    watch_index: dict[str, dict[str, Any]],
    *,
    fallback_skills: list[str] | None = None,
) -> list[str]:
    skills = list(fallback_skills or [])
    seen = set(skills)
    for watch_id in pending_watches:
        watch = watch_index.get(watch_id)
        if not watch:
            continue
        for skill in watch.get("skills", []):
            if skill not in seen:
                skills.append(skill)
                seen.add(skill)
    return sorted(skills)


def parse_legacy_issue(
    *,
    body: str,
    watch_index: dict[str, dict[str, Any]],
    state_watches: dict[str, dict[str, Any]],
) -> tuple[str, list[str], dict[str, dict[str, Any]]] | None:
    match = MARKER_RE.search(body)
    if not match:
        return None

    watch_id = match.group("watch_id")
    watch = watch_index.get(watch_id)
    issue_key = watch["issue_key"] if watch else watch_id
    skills = list(watch.get("skills", [])) if watch else []

    snapshot = minimal_snapshot(state_watches.get(watch_id, {}))
    if not snapshot:
        snapshot = {"value": VALUE_MARKER_RE.search(body).group("value")} if VALUE_MARKER_RE.search(body) else {}
        if watch:
            snapshot.setdefault("kind", watch["kind"])
            snapshot.setdefault("source_url", watch_source_url(watch))

    if watch:
        snapshot.setdefault("kind", watch["kind"])
        snapshot.setdefault("source_url", watch_source_url(watch))

    return issue_key, skills, {watch_id: snapshot}


def parse_open_issue(
    issue: dict[str, Any],
    *,
    watch_index: dict[str, dict[str, Any]],
    state_watches: dict[str, dict[str, Any]],
) -> tuple[str, list[str], dict[str, dict[str, Any]]] | None:
    body = issue.get("body") or ""
    issue_key_match = ISSUE_KEY_MARKER_RE.search(body)
    payload = decode_issue_payload(body)

    if issue_key_match and payload and isinstance(payload.get("watches"), dict):
        issue_key = issue_key_match.group("issue_key")
        raw_skills = payload.get("skills", [])
        skills = [skill for skill in raw_skills if isinstance(skill, str) and skill]
        pending_watches = {
            watch_id: minimal_snapshot(snapshot)
            for watch_id, snapshot in payload["watches"].items()
            if isinstance(watch_id, str) and isinstance(snapshot, dict)
        }
        return issue_key, skills, pending_watches

    return parse_legacy_issue(body=body, watch_index=watch_index, state_watches=state_watches)


def issue_title(issue_name: str) -> str:
    return f"{ISSUE_TITLE_PREFIX}{issue_name}"


def parse_issue_name_from_title(title: str | None) -> str | None:
    if not isinstance(title, str):
        return None
    if not title.startswith(ISSUE_TITLE_PREFIX):
        return None
    issue_name = title[len(ISSUE_TITLE_PREFIX) :].strip()
    return issue_name or None


def truncate_issue_name(issue_name: str) -> str:
    max_issue_name_length = MAX_ISSUE_TITLE_LENGTH - len(ISSUE_TITLE_PREFIX)
    if max_issue_name_length <= 0 or len(issue_name) <= max_issue_name_length:
        return issue_name
    if max_issue_name_length <= 3:
        return issue_name[:max_issue_name_length]
    return issue_name[: max_issue_name_length - 3].rstrip() + "..."


def resolve_issue_name(
    *,
    issue_key: str,
    skills: list[str],
    configured_issue_name: str | None = None,
    existing_issue_name: str | None = None,
) -> str:
    candidates: list[str] = []
    for candidate in (
        configured_issue_name,
        existing_issue_name,
        default_issue_name(skills) if skills else None,
        condensed_issue_name(skills),
        issue_key,
    ):
        if not isinstance(candidate, str):
            continue
        cleaned = candidate.strip()
        if not cleaned or cleaned in candidates:
            continue
        candidates.append(cleaned)

    for candidate in candidates:
        if len(issue_title(candidate)) <= MAX_ISSUE_TITLE_LENGTH:
            return candidate

    fallback = candidates[-1] if candidates else issue_key.strip() or "upstream-watch"
    return truncate_issue_name(fallback)


def issue_body(
    *,
    issue_key: str,
    issue_name: str,
    skills: list[str],
    pending_watches: dict[str, dict[str, Any]],
    watch_index: dict[str, dict[str, Any]],
) -> str:
    lines = [
        f"Automation detected pending upstream changes for **{issue_name}**.",
        "",
        f"- Issue key: `{issue_key}`",
        f"- Affected skills: {', '.join(f'`{skill}`' for skill in skills)}",
        f"- Pending upstream watches: `{len(pending_watches)}`",
        "",
        "Pending upstream sources:",
    ]

    sorted_pending = sorted(
        pending_watches.items(),
        key=lambda item: ((watch_index.get(item[0], {}).get("name") or item[0]).lower(), item[0]),
    )
    for watch_id, snapshot in sorted_pending:
        watch = watch_index.get(watch_id)
        watch_name = watch["name"] if watch else watch_id
        watch_kind = watch["kind"] if watch else snapshot.get("kind", "unknown")
        lines.append(f"- `{watch_id}` | `{watch_kind}` | **{watch_name}**")

        source_url = snapshot.get("source_url") or (watch_source_url(watch) if watch else None)
        if source_url:
            lines.append(f"  - Source: {source_url}")

        current_value = snapshot.get("value", "unknown")
        lines.append(f"  - Current value: `{current_value}`")

        if snapshot.get("published_at"):
            lines.append(f"  - Published at: `{snapshot['published_at']}`")
        if snapshot.get("human"):
            lines.append(f"  - Detail: {snapshot['human']}")

    notes: list[str] = []
    for watch_id in pending_watches:
        watch = watch_index.get(watch_id)
        note = watch.get("notes") if watch else None
        if note and note not in notes:
            notes.append(note)

    if notes:
        lines.extend(["", "Why this matters:"])
        lines.extend(f"- {note}" for note in notes)

    payload = encode_issue_payload(issue_key, skills, pending_watches)
    lines.extend(
        [
            "",
            "Suggested follow-up:",
            "- [ ] Review the upstream release notes or documentation diff",
            "- [ ] Update the affected files under `skills/`",
            "- [ ] Update `README.md` if framework coverage or guidance changed",
            "- [ ] Close this issue after the catalog has been refreshed",
            "",
            f"<!-- upstream-watch:issue-key={issue_key} -->",
            f"<!-- upstream-watch:payload-b64={payload} -->",
        ]
    )
    return "\n".join(lines)


def superseded_comment(
    *,
    replacement_issue_number: int,
    issue_key: str,
    watch: dict[str, Any],
    existing_watch_snapshot: dict[str, Any] | None,
    old_snapshot: dict[str, Any] | None,
    new_snapshot: dict[str, Any],
) -> str:
    previous_value = None
    if existing_watch_snapshot:
        previous_value = existing_watch_snapshot.get("value")
    elif old_snapshot:
        previous_value = old_snapshot.get("value")

    lines = [
        "Automation closed this upstream-watch issue because a newer upstream event superseded it.",
        "",
        f"- Replacement issue: #{replacement_issue_number}",
        f"- Issue key: `{issue_key}`",
        f"- Watch id: `{watch['id']}`",
        f"- Watch: **{watch['name']}**",
    ]
    if previous_value:
        lines.append(f"- Previous tracked value: `{previous_value}`")
    lines.extend(
        [
            f"- New detected value: `{new_snapshot['value']}`",
            f"- Source: {new_snapshot['source_url']}",
        ]
    )
    return "\n".join(lines)


def ensure_labels(repo: str, token: str, labels: list[dict[str, str]], dry_run: bool) -> None:
    if dry_run or not labels:
        return

    existing = gh_api(f"/repos/{repo}/labels?per_page=100", token=token)
    names = {label["name"] for label in existing if isinstance(label, dict)}

    for label in labels:
        if label["name"] in names:
            continue
        gh_api(
            f"/repos/{repo}/labels",
            token=token,
            method="POST",
            data=label,
        )


def list_labeled_issues(repo: str, token: str, watch_label: str, *, state: str) -> list[dict[str, Any]]:
    issues: list[dict[str, Any]] = []
    page = 1
    while True:
        batch = gh_api(
            f"/repos/{repo}/issues?state={state}&labels={watch_label}&per_page=100&page={page}",
            token=token,
        )
        if not isinstance(batch, list):
            raise RuntimeError(f"Unexpected issue payload while listing upstream-watch issues for {repo}")
        if not batch:
            break
        issues.extend(issue for issue in batch if not issue.get("pull_request"))
        page += 1
    return issues


def issue_sort_key(issue: dict[str, Any]) -> tuple[Any, ...]:
    return (
        issue.get("number") or 0,
        issue.get("updated_at") or "",
        issue.get("created_at") or "",
    )


def choose_canonical_issue(issues: list[dict[str, Any]]) -> dict[str, Any]:
    return max(issues, key=issue_sort_key)


def load_open_issue_groups(
    *,
    repo: str,
    token: str,
    watch_label: str,
    watch_index: dict[str, dict[str, Any]],
    state_watches: dict[str, dict[str, Any]],
) -> dict[str, dict[str, Any]]:
    groups: dict[str, dict[str, Any]] = {}
    for issue in list_labeled_issues(repo, token, watch_label, state="open"):
        parsed = parse_open_issue(issue, watch_index=watch_index, state_watches=state_watches)
        if not parsed:
            continue

        issue_key, skills, pending_watches = parsed
        group = groups.setdefault(
            issue_key,
            {
                "issues": [],
                "pending_watches": {},
                "skills": [],
                "issue_name": None,
                "fresh": False,
            },
        )
        group["issues"].append(issue)
        issue_name = parse_issue_name_from_title(issue.get("title"))
        if issue_name and not group.get("issue_name"):
            group["issue_name"] = issue_name

        for skill in skills:
            if skill not in group["skills"]:
                group["skills"].append(skill)

        for watch_id, snapshot in pending_watches.items():
            if watch_id in state_watches:
                group["pending_watches"][watch_id] = minimal_snapshot(state_watches[watch_id])
            else:
                group["pending_watches"][watch_id] = snapshot

    return groups


def load_historical_watch_snapshots(
    *,
    repo: str,
    token: str,
    watch_label: str,
    watch_index: dict[str, dict[str, Any]],
    state_watches: dict[str, dict[str, Any]],
) -> dict[str, dict[str, Any]]:
    historical: dict[str, dict[str, Any]] = {}
    seen_issue_numbers: dict[str, int] = {}

    for issue in list_labeled_issues(repo, token, watch_label, state="all"):
        parsed = parse_open_issue(issue, watch_index=watch_index, state_watches=state_watches)
        if not parsed:
            continue

        _, _, pending_watches = parsed
        issue_number = issue.get("number") or 0
        for watch_id, snapshot in pending_watches.items():
            previous_issue_number = seen_issue_numbers.get(watch_id, -1)
            if issue_number <= previous_issue_number:
                continue
            seen_issue_numbers[watch_id] = issue_number
            historical[watch_id] = minimal_snapshot(snapshot)

    return historical


def close_duplicate_issue(repo: str, token: str, issue_number: int, canonical_number: int) -> None:
    gh_api(
        f"/repos/{repo}/issues/{issue_number}/comments",
        token=token,
        method="POST",
        data={
            "body": (
                f"Automation consolidated this legacy upstream-watch issue into #{canonical_number} "
                "so the library or skill group keeps a single open maintenance thread."
            )
        },
    )
    gh_api(
        f"/repos/{repo}/issues/{issue_number}",
        token=token,
        method="PATCH",
        data={"state": "closed"},
    )


def reconcile_open_issues(
    *,
    repo: str,
    token: str,
    open_issue_groups: dict[str, dict[str, Any]],
    watch_index: dict[str, dict[str, Any]],
    dry_run: bool,
) -> tuple[dict[str, dict[str, Any]], dict[str, int]]:
    normalized: dict[str, dict[str, Any]] = {}
    stats = {
        "groups": 0,
        "issues_rewritten": 0,
        "duplicates_closed": 0,
    }

    for issue_key, group in open_issue_groups.items():
        canonical_issue = choose_canonical_issue(group["issues"])
        pending_watches = group["pending_watches"]
        skills = collect_group_skills(pending_watches, watch_index, fallback_skills=group.get("skills"))
        issue_name = resolve_issue_name(
            issue_key=issue_key,
            skills=skills,
            existing_issue_name=group.get("issue_name"),
        )
        title = issue_title(issue_name)
        body = issue_body(
            issue_key=issue_key,
            issue_name=issue_name,
            skills=skills,
            pending_watches=pending_watches,
            watch_index=watch_index,
        )

        if dry_run:
            if canonical_issue.get("title") != title or canonical_issue.get("body") != body:
                stats["issues_rewritten"] += 1
            stats["duplicates_closed"] += max(0, len(group["issues"]) - 1)
            normalized[issue_key] = {
                "issue": canonical_issue,
                "pending_watches": pending_watches,
                "skills": skills,
                "issue_name": issue_name,
                "fresh": False,
            }
            stats["groups"] += 1
            continue

        if canonical_issue.get("title") != title or canonical_issue.get("body") != body:
            canonical_issue = gh_api(
                f"/repos/{repo}/issues/{canonical_issue['number']}",
                token=token,
                method="PATCH",
                data={"title": title, "body": body},
            )
            stats["issues_rewritten"] += 1

        for issue in group["issues"]:
            if issue["number"] == canonical_issue["number"]:
                continue
            close_duplicate_issue(repo, token, issue["number"], canonical_issue["number"])
            stats["duplicates_closed"] += 1

        normalized[issue_key] = {
            "issue": canonical_issue,
            "pending_watches": pending_watches,
            "skills": skills,
            "issue_name": issue_name,
            "fresh": False,
        }
        stats["groups"] += 1

    return normalized, stats


def rotate_issue(
    *,
    repo: str,
    token: str,
    labels: list[str],
    watch: dict[str, Any],
    old_snapshot: dict[str, Any] | None,
    new_snapshot: dict[str, Any],
    watch_index: dict[str, dict[str, Any]],
    open_issue_groups: dict[str, dict[str, Any]],
    dry_run: bool,
) -> str:
    issue_key = watch["issue_key"]
    existing_group = open_issue_groups.get(issue_key)
    pending_watches = dict(existing_group.get("pending_watches", {})) if existing_group else {}
    existing_watch_snapshot = pending_watches.get(watch["id"])
    pending_watches[watch["id"]] = minimal_snapshot(new_snapshot)
    skills = collect_group_skills(pending_watches, watch_index, fallback_skills=watch.get("skills"))
    issue_name = resolve_issue_name(
        issue_key=issue_key,
        skills=skills,
        configured_issue_name=watch.get("issue_name"),
        existing_issue_name=existing_group.get("issue_name") if existing_group else None,
    )
    title = issue_title(issue_name)
    body = issue_body(
        issue_key=issue_key,
        issue_name=issue_name,
        skills=skills,
        pending_watches=pending_watches,
        watch_index=watch_index,
    )

    if dry_run:
        if existing_group and not existing_group.get("fresh"):
            action = "rotate"
        elif existing_group:
            action = "amend"
        else:
            action = "create"
        print(f"[dry-run] Would {action} issue for {watch['id']} via {issue_key}: {title}")
        return action

    if existing_group and existing_group.get("fresh"):
        updated = gh_api(
            f"/repos/{repo}/issues/{existing_group['issue']['number']}",
            token=token,
            method="PATCH",
            data={"title": title, "body": body},
        )
        open_issue_groups[issue_key] = {
            "issue": updated,
            "pending_watches": pending_watches,
            "skills": skills,
            "issue_name": issue_name,
            "fresh": True,
        }
        return "amend"

    created = gh_api(
        f"/repos/{repo}/issues",
        token=token,
        method="POST",
        data={"title": title, "body": body, "labels": labels},
    )

    action = "create"
    if existing_group:
        existing_issue = existing_group["issue"]
        gh_api(
            f"/repos/{repo}/issues/{existing_issue['number']}/comments",
            token=token,
            method="POST",
            data={
                "body": superseded_comment(
                    replacement_issue_number=created["number"],
                    issue_key=issue_key,
                    watch=watch,
                    existing_watch_snapshot=existing_watch_snapshot,
                    old_snapshot=old_snapshot,
                    new_snapshot=new_snapshot,
                )
            },
        )
        gh_api(
            f"/repos/{repo}/issues/{existing_issue['number']}",
            token=token,
            method="PATCH",
            data={"state": "closed"},
        )
        action = "rotate"

    open_issue_groups[issue_key] = {
        "issue": created,
        "pending_watches": pending_watches,
        "skills": skills,
        "issue_name": issue_name,
        "fresh": True,
    }
    return action


def write_summary(lines: list[str]) -> None:
    summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
    if not summary_path:
        return
    Path(summary_path).write_text("\n".join(lines) + "\n")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Monitor upstream frameworks and docs, then open refresh issues.")
    parser.add_argument("--config", default=".github/upstream-watch.json")
    parser.add_argument("--state", default=".github/upstream-watch-state.json")
    parser.add_argument("--validate-config", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--sync-state-only", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    config_paths = resolve_config_paths(args.config)
    state_path = Path(args.state)

    raw_config = merge_raw_configs(config_paths)
    config = normalize_config(raw_config)
    if args.validate_config:
        config_paths_text = ", ".join(f"`{path}`" for path in config_paths)
        summary = [
            "# Upstream Watch Config",
            "",
            f"- Config files: {config_paths_text}",
            f"- GitHub release watches: `{sum(1 for watch in config['watches'] if watch['kind'] == 'github_release')}`",
            f"- Documentation watches: `{sum(1 for watch in config['watches'] if watch['kind'] == 'http_document')}`",
            f"- Total watches: `{len(config['watches'])}`",
        ]
        print("\n".join(summary))
        write_summary(summary)
        return 0

    state = load_json(state_path, default={"watches": {}})
    prior_watches = state.get("watches", {})
    watch_index = {watch["id"]: watch for watch in config.get("watches", [])}

    token = os.environ.get("GH_TOKEN") or os.environ.get("GITHUB_TOKEN")
    repo = os.environ.get("GITHUB_REPOSITORY")
    watch_label = config.get("watch_issue_label", "upstream-update")
    issue_labels = [label["name"] for label in config.get("labels", [])]

    summary = [
        "# Upstream Watch",
        "",
        f"- Config files: {', '.join(f'`{path}`' for path in config_paths)}",
        f"- State: `{state_path}`",
        f"- Dry run: `{args.dry_run}`",
        f"- Sync state only: `{args.sync_state_only}`",
    ]

    reconciliation_stats = {
        "groups": 0,
        "issues_rewritten": 0,
        "duplicates_closed": 0,
    }
    if token and repo:
        if not args.dry_run and not args.sync_state_only:
            ensure_labels(repo, token, config.get("labels", []), dry_run=False)

        historical_watches = load_historical_watch_snapshots(
            repo=repo,
            token=token,
            watch_label=watch_label,
            watch_index=watch_index,
            state_watches=prior_watches,
        )

        open_issue_groups = load_open_issue_groups(
            repo=repo,
            token=token,
            watch_label=watch_label,
            watch_index=watch_index,
            state_watches=prior_watches,
        )

        reconcile_dry_run = args.dry_run or args.sync_state_only
        open_issue_groups, reconciliation_stats = reconcile_open_issues(
            repo=repo,
            token=token,
            open_issue_groups=open_issue_groups,
            watch_index=watch_index,
            dry_run=reconcile_dry_run,
        )
    else:
        historical_watches = {}
        open_issue_groups = {}

    next_state: dict[str, Any] = {"watches": {}}
    bootstrapped = 0
    changed = 0
    created = 0
    rotated = 0
    amended = 0
    errors: list[str] = []

    for watch in config.get("watches", []):
        previous = prior_watches.get(watch["id"])
        historical = historical_watches.get(watch["id"])
        fallback_snapshot = previous or historical
        try:
            snapshot = fetch_snapshot(watch, token)
            next_state["watches"][watch["id"]] = snapshot
            effective_previous = previous or historical
            if historical and historical.get("value") == snapshot.get("value"):
                effective_previous = historical

            if effective_previous is None:
                bootstrapped += 1
                summary.append(f"- Bootstrapped `{watch['id']}` with `{snapshot['value']}`")
                continue

            if effective_previous.get("value") == snapshot.get("value"):
                summary.append(f"- No change for `{watch['id']}`")
                continue

            changed += 1
            summary.append(
                f"- Change detected for `{watch['id']}`: `{effective_previous.get('value')}` -> `{snapshot.get('value')}`"
            )

            if args.sync_state_only:
                summary.append(f"- Skipped issue action for `{watch['id']}` because sync-state-only mode is enabled")
                continue

            if not args.dry_run and not repo:
                raise RuntimeError("GITHUB_REPOSITORY is required to create or update issues")

            action = rotate_issue(
                repo=repo or "",
                token=token or "",
                labels=issue_labels,
                watch=watch,
                old_snapshot=effective_previous,
                new_snapshot=snapshot,
                watch_index=watch_index,
                open_issue_groups=open_issue_groups,
                dry_run=args.dry_run,
            )
            if action == "create":
                created += 1
            elif action == "rotate":
                rotated += 1
            else:
                amended += 1
        except Exception as exc:  # noqa: BLE001
            if fallback_snapshot is not None:
                next_state["watches"][watch["id"]] = fallback_snapshot
            message = f"{watch.get('id', 'unknown-watch')}: {exc}"
            errors.append(message)
            summary.append(f"- Error for `{watch.get('id', 'unknown-watch')}`: {exc}")

    if not args.dry_run:
        dump_json(state_path, next_state)

    summary.extend(
        [
            "",
            "## Result",
            "",
            f"- Bootstrapped watches: `{bootstrapped}`",
            f"- Changed watches: `{changed}`",
            f"- Issues created: `{created}`",
            f"- Issues rotated: `{rotated}`",
            f"- Same-run issue amendments: `{amended}`",
            f"- Issue groups scanned: `{reconciliation_stats['groups']}`",
            f"- Issue bodies normalized: `{reconciliation_stats['issues_rewritten']}`",
            f"- Duplicate issues closed: `{reconciliation_stats['duplicates_closed']}`",
            f"- Errors: `{len(errors)}`",
        ]
    )

    write_summary(summary)
    print("\n".join(summary))

    if errors:
        print("\n".join(errors), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
