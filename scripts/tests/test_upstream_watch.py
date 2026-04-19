from __future__ import annotations

import importlib.util
import unittest
from unittest import mock
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
MODULE_PATH = REPO_ROOT / "scripts" / "upstream_watch.py"
SPEC = importlib.util.spec_from_file_location("upstream_watch", MODULE_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Failed to load upstream_watch module from {MODULE_PATH}")
UPSTREAM_WATCH = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(UPSTREAM_WATCH)


class UpstreamWatchIssueTests(unittest.TestCase):
    def build_watch_index(self, count: int) -> dict[str, dict[str, str]]:
        return {
            f"watch-{index:03d}": {
                "id": f"watch-{index:03d}",
                "kind": "http_document",
                "name": f"Watch {index:03d}",
                "url": f"https://example.com/{index:03d}",
                "skills": ["dotnet-microsoft-agent-framework"],
                "notes": "Review the linked skill when the upstream documentation changes.",
            }
            for index in range(count)
        }

    def build_pending_watches(self, count: int) -> dict[str, dict[str, str]]:
        return {
            f"watch-{index:03d}": {
                "kind": "http_document",
                "value": f"value-{index:03d}",
                "human": f"Human detail {index:03d}",
                "source_url": f"https://example.com/{index:03d}",
            }
            for index in range(count)
        }

    def test_issue_body_compacts_large_groups(self) -> None:
        watch_index = self.build_watch_index(30)
        pending_watches = self.build_pending_watches(30)

        body = UPSTREAM_WATCH.issue_body(
            issue_key="dotnet-microsoft-agent-framework",
            issue_name="dotnet-microsoft-agent-framework",
            skills=[f"skill-{index:02d}" for index in range(20)],
            pending_watches=pending_watches,
            watch_index=watch_index,
        )

        self.assertLessEqual(len(body), UPSTREAM_WATCH.MAX_ISSUE_BODY_LENGTH)
        self.assertIn("- Pending upstream watches: `30`", body)
        self.assertIn("- ... `5` more pending upstream sources omitted for brevity;", body)
        self.assertIn("`skill-00`", body)
        self.assertIn("(+8 more)", body)
        self.assertNotIn("watch-029", body)

        payload = UPSTREAM_WATCH.decode_issue_payload(body)
        self.assertIsNotNone(payload)
        self.assertEqual(payload["watch_ids"], sorted(pending_watches))
        self.assertNotIn("watches", payload)

    def test_parse_open_issue_rehydrates_compact_payload_from_state(self) -> None:
        watch_index = self.build_watch_index(3)
        pending_watches = self.build_pending_watches(3)

        body = UPSTREAM_WATCH.issue_body(
            issue_key="dotnet-microsoft-agent-framework",
            issue_name="dotnet-microsoft-agent-framework",
            skills=["dotnet-microsoft-agent-framework"],
            pending_watches=pending_watches,
            watch_index=watch_index,
        )

        parsed = UPSTREAM_WATCH.parse_open_issue(
            {"body": body},
            watch_index=watch_index,
            state_watches=pending_watches,
        )

        self.assertIsNotNone(parsed)
        issue_key, skills, restored_pending_watches = parsed
        self.assertEqual(issue_key, "dotnet-microsoft-agent-framework")
        self.assertEqual(skills, ["dotnet-microsoft-agent-framework"])
        self.assertEqual(set(restored_pending_watches), set(pending_watches))
        self.assertEqual(restored_pending_watches["watch-000"]["value"], "value-000")

    def test_transient_fetch_error_detection(self) -> None:
        self.assertTrue(UPSTREAM_WATCH.is_transient_fetch_error("curl: (56) The requested URL returned error: 502"))
        self.assertTrue(UPSTREAM_WATCH.is_transient_fetch_error("curl: (22) The requested URL returned error: 503"))
        self.assertTrue(UPSTREAM_WATCH.is_transient_fetch_error("curl: (28) Operation timed out"))
        self.assertFalse(UPSTREAM_WATCH.is_transient_fetch_error("curl: (22) The requested URL returned error: 404"))

    def test_rotate_issue_group_batches_multiple_watch_changes_into_one_issue_write(self) -> None:
        watch_index = self.build_watch_index(3)
        pending_watches = self.build_pending_watches(3)
        gh_calls: list[tuple[str, str, dict | None]] = []

        def fake_gh_api(path: str, *, token: str | None, method: str = "GET", data: dict | None = None) -> dict:
            gh_calls.append((path, method, data))
            if path == "/repos/managedcode/dotnet-skills/issues" and method == "POST":
                return {"number": 601, "title": data["title"], "body": data["body"]}
            return {}

        with mock.patch.object(UPSTREAM_WATCH, "gh_api", side_effect=fake_gh_api):
            action = UPSTREAM_WATCH.rotate_issue_group(
                repo="managedcode/dotnet-skills",
                token="token",
                labels=["upstream-update", "automation"],
                issue_key="dotnet-microsoft-agent-framework",
                configured_issue_name="dotnet-microsoft-agent-framework",
                fallback_skills=["dotnet-microsoft-agent-framework"],
                changed_watches={watch_id: watch_index[watch_id] for watch_id in pending_watches},
                new_snapshots=pending_watches,
                watch_index=watch_index,
                open_issue_groups={},
                dry_run=False,
            )

        self.assertEqual(action, "create")
        issue_posts = [call for call in gh_calls if call[0] == "/repos/managedcode/dotnet-skills/issues" and call[1] == "POST"]
        self.assertEqual(len(issue_posts), 1)
        issue_body = issue_posts[0][2]["body"]
        self.assertIn("- Pending upstream watches: `3`", issue_body)
        self.assertIn("watch-000", issue_body)
        self.assertIn("watch-001", issue_body)
        self.assertIn("watch-002", issue_body)


if __name__ == "__main__":
    unittest.main()
